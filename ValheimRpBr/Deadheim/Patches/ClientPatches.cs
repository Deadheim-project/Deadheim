using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    }
}
