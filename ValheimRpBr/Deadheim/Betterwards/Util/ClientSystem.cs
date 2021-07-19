using UnityEngine;
using HarmonyLib;
using Deadheim;

namespace BetterWards.Util
{
    [HarmonyPatch]
    public class ClientSystem
    {

        public static void RPC_BadRequestMsg(long sender, ZPackage pkg)
        {
            if (sender != ZRoutedRpc.instance.GetServerPeerID() || pkg == null || pkg.Size() <= 0)
                return;
            string str = pkg.ReadString();
            if (!(str != ""))
                return;
            Chat.m_instance.AddString("Server", "<color=\"red\">" + str + "</color>", Talker.Type.Normal);
        }


        public static void RPC_EventTestConnection(long sender, ZPackage pkg)
        {
            Debug.Log((object)"Server has Better Wards installed");
            BetterWardsPlugin.valid_server = true;
        }

        public static void RPC_RequestTestConnection(long sender, ZPackage pkg)
        {
        }

        public static void RPC_EventAdminSync(long sender, ZPackage pkg)
        {
            Debug.Log((object)"This account is an admin.");
            Chat.m_instance.AddString("[Better Wards]", "<color=\"green\">" + "Admin permissions synced" + "</color>", Talker.Type.Normal);
            Plugin.age = pkg.ReadString();
            BetterWardsPlugin.admin = true;
        }

        public static void RPC_EventVipSync(long sender, ZPackage pkg)
        {
            Debug.Log((object)"This account is vip.");
            Chat.m_instance.AddString("[Vip]", "<color=\"green\">" + "Vip permissions synced" + "</color>", Talker.Type.Normal);

            Plugin.playerIsVip = true;        
        }

        public static void RPC_RequestAdminSync(long sender, ZPackage pkg)
        {
        }
    }
}
