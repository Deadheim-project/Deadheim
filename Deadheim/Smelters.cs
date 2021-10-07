using HarmonyLib;

namespace Deadheim
{
    class Smelters
    {
        [HarmonyPatch(typeof(Smelter), nameof(Smelter.Awake))]
        public static class Smelter_Awake_Patch
        {
            private static void Prefix(ref Smelter __instance)
            {
                string kilnName = "$piece_charcoalkiln";
                string smelterName = "$piece_smelter";
                string furnaceName = "$piece_blastfurnace";

                if (__instance.m_name.Equals(kilnName))
                {
                    __instance.m_maxOre = 100;
                    __instance.m_secPerProduct = 20;
                }
                else if (__instance.m_name.Equals(smelterName))
                {
                    __instance.m_maxOre = 50;
                    __instance.m_maxFuel = 100;
                    __instance.m_secPerProduct = 20;
                    __instance.m_fuelPerProduct = 2;
                }
                else if (__instance.m_name.Equals(furnaceName))
                {
                    __instance.m_maxOre = 50;
                    __instance.m_maxFuel = 100;
                    __instance.m_secPerProduct = 20;
                    __instance.m_fuelPerProduct = 2;
                }
            }
        }

        [HarmonyPatch(typeof(Smelter), nameof(Smelter.Awake))]
        public static class OnAddOre
        {
            private static void Postfix(Smelter __instance)
            {
                if (!__instance) return;

                var item = __instance.m_conversion.Find(x => x.m_from.m_itemData.m_dropPrefab.name.ToLower().Contains("flametal"));

                __instance.m_conversion.Remove(item);
            }
        }
    }
}
