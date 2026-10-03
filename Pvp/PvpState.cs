using System;
using System.Collections.Generic;
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
        Castle = 128,
        /// <summary>Bateu primeiro em alguem sem marca: mata-lo nao gera PK (legitima defesa).</summary>
        Aggressor = 256,
        /// <summary>Dentro de um ward onde tem permissao: o servidor pausa a bounty.</summary>
        InOwnWard = 512,
        /// <summary>PK permanente: so sai morto por jogador.</summary>
        PkPermanent = 1024,
        /// <summary>PvE permanente (/pve): fora do PvP para sempre.</summary>
        Pve = 2048,
    }

    internal static class PvpState
    {
        public static readonly int ZdoFlags = "dh_pvpFlags".GetStableHashCode();
        /// <summary>Quantos abates deram PK a este jogador (contador de PK), para o nome sobre a cabeca.</summary>
        public static readonly int ZdoPkCount = "dh_pkCount".GetStableHashCode();
        /// <summary>playerID de quem bateu por ultimo: o servidor le ao deslogar em combate.</summary>
        public static readonly int ZdoLastAttacker = "dh_lastPvpAttacker".GetStableHashCode();

        // Formato antigo (7.3.0 e antes): horario de parede em que a imunidade acaba. So lido, para converter.
        private const string KeyImmuneUntilLegacy = "dh_pvpImmuneUntil";
        // Segundos de imunidade que faltam, gravados no personagem (m_customData).
        private const string KeyImmuneLeft = "dh_pvpImmuneLeft";

        /// <summary>
        /// Relogio de parede em segundos UTC. So no servidor (relogio confiavel) e para converter
        /// dado antigo: no cliente, horario de parede e o que o jogador quiser (A3).
        /// </summary>
        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        /// <summary>
        /// Relogio monotonico do cliente, em segundos. Imunidade, PK, cacado e aviso de bounty
        /// contam por ele: mudar a hora do Windows nao apaga marca nem estica imunidade.
        /// </summary>
        public static double Mono => Time.realtimeSinceStartupAsDouble;

        // ------------------------------------------------------- estado do jogador local

        private static double _immuneUntil;
        private static double _pkUntil;
        private static bool _pkPermanent;
        private static PvpConfig.PkPenalty _pkPenalty;
        private static bool _huntedForever;
        private static float _lastTick;
        private static double _huntPendingUntil;
        private static double _huntedUntil;
        private static float _combatUntil;
        private static float _aggressorUntil;
        private static float _pveCombatUntil;
        private static int _pkCount;

        private static ZDOID _lastPvpAttacker = ZDOID.None;
        private static float _lastPvpHitTime = -9999f;
        // Veneno/fogo que um jogador deixou: o credito vale ate o efeito acabar.
        private static float _dotCreditUntil = -9999f;

        // Quem bateu no jogador local e quando (Time.time). Revidar nele e legitima defesa mesmo
        // antes de a bandeira de agressor dele chegar pela ZDO (o Tick dele e a sincronia levam
        // de 0,25 s a 1 s): sem isto quem revidava rapido virava agressor tambem (M3).
        private static readonly Dictionary<ZDOID, float> _hitMeAt = new Dictionary<ZDOID, float>();

        public static PvpFlags Current { get; private set; }
        public static string ZoneLabel { get; private set; }

        public static bool IsImmune => _immuneUntil > Mono;
        public static bool IsPk => _pkPermanent || _pkUntil > Mono;
        public static bool IsPkPermanent => _pkPermanent;
        /// <summary>O que o PK perde ao morrer, pelo nivel atual (PkTiers).</summary>
        public static PvpConfig.PkPenalty PkPenalty => _pkPenalty;
        public static bool IsHunted => _huntedForever || _huntedUntil > Mono;
        /// <summary>Bounty sem fim: so acaba morrendo para jogador.</summary>
        public static bool IsHuntedForever => _huntedForever;
        public static bool IsHuntPending => _huntPendingUntil > Mono;
        public static bool InCombat => Time.time < _combatUntil;
        public static bool IsAggressor => Time.time < _aggressorUntil;
        public static float AggressorRemaining => Mathf.Max(0f, _aggressorUntil - Time.time);
        public static bool InPveCombat => Time.time < _pveCombatUntil;
        public static float PveCombatRemaining => Mathf.Max(0f, _pveCombatUntil - Time.time);

        /// <summary>Combate que bloqueia teleporte: PvP sempre; PvE se CombatFromPve.</summary>
        public static bool InEscapeCombat
            => InCombat || (PvpConfig.CombatFromPve != null && PvpConfig.CombatFromPve.Value && InPveCombat);

        public static float EscapeCombatRemaining
            => Mathf.Max(CombatRemaining, PvpConfig.CombatFromPve != null && PvpConfig.CombatFromPve.Value ? PveCombatRemaining : 0f);

        /// <summary>
        /// Luta com monstro que bloqueia algo agora: todo teleporte longo (CombatFromPve) ou so o
        /// retreat e a pedra (RetreatBlockedByPveCombat). E o que o jogador precisa ver na tela.
        /// </summary>
        public static bool PveCombatBlocks
            => InPveCombat
               && ((PvpConfig.CombatFromPve != null && PvpConfig.CombatFromPve.Value)
                   || (PvpConfig.RetreatBlockedByPveCombat != null && PvpConfig.RetreatBlockedByPveCombat.Value));

        /// <summary>Contagem do buff "Em combate": luta com jogador ou com monstro que bloqueia algo.</summary>
        public static float ShownCombatRemaining
            => Mathf.Max(CombatRemaining, PveCombatBlocks ? PveCombatRemaining : 0f);

        public static void MarkPveCombat()
            => _pveCombatUntil = Time.time + Mathf.Max(0f, PvpConfig.CombatTagSeconds.Value);
        public static int PkCount => _pkCount;

        public static double ImmuneRemaining => Math.Max(0d, _immuneUntil - Mono);
        public static double PkRemaining => Math.Max(0d, _pkUntil - Mono);
        public static double HuntedRemaining => Math.Max(0d, _huntedUntil - Mono);
        public static double HuntPendingRemaining => Math.Max(0d, _huntPendingUntil - Mono);
        public static float CombatRemaining => Mathf.Max(0f, _combatUntil - Time.time);

        /// <summary>Zera tudo que nao deve sobreviver a um logout.</summary>
        public static void ResetSession()
        {
            _pkUntil = 0d;
            _pkPermanent = false;
            _pkPenalty = PvpConfig.PkPenalty.Skills;
            _huntedForever = false;
            _huntPendingUntil = 0d;
            _huntedUntil = 0d;
            _combatUntil = 0f;
            _aggressorUntil = 0f;
            _pveCombatUntil = 0f;
            _pkCount = 0;
            _lastTick = 0f;
            _lastPvpAttacker = ZDOID.None;
            _lastPvpHitTime = -9999f;
            _dotCreditUntil = -9999f;
            _hitMeAt.Clear();
            Current = PvpFlags.None;
            ZoneLabel = null;
            PvpPve.ResetSession();
            PvpBosses.ResetSession();
        }

        /// <summary>
        /// Imunidade vive no personagem (m_customData), entao relogar nao a apaga. Gravada como
        /// segundos que faltam e descontada so com o jogo aberto: o horario de parede do 7.3.0
        /// deixava atrasar o relogio do Windows e ficar imune por meses.
        /// </summary>
        public static void LoadFromPlayer(Player player)
        {
            _immuneUntil = 0d;
            if (player == null) return;
            double left = 0d;
            if (player.m_customData.TryGetValue(KeyImmuneLeft, out string raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double stored))
                left = stored;
            else if (player.m_customData.TryGetValue(KeyImmuneUntilLegacy, out string legacy)
                     && double.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out double until))
                left = until - Now;
            player.m_customData.Remove(KeyImmuneUntilLegacy);
            // Nunca mais que uma imunidade inteira: um valor antigo de relogio torto nao vira meses.
            left = Math.Min(left, Math.Max(0d, PvpConfig.ImmunityMinutes.Value * 60d));
            _immuneUntil = left > 0d ? Mono + left : 0d;
            SaveTimers(player);
        }

        public static void GrantImmunity(Player player, double seconds)
        {
            _immuneUntil = seconds > 0d ? Mono + seconds : 0d;
            SaveTimers(player);
        }

        /// <summary>Grava no personagem o que falta de imunidade (antes de o perfil ser salvo, e ao mudar).</summary>
        public static void SaveTimers(Player player)
        {
            if (player == null) return;
            double left = ImmuneRemaining;
            if (left > 0d) player.m_customData[KeyImmuneLeft] = left.ToString("R", CultureInfo.InvariantCulture);
            else player.m_customData.Remove(KeyImmuneLeft);
        }

        public static void ClearImmunity(Player player) => GrantImmunity(player, 0d);

        /// <summary>
        /// O servidor manda segundos restantes, nunca horario: relogios diferentes nao importam. Daqui
        /// em diante contam pelo relogio monotonico, que mudar a hora do Windows nao mexe.
        /// </summary>
        public static void ApplyServerTimers(double pkRemaining, double huntPendingRemaining, double huntedRemaining, bool huntedUntilDeath = false)
        {
            double now = Mono;
            _pkUntil = pkRemaining > 0d ? now + pkRemaining : 0d;
            _huntPendingUntil = huntPendingRemaining > 0d ? now + huntPendingRemaining : 0d;
            _huntedUntil = huntedRemaining > 0d ? now + huntedRemaining : 0d;
            _huntedForever = huntedUntilDeath && huntPendingRemaining <= 0d;
        }

        public static void ApplyPk(bool permanent, PvpConfig.PkPenalty penalty)
        {
            _pkPermanent = permanent;
            _pkPenalty = penalty;
        }

        public static void ApplyPkCount(Player player, int count)
        {
            _pkCount = Math.Max(0, count);
            ZDO zdo = player != null && player.m_nview != null && player.m_nview.IsValid() ? player.m_nview.GetZDO() : null;
            if (zdo != null && zdo.GetInt(ZdoPkCount, 0) != _pkCount) zdo.Set(ZdoPkCount, _pkCount);
        }

        public static int PkCountOf(Player player)
        {
            if (player == null) return 0;
            if (player == Player.m_localPlayer) return _pkCount;
            ZNetView nview = player.m_nview;
            return nview != null && nview.IsValid() ? nview.GetZDO().GetInt(ZdoPkCount, 0) : 0;
        }

        /// <summary>
        /// Golpe dado pelo jogador local em <paramref name="victim"/>. Quem ataca alguem sem marca
        /// (nao agressor, nao PK, nao cacado) fora de arena e castelo vira agressor. Revidar em
        /// quem ja e agressor, ou em quem bateu no jogador local ha pouco, e defesa e nao marca.
        /// </summary>
        public static void MarkAttack(Player victim)
        {
            MarkCombat();
            if (PvpConfig.AggressorRule == null || !PvpConfig.AggressorRule.Value || victim == null) return;
            PvpFlags target = FlagsOf(victim);
            if ((target & (PvpFlags.Aggressor | PvpFlags.Pk | PvpFlags.Hunted | PvpFlags.Arena | PvpFlags.Castle)) != 0) return;
            if ((Current & (PvpFlags.Arena | PvpFlags.Castle)) != 0) return;
            if (HitMeRecently(victim.GetZDOID())) return;
            _aggressorUntil = Time.time + Mathf.Max(0f, PvpConfig.AggressorSeconds.Value);
        }

        /// <summary>
        /// <paramref name="attacker"/> bateu no jogador local nos ultimos AggressorSeconds: e quem
        /// comecou. A janela e a mesma da marca de agressor que ele ganhou ao bater.
        /// </summary>
        public static bool HitMeRecently(ZDOID attacker)
        {
            if (attacker.IsNone() || !_hitMeAt.TryGetValue(attacker, out float at)) return false;
            if (Time.time - at <= Mathf.Max(0f, PvpConfig.AggressorSeconds.Value)) return true;
            _hitMeAt.Remove(attacker);
            return false;
        }

        public static void ClearPk()
        {
            _pkUntil = 0d;
            _pkPermanent = false;
            _pkPenalty = PvpConfig.PkPenalty.Skills;
        }

        public static void MarkCombat()
            => _combatUntil = Time.time + Mathf.Max(0f, PvpConfig.CombatTagSeconds.Value);

        public static void RecordPvpHit(ZDOID attacker)
        {
            _lastPvpAttacker = attacker;
            _lastPvpHitTime = Time.time;
            if (!attacker.IsNone()) _hitMeAt[attacker] = Time.time;
            MarkCombat();

            // O servidor precisa saber quem bateu se o jogador deslogar em combate.
            Player local = Player.m_localPlayer;
            if (local != null && local.m_nview != null && local.m_nview.IsValid()
                && PvpRules.IsPlayerZdo(attacker, out long attackerId))
                local.m_nview.GetZDO().Set(ZdoLastAttacker, attackerId);
        }

        /// <summary>
        /// Depois de um golpe PvP: se ele deixou veneno ou fogo, o credito da morte vale
        /// enquanto o efeito durar. Veneno forte passa de 20 s (2 + raiz(2 x dano)) e o
        /// tique dele nao tem atacante, entao sem isso a morte cairia em PvE.
        /// </summary>
        public static void CoverDamageOverTime(Player victim)
        {
            SEMan seman = victim != null ? victim.GetSEMan() : null;
            if (seman == null) return;
            float longest = 0f;
            foreach (StatusEffect effect in seman.GetStatusEffects())
                if (effect is SE_Poison || effect is SE_Burning)
                    longest = Mathf.Max(longest, effect.GetRemaningTime());
            if (longest > 0f) _dotCreditUntil = Mathf.Max(_dotCreditUntil, Time.time + longest + 1f);
        }

        /// <summary>Quem leva o credito de uma morte que nao veio direto de um jogador.</summary>
        public static ZDOID RecentAttacker()
        {
            float window = Mathf.Max(0f, PvpConfig.KillCreditSeconds.Value);
            bool recent = Time.time - _lastPvpHitTime <= window || Time.time <= _dotCreditUntil;
            return recent ? _lastPvpAttacker : ZDOID.None;
        }

        public static void ClearAggressor() => _aggressorUntil = 0f;

        /// <summary>Morreu: a luta acabou (senao deslogar logo depois de renascer contaria como fuga).</summary>
        public static void ClearCombat()
        {
            _combatUntil = 0f;
            _pveCombatUntil = 0f;
        }

        public static void ForgetAttacker()
        {
            _lastPvpAttacker = ZDOID.None;
            _lastPvpHitTime = -9999f;
            _dotCreditUntil = -9999f;
            ClearPublishedAttacker(Player.m_localPlayer);
        }

        /// <summary>
        /// Quem bateu por ultimo (dh_lastPvpAttacker, que o servidor le se o jogador deslogar em
        /// combate) so vale enquanto o golpe for recente: CombatTagSeconds ou KillCreditSeconds, o
        /// maior. Antes o valor nunca saia da ZDO, e deslogar em combate com outra pessoa dava o
        /// abate a quem tinha batido horas antes (M4).
        /// </summary>
        private static void ExpirePublishedAttacker(Player player)
        {
            float window = Mathf.Max(PvpConfig.CombatTagSeconds.Value, PvpConfig.KillCreditSeconds.Value);
            if (Time.time - _lastPvpHitTime > window) ClearPublishedAttacker(player);
        }

        private static void ClearPublishedAttacker(Player player)
        {
            ZDO zdo = player != null && player.m_nview != null && player.m_nview.IsValid() ? player.m_nview.GetZDO() : null;
            if (zdo != null && zdo.GetLong(ZdoLastAttacker, 0L) != 0L) zdo.Set(ZdoLastAttacker, 0L);
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

            // Agressor em luta nao perde a marca: o relogio para enquanto ele estiver em combate
            // com jogador, senao ela acabaria no meio da briga e o revide viraria PK.
            float delta = _lastTick > 0f ? Mathf.Max(0f, Time.time - _lastTick) : 0f;
            _lastTick = Time.time;
            if (IsAggressor && InCombat && PvpConfig.AggressorPausesInCombat.Value) _aggressorUntil += delta;
            ExpirePublishedAttacker(player);

            Vector3 pos = player.transform.position;
            bool arena = PvpZones.IsArena(pos);
            string castle = PvpBridge.Castle(pos);
            // Castelo que ja caiu nesta janela de raid vira zona segura: a luta ali acabou.
            string fallenCastle = castle != null && PvpBridge.CastleSafe(pos) ? castle : null;
            if (fallenCastle != null) castle = null;
            // Castelo do RaidSystem e zona de guerra: la a imunidade nao segura ninguem.
            bool warZone = arena || (castle != null && PvpConfig.CastleIgnoresImmunity.Value);
            bool hunted = IsHunted;
            bool immune = IsImmune;
            bool combat = InCombat;
            string safeArea = PvpZones.SafeAreaName(pos)
                              ?? (fallenCastle != null ? $"Castelo {fallenCastle} (conquistado)" : null);
            bool transport = PvpZones.IsOnTransport(player);
            // Cacado e PK (PkNoSafeZone) nao tem zona segura nem transporte que os proteja.
            bool outlaw = hunted || (IsPk && PvpConfig.PkNoSafeZone.Value);

            bool protectedZone = !outlaw && !combat && (safeArea != null || transport);

            bool pvp;
            string label;
            bool pve = PvpPve.IsLocal;
            if (pve)
            {
                // PvE permanente vale em todo lugar, arena e castelo inclusive.
                pvp = false;
                label = PvpPve.Title;
            }
            else if (arena)
            {
                pvp = true;
                label = "Arena: " + PvpZones.ArenaName(pos);
            }
            else if (warZone)
            {
                pvp = true;
                label = "Castelo: " + castle;
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
            if (pve) flags |= PvpFlags.Pve;
            if (immune && !warZone && !pve) flags |= PvpFlags.Immune;
            if (IsPk) flags |= PvpFlags.Pk;
            if (_pkPermanent) flags |= PvpFlags.PkPermanent;
            if (hunted) flags |= PvpFlags.Hunted;
            if (IsHuntPending) flags |= PvpFlags.HuntPending;
            if (protectedZone && !warZone) flags |= PvpFlags.Protected;
            if (combat) flags |= PvpFlags.Combat;
            if (arena) flags |= PvpFlags.Arena;
            if (castle != null) flags |= PvpFlags.Castle;
            if (IsAggressor) flags |= PvpFlags.Aggressor;
            if (PvpRules.InOwnTerritory(player.GetPlayerID(), pos)) flags |= PvpFlags.InOwnWard;

            // Estado antes da bandeira: o aviso de troca (PvpHud.OnPvpChanged) le o motivo daqui.
            Current = flags;
            ZoneLabel = label;

            SetPvp(player, pvp);

            ZDO zdo = player.m_nview.GetZDO();
            if (zdo.GetInt(ZdoFlags, 0) != (int)flags) zdo.Set(ZdoFlags, (int)flags);
            PvpBosses.Tick(player);
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
