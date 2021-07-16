using HarmonyLib;
using System;
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
            "battleaxe"
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
              "padded"
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

        public static List<string> GetDisabledCrafts() {
            if (Plugin.age == "bronze") return ageOfIron.Concat(ageOfSilver).Concat(ageOfLinen).ToList();
            if (Plugin.age == "iron") return ageOfSilver.Concat(ageOfLinen).ToList();
            if (Plugin.age == "silver") return ageOfLinen.ToList();
            if (Plugin.age == "linen") return new List<string> ();

            return ageOfBronze.Concat(ageOfIron).Concat(ageOfSilver).Concat(ageOfLinen).ToList();            
        }

        
        public static bool isDisabled(string recipeName)
        {
            bool disabled = false;
            GetDisabledCrafts().ForEach(x =>
            {
                if (!recipeName.Contains(x))
                    return;

                disabled = true;
            });
            return disabled;
        }       
    }
}