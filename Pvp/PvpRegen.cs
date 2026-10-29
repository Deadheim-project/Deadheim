using HarmonyLib;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Regeneracao de vida pela comida reduzida em combate PvP (PvpCombatRegenReduction): enquanto a
    /// marca de combate dura, o relogio de 10 s da regeneracao do jogo anda mais devagar na mesma
    /// proporcao (50% = cura a cada 20 s, 100% = nunca). Pocao, hidromel, cajado e buff continuam
    /// curando: so a cura passiva diminui.
    /// </summary>
    internal static class PvpRegen
    {
        // Player.m_foodRegenTimer e privado no jogo: por reflexao, nunca lido direto.
        private static readonly AccessTools.FieldRef<Player, float> RegenTimer =
            AccessTools.FieldRefAccess<Player, float>("m_foodRegenTimer");

        private static float Reduction()
        {
            if (!PvpConfig.Active || PvpConfig.PvpCombatRegenReduction == null) return 0f;
            return Mathf.Clamp01(PvpConfig.PvpCombatRegenReduction.Value / 100f);
        }

        [HarmonyPatch(typeof(Player), "UpdateFood")]
        private static class UpdateFoodPatch
        {
            private static void Prefix(Player __instance, out float __state)
            {
                __state = RegenTimer(__instance);
            }

            private static void Postfix(Player __instance, float __state)
            {
                if (__instance != Player.m_localPlayer || !PvpState.InCombat) return;
                float reduction = Reduction();
                if (reduction <= 0f) return;
                float now = RegenTimer(__instance);
                // Menor que antes = o jogo acabou de curar e zerou o relogio: fica como esta.
                if (now > __state) RegenTimer(__instance) = __state + (now - __state) * (1f - reduction);
            }
        }
    }
}
