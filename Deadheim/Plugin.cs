using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [BepInPlugin(PluginGUID, PluginGUID, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "2.1.0";
        public const string PluginGUID = "Detalhes.Deadheim";
        public static string steamId = "";
        public static ConfigEntry<string> Age;
        public static ConfigEntry<string> Vip;
        public static ConfigEntry<string> Tag;
        public static ConfigEntry<string> Stone;
        public static ConfigEntry<string> Bronze;
        public static ConfigEntry<string> Iron;
        public static ConfigEntry<string> Silver;
        public static ConfigEntry<string> Blackmetal;
        public static ConfigEntry<string> Fire;
        public static ConfigEntry<float> WardReductionDamage;
        public static ConfigEntry<float> SkillMultiplier;
        public static ConfigEntry<int> SafeArea;
        public static ConfigEntry<bool> ResetWorldDay;

        public static int maxPlayers = 50;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();
        public static List<string> dropTypes = new List<string>(new string[] { "Material", "Ammo", "Customization", "Trophie", "Torch", "Misc" });

        public static bool hasSpawned = false;
        Harmony _harmony = new Harmony("Detalhes.deadheim");

        private void Awake()
        {
            Config.SaveOnConfigSet = true;

            Age = Config.Bind("Server config", "Age", "stone",
                       new ConfigDescription("Age", null,
                                new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Vip = Config.Bind("Server config", "Vip", "76561198053330247",
           new ConfigDescription("VipList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Tag = Config.Bind("Server config", "Tag", "76561198053330247,PVPTOTAL;",
           new ConfigDescription("PvpList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardReductionDamage = Config.Bind("Server config", "WardReductionDamage", 99.0f,
            new ConfigDescription("WardReductionDamage", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillMultiplier = Config.Bind("Server config", "SkillMultiplier", 0.5f,
            new ConfigDescription("SkillMultiplier", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SafeArea = Config.Bind("Server config", "SafeArea", 1500,
            new ConfigDescription("SafeArea", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ResetWorldDay = Config.Bind("Server config", "ResetWorldDay", false,
            new ConfigDescription("ResetWorldDay", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));


            Tag = Config.Bind("Server config", "Tag", "76561198053330247,PVPTOTAL;",
           new ConfigDescription("PvpList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Stone = Config.Bind("Age Server config", "Stone", "",
                new ConfigDescription("Stone", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Bronze = Config.Bind("Age Server config", "Bronze", "item_chest_bronze,item_legs_bronze,item_helmet_bronze,item_shield_bronzebuckler,item_mace_bronze,item_spear_bronze,item_sword_bronze,item_pickaxe_bronze,item_axe_bronze,item_atgeir_bronze,item_knife_copper,item_carrotsoup,Porridge,jam,Carrot Butter,Pork Rind,Broth,item_meadbase,T2",
                new ConfigDescription("Bronze", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Iron = Config.Bind("Age Server config", "Iron", "iron,stonecutter,arrow_poison,huntsman,piece_workbench_ext4,piece_forge_ext3,sausage,draugr,ooze,Vial,Elixir,Flask,Potion,knifechitin,razor,banded,serpentscale,battleaxe,T3",
                new ConfigDescription("Iron", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Silver = Config.Bind("Age Server config", "Silver", "arrow_obsidian,arrow_frost,item_serpentstew,silver,drake,wolf,item_spear_wolffang,item_spear_ancientbark,Kebab,Smoked Fish,Pancakes,draugr,Omlette,T4",
                new ConfigDescription("Silver", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Blackmetal = Config.Bind("Age Server config", "Blackmetal", "artisan,windmill,arrow_needle,needle,lox,item_loxpie,item_fishwraps,item_bloodpudding,item_bread,Fish Stew,Blood Sausage,lox,spinning,blastfurnace,blackmetal,item_mace_needle,padded,T5",
                new ConfigDescription("Blackmetal", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Fire = Config.Bind("Age Server config", "Fire", "",
                new ConfigDescription("Fire", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            _harmony.PatchAll();
            PortalToken.LoadAssets();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Numlock))
            {
                Debug.Log(EnvMan.instance.GetDay(ZNet.instance.m_netTime));
            }      
        }
    }
}
