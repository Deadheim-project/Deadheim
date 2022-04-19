using HarmonyLib;
using UnityEngine;

namespace Deadheim
{
    [HarmonyPatch]
    public class FasterBoats
    {

        [HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
        private class ChangeShipBaseSpeed
        {
            private static void Postfix(ref Vector3 __result)
            {
                __result *= Plugin.BoatWindSpeedmultiplier.Value;
            }

        }
    }
}
