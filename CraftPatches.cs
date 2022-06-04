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
    }
}
