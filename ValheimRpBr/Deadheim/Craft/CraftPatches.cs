using BepInEx;
using HarmonyLib;
using Deadheim.agesystem;

namespace Deadheim.Craft
{
    public class CraftPatches
    {
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private class FasterCrafting : BaseUnityPlugin
        {
            private static void Prefix(ref InventoryGui __instance) => __instance.m_craftDuration = 0.25f;
        }

        [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
        private class UpdateKnownRecipesList : BaseUnityPlugin
        {
            private static void Prefix()
            {
                AgeSystem.RemoveDisabledRecipes();
                AgeSystem.RemoveDisabledItems();
            }
        }
    }
}

