using HarmonyLib;
using System;
using System.Collections.Generic;

namespace BetterWards.Server
{

    [HarmonyPatch(typeof(ZNet), "Awake")]
    public static class EnableOnDedicatedServer
    {
        private static void Prefix(ref ZNet __instance)
        {
            // Enable on dedicated servers
            if (__instance.IsDedicated())
            {
                ZLog.Log("Better Wards - Enabling for dedicated server");
            }
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    public static class RegisterAndCheckVersion
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {

            // Register version check call
            ZLog.Log("Better Wards - Registering version RPC handler");
            peer.m_rpc.Register<ZPackage>("BetterWards_Version", new Action<ZRpc, ZPackage>(RpcHandlers.RPC_BetterWards_Version));

            // Make calls to check versions
            ZLog.Log("Better Wards - Invoking version check");
            ZPackage zpackage = new ZPackage();
            zpackage.Write(BetterWardsPlugin.version);
            peer.m_rpc.Invoke("BetterWards_Version", (object)zpackage);
        }
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    public static class VerifyClient
    {
        private static bool Prefix(ZRpc rpc, ZPackage pkg, ref ZNet __instance)
        {
            if (__instance.IsServer() && !RpcHandlers._validatedPeers.Contains(rpc))
            {
                // Disconnect peer if they didn't send mod version at all
                ZLog.LogWarning("Better Wards - Peer never sent version, disconnecting");
                rpc.Invoke("Error", (object)3);
                return false; // Prevent calling underlying method
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ZNet), "Disconnect")]
    public static class RemoveDisconnectedPeerFromVerified
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {
            if (__instance.IsServer())
            {
                // Remove peer from validated list
                ZLog.Log("Better Wards - Peer disconnected, removing from validated list");
                RpcHandlers._validatedPeers.Remove(peer.m_rpc);
            }
        }
    }

    public static class RpcHandlers
    {
        public static List<ZRpc> _validatedPeers = new List<ZRpc>();

        public static void RPC_BetterWards_Version(ZRpc rpc, ZPackage pkg)
        {
            var version = pkg.ReadString();
            ZLog.Log("Better Wards - Version check, server: " + version + ",  mine: " + BetterWardsPlugin.version);
            if (version != BetterWardsPlugin.version)
            {

                if (ZNet.instance.IsServer())
                {
                    // Different versions - force disconnect client from server
                    ZLog.LogWarning("Better Wards - Peer has incompatible version, disconnecting");
                    rpc.Invoke("Error", (object)3);
                }
            }
            else
            {
                if (!ZNet.instance.IsServer())
                {
                    // Enable mod on client if versions match
                    ZLog.Log("Better Wards - Recieved same version from server!");
                }
                else
                {
                    // Add client to validated list
                    ZLog.Log("Better Wards - Adding peer to validated list");
                    _validatedPeers.Add(rpc);
                }
            }
        }
    }
}