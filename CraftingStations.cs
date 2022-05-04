using HarmonyLib;
using UnityEngine;

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
                __instance.m_areaMarker.m_radius = Plugin.WardRadius.Value;
                __instance.m_radius = Plugin.WardRadius.Value;

            }
        }
    }
}

