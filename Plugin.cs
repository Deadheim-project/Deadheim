using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
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
        public const string Version = "3.2.4";
        public const string PluginGUID = "ZzDetalhes.Deadheim";
        public static string steamId = "";
        public static ConfigEntry<string> Vip;
        public static ConfigEntry<string> Tag;
        public static ConfigEntry<float> WardReductionDamage;
        public static ConfigEntry<float> MonsterDamageWardReduction;
        public static ConfigEntry<int> WardRadius;
        public static ConfigEntry<string> TimeToBlockRaid;
        public static ConfigEntry<string> ItemToSetMaxLevel;
        public static ConfigEntry<int> PlayersInsideWardForRaid;
        public static ConfigEntry<float> SkillMultiplier;
        public static ConfigEntry<float> BoatWindSpeedmultiplier;
        public static ConfigEntry<float> BoatRudderSpeedmultiplier;
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

        public static bool IsAdmin = false;

        public static int maxPlayers = 100;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();

        public static bool hasSpawned = false;
        Harmony _harmony = new Harmony("ZzDetalhes.deadheim");

        private void Awake()
        {
            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    ItemService.ModifyRecipesCost();
                    ItemService.ModififyItemMaxLevel();
                    ItemService.SetBoatsToDrop();
                    ItemService.SetWardFirePlace();
                    ItemService.ModifyItemsCost();
                    ItemService.LoxTameable();
                    ItemService.WolvesTameable();
                    ItemService.StubNoLife();

                    IsAdmin = SynchronizationManager.Instance.PlayerIsAdmin;
                }
                else
                {
                    ItemService.ModififyItemMaxLevel();
                    Jotunn.Logger.LogMessage("Config sync event received");
                }
            };

            Config.SaveOnConfigSet = true;


            SkillCap = Config.Bind("Server config", "SkillCap", 100,
new ConfigDescription("SkillCap", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));


            Vip = Config.Bind("Server config", "Vip", "76561198053330247",
           new ConfigDescription("VipList", null,
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

            TimeToBlockRaid = Config.Bind("Server config", "TimeToBlockRaid", "21,12",
new ConfigDescription("Hora de inicio UTC:Fim", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ItemToSetMaxLevel = Config.Bind("Server config", "ItemToSetMaxLevel", "RogueSword_DoD:5,RogueSword_DoD:5",
new ConfigDescription("RogueSword_DoD:5,RogueSword_DoD:5", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PlayersInsideWardForRaid = Config.Bind("Server config", "PlayersInsideWardForRaid", 0,
new ConfigDescription("PlayersInsideWardForRaid", null,
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

            WardRadius = Config.Bind("Server config", "WardRadius", 150,
new ConfigDescription("WardRadius", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Tag = Config.Bind("Server config", "Tag", "76561198053330247,PVPTOTAL;",
           new ConfigDescription("PvpList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardReductionDamage = Config.Bind("Server config", "WardReductionDamage", 99.0f,
            new ConfigDescription("WardReductionDamage", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            MonsterDamageWardReduction = Config.Bind("Server config", "MonsterDamageWardReduction", 95.0f,
            new ConfigDescription("MonsterDamageWardReduction", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillMultiplier = Config.Bind("Server config", "SkillMultiplier", 0.5f,
            new ConfigDescription("SkillMultiplier", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ResetWorldDay = Config.Bind("Server config", "ResetWorldDay", false,
            new ConfigDescription("ResetWorldDay", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            Tag = Config.Bind("Server config", "Tag", "76561198053330247,PVPTOTAL;",
           new ConfigDescription("PvpList", null,
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
    }
}
