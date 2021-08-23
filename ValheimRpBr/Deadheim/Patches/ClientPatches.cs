using Deadheim.agesystem;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Patches
{
    [HarmonyPatch]
    public class ClientPatches
    {
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private static class ZNet__OnNewConnection
        {
            public static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (!__instance.IsServer())
                {
                    Plugin.steamId = SteamUser.GetSteamID().ToString();
                }
            }
        }

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        internal class OnNewChatMessage
        {
            private static void Prefix(string user, string text)
            {
                if (text.Contains("/setcolor") && user == Player.m_localPlayer.GetPlayerName())
                {
                    var colorValueFromText = text.Split(' ')[1];
                    ColorfulPieces.ColorfulPieces.UpdateColorValue(colorValueFromText);
                }

                if (text.Contains("/sethaircolor") && user == Player.m_localPlayer.GetPlayerName())
                {
                    var colorValueFromText = text.Split(' ')[1];
                    DyeHard.DyeHard.UpdatePlayerHairColorValue(colorValueFromText);
                }

                Datadog.Datadog.SendLog("-- Chatlog: " + text + " steamId: " + Plugin.steamId + " user:" + user + " LocalChat");
            }
        }

        [HarmonyPatch(typeof(Player), "FixedUpdate")]
        public static class PlayerCheck
        {
            private static void Postfix(Player __instance)
            {
                if (!__instance && !String.IsNullOrEmpty(Plugin.steamId)) return;

                Datadog.Datadog.SendPlayerPosition(__instance);
                Datadog.Datadog.verifyIfPlayerIsCheating(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), "Update")]
        private static void GameUpdate()
        {
            if (Player.m_localPlayer)
            {
                Player.m_localPlayer.SetPVP(true);
                ZNet.instance.SetPublicReferencePosition(true);
                InventoryGui.instance.m_pvp.isOn = true;
                InventoryGui.instance.m_pvp.interactable = false;
            }
        }

        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class ConsumeLog
        {
            private static void Postfix(Inventory inventory, ItemDrop.ItemData item, Player __instance)
            {
                if (item == null) return;
                if (__instance == null) return;
                Datadog.Datadog.SendLog("-- ConsumeLog: " + __instance.GetPlayerName() + " : consume : " + item.m_shared.m_name + " crafted by: " + (string)item.m_crafterName + " -- located at: " + Player.m_localPlayer.transform.position);
            }
        }

        [HarmonyPatch(typeof(Sign), "SetText")]
        public static class SignText
        {
            private static void Postfix(string text) => Datadog.Datadog.SendLog("-- SetTextLog: " + Player.m_localPlayer.GetPlayerName() + " : sign_text : " + text + " -- located at: " + Player.m_localPlayer.transform.position);
        }

        [HarmonyPatch(typeof(TeleportWorld), "SetText")]
        public static class TeleportText
        {
            private static void Postfix(string text) => Datadog.Datadog.SendLog("-- SetTextLog: " + Player.m_localPlayer.GetPlayerName() + " : teleport_text : " + text + " -- located at: " + Player.m_localPlayer.transform.position);
        }

        [HarmonyPatch(typeof(Ship), "OnDestroyed")]
        private class shipDestroyed
        {
            private static void Postfix(ref Ship __instance)
            {
                if (__instance == null) return;

                List<Player> playerList = new List<Player>();
                Player.GetPlayersInRange(((Component)__instance).transform.position, 20f, playerList);
                Datadog.Datadog.SendLog("-- Shiplog: Ship destroyed" + __instance.gameObject.name + " creator: " + __instance.m_nview.GetZDO().GetString("creatorName", "") + " around: " + string.Join(",", ((IEnumerable<Player>)playerList).Select<Player, string>((Func<Player, string>)(p => p.GetPlayerName()))));
            }
        }

        [HarmonyPatch(typeof(Player), "StartShipControl")]
        private class shipControlled
        {
            private static void Postfix(ref ShipControlls shipControl) => Datadog.Datadog.SendLog("-- Shiplog: ship controlled " + shipControl.GetShip().gameObject.name + " creator: " + shipControl.GetShip().m_nview.GetZDO().GetString("creatorName", "") + " player: " + Game.instance.GetPlayerProfile().GetName());
        }

        [HarmonyPatch(typeof(ZNet), "GetOtherPublicPlayers")]
        private class playpinsadmin
        {
            private static bool Prefix(List<ZNet.PlayerInfo> playerList, ZNet __instance)
            {
                if (Plugin.admin && Player.m_localPlayer.name.ToLower() != "jah")
                {
                    foreach (ZNet.PlayerInfo player in __instance.m_players)
                    {
                        ZDOID characterId = (ZDOID)player.m_characterID;
                        if (!characterId.IsNone() && !player.m_characterID.Equals(__instance.m_characterID))
                            playerList.Add(player);

                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (ZNet.m_isServer || piece.gameObject.name != "guard_stone")
                {
                    return true;
                }

                bool isInNotAllowedArea = false;

                List<PrivateArea> privateAreaList = new List<PrivateArea>();

                foreach (PrivateArea area in PrivateArea.m_allAreas)
                {
                    bool isInsideArea = Vector3.Distance(__instance.transform.position, area.transform.position) <= (area.m_radius * 2.5);
                    if (isInsideArea) privateAreaList.Add(area);
                }

                foreach (PrivateArea area in privateAreaList)
                {
                    bool isPermitted = area.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || area.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());
                    if (!isPermitted) isInNotAllowedArea = true;
                }

                if (isInNotAllowedArea)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível construir wards próximo da área de outros wards.", 0, null);
                    return false;

                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix()
        {
            if (ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "Sync", new ZPackage());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), "Update")]
        private static void FejdStartup__Update(GameObject ___m_startGamePanel, Button ___m_worldStart)
        {
            if (!___m_startGamePanel.activeInHierarchy)
                return;
            GameObject gameObject = GameObject.Find("Start");
            if (gameObject != null)
            {
                Text componentInChildren = gameObject.GetComponentInChildren<Text>();
                if (componentInChildren != null)
                    componentInChildren.text = "Desativado no Deadheim";
            }
            ___m_worldStart.interactable = false;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "GetBuildPieces")]
        private static void GetBuildPieces(List<Piece> __result)
        {
            List<string> vipItems = new List<string>() { "wall", "roof", "floor", "stake", "pole", "pillar", "stair", "beam" };
            foreach (Piece piece in __result)
            {
                if (Plugin.playerIsVip)
                {
                    if (vipItems.Any(x => piece.m_name.Contains(x)))
                    {
                        foreach (Piece.Requirement requirement in piece.m_resources.ToList())
                        {
                            if (requirement.m_amount > 1)
                            {
                                requirement.m_amount = (int) Math.Ceiling((decimal)(requirement.m_amount / 2));
                            }
                        }
                    }
                }
            }
        }
    }
}