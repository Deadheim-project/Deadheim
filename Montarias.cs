using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Estamina da sela das montarias (Lox, Asksvin). Era o mod SaddleStaminaControl
    /// (Detalhes.SaddleControl), que agora vive aqui: um mod a menos no pacote, a mesma config
    /// sincronizada pelo servidor e valendo na hora para as selas que ja existem.
    ///
    /// 0 em qualquer valor = o do jogo para aquela montaria (cada sela tem o seu).
    /// </summary>
    internal static class Montarias
    {
        public static ConfigEntry<float> MaxStamina;
        public static ConfigEntry<float> RunStaminaDrain;
        public static ConfigEntry<float> SwimStaminaDrain;
        public static ConfigEntry<float> StaminaRegen;
        public static ConfigEntry<float> StaminaRegenHungry;

        // Valores do jogo por tipo de montaria, lidos antes de sobrescrever.
        private static readonly Dictionary<string, float[]> _vanilla = new Dictionary<string, float[]>();

        public static void Bind(ConfigFile config)
        {
            const string section = "Montarias";
            // Padroes do SaddleStaminaControl, que o servidor ja usava.
            MaxStamina = Bind(config, section, "MaxStamina", 240f, "Estamina maxima da sela.");
            RunStaminaDrain = Bind(config, section, "RunStaminaDrain", 1f, "Estamina gasta por segundo correndo.");
            SwimStaminaDrain = Bind(config, section, "SwimStaminaDrain", 1f, "Estamina gasta por segundo nadando.");
            StaminaRegen = Bind(config, section, "StaminaRegen", 5f, "Estamina recuperada por segundo com a montaria alimentada.");
            StaminaRegenHungry = Bind(config, section, "StaminaRegenHungry", 3f, "Estamina recuperada por segundo com a montaria com fome.");

            foreach (ConfigEntry<float> entry in new[] { MaxStamina, RunStaminaDrain, SwimStaminaDrain, StaminaRegen, StaminaRegenHungry })
                entry.SettingChanged += (_, __) => ApplyAll();
        }

        private static ConfigEntry<float> Bind(ConfigFile config, string section, string key, float value, string description)
            => Plugin.Synced(config.Bind(section, key, value, description + " 0 = valor do jogo."));

        private static float Pick(ConfigEntry<float> entry, float vanilla) => entry.Value > 0f ? entry.Value : vanilla;

        /// <summary>
        /// Valores do jogo para esta montaria, lidos do prefab (que nunca e alterado): assim
        /// voltar uma config para 0 devolve o valor original mesmo numa sela ja ajustada.
        /// </summary>
        private static float[] VanillaFor(Sadle saddle)
        {
            Character mount = saddle.GetComponentInParent<Character>(true);
            string key = mount != null ? Wards.WardProfiles.CleanName(mount.gameObject) : null;
            if (key != null && _vanilla.TryGetValue(key, out float[] cached)) return cached;

            GameObject prefab = key != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(key) : null;
            Sadle source = prefab != null ? prefab.GetComponentInChildren<Sadle>(true) : null;
            if (source == null) return null;
            float[] vanilla = { source.m_maxStamina, source.m_runStaminaDrain, source.m_swimStaminaDrain, source.m_staminaRegen, source.m_staminaRegenHungry };
            _vanilla[key] = vanilla;
            return vanilla;
        }

        public static void Apply(Sadle saddle)
        {
            if (saddle == null || MaxStamina == null) return;
            float[] vanilla = VanillaFor(saddle);
            if (vanilla == null) return;

            saddle.m_maxStamina = Pick(MaxStamina, vanilla[0]);
            saddle.m_runStaminaDrain = Pick(RunStaminaDrain, vanilla[1]);
            saddle.m_swimStaminaDrain = Pick(SwimStaminaDrain, vanilla[2]);
            saddle.m_staminaRegen = Pick(StaminaRegen, vanilla[3]);
            saddle.m_staminaRegenHungry = Pick(StaminaRegenHungry, vanilla[4]);
        }

        /// <summary>Config nova (cfg salvo ou valor do servidor): vale para as selas ja carregadas.</summary>
        public static void ApplyAll()
        {
            foreach (Sadle saddle in Object.FindObjectsByType<Sadle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Apply(saddle);
        }

        [HarmonyPatch(typeof(Sadle), "Awake")]
        private static class SaddleAwakePatch
        {
            private static void Postfix(Sadle __instance) => Apply(__instance);
        }
    }
}
