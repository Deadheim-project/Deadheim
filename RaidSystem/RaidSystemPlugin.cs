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
        public static ConfigEntry<Toggle> ForcePvpInZones;
        public static ConfigEntry<Toggle> WardOnlyAdminCanBuild;
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

        // Economia de territorio (Fases 1-3)
        public static ConfigEntry<Toggle> OrePortalEnabled;
        public static ConfigEntry<int> OrePortalMinTier;
        public static ConfigEntry<int> TributeIntervalMinutes;
        public static ConfigEntry<int> TributeMaxCharges;
        public static ConfigEntry<int> TributeRequiredFreeSlots;
        public static ConfigEntry<float> WardReductionDamageSiege;
        public static ConfigEntry<Toggle> SiegeOnly;
        public static ConfigEntry<Toggle> LogWardHits;
        public static ConfigEntry<float> SiegeAttributionRadius;

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
            RegisterWardBridge();
            _harmony = new Harmony(PluginGUID); _harmony.PatchAll();
            Logger.LogInfo($"RaidSystem v{PluginVersion} loaded.");
        }

        private void Update()
        {
            RaidDoorManager.Update();
            TributeManager.Update();

            Player lp = Player.m_localPlayer;
            if (!lp || lp.IsDead() || lp.InCutscene() || lp.IsTeleporting()) return;
            if (Input.GetKeyDown(KeyboardShortcut.Value))
            { GUI.ToggleMenu(); ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_RequestFullSync", new ZPackage()); }
            if (Input.GetKeyDown(ScoreboardShortcut.Value))
            { GUI.ToggleScoreboard(); ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_RequestScores", new ZPackage()); }
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();

        /// <summary>
        /// Entrega ao modulo de wards do Deadheim as duas coisas que so o RaidSystem sabe:
        /// onde valem as regras de raid, e a qual guild um jogador pertence.
        /// </summary>
        private static void RegisterWardBridge()
        {
            Deadheim.Wards.WardBridge.IsExternallyGoverned = Util.IsRaidEnabledHere;
            Deadheim.Wards.WardBridge.GuildOfPlayer = playerId =>
            {
                Player lp = Player.m_localPlayer;
                return lp != null && lp.GetPlayerID() == playerId
                    ? GuildsIntegration.GetOwnGuildName()
                    : GuildsIntegration.GetPlayerTeam(playerId);
            };
        }

        private void BindConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "Only admins can change config.");
            _configSync.AddLockingConfigEntry(_serverConfigLocked);

            RaidTimeToAllowUtc = config("2 - Raid Rules", "Raid Hours (UTC)", "0-24", "Global UTC hours when raids are enabled. Supports ranges like 18-24, comma lists like 18,19,20, or * for all day.");
            RaidEnabledPositions = config("2 - Raid Rules", "Raid Zones", "", "Named zones: name,x,z,wardRadius,pvpRadius[,hoursUtc[,tier[,minToolTier]]]|... Per-zone hours use UTC ranges separated by semicolon, for example Castelo,500,300,150,300,18-24;0-2,3,2. tier drives the tribute table and the ore portal; minToolTier is the minimum weapon tool tier able to damage the ward. Empty disables territorial raid rules.");
            AreaRadius = config("2 - Raid Rules", "Area Radius", 150, "Radius around ward for raid zone.");
            WardReductionDamage = config("2 - Raid Rules", "Ward Damage Reduction %", 99.0f, "Damage reduction % on structures.");
            HitPoints = config("2 - Raid Rules", "Ward HP", 10000, "Hit points of raid ward.");
            SpawnDelayMS = config("2 - Raid Rules", "Respawn Delay (ms)", 5000, "Delay before ward respawns.");
            Scale = config("2 - Raid Rules", "Ward Scale", 3, "Scale multiplier of ward object.");

            // A chave ja existia no cfg do servidor mas nao era lida por ninguem:
            // o RaidWard estava no martelo para qualquer jogador.
            WardOnlyAdminCanBuild = config("5 - Ward", "Only Admin Can Build", Toggle.On, "Only admins can place the territorial RaidWard.");

            ForcePvpInZones = config("3 - PvP", "Force PvP In Zones", Toggle.Off, "Force PvP on inside the pvpRadius of a raid zone, even for players with PvP off. Off keeps pvpRadius parsed but inert.");


            PointsPerKill = config("4 - Scoring", "Points Per Kill", 10, "Points per enemy kill.");
            PointsPerConquest = config("4 - Scoring", "Points Per Conquest", 50, "Points for conquering territory.");
            PointsPerDefense = config("4 - Scoring", "Points Per Defense", 25, "Points for killing an enemy inside territory your own guild holds.");
            PointsLostPerDeath = config("4 - Scoring", "Points Lost Per Death", 3, "Points lost on death.");

            ColorAlpha = config("6 - Visual", "Map Color Alpha", 0.7f, "Territory overlay transparency.");
            RadiusDrawMap = config("6 - Visual", "Map Draw Radius", 30, "Territory circle radius on map.");

            // sync: false de proposito. O ServerSync empurra config sincronizada para todos
            // os clientes, e a URL do webhook e um segredo: quem tiver ela posta no Discord.
            WebhookUrl = config("7 - Integration", "Discord Webhook URL", DefaultWebhookUrl, "Discord webhook for RaidSystem notifications. Server-side only, never sent to clients.", false);

            ConquestMessage = config("8 - UI Text", "Conquest Message", "conquistou o território em:", "Conquest notification text.");
            ScoreboardTitle = config("8 - UI Text", "Scoreboard Title", "Ranking de Guerra", "Scoreboard title.");

            WardReductionDamageSiege = config("2 - Raid Rules", "Ward Damage Reduction % (Siege)", 0f,
                "Reducao de dano aplicada a acertos de catapulta.");
            SiegeOnly = config("2 - Raid Rules", "Siege Only", Toggle.Off,
                "Ward so recebe dano de cerco. Ligar apenas apos calibrar.");
            LogWardHits = config("2 - Raid Rules", "Log Ward Hits", Toggle.Off,
                "Loga hitType e toolTier de cada acerto no ward. Use para calibrar minToolTier.");
            SiegeAttributionRadius = config("2 - Raid Rules", "Siege Attribution Radius", 200f,
                "Raio de busca da catapulta que atribui o tiro sem dono.");

            OrePortalEnabled = config("10 - Economia", "Ore Portal Enabled", Toggle.On,
                "Portais aceitam minerio quando o destino e territorio da sua guild.");
            OrePortalMinTier = config("10 - Economia", "Ore Portal Min Tier", 1,
                "Tier minimo de territorio para conceder portal de minerio.");
            TributeIntervalMinutes = config("10 - Economia", "Tribute Interval Minutes", 120,
                "Minutos por carga de tributo.");
            TributeMaxCharges = config("10 - Economia", "Tribute Max Charges", 6,
                "Teto de cargas acumuladas. Guild inativa para de render.");
            TributeRequiredFreeSlots = config("10 - Economia", "Tribute Required Free Slots", 6,
                "Espacos livres exigidos na mochila para resgatar.");

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
                sb.AppendLine("## Raid Zones format: name,x,z,wardRadius,pvpRadius[,hoursUtc[,tier[,minToolTier]]]");
                sb.AppendLine("## tier selects the Tribute.json table and gates the ore portal; minToolTier is the minimum weapon tool tier that damages the ward.");
                sb.AppendLine("## Zones written with the old 5 or 6 field format keep tier 1 and minToolTier 0.");
                sb.AppendLine("## Per-zone hours use UTC ranges like 6-18 or 18-6. End hour is exclusive.");
                sb.AppendLine("## Because Raid Zones uses commas between fields, multiple per-zone hour windows must use semicolon: 18-24;0-2;6.");
                sb.AppendLine();

                AppendConfig(sb, "1 - General", "Lock Configuration", "On", "Only admins can change synchronized config.");

                AppendConfig(sb, "2 - Raid Rules", "Raid Hours (UTC)", "18-24", "Global fallback hours when a zone has no own hours. Example opens from 18:00 through 23:59 UTC.");
                AppendConfig(sb, "2 - Raid Rules", "Raid Zones", "Castelo,500,300,150,300,18-24;0-2,3,2|Porto,800,-200,120,250,6-18,2,0|Arena,0,0,200,400,*,1,0", "Example named raid areas with optional per-zone UTC hours, tier and minToolTier. Empty disables territorial raid rules.");
                AppendConfig(sb, "2 - Raid Rules", "Area Radius", "150", "Fallback ward radius when a zone does not specify one.");
                AppendConfig(sb, "2 - Raid Rules", "Ward Damage Reduction %", "99", "Damage reduction applied to RaidWard damage.");
                AppendConfig(sb, "2 - Raid Rules", "Ward HP", "10000", "RaidWard health.");
                AppendConfig(sb, "2 - Raid Rules", "Respawn Delay (ms)", "5000", "Delay before RaidWard respawns after conquest.");
                AppendConfig(sb, "2 - Raid Rules", "Ward Scale", "3", "RaidWard visual scale.");
                AppendConfig(sb, "2 - Raid Rules", "Ward Damage Reduction % (Siege)", "0", "Damage reduction applied to catapult hits on the RaidWard.");
                AppendConfig(sb, "2 - Raid Rules", "Siege Only", "Off", "RaidWard only takes catapult damage. Turn on only after calibrating with Log Ward Hits.");
                AppendConfig(sb, "2 - Raid Rules", "Log Ward Hits", "Off", "Logs hitType and toolTier of every RaidWard hit. Use it to pick minToolTier per zone.");
                AppendConfig(sb, "2 - Raid Rules", "Siege Attribution Radius", "200", "Search radius for the catapult that owns an ownerless siege shot.");

                AppendConfig(sb, "3 - PvP", "Force PvP In Zones", "Off", "Force PvP on inside the pvpRadius of a raid zone. Off keeps pvpRadius inert.");

                AppendConfig(sb, "5 - Ward", "Only Admin Can Build", "On", "Only admins can place the territorial RaidWard.");

                AppendConfig(sb, "4 - Scoring", "Points Per Kill", "10", "Points per enemy kill.");
                AppendConfig(sb, "4 - Scoring", "Points Per Conquest", "50", "Points per conquered territory.");
                AppendConfig(sb, "4 - Scoring", "Points Per Defense", "25", "Points for killing an enemy inside territory your guild owns.");
                AppendConfig(sb, "4 - Scoring", "Points Lost Per Death", "3", "Points lost per death.");

                AppendConfig(sb, "6 - Visual", "Map Color Alpha", "0.7", "Territory overlay transparency.");
                AppendConfig(sb, "6 - Visual", "Map Draw Radius", "30", "Territory circle radius on minimap texture.");

                AppendConfig(sb, "7 - Integration", "Discord Webhook URL", DefaultWebhookUrl, "Discord webhook used for guild, kill, and conquest notifications. Never synchronized to clients.");

                AppendConfig(sb, "8 - UI Text", "Conquest Message", "conquistou o territorio em:", "Center-screen conquest notification text.");
                AppendConfig(sb, "8 - UI Text", "Scoreboard Title", "Ranking de Guerra", "Scoreboard title.");

                AppendConfig(sb, "9 - Client", "Menu Key", "PageUp", "Client key to open the raid menu.");
                AppendConfig(sb, "9 - Client", "Scoreboard Key", "PageDown", "Client key to open the scoreboard.");

                AppendConfig(sb, "10 - Economia", "Ore Portal Enabled", "On", "Portals accept ore when the destination is territory held by your guild.");
                AppendConfig(sb, "10 - Economia", "Ore Portal Min Tier", "1", "Minimum territory tier that grants the ore portal.");
                AppendConfig(sb, "10 - Economia", "Tribute Interval Minutes", "120", "Minutes per accumulated tribute charge.");
                AppendConfig(sb, "10 - Economia", "Tribute Max Charges", "6", "Cap of accumulated charges. An inactive guild stops earning.");
                AppendConfig(sb, "10 - Economia", "Tribute Required Free Slots", "6", "Free inventory slots required to claim tribute at the RaidWard.");

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
