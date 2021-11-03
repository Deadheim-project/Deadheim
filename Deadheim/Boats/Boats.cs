using HarmonyLib;

namespace Deadheim.FasterBoats
{
    [HarmonyPatch]
    public class FasterBoats
    {
        private static float boatWindSpeedmultiplier = 2f;
        private static float boatRudderSpeedmultiplier = 4f;

        [HarmonyPatch(typeof(Ship), "GetSailForce")]
        private class BoatWindSpeedPatcher
        {
            private static void Prefix(ref float sailSize) => sailSize *= boatWindSpeedmultiplier;
        }

        [HarmonyPatch(typeof(Ship), "Start")]
        private class BoatRudderSpeedPatcher
        {
            private static void Prefix(Ship __instance)
            {
                float num = boatRudderSpeedmultiplier * 0.125f;
                __instance.m_backwardForce = num;
            }
        }
    }
}
