using HarmonyLib;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Deadheim.Craft
{
    public class CraftPatches
    {
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private class FasterCrafting
        {
            private static void Prefix(ref InventoryGui __instance) => __instance.m_craftDuration = 0.25f;
        }

        [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
        private class UpdateKnownRecipesList
        {
            private static void Postfix()
            {
                ItemService.RemoveDisabledRecipes();
            }
        }

        [HarmonyPatch(typeof(Player), "GetBuildPieces")]
        private class GetBuildPieces
        {
            private static List<Piece> Postfix(List<Piece> __result)
            {
                return ItemService.RemoveDisabledItems(__result);
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        public static class ItemDrop_Awake_Patch
        {
            private static void Prefix(ref ItemDrop __instance)
            {
                float itemWeightReduction = 0.3f;
                float itemStackMultiplier = 0.6f;
                __instance.m_itemData.m_shared.m_weight = __instance.m_itemData.m_shared.m_weight * (1 - itemWeightReduction);

                if (__instance.m_itemData.m_shared.m_maxStackSize <= 1) return;

                __instance.m_itemData.m_shared.m_maxStackSize = (int)Math.Round(__instance.m_itemData.m_shared.m_maxStackSize * (1 + itemStackMultiplier));
            }
        }
    }
}
