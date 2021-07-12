using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Deadheim.agesystem
{
  internal class AgeSystem
  {
    public static List<string> disabledCrafts = new List<string>()
    {
      "portal",
      "piece_workbench_ext4",
      "stonecutter",
      "artisan",
      "piece_forge_ext4",
      "piece_forge_ext3",
      "piece_blastfurnace",
      "windmill",
      "spinning",
      "item_cape",
      "trollleather",
      "Bone Arrow",
      "arrow_obsidian",
      "arrow_poison",
      "arrow_fire",
      "arrow_frost",
      "arrow_needle",
      "huntsman",
      "draugr",
      "ooze",
      "queensjam",
      "item_carrotsoup",
      "item_serpentstew",
      "item_turnipstew",
      "lox",
      "sausage",
      "item_meadbase",
      "item_loxpie",
      "item_fishwraps",
      "item_bloodpudding",
      "item_bread",
      "item_stagbreaker",
      "item_chest_bronze",
      "item_legs_bronze",
      "item_helmet_bronze",
      "item_shield_bronzebuckler",
      "item_mace_bronze",
      "item_spear_bronze",
      "item_sword_bronze",
      "item_pickaxe_bronze",
      "item_axe_bronze",
      "item_atgeir_bronze",
      "item_knife_copper",
      "battleaxe",
      "iron",
      "silver",
      "lox",
      "blackmetal",
      "razor",
      "chitin",
      "serpentscale",
      "item_mace_needle",
      "padded",
      "wolf",
      "item_spear_wolffang",
      "item_spear_ancientbark",
      "Vial",
      "Elixir",
      "Flask",
      "Potion",
      "trophy",
      " Dust",
      " Shard",
      " Essence",
      " Reagent",
      "Enchanter",
      "Augmenter",
      "Rune of",
      " jam",
      "Carrot Butter",
      "Nut-ella",
      "Ice Cream",
      "Pork Rind",
      "Kebab",
      "Chicken Fried Lox",
      "Glazed Carrots",
      "Bacon",
      "Smoked Fish",
      "Pancakes",
      "Pizza",
      "Coffee",
      "Spiced Latte",
      "Fire Cream",
      "Electric Cream Cone",
      "Acid Cream",
      "Porridge",
      "Jimmy's PBJ",
      "RK's Birthday Cake",
      "Hagis",
      "Candied Turnips",
      "Moochi",
      "Broth",
      "Fish Stew",
      "Blood Sausage",
      "Burger",
      "Omlette",
      "Boiled Egg",
      "Carrot Sticks",
      "Chef Hat",
      "Oven",
      "Grill",
      "Griddle",
      "Prep Table",
      "BronzeNails"
    }; 

    public static bool isDisabled(string recipeName)
    {
        bool disable = false;

        AgeSystem.disabledCrafts.ForEach((Action<string>) (x =>
        {
            if (!recipeName.Contains(x))
                return;

            disable = true;
        }));

        return disable;
    }

    [HarmonyPatch(typeof (Player), "GetBuildPieces")]
    private class getbuildpicesfix
    {
      private static void Postfix(ref List<Piece> __result)
      {
        if (__result == null)
          return;
        List<Piece> removethese = new List<Piece>();
        __result.ForEach((Action<Piece>) (p =>
        {
          if (!AgeSystem.isDisabled((string) p.m_name))
            return;
          removethese.Add(p);
          for (int index = 0; index < p.m_resources.Length; ++index)
            ((Piece.Requirement) p.m_resources[index]).m_amount = 999;
        }));
        for (int index = 0; index < removethese.Count; ++index)
        {
          Piece piece = removethese[index];
          __result.Remove(piece);
        }
      }
    }

    [HarmonyPatch(typeof (Player), "GetAvailableRecipes")]
    private class hideRecipes
    {
      private static void Postfix(ref List<Recipe> available)
      {
        List<Recipe> recipeList = new List<Recipe>();
        foreach (Recipe recipe in available)
        {
          ZLog.LogWarning(recipe.m_item.m_itemData.m_shared.m_name);
          if (AgeSystem.isDisabled(recipe.m_item.m_itemData.m_shared.m_name))
          {
            recipeList.Add(recipe);
            for (int index = 0; index < recipe.m_resources.Length; ++index)
              ((Piece.Requirement) recipe.m_resources[index]).m_amount = 999;
          }
        }
        foreach (Recipe recipe in recipeList)
          available.Remove(recipe);
      }
    }    
  }
}
