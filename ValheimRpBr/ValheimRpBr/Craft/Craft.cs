using BepInEx;
using HarmonyLib;

namespace Deadheim.Craft
{
    public class Craft
    {
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private class fasterCrafting : BaseUnityPlugin
        {
            private static void Prefix(ref InventoryGui __instance) => __instance.m_craftDuration = 0.25f;
        }
    }
}
