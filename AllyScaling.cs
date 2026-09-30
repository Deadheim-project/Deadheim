using System;
using System.Reflection;
using Deadheim.Pvp;
using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Bonus de monstro por jogador perto (vanilla: +40% de vida e +4% de dano por jogador a
    /// 200 m; o CLLC muda os percentuais) so conta aliados de quem esta lutando: o proprio,
    /// o grupo e a guilda. Sem isso, um estranho passando perto deixa o monstro dos outros
    /// mais forte.
    ///
    /// O vanilla conta por posicao (Game.GetPlayerDifficulty) e nao sabe quem luta. Quem
    /// luta e conhecido no RPC_Damage, que roda no dono do alvo: golpe de monstro em jogador
    /// ancora na vitima; golpe de jogador em monstro, no atacante. Fora disso (veneno, fogo,
    /// queda), fica a conta do vanilla.
    ///
    /// O Groups so conhece o grupo do jogador local; para comparar dois outros jogadores cada
    /// cliente publica a chave do seu grupo na ZDO (ZdoGroupKey).
    /// </summary>
    internal static class AllyScaling
    {
        internal static readonly int ZdoGroupKey = "dh_groupKey".GetStableHashCode();

        /// <summary>Quem esta lutando no golpe sendo processado agora. O teste tambem usa.</summary>
        internal static Player Anchor;

        private static float _nextPublish;
        private static bool _cllcResolved;
        private static MethodInfo _cllcRange;

        private static bool Enabled
            => Plugin.MonsterScalingAlliesOnly == null || Plugin.MonsterScalingAlliesOnly.Value;

        internal static bool Allied(Player a, Player b)
        {
            if (a == null || b == null) return false;
            if (a == b) return true;
            if (PvpGuilds.SameGuild(a, b)) return true;
            if (a == Player.m_localPlayer || b == Player.m_localPlayer) return PvpGroups.SameGroup(a, b);
            long key = GroupKey(a);
            return key != 0L && key == GroupKey(b);
        }

        private static long GroupKey(Player player)
        {
            if (player == Player.m_localPlayer) return PvpGroups.LocalGroupKey();
            ZNetView nview = player.m_nview;
            return nview != null && nview.IsValid() ? nview.GetZDO().GetLong(ZdoGroupKey, 0L) : 0L;
        }

        /// <summary>O raio do CLLC quando ele esta instalado (ele troca o do vanilla); senao o do vanilla.</summary>
        private static float Range(Game game)
        {
            if (!_cllcResolved)
            {
                _cllcResolved = true;
                _cllcRange = AccessTools.TypeByName("CreatureLevelControl.CreatureLevelControl")
                    ?.GetMethod("getMultiplayerRangeValue", BindingFlags.Public | BindingFlags.Static);
            }
            if (_cllcRange != null)
            {
                try { return (float)_cllcRange.Invoke(null, null); }
                catch { _cllcRange = null; }
            }
            return game.m_difficultyScaleRange;
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class DamageContext
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                Anchor = null;
                if (!Enabled || hit == null) return;
                if (__instance is Player victim) Anchor = victim;
                else if (hit.GetAttacker() is Player attacker) Anchor = attacker;
            }

            private static void Finalizer() => Anchor = null;
        }

        [HarmonyPatch(typeof(Game), nameof(Game.GetPlayerDifficulty))]
        private static class CountAllies
        {
            // Antes do CLLC, que aplica o minimo, o maximo e o extra dele em cima desta conta.
            [HarmonyPriority(Priority.First)]
            private static void Postfix(Game __instance, Vector3 pos, ref int __result)
            {
                Player anchor = Anchor;
                if (anchor == null || !Enabled || __instance.m_forcePlayers > 0) return;

                float range = Range(__instance);
                int count = 0;
                foreach (Player player in Player.GetAllPlayers())
                    if (player != null && Utils.DistanceXZ(player.transform.position, pos) < range && Allied(anchor, player))
                        count++;
                __result = Mathf.Clamp(count, 1, Mathf.Max(1, __instance.m_difficultyScaleMaxPlayers));
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class PublishGroupKey
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || Time.time < _nextPublish) return;
                _nextPublish = Time.time + 2f;
                if (!Enabled || __instance.m_nview == null || !__instance.m_nview.IsValid()) return;

                long key = PvpGroups.LocalGroupKey();
                ZDO zdo = __instance.m_nview.GetZDO();
                if (zdo.GetLong(ZdoGroupKey, 0L) != key) zdo.Set(ZdoGroupKey, key);
            }
        }
    }
}
