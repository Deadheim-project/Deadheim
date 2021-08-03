﻿using BepInEx;
using HarmonyLib;
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
        public static bool playerIsVip = false;
        public static bool isModerator = false;
        public static Vector3 hearthStoneSpawn = new Vector3 { x = -250.0f, y = 1.0f, z = 262.0f };
        Harmony _Harmony = new Harmony("Detalhes.deadheim");

        private void Awake()
        {
            _Harmony.PatchAll();
        }
    }
}
