using HarmonyLib;

namespace Deadheim
{
    class CraftingStations
    {
        [HarmonyPatch(typeof(CraftingStation), "CheckUsable")]
        public static class WorkbenchRemoveRestrictions
        {
            private static bool Prefix(ref CraftingStation __instance)
            {
                __instance.m_craftRequireRoof = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), "Awake")]
        public static class PrivateAreaAwake
        {
            public static void Postfix(ref PrivateArea __instance)
            {
                int radius = Plugin.WardRadius.Value;

                if (__instance.m_name.Contains("AdminWard")) radius = 150;
                if (__instance.m_name.Contains("RaidWard")) radius = 100;

                __instance.m_areaMarker.m_radius = radius;
                __instance.m_radius = radius;
            }
        }
    }
}


