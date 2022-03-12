using HarmonyLib;

namespace Deadheim.FasterBoats
{
    [HarmonyPatch]
    public class FasterBoats
    {

        [HarmonyPatch(typeof(Ship), "GetSailForce")]
        private class BoatWindSpeedPatcher
        {
            private static void Prefix(ref float sailSize) => sailSize *= Plugin.BoatWindSpeedmultiplier.Value;
        }

        [HarmonyPatch(typeof(Ship), "Start")]
        private class BoatRudderSpeedPatcher
        {
            private static void Prefix(Ship __instance)
            {
                float num = Plugin.BoatRudderSpeedmultiplier.Value * 0.125f;
                __instance.m_backwardForce = num;
            }
        }
    }
}
