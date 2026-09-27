using BepInEx.Configuration;
using Deadheim.Vanilla;
using HarmonyLib;
using System.Globalization;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Ponto de entrada do modulo de PvP: liga a config, o ciclo de vida e o laco de
    /// atualizacao, e concentra as regras de teleporte que o /retreat e o Hearthstone usam.
    /// </summary>
    internal static class PvpModule
    {
        private const string KeyRetreatAt = "dh_retreatAt";

        private static float _nextTick;
        private static float _coinsDefaultWeight = -1f;
        private static int _coinsDefaultStack = -1;

        public static void Init(ConfigFile config)
        {
            PvpConfig.Bind(config);
            // Acesso de guilda aos wards pelo Guilds mesmo sem o RaidSystem. Com ele instalado,
            // o RaidSystem registra o proprio resolvedor depois e prevalece (e o mesmo Guilds).
            if (Wards.WardBridge.GuildOfPlayer == null)
                Wards.WardBridge.GuildOfPlayer = PvpGuilds.GuildOf;
            Prefabs.PiecesReady += ApplyItemTweaks;
            PvpConfig.CoinsWeightless.SettingChanged += (_, __) => ApplyItemTweaks();
            PvpConfig.CoinsMaxStack.SettingChanged += (_, __) => ApplyItemTweaks();
            PvpConfig.Enabled.SettingChanged += (_, __) => ApplyItemTweaks();
        }

        public static void Update()
        {
            PvpServer.Update();

            Player player = Player.m_localPlayer;
            if (player == null || !PvpConfig.Active) return;

            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + 0.25f;
                PvpState.Tick(player);
            }
            PvpHud.Update(player);
            PvpRankPanel.Update();
        }

        // ---------------------------------------------------------------- teleporte

        /// <summary>Motivo para negar um teleporte "de fuga" (retreat, pedra), ou null.</summary>
        public static string TeleportRefusal()
        {
            if (!PvpConfig.Active) return null;
            if (PvpState.IsHunted) return "Cacado nao pode teleportar.";
            if (PvpState.InCombat) return $"Em combate! Aguarde {Mathf.CeilToInt(PvpState.CombatRemaining)}s.";
            return null;
        }

        public static string RetreatRefusal(Player player)
        {
            string refusal = TeleportRefusal();
            if (refusal != null || !PvpConfig.Active || player == null) return refusal;

            double cooldown = PvpConfig.RetreatCooldownMinutes.Value * 60d;
            if (cooldown <= 0d) return null;
            if (player.m_customData.TryGetValue(KeyRetreatAt, out string raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double last))
            {
                double left = last + cooldown - PvpState.Now;
                if (left > 0d) return "Retreat em recarga: " + PvpClient.FormatDuration(left) + ".";
            }
            return null;
        }

        public static void MarkRetreatUsed(Player player)
        {
            if (player == null) return;
            player.m_customData[KeyRetreatAt] = PvpState.Now.ToString("R", CultureInfo.InvariantCulture);
        }

        // -------------------------------------------------------------------- moeda

        /// <summary>Coins sem peso e com pilha grande. Guarda o original para poder voltar.</summary>
        public static void ApplyItemTweaks()
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData?.m_shared == null) return;

            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            if (_coinsDefaultWeight < 0f)
            {
                _coinsDefaultWeight = shared.m_weight;
                _coinsDefaultStack = shared.m_maxStackSize;
            }

            bool active = PvpConfig.Active;
            shared.m_weight = active && PvpConfig.CoinsWeightless.Value ? 0f : _coinsDefaultWeight;
            shared.m_maxStackSize = active && PvpConfig.CoinsMaxStack.Value > 0
                ? Mathf.Clamp(PvpConfig.CoinsMaxStack.Value, 1, 1000000)
                : _coinsDefaultStack;
        }

        // ----------------------------------------------------------------- ciclo de vida

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                PvpState.LoadFromPlayer(__instance);
                PvpClient.SendHello();
                _nextTick = 0f;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix()
            {
                PvpState.ResetSession();
                PvpHud.ResetSession();
                PvpRankPanel.Close();
            }
        }
    }
}
