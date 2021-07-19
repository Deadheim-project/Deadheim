using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace Deadheim
{
    [BepInPlugin("Deadheim.Br", Plugin.ModName, Plugin.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.1";
        public const string ModName = "Deadheim";
        public static string steamId = "";
        public static string age = "stone";
        public static long playerPing;
        public static bool playerIsVip = false;
        Harmony _Harmony;
        public static ManualLogSource Log;

        private void Awake()
        {
            _Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void OnDestroy()
        {
            if (_Harmony != null) _Harmony.UnpatchSelf();
        }
    }
}
