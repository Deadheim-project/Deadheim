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
            private static void Postfix()
            {
                AgeSystem.RemoveDisabledRecipes();
                AgeSystem.RemoveDisabledItems();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        public static class ObjectDB_CopyOtherDB_Patch
        {
            public static void Postfix()
            {
                AgeSystem.AddPortal();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        public static class ObjectDB_Awake_Patch
        {
            public static void Postfix()
            {
                AgeSystem.AddPortal();
            }
        }
    }
}

