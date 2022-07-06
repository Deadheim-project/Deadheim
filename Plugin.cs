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
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [BepInDependency("org.bepinex.plugins.groups", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "4.2.4";
        public const string PluginGUID = "Detalhes.Deadheim";
        public static string steamId = "";
        public static ConfigEntry<string> Vip;
        public static ConfigEntry<string> AdminList;
        public static ConfigEntry<string> OnlyAdminPieces;
        public static ConfigEntry<string> VipPortalNames;
        public static ConfigEntry<int> WardRadius;
        public static ConfigEntry<string> StaffMessage;
        public static ConfigEntry<string> DungeonPrefabs;
        public static ConfigEntry<float> SkillMultiplier;
        public static ConfigEntry<float> BoatWindSpeedmultiplier;
        public static ConfigEntry<float> BoatRudderSpeedmultiplier;
        public static ConfigEntry<float> CapeRunicSpeed;
        public static ConfigEntry<float> CapeRunicRegen;
        public static ConfigEntry<float> SkillDeathFactor;
        public static ConfigEntry<int> SafeArea;        
        public static ConfigEntry<int> WardLimit;       
        public static ConfigEntry<int> WardLimitVip;
        public static ConfigEntry<int> PortalLimit;
        public static ConfigEntry<int> PortalLimitVip;
        public static ConfigEntry<int> DropPercentagePerItem;
        public static ConfigEntry<int> WardChargeDurationInSec;  
        public static ConfigEntry<bool> ResetWorldDay;
        public static ConfigEntry<bool> WolvesAreTameable;
        public static ConfigEntry<bool> LoxTameable;
        public static ConfigEntry<int> SkillCap;

        public static ConfigEntry<string> dropTypes;

        public static bool IsAdmin = false;
        public static int PlayerWardCount = 999;
        public static int PlayerPortalCount = 999;

        public static int maxPlayers = 50;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();

        public static bool hasSpawned = false;
        Harmony _harmony = new Harmony("Detalhes.deadheim");

        private void Awake()
        {
            SynchronizationManager.OnConfigurationSynchronized += (obj, attr) =>
            {
                if (attr.InitialSynchronization)
                {
                    ItemService.SetBoatsToDrop();
                    ItemService.SetWardFirePlace();
                    ItemService.ModifyItemsCost();
                    ItemService.LoxTameable();
                    ItemService.WolvesTameable();
                    ItemService.StubNoLife();
                    ItemService.OnlyAdminPieces();

                    IsAdmin = AdminList.Value.Contains(Plugin.steamId);
                }
                else
                {
                    Jotunn.Logger.LogMessage("Config sync event received");    
                }
            };

            Config.SaveOnConfigSet = true;

            OnlyAdminPieces = Config.Bind("Server config", "OnlyAdminPieces", "SHGateHouse,SHWallMusteringHall,SHTowerSquareTwoFloorCenter,SHTowerSquareTwoFloorCorner,SHTowerSquareTwoFloorJunction,SHWallOpenTwoFloorCapped,SHWallOpenTwoFloorWithNest,SHWallOpenTwoFloorWithNestCapped,SHWallOpenTwoFloor,SHEnclosedTower,SHBunkhouse,SHWell,SHOuterWallCovered,SHOuterWallOpenCapped,SHOuterWallOpen,SHOuterWallTowerSquareCenter,SHOuterWallTowerTransition,SHOuterWallTowerRound,SHOuterWallGate,SHWatchtower,SHTowerRoundWallEnd,SHOuterWallCoverdCapped,SHWallInnerArch,SHWallInnerPillar,SHWallInnerPlain,SHWallInnerPosh,SHHouseSmall,SHHouseMedium,SHHouseLarge,SHHayBarn,SHOldBarn,SHStorageBarn,SHMainHall",
new ConfigDescription("OnlyAdminPieces", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            VipPortalNames = Config.Bind("Server config", "VipPortalNames", "cavalinho,eguinha",
new ConfigDescription("VipPortalNames", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillCap = Config.Bind("Server config", "SkillCap", 100,
new ConfigDescription("SkillCap", null,
new ConfigurationManagerAttributes { IsAdminOnly = true }));

            AdminList = Config.Bind("Server config", "AdminList", "76561198053330247 76561197961128381 76561198111650012 76561197993642177 76561198993982965",
           new ConfigDescription("AdminList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));


            Vip = Config.Bind("Server config", "Vip", "76561198053330247",
           new ConfigDescription("VipList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SkillDeathFactor = Config.Bind("Server config", "SkillDeathFactor", 0.05f,
new ConfigDescription("SkillDeathFactor", null,
new AcceptableValueRange<float>(0f, 0.1f), null,
        new ConfigurationManagerAttributes { IsAdminOnly = true }));

            StaffMessage = Config.Bind("Server config", "StaffMessage", "",
new ConfigDescription("StaffMessage", null,
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

            SafeArea = Config.Bind("Server config", "SafeArea", 1500,
new ConfigDescription("SafeArea", null,
         new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardChargeDurationInSec = Config.Bind("Server config", "WardChargeDurationInSec", 86400,
    new ConfigDescription("WardChargeDurationInSec", null,
             new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PortalLimit = Config.Bind("Server config", "PortalLimit", 2,
new ConfigDescription("PortalLimit", null,
 new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PortalLimitVip = Config.Bind("Server config", "PortalLimitVip", 6,
new ConfigDescription("PortalLimitVip", null,
 new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardLimit = Config.Bind("Server config", "WardLimit", 3,
    new ConfigDescription("WardLimit", null,
             new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardLimitVip = Config.Bind("Server config", "WardLimitVip", 5,
    new ConfigDescription("WardLimitVip", null,
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

            SkillMultiplier = Config.Bind("Server config", "SkillMultiplier", 0.5f,
            new ConfigDescription("SkillMultiplier", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            ResetWorldDay = Config.Bind("Server config", "ResetWorldDay", false,
            new ConfigDescription("ResetWorldDay", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            _harmony.PatchAll();
            ClonedItems.LoadAssets();
        }        
    }
}
