using BepInEx;
using BepInEx.Configuration;
using ServerSync;
using System.IO;

namespace VipList
{
    [BepInPlugin(PluginGuid, PluginName, Version)]
    public sealed class VipListPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Detalhes.VipList";
        public const string PluginName = "VipList";
        public const string Version = "1.0.0";

        // No lugar do NetworkCompatibility e do IsAdminOnly do Jotunn: a lista de VIPs
        // que vale e a do servidor, e cliente sem o mod (ou abaixo de 1.0) e recusado.
        private static readonly ConfigSync ServerConfigSync = new ConfigSync(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = Version,
            MinimumRequiredVersion = "1.0.0",
            ModRequired = true,
            IsLocked = true
        };

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
                    "Platform user IDs that receive VIP access. Separate IDs with spaces, commas, semicolons, pipes, or new lines."));
            ServerConfigSync.AddConfigEntry(vipIds).SynchronizedConfig = true;

            VipListApi.Initialize(vipIds);
            Logger.LogInfo($"VipList API ready with {VipListApi.Count} VIP(s).");
        }
    }
}
