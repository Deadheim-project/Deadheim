using BepInEx;
using Deadheim.agesystem;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim.Craft
{
    public class Craft
    {
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private class FasterCrafting : BaseUnityPlugin
        {
            private static void Prefix(ref InventoryGui __instance) => __instance.m_craftDuration = 0.25f;
        }

        [HarmonyPatch(typeof(Player), "GetBuildPieces")]
        private class GetBuildPieces
        {
            private static void Postfix(List<Piece> __result)
            {
                if (__result == null) return;

                __result.ForEach((Action<Piece>)(p =>
                {

                    if (p.m_name.Contains("portal"))
                    {
                        p.m_resources[0].m_amount = 150;
                        p.m_resources[1].m_amount = 300;
                        p.m_resources[2].m_amount = 100;
                        return;
                    }

                    if (!AgeSystem.isDisabled((string)p.m_name))
                        return;

                    for (int index = 0; index < p.m_resources.Length; ++index)
                        ((Piece.Requirement)p.m_resources[index]).m_amount = 9999;
                }));
            }
        }

        [HarmonyPatch(typeof(Player), "GetAvailableRecipes")]
        private class GetAvailableRecipes
        {
            private static void Postfix(ref List<Recipe> available)
            {
                List<Recipe> recipeList = new List<Recipe>();
                foreach (Recipe recipe in available)
                {
                    if (AgeSystem.isDisabled(recipe.m_item.m_itemData.m_shared.m_name))
                    {
                        for (int index = 0; index < recipe.m_resources.Length; ++index)
                            ((Piece.Requirement)recipe.m_resources[index]).m_amount = 9999;
                    }
                }
            }
        }
    }
}

