using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Patches
{
    [HarmonyPatch]
    public class ClientPatches : MonoBehaviour
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

        [HarmonyPatch(typeof(Player), "SetPlayerID")]
        internal class SetPlayerID
        {
            private static void Postfix(long playerID, string name)
            {
                if (Plugin.isPvp) Player.m_localPlayer.m_nview.GetZDO().Set("playerName", name + " - PVP");
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
                ZNet.instance.SetPublicReferencePosition(true);

                if (Vector3.Distance(new Vector3(-249, 262), Player.m_localPlayer.transform.position) <= Plugin.safeArea)
                {
                    InventoryGui.instance.m_pvp.isOn = false;
                    Player.m_localPlayer.SetPVP(false);
                } else
                {
                    InventoryGui.instance.m_pvp.isOn = true;
                    Player.m_localPlayer.SetPVP(true);
                }
                
                InventoryGui.instance.m_pvp.interactable = false;
            }
        }


        [HarmonyPatch(typeof(ZNet), "GetOtherPublicPlayers")]
        private class GetOtherPublicPlayers
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


        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix()
        {
            if (ZRoutedRpc.instance == null)
                return;

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "Sync", new ZPackage());
        }


        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        private class EnhanceHitDirection
        {
            private static void Prefix(
              ref float ___m_maxYAngle,
              ref float ___m_attackOffset,
              ref float ___m_attackHeight)
            {
                ___m_maxYAngle = 180f;
                ___m_attackOffset = 0;
                ___m_attackHeight = 1f;
            }
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        [HarmonyPriority(Priority.First)]
        static class OnDeath
        {
            static bool Prefix(Player __instance, Inventory ___m_inventory, ZNetView ___m_nview)
            {
                List<ItemDrop.ItemData> itemsToDrop = new List<ItemDrop.ItemData>();
                List<ItemDrop.ItemData> itemsToKeep = Traverse.Create(___m_inventory).Field("m_inventory").GetValue<List<ItemDrop.ItemData>>();

                ___m_nview.GetZDO().Set("dead", true);
                ___m_nview.InvokeRPC(ZNetView.Everybody, "OnDeath", new object[] { });
                Traverse.Create(__instance).Method("CreateDeathEffects").GetValue();

                for (int i = itemsToKeep.Count - 1; i >= 0; i--)
                {
                    ItemDrop.ItemData item = itemsToKeep[i];

                    if (item.m_equiped)
                        continue;

                    if (item.m_gridPos.y == 0)
                        continue;

                    if (item.m_shared.m_questItem)
                        continue;

                    if (Plugin.dropTypes.Contains(Enum.GetName(typeof(ItemDrop.ItemData.ItemType), item.m_shared.m_itemType)))
                    {
                        itemsToDrop.Add(item);
                        itemsToKeep.RemoveAt(i);
                    }
                }

                Traverse.Create(___m_inventory).Method("Changed").GetValue();

                if (itemsToDrop.Any())
                {
                    GameObject gameObject = Instantiate(__instance.m_tombstone, __instance.GetCenterPoint(), __instance.transform.rotation);
                    gameObject.GetComponent<Container>().GetInventory().RemoveAll();

                    int width = Traverse.Create(___m_inventory).Field("m_width").GetValue<int>();
                    int height = Traverse.Create(___m_inventory).Field("m_height").GetValue<int>();
                    Traverse.Create(gameObject.GetComponent<Container>().GetInventory()).Field("m_width").SetValue(width);
                    Traverse.Create(gameObject.GetComponent<Container>().GetInventory()).Field("m_height").SetValue(height);
                    Traverse.Create(gameObject.GetComponent<Container>().GetInventory()).Field("m_inventory").SetValue(itemsToDrop);
                    Traverse.Create(gameObject.GetComponent<Container>().GetInventory()).Method("Changed").GetValue();

                    TombStone component = gameObject.GetComponent<TombStone>();
                    PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
                    component.Setup(playerProfile.GetName(), playerProfile.GetPlayerID());
                }

                Player.m_localPlayer.ClearFood();
                Game.instance.GetPlayerProfile().m_playerStats.m_deaths++;
                Game.instance.GetPlayerProfile().SetDeathPoint(__instance.transform.position);
                Game.instance.RequestRespawn(10f);
                return false;
            }

        }
    }
}