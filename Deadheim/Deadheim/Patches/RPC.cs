using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Deadheim.Patches
{ 
    [HarmonyPatch]
    public class RPC
    {
        static bool alreadyRouted = false;

        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (alreadyRouted == false)
            {
                alreadyRouted = true;
                ZRoutedRpc.instance.Register<ZPackage>("SendLog", new Action<long, ZPackage>(Datadog.Datadog.RPC_SendLog));
                ZRoutedRpc.instance.Register<ZPackage>("Sync", new Action<long, ZPackage>(RPC_Sync));
                ZRoutedRpc.instance.Register<ZPackage>("AdminSync", new Action<long, ZPackage>(RPC_AdminSync));
                ZRoutedRpc.instance.Register<ZPackage>("VipSync", new Action<long, ZPackage>(RPC_VipSync));
                ZRoutedRpc.instance.Register<ZPackage>("ModeratorSync", new Action<long, ZPackage>(RPC_ModeratorSync));
                ZRoutedRpc.instance.Register<ZPackage>("EraSync", new Action<long, ZPackage>(RPC_EraSync));
            }
        }

        public static void RPC_AdminSync(long sender, ZPackage pkg)
        {
            Chat.m_instance.AddString("Server", "Admin permissions synced", Talker.Type.Normal);
            Plugin.admin = true;
        }

        public static void RPC_VipSync(long sender, ZPackage pkg)
        {
            Chat.m_instance.AddString("Server", "Vip permissions synced", Talker.Type.Normal);
            Plugin.playerIsVip = true;
        }

        public static void RPC_ModeratorSync(long sender, ZPackage pkg)
        {
            Chat.m_instance.AddString("Server", "Moderator permissions synced", Talker.Type.Normal);
            Plugin.isModerator = true;
        }

        public static void RPC_EraSync(long sender, ZPackage pkg)
        {
            string age = pkg.ReadString();
            Chat.m_instance.AddString("Server", "You are in the age of: " + age, Talker.Type.Normal);
            Plugin.age = age;
        }

        public static void RPC_Sync(long sender, ZPackage pkg)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(sender);

            if (peer != null)
            {
                string str = ((ZSteamSocket)peer.m_socket).GetPeerID().m_SteamID.ToString();
                List<string> vipList = File.ReadAllText(Utils.GetSaveDataPath() + "/vip.txt").Split(' ').ToList();
                List<string> moderatorList = File.ReadAllText(Utils.GetSaveDataPath() + "/moderator.txt").Split(' ').ToList();

                if (!String.IsNullOrEmpty(str) && vipList.Contains(str)) ZRoutedRpc.instance.InvokeRoutedRPC(sender, "VipSync", new ZPackage());
                if (!String.IsNullOrEmpty(str) && moderatorList.Contains(str)) ZRoutedRpc.instance.InvokeRoutedRPC(sender, "ModeratorSync", new ZPackage());

                ZPackage eraPackage = new ZPackage();
                eraPackage.Write(File.ReadAllText(Utils.GetSaveDataPath() + "/era.txt"));
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "EraSync", eraPackage);

                if (ZNet.instance.m_adminList == null || !ZNet.instance.m_adminList.Contains(str))
                    return;

                ZRoutedRpc.instance.InvokeRoutedRPC(sender, "AdminSync", new ZPackage());
            }
        }
    }
}
