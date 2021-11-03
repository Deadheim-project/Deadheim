using HarmonyLib;
using System;

namespace Deadheim
{
    class NewCharacterValidatior
    {
        private static string connectionError = "";

        [HarmonyPatch(typeof(Game), "Start")]
        public static class GameStart
        {
            public static void Postfix()
            {
                if (ZRoutedRpc.instance == null)
                    return;

                ZRoutedRpc.instance.Register<ZPackage>("DeadheimInventory", new Action<long, ZPackage>(DontHaveInventory));
            }
        }

        public static void DontHaveInventory(long sender, ZPackage pkg)
        {
            if (Game.instance.m_playerProfile.m_worldData.Count != 0)
            {
                Game.instance.Logout();
                ZNet.m_connectionStatus = ZNet.ConnectionStatus.ErrorConnectFailed;
                connectionError = "Crie um novo personagem antes de entrar em Deadheim.";
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError))]
        private class ShowConnectionError
        {
            private static void Postfix(FejdStartup __instance)
            {
                if (__instance.m_connectionFailedPanel.activeSelf && connectionError != "")
                {
                    __instance.m_connectionFailedError.text += "\n" + connectionError;
                    connectionError = "";
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
        private class RPC_PeerInfo
        {
            private static void Postfix(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer())
                {
                    ZNetPeer peer = (ZNetPeer)AccessTools.DeclaredMethod(typeof(ZNet), "GetPeer", new[] { typeof(ZRpc) }).Invoke(__instance, new object[] { rpc });

                    string steamId = ((ZSteamSocket)peer.m_socket).GetPeerID().m_SteamID.ToString();
                    string playerName = peer.m_playerName;
                    string directory = ".config/unity3d/IronGate/Valheim/inventories/" + playerName + "-" + steamId + ".json";

                    bool hasInventory = System.IO.File.Exists(directory);

                    if (!hasInventory) ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "DeadheimInventory", new ZPackage());
                }
            }
        }
    }
}
