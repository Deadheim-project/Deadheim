using UnityEngine;
using HarmonyLib;
using System.IO;

namespace BetterWards.Server
{
    [HarmonyPatch]
    public class ServerAdminSystem
    {
        public static void RPC_RequestSync(long sender, ZPackage pkg)
        {

            if (ZNet.instance.GetPeer(sender) != null)
            {
                ZPackage zpackage1 = new ZPackage();
                string data = EnvMan.instance.m_debugTime.ToString();
                zpackage1.Write(data);
                ZPackage zpackage2 = new ZPackage();
                string debugEnv = EnvMan.instance.m_debugEnv;
                zpackage2.Write(debugEnv);
                Debug.Log((object)"Syncing with clients...");
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "EventTestConnection", (object)new ZPackage());

            }
            else
            {
                ZPackage zpackage = new ZPackage();
                zpackage.Write("Peer doesn't exist");
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "BadRequestMsg", (object)zpackage);
            }
        }

        public static void RPC_EventSync(long sender, ZPackage pkg)
        {
        }

        public static void RPC_RequestAdminSync(long sender, ZPackage pkg)
        {

            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (peer != null)
            {
                string str = ((ZSteamSocket)peer.m_socket).GetPeerID().m_SteamID.ToString();
                if (ZNet.instance.m_adminList == null || !ZNet.instance.m_adminList.Contains(str))
                    return;
                ZPackage zpackage = new ZPackage();
                zpackage.Write(File.ReadAllText(Utils.GetSaveDataPath() + "/era.txt"));
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "EventAdminSync", zpackage);
            }
            else
            {
                ZPackage zpackage = new ZPackage();
                zpackage.Write("You aren't an Admin!");
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "BadRequestMsg", (object)zpackage);
            }
        }

        public static void RPC_EventAdminSync(long sender, ZPackage pkg)
        {
        }

    }
}
