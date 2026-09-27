using Deadheim.Wards;
using System;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// As regras de PvP num lugar so. Quem aplica e o cliente da vitima (dono da ZDO dela,
    /// que e quem roda Character.RPC_Damage) e, para a UX, o cliente do atacante.
    /// </summary>
    internal static class PvpRules
    {
        public enum Verdict
        {
            Allow,
            AttackerProtected,
            VictimProtected,
            Ally,
        }

        /// <summary>
        /// Pode <paramref name="attacker"/> ferir <paramref name="victim"/>?
        /// Cada lado ja decidiu a propria bandeira de PvP (zona segura, imunidade, arena,
        /// cacado) em PvpState.Tick; aqui so cruzamos as duas e checamos alianca.
        /// </summary>
        public static Verdict Check(Player attacker, Player victim)
        {
            if (attacker == null || victim == null || attacker == victim) return Verdict.Allow;
            if (!attacker.IsPVPEnabled()) return Verdict.AttackerProtected;
            if (!victim.IsPVPEnabled()) return Verdict.VictimProtected;
            if (AreAllies(attacker, victim, victim.transform.position)) return Verdict.Ally;
            return Verdict.Allow;
        }

        private static readonly int PlayerPrefab = "Player".GetStableHashCode();

        /// <summary>
        /// O ZDOID e de um jogador? Olha a ZDO, nao o objeto na cena: o matador pode estar
        /// longe demais para estar carregado (flecha de longe) ou ja ter saido, e nem por isso
        /// a morte vira PvE.
        /// </summary>
        public static bool IsPlayerZdo(ZDOID id, out long playerId)
        {
            playerId = 0L;
            if (id.IsNone() || ZDOMan.instance == null) return false;
            ZDO zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null || zdo.GetPrefab() != PlayerPrefab) return false;
            playerId = zdo.GetLong(ZDOVars.s_playerID, 0L);
            return true;
        }

        public enum DeathCause
        {
            Pve,
            /// <summary>O golpe que matou veio de um jogador.</summary>
            PlayerDirect,
            /// <summary>Morreu de outra coisa (queda, fogo, afogamento, mob) logo depois de levar dano PvP.</summary>
            PlayerCredit,
        }

        /// <summary>
        /// Morte por jogador ou por PvE. E daqui que saem imunidade, PK, ranking, recompensa
        /// de defesa e o fim do desafio, entao a regra tem que ser uma so:
        /// 1. o ultimo golpe tem atacante e ele e um jogador (pela ZDO) -> jogador;
        /// 2. senao, levou dano PvP ha menos de KillCreditSeconds -> credito para quem bateu;
        /// 3. senao -> PvE.
        /// </summary>
        public static DeathCause ClassifyDeath(Player victim, HitData lastHit, out ZDOID killer, out long killerId)
        {
            killer = ZDOID.None;
            killerId = 0L;
            ZDOID self = victim.GetZDOID();

            if (lastHit != null && lastHit.m_attacker != self && IsPlayerZdo(lastHit.m_attacker, out killerId))
            {
                killer = lastHit.m_attacker;
                return DeathCause.PlayerDirect;
            }

            ZDOID recent = PvpState.RecentAttacker();
            if (recent != self && IsPlayerZdo(recent, out killerId))
            {
                killer = recent;
                return DeathCause.PlayerCredit;
            }

            killerId = 0L;
            return DeathCause.Pve;
        }

        public static bool AreAllies(Player a, Player b, Vector3 where)
        {
            // Na arena o fogo amigo e liberado: e onde o cla treina entre si.
            if (PvpConfig.ArenaFriendlyFire.Value && PvpZones.IsArena(where)) return false;

            if (PvpConfig.NoFriendlyFireGuild.Value && PvpGuilds.SameGuild(a, b)) return true;
            if (PvpConfig.NoFriendlyFireTerritory.Value && SharesTerritory(a.GetPlayerID(), b.GetPlayerID(), where)) return true;
            return false;
        }

        /// <summary>Os dois tem permissao num mesmo ward que cobre este ponto.</summary>
        public static bool SharesTerritory(long a, long b, Vector3 where)
        {
            if (a == 0L || b == 0L) return false;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || WardProfiles.For(area) == null) continue;
                if (!area.IsInside(where, 0f)) continue;
                if (WardCore.IsPermittedIn(area, a) && WardCore.IsPermittedIn(area, b)) return true;
            }
            return false;
        }

        /// <summary>
        /// Ward ativo (com combustivel) que cobre o ponto e onde o jogador tem permissao:
        /// o territorio dele. Ward sem combustivel nao defende ninguem.
        /// </summary>
        public static bool InOwnTerritory(long playerId, Vector3 where)
        {
            if (playerId == 0L) return false;
            foreach (PrivateArea area in PrivateArea.m_allAreas)
            {
                if (area == null || WardProfiles.For(area) == null) continue;
                if (!area.IsInside(where, 0f) || !IsActive(area)) continue;
                if (WardCore.IsPermittedIn(area, playerId)) return true;
            }
            return false;
        }

        /// <summary>
        /// Defesa de castelo: o matador e da guilda que domina o castelo onde a vitima morreu,
        /// e a vitima nao e. So o cliente da vitima tem os dois jogadores carregados para ler a
        /// guilda pelo Guilds; o servidor so paga a recompensa.
        /// </summary>
        public static bool IsDefendingCastle(Player killer, Player victim, Vector3 where)
        {
            if (killer == null || victim == null) return false;
            string owner = PvpBridge.Owner(where);
            if (owner == null || PvpBridge.Castle(where) == null) return false;
            return string.Equals(PvpGuilds.GuildOf(killer), owner, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(PvpGuilds.GuildOf(victim), owner, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ligado pelo dono e com combustivel. Le a ZDO direto em vez de PrivateArea.IsEnabled,
        /// que o modulo de wards desliga de proposito enquanto o jogador mira porta de dungeon.
        /// </summary>
        private static bool IsActive(PrivateArea area)
        {
            ZNetView nview = area.m_nview;
            if (nview == null || !nview.IsValid()) return false;
            return nview.GetZDO().GetBool(ZDOVars.s_enabled, area.m_enabledByDefault) && WardCore.HasFuel(area);
        }

        /// <summary>Multiplicador final do dano de jogador em jogador, visto pela vitima.</summary>
        public static float DamageMultiplier(Player victim)
        {
            float multiplier = Mathf.Max(0f, PvpConfig.DamageMultiplier.Value);
            if (victim != null && InOwnTerritory(victim.GetPlayerID(), victim.transform.position))
                multiplier *= Mathf.Max(0f, PvpConfig.WardDefenseMultiplier.Value);
            return multiplier;
        }

        public static string Explain(Verdict verdict)
        {
            switch (verdict)
            {
                case Verdict.AttackerProtected: return "Voce esta protegido (zona segura ou imunidade) e nao pode atacar jogadores.";
                case Verdict.VictimProtected: return "Este jogador esta protegido.";
                case Verdict.Ally: return "Aliado: sem fogo amigo.";
                default: return null;
            }
        }
    }
}
