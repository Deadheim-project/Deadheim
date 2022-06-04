using HarmonyLib;
using System;

namespace Deadheim
{
    [HarmonyPatch]
    class RPC
    {
        public static void RPC_PortalAndTotemCountServer(long sender, ZPackage pkg)
        {
            if (!ZNet.instance.IsServer()) return;

            long creatorId = pkg.ReadLong();

            string result = Util.GetCreatorWardAndPortalCount(creatorId);

            ZPackage toSend = new();
            toSend.Write(result);

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "DeadheimPortalAndTotemCountClient", toSend);
        }

        public static void RPC_PortalAndTotemCountClient(long sender, ZPackage pkg)
        {
            string counts = pkg.ReadString();

            Plugin.PlayerPortalCount = Convert.ToInt32(counts.Split(',')[0]);
            Plugin.PlayerWardCount = Convert.ToInt32(counts.Split(',')[1]);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class OnSpawned
        {
            public static void Postfix()
            {
                ZPackage pkg = new();
                pkg.Write(Player.m_localPlayer.GetPlayerID());
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
            }
        }

        [HarmonyPatch(typeof(Game), "Start")]
        public static class GameStart
        {
            public static void Postfix()
            {
                if (ZRoutedRpc.instance == null)
                    return;

                ZRoutedRpc.instance.Register<ZPackage>("DeadheimPortalAndTotemCountServer", new Action<long, ZPackage>(RPC_PortalAndTotemCountServer));
                ZRoutedRpc.instance.Register<ZPackage>("DeadheimPortalAndTotemCountClient", new Action<long, ZPackage>(RPC_PortalAndTotemCountClient));
            }
        }
    }
}
