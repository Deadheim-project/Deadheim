extern alias GuildsMod;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RaidSystem
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("org.bepinex.plugins.guilds", BepInDependency.DependencyFlags.HardDependency)]
    public class RaidSystemPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "Detalhes.RaidSystem";
        public const string PluginName = "RaidSystem";
        public const string PluginVersion = "2.0.0";
        public const string DefaultWebhookUrl = "";
        public static RaidSystemPlugin Instance { get; private set; }
        private Harmony _harmony;

        private static readonly ConfigSync _configSync = new ConfigSync(PluginGUID) { DisplayName = PluginName, CurrentVersion = PluginVersion, MinimumRequiredVersion = "2.0.0" };

        public static readonly string ModPath = Path.GetDirectoryName(typeof(RaidSystemPlugin).Assembly.Location);
        public static readonly string FileDirectory = Path.Combine(Paths.ConfigPath, "RaidSystem");

        public static PlayerInfo LocalPlayerInfo;
        public static bool HasTeam = false;
        public static string SteamId = "";
        public static bool GuildsInstalled { get; private set; }

        // Configs
        private static ConfigEntry<Toggle> _serverConfigLocked;
        public static ConfigEntry<string> RaidTimeToAllowUtc;
        public static ConfigEntry<string> RaidEnabledPositions;
        public static ConfigEntry<int> AreaRadius;
        public static ConfigEntry<float> WardReductionDamage;
        public static ConfigEntry<int> HitPoints;
        public static ConfigEntry<int> SpawnDelayMS;
        public static ConfigEntry<int> Scale;
        public static ConfigEntry<int> RaidCooldownMinutes;
        public static ConfigEntry<int> PointsPerKill;
        public static ConfigEntry<int> PointsPerConquest;
        public static ConfigEntry<int> PointsPerDefense;
        public static ConfigEntry<int> PointsLostPerDeath;
        public static ConfigEntry<float> ColorAlpha;
        public static ConfigEntry<int> RadiusDrawMap;
        public static ConfigEntry<string> WebhookUrl;
        public static ConfigEntry<string> ConquestMessage;
        public static ConfigEntry<string> ScoreboardTitle;
        public static ConfigEntry<KeyCode> KeyboardShortcut;
        public static ConfigEntry<KeyCode> ScoreboardShortcut;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription desc, bool sync = true)
        { var e = Config.Bind(group, name, value, desc); var s = _configSync.AddConfigEntry(e); s.SynchronizedConfig = sync; return e; }
        private ConfigEntry<T> config<T>(string group, string name, T value, string desc, bool sync = true) => config(group, name, value, new ConfigDescription(desc), sync);

        private void Awake()
        {
            Instance = this; Config.SaveOnConfigSet = true;
            GuildsInstalled = GuildsMod::Guilds.API.IsLoaded();
            Logger.LogInfo($"Guilds mod active: {GuildsInstalled}");
            BindConfigs();
            WriteDefaultConfigExample();
            WardSetup.LoadAssets();
            _harmony = new Harmony(PluginGUID); _harmony.PatchAll();
            Logger.LogInfo($"RaidSystem v{PluginVersion} loaded.");
        }

        private void Update()
        {
            RaidDoorManager.Update();

            Player lp = Player.m_localPlayer;
            if (!lp || lp.IsDead() || lp.InCutscene() || lp.IsTeleporting()) return;
            if (Input.GetKeyDown(KeyboardShortcut.Value))
            { GUI.ToggleMenu(); ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_RequestFullSync", new ZPackage()); }
            if (Input.GetKeyDown(ScoreboardShortcut.Value))
            { GUI.ToggleScoreboard(); ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_RequestScores", new ZPackage()); }
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();

        private void BindConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "Only admins can change config.");
            _configSync.AddLockingConfigEntry(_serverConfigLocked);

            RaidTimeToAllowUtc = config("2 - Raid Rules", "Raid Hours (UTC)", "0-24", "Global UTC hours when raids are enabled. Supports ranges like 18-24, comma lists like 18,19,20, or * for all day.");
            RaidEnabledPositions = config("2 - Raid Rules", "Raid Zones", "", "Named zones: name,x,z,wardRadius,pvpRadius[,hoursUtc]|... Per-zone hours use UTC ranges separated by semicolon, for example Castelo,500,300,150,300,18-24;0-2. Empty disables territorial raid rules.");
            AreaRadius = config("2 - Raid Rules", "Area Radius", 150, "Radius around ward for raid zone.");
            WardReductionDamage = config("2 - Raid Rules", "Ward Damage Reduction %", 99.0f, "Damage reduction % on structures.");
            HitPoints = config("2 - Raid Rules", "Ward HP", 10000, "Hit points of raid ward.");
            SpawnDelayMS = config("2 - Raid Rules", "Respawn Delay (ms)", 5000, "Delay before ward respawns.");
            Scale = config("2 - Raid Rules", "Ward Scale", 3, "Scale multiplier of ward object.");

            RaidCooldownMinutes = config("3 - Cooldown", "Cooldown Minutes", 0, "Legacy setting. Raid availability is controlled by Raid Hours (UTC).");

            PointsPerKill = config("4 - Scoring", "Points Per Kill", 10, "Points per enemy kill.");
            PointsPerConquest = config("4 - Scoring", "Points Per Conquest", 50, "Points for conquering territory.");
            PointsPerDefense = config("4 - Scoring", "Points Per Defense", 25, "Points for defending territory.");
            PointsLostPerDeath = config("4 - Scoring", "Points Lost Per Death", 3, "Points lost on death.");

            ColorAlpha = config("6 - Visual", "Map Color Alpha", 0.7f, "Territory overlay transparency.");
            RadiusDrawMap = config("6 - Visual", "Map Draw Radius", 30, "Territory circle radius on map.");

            WebhookUrl = config("7 - Integration", "Discord Webhook URL", DefaultWebhookUrl, "Discord webhook for RaidSystem notifications.");

            ConquestMessage = config("8 - UI Text", "Conquest Message", "conquistou o território em:", "Conquest notification text.");
            ScoreboardTitle = config("8 - UI Text", "Scoreboard Title", "Ranking de Guerra", "Scoreboard title.");

            KeyboardShortcut = config("9 - Client", "Menu Key", KeyCode.PageUp, "Open raid menu.", false);
            ScoreboardShortcut = config("9 - Client", "Scoreboard Key", KeyCode.PageDown, "Open scoreboard.", false);
        }

        private void WriteDefaultConfigExample()
        {
            try
            {
                if (!Directory.Exists(FileDirectory))
                    Directory.CreateDirectory(FileDirectory);

                string path = Path.Combine(FileDirectory, "RaidSystem.Default.cfg");
                var sb = new StringBuilder();
                sb.AppendLine("## RaidSystem default configuration example");
                sb.AppendLine("## Copy values from this file into BepInEx/config/Detalhes.RaidSystem.cfg if needed.");
                sb.AppendLine("## Raid Hours use UTC, not local server time.");
                sb.AppendLine("## Raid Zones format: name,x,z,wardRadius,pvpRadius[,hoursUtc]|name,x,z,wardRadius,pvpRadius[,hoursUtc]");
                sb.AppendLine("## Per-zone hours use UTC ranges like 6-18 or 18-6. End hour is exclusive.");
                sb.AppendLine("## Because Raid Zones uses commas between fields, multiple per-zone hour windows must use semicolon: 18-24;0-2;6.");
                sb.AppendLine();

                AppendConfig(sb, "1 - General", "Lock Configuration", "On", "Only admins can change synchronized config.");

                AppendConfig(sb, "2 - Raid Rules", "Raid Hours (UTC)", "18-24", "Global fallback hours when a zone has no own hours. Example opens from 18:00 through 23:59 UTC.");
                AppendConfig(sb, "2 - Raid Rules", "Raid Zones", "Castelo,500,300,150,300,18-24;0-2|Porto,800,-200,120,250,6-18|Arena,0,0,200,400,*", "Example named raid areas with optional per-zone UTC hours. Empty disables territorial raid rules.");
                AppendConfig(sb, "2 - Raid Rules", "Area Radius", "150", "Fallback ward radius when a zone does not specify one.");
                AppendConfig(sb, "2 - Raid Rules", "Ward Damage Reduction %", "99", "Damage reduction applied to RaidWard damage.");
                AppendConfig(sb, "2 - Raid Rules", "Ward HP", "10000", "RaidWard health.");
                AppendConfig(sb, "2 - Raid Rules", "Respawn Delay (ms)", "5000", "Delay before RaidWard respawns after conquest.");
                AppendConfig(sb, "2 - Raid Rules", "Ward Scale", "3", "RaidWard visual scale.");

                AppendConfig(sb, "3 - Cooldown", "Cooldown Minutes", "0", "Legacy setting. Raid availability is controlled by Raid Hours (UTC).");

                AppendConfig(sb, "4 - Scoring", "Points Per Kill", "10", "Points per enemy kill.");
                AppendConfig(sb, "4 - Scoring", "Points Per Conquest", "50", "Points per conquered territory.");
                AppendConfig(sb, "4 - Scoring", "Points Per Defense", "25", "Reserved defense score value.");
                AppendConfig(sb, "4 - Scoring", "Points Lost Per Death", "3", "Points lost per death.");

                AppendConfig(sb, "6 - Visual", "Map Color Alpha", "0.7", "Territory overlay transparency.");
                AppendConfig(sb, "6 - Visual", "Map Draw Radius", "30", "Territory circle radius on minimap texture.");

                AppendConfig(sb, "7 - Integration", "Discord Webhook URL", DefaultWebhookUrl, "Discord webhook used for guild, kill, and conquest notifications.");

                AppendConfig(sb, "8 - UI Text", "Conquest Message", "conquistou o territorio em:", "Center-screen conquest notification text.");
                AppendConfig(sb, "8 - UI Text", "Scoreboard Title", "Ranking de Guerra", "Scoreboard title.");

                AppendConfig(sb, "9 - Client", "Menu Key", "PageUp", "Client key to open the raid menu.");
                AppendConfig(sb, "9 - Client", "Scoreboard Key", "PageDown", "Client key to open the scoreboard.");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Logger.LogInfo($"RaidSystem default config example written to {path}");
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Could not write RaidSystem default config example: {ex.Message}");
            }
        }

        private static void AppendConfig(StringBuilder sb, string section, string key, string value, string description)
        {
            sb.AppendLine($"[{section}]");
            sb.AppendLine($"## {description}");
            sb.AppendLine($"{key} = {value}");
            sb.AppendLine();
        }
    }
}
