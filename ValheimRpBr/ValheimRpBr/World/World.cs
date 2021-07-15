using HarmonyLib;
using System;

namespace Deadheim.world
{
    internal class World
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DungeonGenerator), "Generate", new Type[] { typeof(int), typeof(ZoneSystem.SpawnMode) })]
        private static void ApplyGeneratorSettings(ref DungeonGenerator __instance)
        {
            __instance.m_minRooms = 15;
            __instance.m_maxRooms = 30;
            __instance.m_campRadiusMin = 20;
            __instance.m_campRadiusMax = 40;
        }
    }
}
