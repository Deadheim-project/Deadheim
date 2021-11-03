using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using System.Collections.Generic;

namespace Deadheim
{
    [BepInPlugin(PluginGUID, PluginGUID, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "2.0.0";
        public const string PluginGUID = "Detalhes.Deadheim";
        public static string steamId = "";
        public static ConfigEntry<string> Age;
        public static ConfigEntry<string> Vip;
        public static ConfigEntry<string> Pvp;
        public static ConfigEntry<float> WardReductionDamage;
        public static ConfigEntry<int> SafeArea;

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

            Pvp = Config.Bind("Server config", "Pvp", "76561198053330247",
           new ConfigDescription("PvpList", null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            WardReductionDamage = Config.Bind("Server config", "WardReductionDamage", 99.0f,
            new ConfigDescription("WardReductionDamage", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            SafeArea = Config.Bind("Server config", "SafeArea", 1500,
            new ConfigDescription("SafeArea", null,
                     new ConfigurationManagerAttributes { IsAdminOnly = true }));

            _harmony.PatchAll();
            PortalToken.LoadAssets();
        }
    }
}
