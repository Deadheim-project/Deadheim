using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    [BepInPlugin(PluginGUID, PluginGUID, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    [BepInDependency("org.bepinex.plugins.groups", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "3.9.97";
        public const string PluginGUID = "ZzDetalhes.Deadheim";
        public static string steamId = "";
        public static ConfigEntry<string> Vip;
        public static ConfigEntry<string> AdminList;
        public static ConfigEntry<string> PrefabToRecalculateRecipeOnLogout;
        public static ConfigEntry<string> PiecesToRemoveResourceDrop;
        public static ConfigEntry<int> WardRadius;
        public static ConfigEntry<string> RaidTimeToAllowUtc;
        public static ConfigEntry<string> RaidEnabledPositions;
        public static ConfigEntry<string> StaffMessage;
        public static ConfigEntry<string> ItemToSetMaxLevel;
        public static ConfigEntry<string> CityLocation;
        public static ConfigEntry<string> DungeonPrefabs;
        public static ConfigEntry<int> PlayersInsideWardForRaid;
        public static ConfigEntry<float> SkillMultiplier;
        public static ConfigEntry<float> BoatWindSpeedmultiplier;
        public static ConfigEntry<float> BoatRudderSpeedmultiplier;
        public static ConfigEntry<float> CapeRunicSpeed;
        public static ConfigEntry<float> CapeRunicRegen;
        public static ConfigEntry<float> SkillDeathFactor;
        public static ConfigEntry<int> SafeArea;        
        public static ConfigEntry<int> WardLimit;       
        public static ConfigEntry<int> WardLimitVip;        
        public static ConfigEntry<int> DropPercentagePerItem;
        public static ConfigEntry<int> WardChargeDurationInSec;
        public static ConfigEntry<int> RecipeCostMultiplier;
        public static ConfigEntry<int> RecipeArrowCostMultiplier;
        public static ConfigEntry<int> RecipeShieldCostMultiplier;
        public static ConfigEntry<int> RecipeTwoHandedCostMultiplier;
        public static ConfigEntry<int> RecipeOneHandedCostMultiplier;
        public static ConfigEntry<int> RecipeBowCostMultiplier;
        public static ConfigEntry<int> RecipeConsumableCostMultiplier;
        public static ConfigEntry<bool> ResetWorldDay;
        public static ConfigEntry<bool> WolvesAreTameable;
        public static ConfigEntry<bool> LoxTameable;
        public static ConfigEntry<int> SkillCap;

        public static ConfigEntry<string> dropTypes;

        public static bool IsAdmin = false;

        public static int maxPlayers = 100;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();

        public static List<City> cities = new();

        public static bool hasSpawned = false;
        Harmony _harmony = new Harmony("ZzDetalhes.deadheim");

        private void Awake()
        {
            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    ItemService.ModififyItemMaxLevel();
                    ItemService.SetBoatsToDrop();
                    ItemService.SetWardFirePlace();
                    ItemService.ModifyItemsCost();
                    ItemService.LoxTameable();
                    ItemService.WolvesTameable();
                    ItemService.StubNoLife();
                    ItemService.PiecesToRemoveResourcesDrop();
                    ItemService.TurnWandsIntoTwoHandeds();

                    cities = City.GetCities();

                    IsAdmin = AdminList.Value.Contains(Plugin.steamId);
                }
                else
                {
                    cities = City.GetCities();
                    ItemService.ModififyItemMaxLevel();
                    Jotunn.Logger.LogMessage("Config sync event received");    
                }
            };

            Config.SaveOnConfigSet = true;

            SkillCap = Config.Bind("Server config", "SkillCap", 100,
new ConfigDescription("SkillCap", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PiecesToRemoveResourceDrop = Config.Bind("Server config", "PiecesToRemoveResourceDrop", "piece_Scales",
new ConfigDescription("PiecesToRemoveResourceDrop", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AdminList = Config.Bind("Server config", "AdminList", "76561198053330247 76561197961128381 76561198111650012 76561197993642177 76561198993982965",
           new ConfigDescription("AdminList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));


            Vip = Config.Bind("Server config", "Vip", "76561198053330247",
           new ConfigDescription("VipList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));


            dropTypes = Config.Bind("Server config", "dropTypes", "Material,Ammo,Customization,Trophie,Torch,Misc",
           new ConfigDescription("dropTypes", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillDeathFactor = Config.Bind("Server config", "SkillDeathFactor", 0.05f,
new ConfigDescription("SkillDeathFactor", null,
new AcceptableValueRange<float>(0f, 0.1f), null,
        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PrefabToRecalculateRecipeOnLogout = Config.Bind("Server Config", "PrefabToRecalculateRecipeOnLogout", "BlackMetalBattleaxeHTD,BlackMetalGreatSwordAltHTD,BlackMetalGreatSwordHTD,BoneGreatMaceHTD,BoneGreatSwordHTD,BronzeBattleaxeHTD,BronzeCrowbillHTD,BronzeFistsHTD,BronzeGreatSwordHTD,BronzeHammerHTD,CoreAxeBlueHTD,CoreAxeGreenHTD,CoreAxeHTD,CoreGreatAxeBlueHTD,CoreGreatAxeGreenHTD,CoreGreatAxeHTD,CoreGreatMaceBlueHTD,CoreGreatMaceGreenHTD,CoreGreatMaceHTD,CoreMaceBlueHTD,CoreMaceGreenHTD,CoreMaceHTD,DeerFistsHTD,DragonSlayerSwordHTD,FlametalGreatSwordHTD,IronFistsHTD,IronGreatSwordHTD,IronHeavyGreatSwordHTD,ObsidianGreatSwordHTD,ObsidianGreatSwordRedHTD,SilverBattleaxeHTD,SilverFistsHTD,SilverGreatMaceHTD,SilverGreatSwordHTD,ArmorChestBoarHTD,ArmorHelmetBoarHTD,ArmorLegsBoarHTD,ArmorShoulderBoarHTD,ArmorBarbarianBronzeHelmetJD,ArmorBarbarianBronzeChestJD,ArmorBarbarianBronzeLegsJD,ArmorBarbarianCapeJD,ArmorMistlandsHelmet,ArmorMistlandsChest,ArmorMistlandsLegs,ArmorSerpentHelemt,ArmorSerpentChest,ArmorSerpentLegs,ArmorSerpentCape,ArmorWandererHelmet,ArmorWandererChest,ArmorWandererLegs,ArmorWandererCape,ArmorBlackmetalgarbHelmet,ArmorBlackmetalgarbChest,ArmorBlackmetalgarbLegs,ArmorPlateIronHelmetJD,ArmorPlateIronChestJD,ArmorPlateIronLegsJD,ArmorPlateCape,ArmorWarriorHelmet,ArmorWarriorChest,ArmorWarriorLegs,ArmorDragonslayerHelmet,ArmorDragonslayerChest,ArmorDragonslayerLegs,BackpackHeavy,BackpackSimple",
                new ConfigDescription("PrefabToRecalculateRecipeOnLogout", null,
 new ConfigurationManagerAttributes { IsAdminOnly = true }));

            StaffMessage = Config.Bind("Server config", "StaffMessage", "",
new ConfigDescription("StaffMessage", null,
 new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CityLocation = Config.Bind("Server config", "CityLocation", "300:500:100|1000:900:100",
new ConfigDescription("CityLocation", null,
        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DungeonPrefabs = Config.Bind("Server config", "DungeonPrefabs", "dungeon_forestcrypt_door,dungeon_sunkencrypt_irongate",
new ConfigDescription("DungeonPrefabs", null,
        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WolvesAreTameable = Config.Bind("Server config", "WolvesAreTameable", false,
new ConfigDescription("WolvesAreTameable", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            LoxTameable = Config.Bind("Server config", "LoxTameable", false,
new ConfigDescription("LoxTameable", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeCostMultiplier = Config.Bind("Server config", "RecipeCostMultiplier", 2,
            new ConfigDescription("RecipeCostMultiplier", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeArrowCostMultiplier = Config.Bind("Server config", "RecipeArrowCostMultiplier", 2,
new ConfigDescription("RecipeArrowCostMultiplier", null,
         new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeShieldCostMultiplier = Config.Bind("Server config", "RecipeShieldCostMultiplier", 2,
new ConfigDescription("RecipeShieldCostMultiplier", null,   
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeTwoHandedCostMultiplier = Config.Bind("Server config", "RecipeTwoHandedCostMultiplier", 2,
new ConfigDescription("RecipeTwoHandedCostMultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeOneHandedCostMultiplier = Config.Bind("Server config", "RecipeOneHandedCostMultiplier", 2,
new ConfigDescription("RecipeOneHandedCostMultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeConsumableCostMultiplier = Config.Bind("Server config", "RecipeConsumableCostMultiplier", 2,
new ConfigDescription("RecipeConsumableCostMultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RecipeBowCostMultiplier = Config.Bind("Server config", "RecipeBowCostMultiplier", 2,
new ConfigDescription("RecipeArrowCostMultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SafeArea = Config.Bind("Server config", "SafeArea", 1500,
new ConfigDescription("SafeArea", null,
         new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardChargeDurationInSec = Config.Bind("Server config", "WardChargeDurationInSec", 86400,
    new ConfigDescription("WardChargeDurationInSec", null,
             new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardLimit = Config.Bind("Server config", "WardLimit", 3,
    new ConfigDescription("WardLimit", null,
             new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardLimitVip = Config.Bind("Server config", "WardLimitVip", 5,
    new ConfigDescription("WardLimitVip", null,
             new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ItemToSetMaxLevel = Config.Bind("Server config", "ItemToSetMaxLevel", "RogueSword_DoD:5,RogueSword_DoD:5",
new ConfigDescription("RogueSword_DoD:5,RogueSword_DoD:5", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            DropPercentagePerItem = Config.Bind("Server config", "DropPercentagePerItem", 5,
new ConfigDescription("DropPercentagePerItem", null,
         new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardRadius = Config.Bind("Server config", "WardRadius", 150,
new ConfigDescription("WardRadius", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BoatWindSpeedmultiplier = Config.Bind("Server config", "boatWindSpeedmultiplier", 1f,
new ConfigDescription("boatWindSpeedmultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            BoatRudderSpeedmultiplier = Config.Bind("Server config", "BoatRudderSpeedmultiplier", 1f,
new ConfigDescription("BoatRudderSpeedmultiplier", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CapeRunicRegen = Config.Bind("Server config", "CapeRunicRegen", 1.25f,
new ConfigDescription("CapeRunicRegen", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            CapeRunicSpeed = Config.Bind("Server config", "CapeRunicSpeed", 0.05f,
new ConfigDescription("CapeRunicSpeed", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardRadius = Config.Bind("Server config", "WardRadius", 150,
new ConfigDescription("WardRadius", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillMultiplier = Config.Bind("Server config", "SkillMultiplier", 0.5f,
            new ConfigDescription("SkillMultiplier", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ResetWorldDay = Config.Bind("Server config", "ResetWorldDay", false,
            new ConfigDescription("ResetWorldDay", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            _harmony.PatchAll();
            ClonedItems.LoadAssets();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Numlock))
            {
                Debug.Log(EnvMan.instance.GetDay(ZNet.instance.m_netTime));
            }      
        }

        public class City
        {
            public Vector3 Position { get; set; }
            public float Radius { get; set; }

            public static List<City> GetCities()
            {
                List<City> cities = new();
                foreach (string cityString in Plugin.CityLocation.Value.Split('|'))
                {
                    string[] splittedCity = cityString.Split(':');
                    City city = new();
                    city.Position = new Vector3(Convert.ToInt64(splittedCity[0]), 0, Convert.ToInt64(splittedCity[1]));
                    city.Radius = Convert.ToInt64(splittedCity[2]);

                    cities.Add(city);
                }

                return cities;
            }
        }
    }
}
