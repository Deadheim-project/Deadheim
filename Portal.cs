using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if (Plugin.IsAdmin) return true;

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

                return true;
            }
        }


        private static int GetPortalCount()
        {
            if (Plugin.IsAdmin) return 0;

            ZPackage pkg = new();
            pkg.Write(Player.m_localPlayer.GetPlayerID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);

            return Plugin.PlayerPortalCount;
        }
    }
}
