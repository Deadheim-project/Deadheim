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
                try
                {
                    __instance.m_areaMarker.m_radius = Plugin.WardRadius.Value;
                    __instance.m_radius = Plugin.WardRadius.Value;

                }
                catch
                {
                }
            }
        }

        [HarmonyPatch(typeof(CraftingStation), "Start")]
        public static class WorkbenchRangeIncrease
        {
            public static void Prefix(ref CraftingStation __instance, ref float ___m_rangeBuild, GameObject ___m_areaMarker)
            {
                try
                {
                    ___m_rangeBuild = 60;
                    ___m_areaMarker.GetComponent<CircleProjector>().m_radius = ___m_rangeBuild;
                    float scaleIncrease = (___m_rangeBuild - 20f) / 20f * 100f;
                    ___m_areaMarker.gameObject.transform.localScale = new Vector3(scaleIncrease / 100, 1f, scaleIncrease / 100);         
                }
                catch
                {
                }
            }
        }
    }
}

