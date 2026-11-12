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
        // Formato 7.9 (recarga unica do /retreat e da pedra): so lido, para converter.
        private const string KeyRetreatLeft = "dh_retreatLeft";
        private static float _nextTick;

        public static void Init(ConfigFile config)
        {
            PvpConfig.Bind(config);
            DiscordLog.Bind(config);
            // Acesso de guilda aos wards pelo mod Guilds.
            if (Wards.WardBridge.GuildOfPlayer == null)
                Wards.WardBridge.GuildOfPlayer = PvpGuilds.GuildOf;
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
            // A mesma regra do portal (HuntedCanUsePortals), que a descricao da config promete para a pedra e o retreat.
            if (PvpState.IsHunted && !PvpConfig.HuntedCanUsePortals.Value) return "Cacado: nao da para teleportar.";
            if (PvpState.InEscapeCombat) return $"Em combate: aguarde {Mathf.CeilToInt(PvpState.EscapeCombatRemaining)}s.";
            if (pveBlocks && PvpState.InPveCombat) return $"Em combate com monstro: aguarde {Mathf.CeilToInt(PvpState.PveCombatRemaining)}s.";
            return null;
        }

        public static string RetreatRefusal(Player player, Cooldown kind = Cooldown.Retreat1)
        {
            string refusal = TeleportRefusal(PvpConfig.RetreatBlockedByPveCombat.Value);
            if (refusal != null || !PvpConfig.Active || player == null) return refusal;

            double left = Remaining(kind);
            return left > 0d ? Label(kind) + " em recarga: faltam " + PvpClient.FormatDuration(left) + "." : null;
        }

        /// <summary>Os tres cooldowns independentes de teleporte: pedra, /retreat 1 e /retreat 2.</summary>
        public enum Cooldown { Hearthstone = 0, Retreat1 = 1, Retreat2 = 2 }

        private static readonly string[] CooldownKeys = { "dh_cdLeft_hearthstone", "dh_cdLeft_retreat1", "dh_cdLeft_retreat2" };
        // Fim de cada recarga no relogio monotonico (PvpState.Mono): adiantar a hora do Windows nao a zera.
        private static readonly double[] _readyAt = new double[3];

        public static string Label(Cooldown kind) =>
            kind == Cooldown.Hearthstone ? "Hearthstone" : kind == Cooldown.Retreat2 ? "Retreat 2" : "Retreat 1";

        public static double TotalSeconds(Cooldown kind)
        {
            ConfigEntry<float> entry = kind == Cooldown.Hearthstone ? PvpConfig.HearthstoneCooldownSeconds
                : kind == Cooldown.Retreat2 ? PvpConfig.Retreat2CooldownSeconds : PvpConfig.Retreat1CooldownSeconds;
            return Math.Max(0d, entry?.Value ?? 1800f);
        }

        /// <summary>Segundos que faltam da recarga (0 se livre), limitados ao total configurado.</summary>
        public static double Remaining(Cooldown kind) =>
            Math.Min(Math.Max(0d, _readyAt[(int)kind] - PvpState.Mono), TotalSeconds(kind));

        public static void MarkRetreatUsed(Player player, Cooldown kind = Cooldown.Retreat1)
        {
            if (player == null) return;
            _readyAt[(int)kind] = PvpState.Mono + TotalSeconds(kind);
            SaveCooldowns(player);
        }

        /// <summary>Zera as tres recargas (usado pelo PvpTestDriver).</summary>
        public static void ClearRetreatCooldown(Player player)
        {
            for (int i = 0; i < _readyAt.Length; i++) _readyAt[i] = 0d;
            SaveCooldowns(player);
        }

        /// <summary>
        /// Recargas gravadas como segundos que faltam e descontadas so com o jogo aberto.
        /// Migra os formatos antigos: dh_retreatLeft (7.9, recarga unica) vira a do Retreat 1 e
        /// dh_retreatAt (7.3.0, horario de parede) tambem.
        /// </summary>
        private static void LoadCooldowns(Player player)
        {
            for (int i = 0; i < _readyAt.Length; i++) _readyAt[i] = 0d;
            if (player == null) return;
            for (int i = 0; i < _readyAt.Length; i++)
            {
                double total = TotalSeconds((Cooldown)i);
                double left = 0d;
                if (player.m_customData.TryGetValue(CooldownKeys[i], out string raw)
                    && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double stored))
                    left = stored;
                else if (i == (int)Cooldown.Retreat1)
                {
                    if (player.m_customData.TryGetValue(KeyRetreatLeft, out string old)
                        && double.TryParse(old, NumberStyles.Float, CultureInfo.InvariantCulture, out double oldLeft))
                        left = oldLeft;
                    else if (player.m_customData.TryGetValue(KeyRetreatAtLegacy, out string legacy)
                             && double.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out double last))
                        left = last + total - PvpState.Now;
                }
                left = Math.Min(left, total);
                _readyAt[i] = left > 0d ? PvpState.Mono + left : 0d;
            }
            player.m_customData.Remove(KeyRetreatAtLegacy);
            player.m_customData.Remove(KeyRetreatLeft);
            SaveCooldowns(player);
        }

        private static void SaveCooldowns(Player player)
        {
            if (player == null) return;
            for (int i = 0; i < _readyAt.Length; i++)
            {
                double left = Remaining((Cooldown)i);
                if (left > 0d) player.m_customData[CooldownKeys[i]] = left.ToString("R", CultureInfo.InvariantCulture);
                else player.m_customData.Remove(CooldownKeys[i]);
            }
        }

        // ----------------------------------------------------------------- ciclo de vida

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                PvpState.LoadFromPlayer(__instance);
                LoadCooldowns(__instance);
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
                SaveCooldowns(__instance);
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
                    SaveCooldowns(player);
                }
                for (int i = 0; i < _readyAt.Length; i++) _readyAt[i] = 0d;
                RelogioServidor.Esquecer();
                PvpState.ResetSession();
                PvpClient.ResetSession();
                PvpHud.ResetSession();
                PvpRankPanel.Close();
            }
        }
    }
}
