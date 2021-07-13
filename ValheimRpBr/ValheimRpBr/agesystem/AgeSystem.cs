using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Deadheim.agesystem
{
    internal class AgeSystem
    {
        public static List<string> ageOfBronze = new List<string>()
        {
            "portal",
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
            "arrow_fire",
            "item_carrotsoup",
            "Porridge",
            " jam",
            "Carrot Butter",
            "Pork Rind",
            "Broth"
        };

        public static List<string> ageOfIron = new List<string>()
        {
            "iron",
            "stonecutter",
            "arrow_poison",
            "huntsman",
            "piece_workbench_ext4",
            "piece_forge_ext3",
            "sausage",
            "item_meadbase",
            "draugr",
            "ooze",
            "queensjam",
            "Vial",
            "Elixir",
            "Flask",
            "Potion",
            "razor",
            "chitin",
            "serpentscale",
            "battleaxe",
        };

        public static List<string> ageOfSilver = new List<string>()
        {
            "stonecutter",
            "arrow_poison",
            "huntsman",
            "piece_forge_ext4",
            "arrow_obsidian",
            "arrow_frost",     
            "item_serpentstew",
            "item_turnipstew",
            "silver",
            "drake",
            "wolf",
            "item_spear_wolffang",
            "item_spear_ancientbark",
            "Kebab",
            "Smoked Fish",
            "Pancakes"
        };

        public static List<string> ageOfLinen = new List<string>()
        {
              "artisan",
              "piece_blastfurnace",
              "windmill",
              "spinning",
              "arrow_needle",
              "lox",
              "item_loxpie",
              "item_fishwraps",
              "item_bloodpudding",
              "item_bread",
              "Fish Stew",
              "Blood Sausage",
              "Omlette",
              "lox",
              "blackmetal",
              "item_mace_needle",
              "padded",
        };

        public static List<string> Enchantments = new List<string>()
        {
              " Dust",
              " Shard",
              " Essence",
              " Reagent",
              "Enchanter",
              "Augmenter",
              "Rune of",
        };

        public static List<string> disabledCrafts = ageOfBronze.Concat(ageOfIron).Concat(ageOfSilver).Concat(ageOfLinen).Concat(Enchantments).ToList();

        public static bool isDisabled(string recipeName)
        {
           bool disabled = false;
           AgeSystem.disabledCrafts.ForEach(x =>
           {
               if (!recipeName.Contains(x))
                   return;

               disabled = true;
           });
           return disabled;
        }

        [HarmonyPatch(typeof(Player), "GetBuildPieces")]
        private class GetBuildPieces
        {
            private static void Postfix(List<Piece> __instance)
            {
                if (__instance == null) return;
                    
                List<Piece> piecesToBeRemoved = new List<Piece>();
                __instance.ForEach(p =>
               {
                   if (!isDisabled((string)p.m_name))
                       return;
                   piecesToBeRemoved.Add(p);
               });
                for (int index = 0; index < piecesToBeRemoved.Count; ++index)
                {
                    Piece piece = piecesToBeRemoved[index];
                    __instance.Remove(piece);
                }
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
                    if (isDisabled(recipe.m_item.m_itemData.m_shared.m_name))
                    {
                        recipeList.Add(recipe); 
                    }
                }
                foreach (Recipe recipe in recipeList)
                    available.Remove(recipe);
            }
        }
    }
}
