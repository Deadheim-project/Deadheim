using System.Reflection;
using HarmonyLib;
using BepInEx;
using BepInEx.Logging;
using ModConfigEnforcer;
using System.Linq;
using UnityEngine;

namespace BetterWards
{
    [BepInPlugin("azumatt.BetterWards", "Better Wards (STFU)", version)]
    [BepInDependency("pfhoenix.modconfigenforcer")]
    public class BetterWardsPlugin : BaseUnityPlugin
    {
        public const string version = "1.4.3";
        public static string newestVersion = "";
        public const string ModName = "Better Wards (STFU)";
        internal const string Author = "Azumatt";
        internal const string HarmonyGUID = "Harmony." + Author + "." + ModName;
        public static bool isUpToDate = false;
        public static bool valid_server = false;
        public static bool admin = false;
        public static int wardCount = 0;

        public static ManualLogSource logger;

        public static ConfigVariable<bool> wardEnabled;
        public static UnityEngine.KeyCode wardHotKey;
        public static ConfigVariable<bool> wardRangeEnabled;
        public static ConfigVariable<float> wardRange;
        public static ConfigVariable<bool> showMarker;
        public static ConfigVariable<bool> noTerrain;   
        public static ConfigVariable<bool> autoClose;
        public static ConfigVariable<bool> wardNotify;
        public static ConfigVariable<int> SilverReq;
        public static ConfigVariable<int> EggReq;
        public static ConfigVariable<int> CoreReq;
        public static ConfigVariable<bool> wardHeal;
        public static ConfigVariable<int> wardHealAmount;
        public static ConfigVariable<bool> adminAutoPerm;
        public static ConfigVariable<float> wardDamageReduction;        
        public static ConfigVariable<string> itemNames;
        public static ConfigVariable<bool> wardStructures;
        public void Awake()
        {
            ConfigManager.RegisterMod(ModName, Config);

            wardEnabled = ConfigManager.RegisterModConfigVariable(ModName, "wardEnabled", true, "General", "Enable Better Ward Configurations", false);
            showMarker = ConfigManager.RegisterModConfigVariable(ModName, "showMarker", true, "General", "Whether or not you want to show the area marker for wards", true);
            wardHotKey = UnityEngine.KeyCode.K;
            autoClose = ConfigManager.RegisterModConfigVariable(ModName, "AutoCloseDoors", true, "General", "Whether or not you want to have doors auto close inside the ward.", true);
            wardNotify = ConfigManager.RegisterModConfigVariable(ModName, "wardNotify", true, "General", "Whether or not you want to be notified when entering and leaving a ward.", true);
            adminAutoPerm = ConfigManager.RegisterModConfigVariable(ModName, "adminAutoPerm", true, "General", "Enable or disable the auto-permit on wards for admins (CLIENT SIDE)", true);

            /* Ward Range */
            wardRangeEnabled = ConfigManager.RegisterModConfigVariable(ModName, "wardRangeEnabled", false, "WardRange", "Enable Better Ward Range Configurations\nValheim+ conflicts by overwriting the range.", false);
            wardRange = ConfigManager.RegisterModConfigVariable<float>(ModName, "wardRange", 100, "WardRange", "Range of the ward", false);

            wardDamageReduction = ConfigManager.RegisterModConfigVariable<float>(ModName, "wardDamageReduction", 0.0f, "Structures", "Reduce incoming damage to player built structures/items\nValues are percentage 0% - 100%. Can use decimals.", false);
            wardStructures = ConfigManager.RegisterModConfigVariable(ModName, "Indestructible Items", false, "Structures", "Whether or not you want to have indestructible structures inside a ward. If this is set to true, and items are defined damage reduction is 100%", false);
            itemNames = ConfigManager.RegisterModConfigVariable(ModName, "Items", "portal,guard,chest,gate,door,iron,stone", "Structures", "The items inside a ward you want to make indestructible.\nUses m_piece.m_name.\nIf the m_piece.m_name contains the string, it will be indestructible.", false);

            Logger.LogInfo("Loading Better Wards configuration file");
            Logger.LogInfo("Starting Better Wards-Client");
        }

        public static AssetBundle GetAssetBundle(string filename)
        {
            var execAssembly = Assembly.GetExecutingAssembly();

            var resourceName = execAssembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(filename));

            using (var stream = execAssembly.GetManifestResourceStream(resourceName))
            {
                return AssetBundle.LoadFromStream(stream);
            }
        }
    }
}
