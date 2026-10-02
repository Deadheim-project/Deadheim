using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Faixa de PvP por chefes. O nivel de cada jogador e o chefe mais avancado que ele ajudou a
    /// matar: o jogo conta a morte do chefe no perfil de todo mundo que deu dano nele
    /// (Character.OnDeath -> Game.RPC_RegisterKill), e o ServerCharacters guarda o perfil no
    /// servidor. Dois jogadores so se ferem se a diferenca de nivel for no maximo BossGapMax;
    /// fora disso nem um nem o outro causa dano. E o que protege o novato do veterano.
    ///
    /// Cada cliente publica o proprio nivel na ZDO, como as bandeiras de PvP (PvpState).
    /// </summary>
    internal static class PvpBosses
    {
        public static readonly int ZdoBossTier = "dh_bossTier".GetStableHashCode();

        /// <summary>So para o driver de teste: nivel do jogador local sem mexer no perfil.</summary>
        internal static Func<int> TestLocalTier;

        private static int _localTier;
        private static float _nextRefresh;
        private static string _orderText;
        private static List<string> _order;

        public static bool Active => PvpConfig.BossGapMax != null && PvpConfig.BossGapMax.Value >= 0;

        public static int LocalTier => _localTier;

        /// <summary>Chefes em ordem de progressao (BossOrder).</summary>
        public static List<string> Order
        {
            get
            {
                string text = PvpConfig.BossOrder.Value ?? string.Empty;
                if (_order != null && _orderText == text) return _order;
                _orderText = text;
                _order = text.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                return _order;
            }
        }

        public static void ResetSession()
        {
            _localTier = 0;
            _nextRefresh = 0f;
        }

        /// <summary>Recalcula o nivel do jogador local (a cada 5 s, o perfil muda pouco) e publica na ZDO.</summary>
        public static void Tick(Player player)
        {
            if (player == null || player.m_nview == null || !player.m_nview.IsValid()) return;
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 5f;
                _localTier = TestLocalTier != null ? TestLocalTier() : ComputeLocalTier();
            }
            ZDO zdo = player.m_nview.GetZDO();
            if (zdo.GetInt(ZdoBossTier, 0) != _localTier) zdo.Set(ZdoBossTier, _localTier);
        }

        public static void Refresh() => _nextRefresh = 0f;

        private static int ComputeLocalTier()
        {
            try
            {
                PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                if (profile?.m_playerStats == null || profile.m_playerStats.Length == 0) return 0;
                Dictionary<string, float>[] byModifier = profile.m_playerStats[0]?.m_enemyStats;
                Dictionary<string, float> kills = byModifier != null && byModifier.Length > 0 ? byModifier[0] : null;
                if (kills == null) return 0;

                List<string> order = Order;
                for (int i = order.Count - 1; i >= 0; i--)
                    if (kills.TryGetValue(order[i], out float count) && count > 0f) return i + 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim PvP] Nao foi possivel ler os chefes do perfil: " + ex.Message);
            }
            return 0;
        }

        public static int TierOf(Player player)
        {
            if (player == null) return 0;
            if (player == Player.m_localPlayer) return _localTier;
            ZNetView nview = player.m_nview;
            return nview != null && nview.IsValid() ? nview.GetZDO().GetInt(ZdoBossTier, 0) : 0;
        }

        /// <summary>Longe demais em chefes para se ferir. Na arena a faixa nao vale.</summary>
        public static bool OutOfRange(Player a, Player b, Vector3 where)
        {
            if (!Active || a == null || b == null || a == b) return false;
            if (PvpZones.IsArena(where)) return false;
            return Math.Abs(TierOf(a) - TierOf(b)) > PvpConfig.BossGapMax.Value;
        }

        public static string TierName(int tier)
        {
            if (tier <= 0) return "nenhum chefe";
            List<string> order = Order;
            string token = tier <= order.Count ? order[tier - 1] : "?";
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }

        /// <summary>Com quem o jogador de nivel <paramref name="tier"/> luta, para o /pvp.</summary>
        public static string RangeText(int tier)
        {
            if (!Active) return "Faixa por chefes desligada: luta com todo mundo.";
            int gap = PvpConfig.BossGapMax.Value;
            int low = Math.Max(0, tier - gap);
            int high = Math.Min(Order.Count, tier + gap);
            return $"Chefes: {tier} ({TierName(tier)}). Voce so luta com quem tem de {low} a {high} chefes (fora da arena).";
        }
    }
}
