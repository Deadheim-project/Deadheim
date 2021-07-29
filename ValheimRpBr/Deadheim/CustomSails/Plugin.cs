using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CustomSails
{
    [BepInPlugin("com.rolopogo.CustomSails", "CustomSails", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        private string keyConfig;

        private void Awake()
        {
            instance = this;
            keyConfig = "LeftControl";
        }

        public bool AllowInput()
        {
            if (Enum.TryParse(keyConfig, out KeyCode key))
            { 
                if (Input.GetKey(key))
                {
                    return true;
                }
            }
            return false;
        }
         
    }
}
