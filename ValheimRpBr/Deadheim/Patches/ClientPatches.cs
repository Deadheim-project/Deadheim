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

                if (text.Contains("/whitelist") && user == Player.m_localPlayer.GetPlayerName() && Plugin.isModerator)
                {
                    var steamIdFromText = text.Split(' ')[1];
                    RPC.SendWhitelist(steamIdFromText);
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
                Datadog.Datadog.SendPlayerPing(__instance);
                Datadog.Datadog.verifyIfPlayerIsCheating(__instance);
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
                if (BetterWards.BetterWardsPlugin.admin)
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
    }
}
