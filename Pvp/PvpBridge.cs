using System;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>Um abate PvP ja classificado e registrado pelo servidor.</summary>
    internal struct PvpKill
    {
        public long KillerId;
        public string KillerName;
        public long VictimId;
        public string VictimName;
        public Vector3 Position;
        public bool Arena;
        public string Castle;
        public bool KillerDefendingCastle;
    }

    /// <summary>
    /// Ponto de encontro com o RaidSystem, no mesmo molde do Wards.WardBridge: o RaidSystem
    /// referencia o Deadheim, nunca o contrario, e registra aqui o que so ele sabe (onde ficam
    /// os castelos e quem domina cada um). Sem ninguem registrado, nao ha castelo e o resto
    /// do PvP funciona sozinho.
    /// </summary>
    public static class PvpBridge
    {
        /// <summary>Nome do castelo (zona do RaidSystem) que cobre o ponto, ou null.</summary>
        public static Func<Vector3, string> CastleAt;

        /// <summary>Guilda que domina o castelo que cobre o ponto, ou null.</summary>
        public static Func<Vector3, string> CastleOwner;

        /// <summary>
        /// O castelo que cobre o ponto ja caiu nesta janela de raid: vira zona segura ate a
        /// proxima janela (a luta acabou). Sem resolvedor, nunca.
        /// </summary>
        public static Func<Vector3, bool> CastleSafeAt;

        /// <summary>
        /// Servidor: cada morte por jogador, ja com matador resolvido pela ZDO. O RaidSystem
        /// usa para o Ranking de Guerra: o patch antigo dele so contava abate quando a morte
        /// era processada no servidor, o que nunca acontece com jogador em servidor dedicado.
        /// </summary>
        public static event Action<long, string, long, string, Vector3, bool, string> PlayerKilled;

        /// <summary>
        /// O jogador e PvE permanente ou esta imune (pelas bandeiras que ele publica): nada de fora
        /// (o "Force PvP In Zones" do RaidSystem) deve ligar o PvP dele.
        /// </summary>
        public static bool IsPveOrImmune(Player player)
            => player != null && (PvpState.FlagsOf(player) & (PvpFlags.Pve | PvpFlags.Immune)) != 0;

        internal static string Castle(Vector3 point) => Call(CastleAt, point, nameof(CastleAt));

        internal static string Owner(Vector3 point) => Call(CastleOwner, point, nameof(CastleOwner));

        internal static bool CastleSafe(Vector3 point)
        {
            if (CastleSafeAt == null) return false;
            try
            {
                return CastleSafeAt(point);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim PvP] {nameof(CastleSafeAt)} falhou: {ex.Message}");
                return false;
            }
        }

        internal static void RaiseKilled(PvpKill kill)
        {
            Action<long, string, long, string, Vector3, bool, string> handlers = PlayerKilled;
            if (handlers == null) return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<long, string, long, string, Vector3, bool, string>)handler)(
                        kill.KillerId, kill.KillerName, kill.VictimId, kill.VictimName, kill.Position, kill.Arena, kill.Castle);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Deadheim PvP] Assinante de PlayerKilled falhou: " + ex);
                }
            }
        }

        private static string Call(Func<Vector3, string> resolver, Vector3 point, string name)
        {
            if (resolver == null) return null;
            try
            {
                string value = resolver(point);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim PvP] {name} falhou: {ex.Message}");
                return null;
            }
        }
    }
}
