using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// PvE: o jogador escolhe no NPC Juiz de Guerra (NpcValheim, pela PvpApi) sair do PvP ou
    /// voltar a ele. Entre duas trocas ha PveSwitchCooldownHours. Quem e PvE sobe skill na
    /// PveSkillMultiplier e coleta na PveResourceRate; quem joga PvP, na PvpSkillMultiplier e
    /// na PvpResourceRate. Os valores o admin ajusta no menu do proprio NPC.
    ///
    /// O servidor guarda a escolha (PvpStore) e decide a troca; o cliente aplica o que e
    /// dele: bandeira de PvP sempre desligada (PvpState.Tick), o ganho de skill e a taxa de
    /// coleta local.
    ///
    /// A coleta: o jogo multiplica o que cai pela taxa do mundo (Game.m_resourceRate) no
    /// cliente que processa a quebra ou a morte (dono do objeto). Cada cliente usa a taxa de quem
    /// joga nele, entao o que um jogador junta sozinho vem na taxa dele; perto de outro jogador
    /// pode vir na taxa de quem for dono do objeto.
    /// </summary>
    internal static class PvpPve
    {
        private static bool _local;

        /// <summary>O jogador local e PvE (o servidor disse).</summary>
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
            RefreshResourceRate();
        }

        /// <summary>Estado vindo do servidor.</summary>
        public static void ApplyLocal(bool pve)
        {
            bool changed = pve != _local;
            _local = pve;
            if (changed) RefreshResourceRate();
        }

        /// <summary>Recalcula a taxa do mundo; o postfix abaixo poe a do PvP ou a do PvE por cima.</summary>
        public static void RefreshResourceRate() => ZoneSystem.instance?.UpdateWorldRates();

        /// <summary>Taxa de coleta do jogador local: PveResourceRate ou PvpResourceRate. 0 = a do mundo.</summary>
        public static void ApplyResourceRate()
        {
            // O servidor dedicado nao e jogador nenhum: fica com a taxa do mundo.
            if (!PvpConfig.Active || PvpConfig.PvpResourceRate == null || Application.isBatchMode) return;
            float rate = _local ? PvpConfig.PveResourceRate.Value : PvpConfig.PvpResourceRate.Value;
            if (rate > 0f) Game.m_resourceRate = rate;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.UpdateWorldRates))]
        private static class ResourceRatePatch
        {
            private static void Postfix() => ApplyResourceRate();
        }

        /// <summary>
        /// O que o servidor recusaria, conferido antes de pedir com o que o cliente sabe. Null =
        /// pode pedir; a palavra final e do servidor (SwitchRefusal).
        /// </summary>
        public static string LocalSwitchRefusal(bool toPve)
        {
            if (!PvpConfig.Active) return "O modulo de PvP esta desligado neste servidor.";
            if (Player.m_localPlayer == null) return "Entre no mundo primeiro.";
            if (_local == toPve) return toPve ? $"Voce ja e {Title} (PvE)." : "Voce ja esta no PvP.";
            if (toPve && !PvpConfig.PveEnabled.Value) return "A troca para o PvE esta desligada neste servidor.";
            double wait = PvpClient.PveSwitchReadyIn;
            if (wait > 0d) return "Voce trocou ha pouco. Proxima troca em " + PvpClient.FormatDuration(wait) + ".";
            if (!toPve) return null;
            if (PvpState.IsPk) return "PK nao pode virar PvE: espere a marca sair.";
            if (PvpState.IsHunted || PvpState.IsHuntPending || PvpClient.BountyPot > 0) return "Com bounty na cabeca nao da para virar PvE.";
            if (PvpState.InCombat) return "Em combate nao da para virar PvE.";
            return null;
        }

        // ---------------------------------------------------------------- servidor

        /// <summary>set pve|pvp (o NPC) | join (cliente antigo: virar PvE) | admin-off &lt;jogador&gt;.</summary>
        public static void OnCommand(PvpPeer peer, string action, string target)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "set":
                    Switch(peer, string.Equals((target ?? string.Empty).Trim(), "pve", StringComparison.OrdinalIgnoreCase));
                    break;
                case "join": Switch(peer, true); break;
                case "admin-off":
                    if (!PvpServer.IsAdminPeer(peer.PeerId)) { PvpNet.Message(peer.PeerId, "So admin."); return; }
                    AdminOff(peer, target);
                    break;
            }
        }

        /// <summary>Segundos ate o jogador poder trocar de novo (PveSwitchCooldownHours). 0 = pode agora.</summary>
        public static double SwitchReadyIn(PvpPlayerRecord record)
        {
            double hours = Math.Max(0f, PvpConfig.PveSwitchCooldownHours.Value);
            if (hours <= 0d || record.pveChangedAt <= 0d) return 0d;
            return Math.Max(0d, record.pveChangedAt + hours * 3600d - PvpState.Now);
        }

        private static string SwitchRefusal(PvpPeer peer, PvpPlayerRecord record, bool toPve)
        {
            if (record.pvePermanent == toPve) return toPve ? $"Voce ja e {Title} (PvE)." : "Voce ja esta no PvP.";
            if (toPve && !PvpConfig.PveEnabled.Value) return "A troca para o PvE esta desligada neste servidor.";
            double wait = SwitchReadyIn(record);
            if (wait > 0d) return "Voce trocou ha pouco. Proxima troca em " + PvpClient.FormatDuration(wait) + ".";
            if (!toPve) return null;
            if (record.IsPk) return "PK nao pode virar PvE: espere a marca sair.";
            if (PvpBounty.HasBounty(peer.PlayerId)) return "Com bounty na cabeca nao da para virar PvE.";
            if ((PvpBounty.FlagsOf(peer) & PvpFlags.Combat) != 0) return "Em combate nao da para virar PvE.";
            return null;
        }

        private static void Switch(PvpPeer peer, bool toPve)
        {
            PvpPlayerRecord record = PvpStore.Player(peer.PlayerId, peer.Name);
            string refusal = SwitchRefusal(peer, record, toPve);
            if (refusal != null)
            {
                Answer(peer, false, refusal);
                return;
            }

            record.pvePermanent = toPve;
            record.pveChangedAt = PvpState.Now;
            if (toPve) record.pveSince = PvpState.Now;
            PvpStore.MarkDirty();
            PvpServer.SendState(peer);

            double hours = Math.Max(0f, PvpConfig.PveSwitchCooldownHours.Value);
            string next = hours > 0d ? $" Proxima troca em {PvpClient.FormatDuration(hours * 3600d)}." : string.Empty;
            if (toPve)
            {
                PvpNet.Broadcast($"<color=#9acd32>{peer.Name} virou {Title}</color> (PvE): nao luta com jogadores.");
                Answer(peer, true, $"Voce agora e {Title} (PvE): nao luta com jogadores.{next}");
            }
            else
            {
                PvpNet.Broadcast($"<color=#ff8c00>{peer.Name} voltou ao PvP.</color>");
                Answer(peer, true, $"Voce voltou ao PvP.{next}");
            }
            Debug.Log($"[Deadheim PvP] {peer.Name} ({peer.PlayerId}) {(toPve ? "virou PvE" : "voltou ao PvP")}.");
        }

        private static void Answer(PvpPeer peer, bool ok, string message)
        {
            ZPackage pkg = PvpNet.Package(PvpNet.OpPveResult);
            pkg.Write(ok);
            pkg.Write(message ?? string.Empty);
            PvpNet.SendToClient(peer.PeerId, pkg);
        }

        private static void AdminOff(PvpPeer admin, string targetName)
        {
            if (!PvpBounty.TryFindTarget(targetName, out long targetId, out string name))
            {
                PvpNet.Message(admin.PeerId, $"Nao conheco nenhum jogador chamado '{targetName}'.");
                return;
            }
            PvpPlayerRecord record = PvpStore.Player(targetId, name);
            if (!record.pvePermanent) { PvpNet.Message(admin.PeerId, $"{name} nao e PvE."); return; }
            record.pvePermanent = false;
            PvpStore.MarkDirty();
            PvpServer.SendState(targetId);
            PvpNet.Message(admin.PeerId, $"{name} voltou ao PvP.");
            Debug.Log($"[Deadheim PvP] Admin {admin.Name} tirou {name} ({targetId}) do PvE.");
        }
    }
}
