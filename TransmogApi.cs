using System.Linq;

namespace Deadheim
{
    /// <summary>
    /// Fachada estavel do transmog para outros mods (o NPC Alfaiate do NpcValheim a le por
    /// reflexao). So delega para <see cref="Transmog"/>: as regras ficam la. Mudar a forma de
    /// qualquer metodo = subir Version.
    ///
    /// Lotes viajam como "real>aparencia;real>aparencia" com nomes de prefab; "real>real" volta
    /// aquela peca ao visual real. Os metodos Server* so funcionam no servidor (ou no host) e
    /// recebem o peerId do remetente, nunca um playerId vindo do cliente.
    /// </summary>
    public static class TransmogApi
    {
        public const int Version = 1;

        // ---- cliente ----

        public static bool IsEnabled() => Transmog.Enabled != null && Transmog.Enabled.Value;

        public static bool RequiresVip() => Transmog.RequireVip == null || Transmog.RequireVip.Value;

        public static bool IsLocalVip() => VipList.VipListApi.IsLocalPlayerVip();

        public static int MaxChoices() => Transmog.MaxChoices != null ? Transmog.MaxChoices.Value : 0;

        /// <summary>Pecas desbloqueadas do jogador local, separadas por virgula.</summary>
        public static string LocalUnlocked() => string.Join(",", Transmog.LocalUnlocked.OrderBy(x => x));

        /// <summary>Escolhas ativas do jogador local ("real>aparencia;...").</summary>
        public static string LocalChosen() => string.Join(";", Transmog.LocalChosen.Select(kv => kv.Key + ">" + kv.Value));

        /// <summary>Sobe a cada estado novo vindo do servidor.</summary>
        public static int StateRevision() => Transmog.StateRevision;

        /// <summary>"Helmet", "Chest", "Legs", "Shoulder" ou "" se a peca nao entra no transmog.</summary>
        public static string SlotOf(string prefab) =>
            Transmog.Eligible(prefab) ? Transmog.SharedOf(prefab).m_itemType.ToString() : "";

        /// <summary>Null quando essa aparencia serve para essa peca, senao o motivo.</summary>
        public static string WhyIncompatible(string real, string app) =>
            Transmog.Compatible(real, app, out string why) ? null : why;

        /// <summary>Arena ou zona de raid onde o jogador local esta: la ninguem ve o transmog.</summary>
        public static bool IsSuppressedHere() =>
            Player.m_localPlayer != null && Transmog.IsSuppressedAt(Player.m_localPlayer.transform.position);

        /// <summary>Mostra o lote so no proprio personagem (nada vai para a rede).</summary>
        public static void SetPreview(string pairs) => Transmog.SetPreview(pairs);

        public static void ClearPreview() => Transmog.ClearPreview();

        // ---- servidor ----

        /// <summary>Confere o lote sem mudar nada. Null = pode aplicar.</summary>
        public static string ServerCheck(long peerId, string pairs) =>
            !Pvp.PvpPeer.TryResolve(peerId, out Pvp.PvpPeer peer) ? NoPeer : Transmog.CheckPairs(peer, Transmog.ParsePairs(pairs));

        /// <summary>Quantas pecas do lote mudam de aparencia (base do preco; voltar ao real nao conta).</summary>
        public static int ServerChangedCount(long peerId, string pairs) =>
            !Pvp.PvpPeer.TryResolve(peerId, out Pvp.PvpPeer peer) ? 0 : Transmog.ChangedCount(peer, Transmog.ParsePairs(pairs));

        /// <summary>Confere e aplica o lote (tudo ou nada). Null = aplicado.</summary>
        public static string ServerApply(long peerId, string pairs) =>
            !Pvp.PvpPeer.TryResolve(peerId, out Pvp.PvpPeer peer) ? NoPeer : Transmog.ApplyPairs(peer, Transmog.ParsePairs(pairs));

        /// <summary>Volta uma peca (ou "*" = todas) ao visual real. Null = feito.</summary>
        public static string ServerOff(long peerId, string real)
        {
            if (!Pvp.PvpPeer.TryResolve(peerId, out Pvp.PvpPeer peer)) return NoPeer;
            Transmog.ApplyOff(peer, real);
            return null;
        }

        private const string NoPeer = "O servidor nao conseguiu identificar o seu personagem.";
    }
}
