using BepInEx;
using HarmonyLib;
using System.Collections.Generic;

namespace Deadheim
{
    [BepInPlugin("Deadheim.Br", Plugin.ModName, Plugin.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.1.39";
        public const string ModName = "Deadheim";
        public static string steamId = "";
        public static string age = "linen";
        public static long playerPing;
        public static bool playerIsVip = false;
        public static bool isModerator = false;
        public static bool admin = false;
        public static bool isPvp = false;
        public static int maxPlayers = 50;
        public static List<ZRpc> validatedUsers = new List<ZRpc>();
        public static List<string> dropTypes = new List<string>(new string[] { "Material", "Ammo", "Customization", "Trophie", "Torch", "Misc" });
        public static float wardReductionDamage = 99;
        public static float safeArea = 1500;
        public static bool hasSpawned = false;
        Harmony _Harmony = new Harmony("Detalhes.deadheim");

        private void Awake()
        {
            _Harmony.PatchAll();
        }
    }
}
