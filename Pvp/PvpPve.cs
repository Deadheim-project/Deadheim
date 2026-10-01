using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// PvE permanente: o jogador escolhe (/pve confirmar) sair do PvP para sempre. Ganha um
    /// titulo e um bonus unico nas skills de oficio, mas sobe skill mais devagar e perde o
    /// bonus de coleta do mundo. Nao tem volta pelo jogador; so um admin desfaz.
    ///
    /// O servidor guarda a escolha (PvpStore) e manda no estado; o cliente aplica o que e
    /// dele: bandeira de PvP sempre desligada (PvpState.Tick), o bonus de skill uma vez por
    /// personagem (m_customData), o ganho de skill e a taxa de coleta local.
    ///
    /// A coleta: o jogo multiplica o que cai pela taxa do mundo no cliente que processa a
    /// quebra (dono do objeto). O PvE usa a taxa PveResourceRate no proprio cliente, entao o
    /// que ele junta sozinho vem sem o bonus; perto de outro jogador pode vir com.
    /// </summary>
    internal static class PvpPve
    {
        private const string KeyBonusGiven = "dh_pveBonus";

        private static bool _local;

        /// <summary>O jogador local e PvE permanente (o servidor disse).</summary>
        public static bool IsLocal => _local;

        public static string Title
        {
            get
            {
                string title = PvpConfig.PveTitle?.Value;
                return string.IsNullOrWhiteSpace(title) ? "PvE" : title.Trim();
            }
        }

        public static bool IsPve(Player player)
        {
            if (player == null) return false;
            if (player == Player.m_localPlayer) return _local;
            return (PvpState.FlagsOf(player) & PvpFlags.Pve) != 0;
        }

        // ----------------------------------------------------------------- cliente

        public static void ResetSession()
        {
            if (!_local) return;
            _local = false;
            ZoneSystem.instance?.UpdateWorldRates();
        }

        /// <summary>Estado vindo do servidor. Na primeira vez que liga neste personagem, da o bonus.</summary>
        public static void ApplyLocal(bool pve)
        {
            bool changed = pve != _local;
            _local = pve;
            if (pve) GiveBonusOnce(Player.m_localPlayer);
            if (changed) ZoneSystem.instance?.UpdateWorldRates();
        }

        private static void GiveBonusOnce(Player player)
        {
            if (player == null || player.m_customData.ContainsKey(KeyBonusGiven)) return;
            player.m_customData[KeyBonusGiven] = "1";

            Skills skills = player.GetSkills();
            float cap = Mathf.Min(100f, Mathf.Max(0f, Plugin.SkillCap != null ? Plugin.SkillCap.Value : 100f));
            List<string> given = new List<string>();
            foreach (KeyValuePair<Skills.SkillType, float> bonus in ParseBonus(skills, PvpConfig.PveSkillBonus.Value))
            {
                Skills.Skill skill = skills.GetSkill(bonus.Key);
                if (skill == null) continue;
                float before = skill.m_level;
                skill.m_level = Mathf.Clamp(skill.m_level + bonus.Value, 0f, Mathf.Max(cap, skill.m_level));
                skill.m_accumulator = 0f;
                given.Add($"{bonus.Key} {before:0}->{skill.m_level:0}");
            }
            Debug.Log("[Deadheim PvP] PvE permanente: bonus de skill " + (given.Count > 0 ? string.Join(", ", given) : "(nenhum)"));
            if (given.Count > 0) player.Message(MessageHud.MessageType.TopLeft, "Bonus de PvE: " + string.Join(", ", given));
        }

        /// <summary>
        /// "Fishing:70,Crafting:70": nome da skill do jogo, ou o de uma skill de mod (SkillManager
        /// registra pelo hash do nome). Nome desconhecido e ignorado com aviso.
        /// </summary>
        public static List<KeyValuePair<Skills.SkillType, float>> ParseBonus(Skills skills, string text)
        {
            List<KeyValuePair<Skills.SkillType, float>> result = new List<KeyValuePair<Skills.SkillType, float>>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (string entry in text.Split(','))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2) continue;
                string name = parts[0].Trim();
                if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float amount)) continue;

                Skills.SkillType type;
                if (!Enum.TryParse(name, true, out type) || type == Skills.SkillType.None)
                {
                    type = (Skills.SkillType)Math.Abs(name.GetStableHashCode());
                    if (skills == null || skills.GetSkillDef(type) == null)
                    {
                        Debug.LogWarning($"[Deadheim PvP] PveSkillBonus: skill '{name}' nao existe.");
                        continue;
                    }
                }
                result.Add(new KeyValuePair<Skills.SkillType, float>(type, amount));
            }
            return result;
        }

        /// <summary>Taxa de coleta do PvE: nunca acima de PveResourceRate. 0 = nao mexe.</summary>
        public static void ClampResourceRate()
        {
            float rate = PvpConfig.PveResourceRate != null ? PvpConfig.PveResourceRate.Value : 0f;
            if (_local && rate > 0f && Game.m_resourceRate > rate) Game.m_resourceRate = rate;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateWorldRates))]
        private static class ResourceRatePatch
        {
            private static void Postfix() => ClampResourceRate();
        }

        // ---------------------------------------------------------------- servidor

        /// <summary>join | admin-off &lt;jogador&gt;.</summary>
        public static void OnCommand(PvpPeer peer, string action, string target)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "join": Join(peer); break;
                case "admin-off":
                    if (!PvpServer.IsAdminPeer(peer.PeerId)) { PvpNet.Message(peer.PeerId, "So admin."); return; }
                    AdminOff(peer, target);
                    break;
            }
        }

        private static void Join(PvpPeer peer)
        {
            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            if (!PvpConfig.PveEnabled.Value) { PvpNet.Message(peer.PeerId, "O PvE permanente esta desligado neste servidor."); return; }
            if (record.pvePermanent) { PvpNet.Message(peer.PeerId, "Voce ja e PvE permanente."); return; }
            if (record.IsPk(PvpState.Now)) { PvpNet.Message(peer.PeerId, "PK nao pode virar PvE: espere a marca sair."); return; }
            if (PvpBounty.HasBounty(peer.PlayerId)) { PvpNet.Message(peer.PeerId, "Com bounty na cabeca nao da para virar PvE."); return; }

            record.pvePermanent = true;
            record.pveSince = PvpState.Now;
            PvpStore.MarkDirty();
            PvpServer.SendState(peer);
            PvpNet.Broadcast($"<color=#9acd32>{peer.Name} virou {Title}</color> (PvE permanente): nao luta mais com jogadores.");
            Debug.Log($"[Deadheim PvP] {peer.Name} ({peer.PlayerId}) virou PvE permanente.");
        }

        private static void AdminOff(PvpPeer admin, string targetName)
        {
            if (!PvpBounty.TryFindTarget(targetName, out long targetId, out string name))
            {
                PvpNet.Message(admin.PeerId, $"Nao conheco nenhum jogador chamado '{targetName}'.");
                return;
            }
            PvpPlayerRecord record = PvpStore.Player(targetId, name);
            if (!record.pvePermanent) { PvpNet.Message(admin.PeerId, $"{name} nao e PvE permanente."); return; }
            record.pvePermanent = false;
            PvpStore.MarkDirty();
            PvpServer.SendState(targetId);
            PvpNet.Message(admin.PeerId, $"{name} voltou ao PvP.");
            Debug.Log($"[Deadheim PvP] Admin {admin.Name} tirou {name} ({targetId}) do PvE permanente.");
        }
    }
}
