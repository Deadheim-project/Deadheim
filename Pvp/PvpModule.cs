using BepInEx.Configuration;
using Deadheim.Vanilla;
using HarmonyLib;
using System;
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
        // Formato antigo (7.3.0 e antes): horario de parede do ultimo retreat. So lido, para converter.
        private const string KeyRetreatAtLegacy = "dh_retreatAt";
        // Segundos de recarga do /retreat que faltam, gravados no personagem.
        private const string KeyRetreatLeft = "dh_retreatLeft";
        // Fim da recarga no relogio monotonico (PvpState.Mono): adiantar a hora do Windows nao a zera.
        private static double _retreatReadyAt;

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
            Vanilla.Prefabs.PiecesReady += PvpHud.RegisterStatusEffects;
        }

        public static void Update()
        {
            PvpServer.Update();
            PvpClient.Update();

            Player player = Player.m_localPlayer;
            if (player == null) return;
            if (!PvpConfig.Active)
            {
                // Admin desligou o PvP com o jogo rodando: buff sem prazo (zona segura, PvE) nao fica preso.
                PvpHud.ClearBuffs(player);
                return;
            }

            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + 0.25f;
                PvpState.Tick(player);
            }
            PvpHud.Update(player);
            PvpRankPanel.Update();
        }

        // ---------------------------------------------------------------- teleporte

        /// <summary>
        /// Motivo para negar um teleporte "de fuga" (retreat, pedra), ou null. Com
        /// <paramref name="pveBlocks"/>, luta com monstro tambem bloqueia (RetreatBlockedByPveCombat).
        /// </summary>
        public static string TeleportRefusal(bool pveBlocks = false)
        {
            if (!PvpConfig.Active) return null;
            if (PvpState.IsHunted) return "Cacado nao pode teleportar.";
            if (PvpState.InEscapeCombat) return $"Em combate! Aguarde {Mathf.CeilToInt(PvpState.EscapeCombatRemaining)}s.";
            if (pveBlocks && PvpState.InPveCombat) return $"Em combate com monstro! Aguarde {Mathf.CeilToInt(PvpState.PveCombatRemaining)}s.";
            return null;
        }

        public static string RetreatRefusal(Player player)
        {
            string refusal = TeleportRefusal(PvpConfig.RetreatBlockedByPveCombat.Value);
            if (refusal != null || !PvpConfig.Active || player == null) return refusal;

            double cooldown = PvpConfig.RetreatCooldownMinutes.Value * 60d;
            if (cooldown <= 0d) return null;
            double left = Math.Min(RetreatRemaining, cooldown);
            return left > 0d ? "Retreat em recarga: " + PvpClient.FormatDuration(left) + "." : null;
        }

        public static double RetreatRemaining => Math.Max(0d, _retreatReadyAt - PvpState.Mono);

        public static void MarkRetreatUsed(Player player)
        {
            if (player == null) return;
            _retreatReadyAt = PvpState.Mono + Math.Max(0d, PvpConfig.RetreatCooldownMinutes.Value * 60d);
            SaveRetreat(player);
        }

        /// <summary>
        /// Recarga do /retreat gravada como segundos que faltam e descontada so com o jogo aberto.
        /// O horario de parede do 7.3.0 zerava ao adiantar o relogio do Windows.
        /// </summary>
        private static void LoadRetreat(Player player)
        {
            _retreatReadyAt = 0d;
            if (player == null) return;
            double cooldown = Math.Max(0d, PvpConfig.RetreatCooldownMinutes.Value * 60d);
            double left = 0d;
            if (player.m_customData.TryGetValue(KeyRetreatLeft, out string raw)
                && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double stored))
                left = stored;
            else if (player.m_customData.TryGetValue(KeyRetreatAtLegacy, out string legacy)
                     && double.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out double last))
                left = last + cooldown - PvpState.Now;
            player.m_customData.Remove(KeyRetreatAtLegacy);
            left = Math.Min(left, cooldown);
            _retreatReadyAt = left > 0d ? PvpState.Mono + left : 0d;
            SaveRetreat(player);
        }

        private static void SaveRetreat(Player player)
        {
            if (player == null) return;
            double left = RetreatRemaining;
            if (left > 0d) player.m_customData[KeyRetreatLeft] = left.ToString("R", CultureInfo.InvariantCulture);
            else player.m_customData.Remove(KeyRetreatLeft);
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
                LoadRetreat(__instance);
                PvpClient.SendHello();
                _nextTick = 0f;
            }
        }

        /// <summary>
        /// O perfil vai ser salvo (autosave, logout, respawn): grava o que falta de imunidade e de
        /// recarga do retreat, que correm no relogio monotonico e nao no m_customData.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static class SaveTimersPatch
        {
            private static void Prefix(Player __instance)
            {
                if (__instance == null || __instance != Player.m_localPlayer) return;
                PvpState.SaveTimers(__instance);
                SaveRetreat(__instance);
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix()
            {
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    PvpState.SaveTimers(player);
                    SaveRetreat(player);
                }
                _retreatReadyAt = 0d;
                RelogioServidor.Esquecer();
                PvpState.ResetSession();
                PvpClient.ResetSession();
                PvpHud.ResetSession();
                PvpRankPanel.Close();
            }
        }
    }
}
