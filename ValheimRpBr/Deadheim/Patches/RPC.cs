using HarmonyLib;
using System;
using System.IO;

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
                ZRoutedRpc.instance.Register<ZPackage>("SendWhitelist", new Action<long, ZPackage>(RPC_SendWhitelist));
            }
        }

        public static void SendWhitelist(string steamId)
        {
            if (steamId.Length != 17)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "SteamId em formato incorreto");
                return;
            }

            ZPackage zpackage = new ZPackage();
            zpackage.Write(steamId);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "SendWhitelist", zpackage);
        }

        public static void RPC_SendWhitelist(long sender, ZPackage pkg)
        {
            if (!ZNet.m_isServer) return;

            using (StreamWriter streamWriter1 = new StreamWriter(Utils.GetSaveDataPath() + "/permittedlist.txt", true))
            {
                StreamWriter streamWriter2 = streamWriter1;
                string str = pkg.ReadString();
                streamWriter2.WriteLine(str);
            }
        }
    }
}
