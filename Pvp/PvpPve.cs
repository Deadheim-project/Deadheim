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

        private static readonly int ZdoArenaMatch = "npcv_arenaMatch".GetStableHashCode();
        private static readonly int ZdoArenaPhase = "npcv_arenaPhase".GetStableHashCode();

        /// <summary>
        /// O jogador esta numa partida de arena do NpcValheim (preparo ou luta)? Le as chaves que o
        /// ArenaClient grava no ZDO do proprio jogador; ArenaClient.ClearMatch zera tudo (fim,
        /// saida, cancelamento, relog), entao a suspensao do PvE acaba sozinha ali. A fase de saida
        /// (3) ja nao conta: ninguem mais luta. Nada e gravado: o PvE do servidor nao muda.
        /// </summary>
        public static bool InArenaMatch(Player player)
        {
            ZNetView nview = player != null ? player.m_nview : null;
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo == null || zdo.GetLong(ZdoArenaMatch, 0L) == 0L) return false;
            int phase = zdo.GetInt(ZdoArenaPhase, 0);
            return phase == 1 || phase == 2;
        }

        /// <summary>PvE valendo de fato agora: o escolhido, menos durante a partida de arena.</summary>
        public static bool IsEffectiveLocal(Player player) => _local && !InArenaMatch(player);

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
        public static string LocalSwitchRefusal(bool toPve) => L.Resolve(LocalSwitchRefusalRaw(toPve));

        private static string LocalSwitchRefusalRaw(bool toPve)
        {
            if (!PvpConfig.Active) return L.Net("pvppve.modulo_off");
            if (Player.m_localPlayer == null) return L.Net("pvppve.entre");
            if (_local == toPve) return toPve ? L.Net("pvppve.ja_pve", Title) : L.Net("pvppve.ja_pvp");
            if (toPve && !PvpConfig.PveEnabled.Value) return L.Net("pvppve.troca_off");
            double wait = PvpClient.PveSwitchReadyIn;
            if (wait > 0d) return L.Net("pvppve.trocou", PvpClient.FormatDuration(wait));
            if (!toPve) return null;
            if (PvpState.IsPk) return L.Net("pvppve.pk_nao");
            if (PvpState.IsHunted || PvpState.IsHuntPending || PvpClient.BountyPot > 0) return L.Net("pvppve.bounty_nao");
            if (PvpState.InCombat) return L.Net("pvppve.combate_nao");
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
                    if (!PvpServer.IsAdminPeer(peer.PeerId)) { PvpNet.Message(peer.PeerId, L.Net("pvppve.so_admin")); return; }
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
            if (record.pvePermanent == toPve) return toPve ? L.Net("pvppve.ja_pve", Title) : L.Net("pvppve.ja_pvp");
            if (toPve && !PvpConfig.PveEnabled.Value) return L.Net("pvppve.troca_off");
            double wait = SwitchReadyIn(record);
            if (wait > 0d) return L.Net("pvppve.trocou", PvpClient.FormatDuration(wait));
            if (!toPve) return null;
            if (record.IsPk) return L.Net("pvppve.pk_nao");
            if (PvpBounty.HasBounty(peer.PlayerId)) return L.Net("pvppve.bounty_nao");
            if ((PvpBounty.FlagsOf(peer) & PvpFlags.Combat) != 0) return L.Net("pvppve.combate_nao");
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
            string next = hours > 0d ? L.Net("pvppve.proxima", PvpClient.FormatDuration(hours * 3600d)) : string.Empty;
            if (toPve)
            {
                PvpNet.Broadcast(L.Net("pvppve.virou", peer.Name, Title));
                Answer(peer, true, L.Net("pvppve.agora", Title, next));
            }
            else
            {
                PvpNet.Broadcast(L.Net("pvppve.voltou_todos", peer.Name));
                Answer(peer, true, L.Net("pvppve.voce_voltou", next));
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
                PvpNet.Message(admin.PeerId, L.Net("pvppve.nao_conheco", targetName));
                return;
            }
            PvpPlayerRecord record = PvpStore.Player(targetId, name);
            if (!record.pvePermanent) { PvpNet.Message(admin.PeerId, L.Net("pvppve.nao_e_pve", name)); return; }
            record.pvePermanent = false;
            PvpStore.MarkDirty();
            PvpServer.SendState(targetId);
            PvpNet.Message(admin.PeerId, L.Net("pvppve.adm_voltou", name));
            Debug.Log($"[Deadheim PvP] Admin {admin.Name} tirou {name} ({targetId}) do PvE.");
        }
    }
}
