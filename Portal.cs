using HarmonyLib;
using Jotunn.Managers;

namespace Deadheim
{
    [HarmonyPatch]
    class Portal
    {
        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (SynchronizationManager.Instance.PlayerIsAdmin) return true;

                if (!piece.gameObject.name.Contains("portal_wood")) return true;

                int wards = GetPortalCount();
                bool isVip = Plugin.Vip.Value.Contains(Plugin.steamId);

                if (isVip)
                {
                    if (wards >= Plugin.PortalLimitVip.Value)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você não pode mais colocar portais.", 0, null);
                        return false;
                    }
                }
                else if (wards >= Plugin.PortalLimit.Value)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você não pode mais colocar portais.", 0, null);
                    return false;
                }

                ZPackage pkg = new();
                pkg.Write(Player.m_localPlayer.GetPlayerID());
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
                return true;
            }
        }

        private static int GetPortalCount()
        {
            if (SynchronizationManager.Instance.PlayerIsAdmin) return 0;

            ZPackage pkg = new();
            pkg.Write(Player.m_localPlayer.GetPlayerID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);

            return Plugin.PlayerPortalCount;
        }
    }
}
