using UnityEngine;
using HarmonyLib;
using System;
using System.IO;

namespace Deadheim.Server
{ 
    [HarmonyPatch]
    public class ServerPatches
    {

        static bool alreadyRouted = false;

        [HarmonyPatch(typeof(Game), "Start")]
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (alreadyRouted == false)
            {
                alreadyRouted = true;
                ZRoutedRpc.instance.Register<ZPackage>("SendLog", new Action<long, ZPackage>(RPC_SendLog));
            }
        }

        public static void SendLog(string msg)
        {
            ZPackage zpackage = new ZPackage();
            zpackage.Write(msg);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "SendLog", zpackage);
        }

        public static void RPC_SendLog(long sender, ZPackage pkg)
        {
            if (!ZNet.m_isServer) return;

            using (StreamWriter streamWriter1 = new StreamWriter(Utils.GetSaveDataPath() + "/moderation.txt", true))
            {
                StreamWriter streamWriter2 = streamWriter1;
                DateTime dateTime = DateTime.Now;
                dateTime = dateTime.ToUniversalTime();
                string str = dateTime.ToString() + " " + pkg.ReadString();
                streamWriter2.WriteLine(str);
            }  
        }
    }
}
