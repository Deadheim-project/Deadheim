using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
