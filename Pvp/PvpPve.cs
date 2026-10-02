using HarmonyLib;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// PvE permanente: o jogador escolhe (/pve confirmar) sair do PvP para sempre. Ganha so o
    /// titulo: sobe skill mais devagar (PveSkillMultiplier) e coleta na taxa do PvE
    /// (PveResourceRate, 1x), enquanto quem joga PvP coleta na PvpResourceRate (2x). Nao tem
    /// volta pelo jogador; so um admin desfaz.
    ///
    /// O servidor guarda a escolha (PvpStore) e manda no estado; o cliente aplica o que e
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
            if (record.IsPk) { PvpNet.Message(peer.PeerId, "PK nao pode virar PvE: espere a marca sair."); return; }
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
