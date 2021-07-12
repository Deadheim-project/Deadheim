using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ValheimRpBr
{
    [BepInPlugin("Valheim.Rp.Br", Plugin.ModName, Plugin.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.0";
        public const string ModName = "ValheimRpBr";
        public static string steamId = "";
        public static long playerPing;
        public static List<string> prohibitedProcesses = new List<string>();
        public static List<string> adminList = new List<string> { "76561198053330247" };
        Harmony _Harmony;
        public static ManualLogSource Log;

        private void Awake()
        {
#if DEBUG
			Log = Logger;
#else
            Log = new ManualLogSource(null);
#endif
            Logger.LogError("Inicio Awake");
            _Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Logger.LogError("Fim Awake");
        }

        private void OnDestroy()
        {
            if (_Harmony != null) _Harmony.UnpatchSelf();
        }
    }
}
