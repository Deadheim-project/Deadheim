using HarmonyLib;
using System;
using BetterWards.Util;
using BepInEx;

namespace BetterWards.Server
{
    [HarmonyPatch]
    public class ServerPatches
    {
        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (ZNet.m_isServer)
            {
                ZRoutedRpc.instance.Register<ZPackage>("RequestSync", new Action<long, ZPackage>(ServerAdminSystem.RPC_RequestSync));
                ZRoutedRpc.instance.Register<ZPackage>("EventSync", new Action<long, ZPackage>(ServerAdminSystem.RPC_EventSync));
                ZRoutedRpc.instance.Register<ZPackage>("RequestAdminSync", new Action<long, ZPackage>(ServerAdminSystem.RPC_RequestAdminSync));
                ZRoutedRpc.instance.Register<ZPackage>("EventAdminSync", new Action<long, ZPackage>(ServerAdminSystem.RPC_EventAdminSync));
            }
        }

    }
}
