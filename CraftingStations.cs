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

        // O patch de raio de PrivateArea saiu daqui: o raio agora vem do perfil
        // em Wards/WardProfile.cs e e aplicado em WardPatches.AwakePatch.
    }
}
