using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.agesystem
{
    internal class AgeSystem
    {
        public static List<string> ageOfBronze = new List<string>()
        {
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
            "item_carrotsoup",
            "Porridge",
            " jam",
            "Carrot Butter",
            "Pork Rind",
            "Broth",
            "item_meadbase",
            "T2"
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
            "draugr",
            "ooze",
            "Vial",
            "Elixir",
            "Flask",
            "Potion",
            "knifechitin",
            "razor",
            "banded",
            "serpentscale",
            "battleaxe",
            "T3"
        };

        public static List<string> ageOfSilver = new List<string>()
        {
            "arrow_obsidian",
            "arrow_frost",
            "item_serpentstew",
            "silver",
            "drake",
            "wolf",
            "item_spear_wolffang",
            "item_spear_ancientbark",
            "Kebab",
            "Smoked Fish",
            "Pancakes",
            "draugr",
            "T4"
        };

        public static List<string> ageOfLinen = new List<string>()
        {
              "artisan",
              "piece_blastfurnace",
              "windmill",
              "spinning",
              "arrow_needle",
              "needle",
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
              "T5"
        };

        public static List<string> Enchantments = new List<string>()
        {
              " Dust",
              " Shard",
              " Essence",
              " Reagent",
              "Enchanter",
              "Augmenter",
              "Rune of"
        };

        public static List<string> GetDisabledCrafts()
        {
            if (Plugin.age == "bronze") return ageOfIron.Concat(ageOfSilver).Concat(ageOfLinen).ToList();
            if (Plugin.age == "iron") return ageOfSilver.Concat(ageOfLinen).ToList();
            if (Plugin.age == "silver") return ageOfLinen.ToList();
            if (Plugin.age == "linen") return new List<string>();

            return ageOfBronze.Concat(ageOfIron).Concat(ageOfSilver).Concat(ageOfLinen).ToList();
        }

        public static bool IsDisabled(string recipeName)
        {
            List<string> disabledCrafts = GetDisabledCrafts();
            foreach (string craft in disabledCrafts)
            {
                if (recipeName.ToLower().Contains(craft.ToLower())) return true;
            }

            return false;
        }

        public static void RemoveDisabledItems()
        {
            if (ObjectDB.instance.m_items.Count == 0 || ObjectDB.instance.GetItemPrefab("Amber") == null) return;

            var items = ObjectDB.instance.m_items;

            foreach (GameObject item in items)
            {
                if (IsDisabled(item.name))
                {
                    Piece piece = item.GetComponent<Piece>();

                    if (piece)
                    {
                        foreach (Piece.Requirement requirement in piece.m_resources.ToList())
                        {
                            requirement.m_amount = 9999;
                            requirement.m_recover = false;
                        }
                    }
                }
            }
        }

        public static void AddPortal()
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
            {
                if (gameObject.name == "portal_wood")
                {
                    gameObject.GetComponent<Piece>().m_resources[0].m_amount = 500;
                    gameObject.GetComponent<Piece>().m_resources[1].m_amount = 75;
                    gameObject.GetComponent<Piece>().m_resources[2].m_amount = 50;
                }
            }
        }

        public static void RemoveDisabledRecipes()
        {
            var recipes = ObjectDB.instance.m_recipes;

            if (!recipes.Any()) return;

            foreach (Recipe recipe in recipes)
            {
                if (IsDisabled(recipe.name))
                {
                    foreach (Piece.Requirement requirement in recipe.m_resources.ToList())
                    {
                        requirement.m_amount = 9999;
                        requirement.m_recover = false;
                    }
                }
            }
        }
    }
}
