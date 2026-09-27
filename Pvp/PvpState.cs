using System;
using System.Globalization;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Bandeiras de PvP que cada jogador publica na propria ZDO. Quem decide e o dono
    /// (o cliente do proprio jogador), que e quem sabe onde ele esta, se esta num barco,
    /// ha quanto tempo levou dano; os outros so leem. Sao bits e nao horarios de
    /// proposito: comparar horario entre maquinas diferentes da errado com relogio torto.
    /// </summary>
    [Flags]
    internal enum PvpFlags
    {
        None = 0,
        Immune = 1,
        Pk = 2,
        Hunted = 4,
        Protected = 8,
        Combat = 16,
        HuntPending = 32,
        Arena = 64,
    }

    internal static class PvpState
    {
        public static readonly int ZdoFlags = "dh_pvpFlags".GetStableHashCode();

        private const string KeyImmuneUntil = "dh_pvpImmuneUntil";

        // Relogio de parede em segundos UTC. So e comparado com ele mesmo, nesta maquina.
        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        // ------------------------------------------------------- estado do jogador local

        private static double _immuneUntil;
        private static double _pkUntil;
        private static double _huntPendingUntil;
        private static double _huntedUntil;
        private static float _combatUntil;

        private static ZDOID _lastPvpAttacker = ZDOID.None;
        private static float _lastPvpHitTime = -9999f;

        public static PvpFlags Current { get; private set; }
        public static string ZoneLabel { get; private set; }

        public static bool IsImmune => _immuneUntil > Now;
        public static bool IsPk => _pkUntil > Now;
        public static bool IsHunted => _huntedUntil > Now;
        public static bool IsHuntPending => _huntPendingUntil > Now;
        public static bool InCombat => Time.time < _combatUntil;

        public static double ImmuneRemaining => Math.Max(0d, _immuneUntil - Now);
        public static double PkRemaining => Math.Max(0d, _pkUntil - Now);
        public static double HuntedRemaining => Math.Max(0d, _huntedUntil - Now);
        public static double HuntPendingRemaining => Math.Max(0d, _huntPendingUntil - Now);
        public static float CombatRemaining => Mathf.Max(0f, _combatUntil - Time.time);

        /// <summary>Zera tudo que nao deve sobreviver a um logout.</summary>
        public static void ResetSession()
        {
            _pkUntil = 0d;
            _huntPendingUntil = 0d;
            _huntedUntil = 0d;
            _combatUntil = 0f;
            _lastPvpAttacker = ZDOID.None;
            _lastPvpHitTime = -9999f;
            Current = PvpFlags.None;
            ZoneLabel = null;
        }

        /// <summary>Imunidade vive no personagem (m_customData), entao relogar nao a apaga.</summary>
        public static void LoadFromPlayer(Player player)
        {
            _immuneUntil = 0d;
            if (player == null) return;
            if (player.m_customData.TryGetValue(KeyImmuneUntil, out string raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double until))
                _immuneUntil = until;
        }

        public static void GrantImmunity(Player player, double seconds)
        {
            _immuneUntil = seconds > 0d ? Now + seconds : 0d;
            if (player != null)
                player.m_customData[KeyImmuneUntil] = _immuneUntil.ToString("R", CultureInfo.InvariantCulture);
        }

        public static void ClearImmunity(Player player) => GrantImmunity(player, 0d);

        /// <summary>O servidor manda segundos restantes, nunca horario: relogios diferentes nao importam.</summary>
        public static void ApplyServerTimers(double pkRemaining, double huntPendingRemaining, double huntedRemaining)
        {
            double now = Now;
            _pkUntil = pkRemaining > 0d ? now + pkRemaining : 0d;
            _huntPendingUntil = huntPendingRemaining > 0d ? now + huntPendingRemaining : 0d;
            _huntedUntil = huntedRemaining > 0d ? now + huntedRemaining : 0d;
        }

        public static void ClearPk() => _pkUntil = 0d;

        public static void MarkCombat()
            => _combatUntil = Time.time + Mathf.Max(0f, PvpConfig.CombatTagSeconds.Value);

        public static void RecordPvpHit(ZDOID attacker)
        {
            _lastPvpAttacker = attacker;
            _lastPvpHitTime = Time.time;
            MarkCombat();
        }

        /// <summary>Quem leva o credito de uma morte que nao veio direto de um jogador.</summary>
        public static ZDOID RecentAttacker()
        {
            float window = Mathf.Max(0f, PvpConfig.KillCreditSeconds.Value);
            return Time.time - _lastPvpHitTime <= window ? _lastPvpAttacker : ZDOID.None;
        }

        public static void ForgetAttacker()
        {
            _lastPvpAttacker = ZDOID.None;
            _lastPvpHitTime = -9999f;
        }

        // -------------------------------------------------------------- bandeira de PvP

        /// <summary>
        /// Decide, para o jogador local, se ele participa de PvP agora, e publica o
        /// resultado: m_pvp e a ZDO (que o vanilla le para travar ataque e dano) mais as
        /// nossas bandeiras (que as regras de dano e o nome sobre a cabeca leem).
        /// </summary>
        public static void Tick(Player player)
        {
            if (player == null || player.m_nview == null || !player.m_nview.IsValid()) return;

            Vector3 pos = player.transform.position;
            bool arena = PvpZones.IsArena(pos);
            bool hunted = IsHunted;
            bool immune = IsImmune;
            bool combat = InCombat;
            string safeArea = PvpZones.SafeAreaName(pos);
            bool transport = PvpConfig.TransportsSafe.Value && PvpZones.IsOnTransport(player);

            bool protectedZone = !hunted && !combat && (safeArea != null || transport);

            bool pvp;
            string label;
            if (arena)
            {
                pvp = true;
                label = "Arena: " + PvpZones.ArenaName(pos);
            }
            else if (hunted)
            {
                pvp = true;
                label = "CACADO";
            }
            else if (immune)
            {
                pvp = false;
                label = "Imune";
            }
            else if (protectedZone)
            {
                pvp = false;
                label = safeArea ?? "Transporte";
            }
            else
            {
                pvp = PvpConfig.ForcePvp.Value;
                label = null;
            }

            PvpFlags flags = PvpFlags.None;
            if (immune && !arena) flags |= PvpFlags.Immune;
            if (IsPk) flags |= PvpFlags.Pk;
            if (hunted) flags |= PvpFlags.Hunted;
            if (IsHuntPending) flags |= PvpFlags.HuntPending;
            if (protectedZone && !arena) flags |= PvpFlags.Protected;
            if (combat) flags |= PvpFlags.Combat;
            if (arena) flags |= PvpFlags.Arena;

            // Estado antes da bandeira: o aviso de troca (PvpHud.OnPvpChanged) le o motivo daqui.
            Current = flags;
            ZoneLabel = label;

            SetPvp(player, pvp);

            ZDO zdo = player.m_nview.GetZDO();
            if (zdo.GetInt(ZdoFlags, 0) != (int)flags) zdo.Set(ZdoFlags, (int)flags);
        }

        /// <summary>
        /// SetPVP do vanilla, sem a mensagem central dele: quem avisa a troca e o HUD do
        /// modulo, com o motivo (zona segura, imunidade...).
        /// </summary>
        private static void SetPvp(Player player, bool enabled)
        {
            if (player.m_pvp == enabled) return;
            player.m_pvp = enabled;
            player.m_nview.GetZDO().Set(ZDOVars.s_pvp, enabled);
            PvpHud.OnPvpChanged(enabled);
        }

        public static PvpFlags FlagsOf(Player player)
        {
            if (player == null) return PvpFlags.None;
            if (player == Player.m_localPlayer) return Current;
            ZNetView nview = player.m_nview;
            if (nview == null || !nview.IsValid()) return PvpFlags.None;
            return (PvpFlags)nview.GetZDO().GetInt(ZdoFlags, 0);
        }
    }
}
