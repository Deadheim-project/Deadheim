using BepInEx;
using BepInEx.Configuration;
using Jotunn.Utils;
using System.IO;

namespace VipList
{
    [BepInPlugin(PluginGuid, PluginName, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class VipListPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Detalhes.VipList";
        public const string PluginName = "VipList";
        public const string Version = "1.0.0";

        private void Awake()
        {
            Config.SaveOnConfigSet = true;

            string defaultIds = "76561198053330247";
            string legacyPath = Path.Combine(BepInEx.Paths.ConfigPath, "Detalhes.Deadheim.cfg");
            if (!File.Exists(Config.ConfigFilePath) && File.Exists(legacyPath))
            {
                ConfigFile legacyConfig = new ConfigFile(legacyPath, false);
                legacyConfig.SaveOnConfigSet = false;
                defaultIds = legacyConfig.Bind("Server config", "Vip", defaultIds).Value;
                Logger.LogInfo("Imported the legacy VIP list from Detalhes.Deadheim.cfg.");
            }

            ConfigEntry<string> vipIds = Config.Bind(
                "Server config",
                "VipList",
                defaultIds,
                new ConfigDescription(
                    "Platform user IDs that receive VIP access. Separate IDs with spaces, commas, semicolons, pipes, or new lines.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            VipListApi.Initialize(vipIds);
            Logger.LogInfo($"VipList API ready with {VipListApi.Count} VIP(s).");
        }
    }
}
