using BepInEx;
using HarmonyLib;
using System.Collections.Generic;

namespace Deadheim
{
    [BepInPlugin("Deadheim.Br", Plugin.ModName, Plugin.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.3";
        public const string ModName = "Deadheim";
        public static string steamId = "";
        public static string age = "silver";
        public static long playerPing;
        public static bool playerIsVip = false;
        public static bool isModerator = false;
        public static bool admin = false;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();
        public static List<string> dropTypes = new List<string>(new string[] { "Material", "Ammo", "Customization", "Trophie", "Torch", "Misc" });
        public static double wardReductionDamage = 99.5;
        Harmony _Harmony = new Harmony("Detalhes.deadheim");
        public static AppPaused appPaused;

        private void Awake()
        {
            _Harmony.PatchAll();
            DeadheimMenu.Load();
        }
    }
}
