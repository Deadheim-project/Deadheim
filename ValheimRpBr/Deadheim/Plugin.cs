using BepInEx;
using HarmonyLib;

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
        public static bool isModerator = false;
        Harmony _Harmony = new Harmony("Detalhes.deadheim");

        private void Awake()
        {
            _Harmony.PatchAll();
        }
    }
}
