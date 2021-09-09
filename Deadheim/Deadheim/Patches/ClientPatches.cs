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
                }
                else
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

        [HarmonyPatch(typeof(Inventory), MethodType.Constructor, new Type[] { typeof(string), typeof(Sprite), typeof(int), typeof(int) })]
        public static class Inventory_Constructor_Patch
        {
            public static void Prefix(string name, ref int w, ref int h)
            {
                if (h == 4 && w == 8 || name == "Inventory") h = 8;
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        public class InventoryGui_Show_Patch
        {
            private const float oneRowSize = 70.5f;
            private const float containerOriginalY = -90.0f;
            private const float containerHeight = -340.0f;
            private static float lastValue = 0;

            public static void Postfix(ref InventoryGui __instance)
            {
                RectTransform container = __instance.m_container;
                RectTransform player = __instance.m_player;
                GameObject playerGrid = InventoryGui.instance.m_playerGrid.gameObject;

                // Player inventory background size, only enlarge it up to 6x8 rows, after that use the scroll bar
                int playerInventoryBackgroundSize = Math.Min(6, Math.Max(4, 8));
                float containerNewY = containerOriginalY - oneRowSize * playerInventoryBackgroundSize;
                // Resize player inventory
                player.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, playerInventoryBackgroundSize * oneRowSize);
                // Move chest inventory based on new player invetory size
                container.offsetMax = new Vector2(610, containerNewY);
                container.offsetMin = new Vector2(40, containerNewY + containerHeight);

                // Add player inventory scroll bar if it does not exist
                if (!playerGrid.GetComponent<InventoryGrid>().m_scrollbar)
                {
                    GameObject playerGridScroll = GameObject.Instantiate(InventoryGui.instance.m_containerGrid.m_scrollbar.gameObject, playerGrid.transform.parent);
                    playerGridScroll.name = "PlayerScroll";
                    playerGrid.GetComponent<RectMask2D>().enabled = true;
                    ScrollRect playerScrollRect = playerGrid.AddComponent<ScrollRect>();
                    playerGrid.GetComponent<RectTransform>().offsetMax = new Vector2(800f, playerGrid.GetComponent<RectTransform>().offsetMax.y);
                    playerGrid.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 1f);
                    playerScrollRect.content = playerGrid.GetComponent<InventoryGrid>().m_gridRoot;
                    playerScrollRect.viewport = __instance.m_player.GetComponentInChildren<RectTransform>();
                    playerScrollRect.verticalScrollbar = playerGridScroll.GetComponent<Scrollbar>();
                    playerGrid.GetComponent<InventoryGrid>().m_scrollbar = playerGridScroll.GetComponent<Scrollbar>();

                    playerScrollRect.horizontal = false;
                    playerScrollRect.movementType = ScrollRect.MovementType.Clamped;
                    playerScrollRect.scrollSensitivity = oneRowSize;
                    playerScrollRect.inertia = false;
                    playerScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                    Scrollbar playerScrollbar = playerGridScroll.GetComponent<Scrollbar>();
                    lastValue = playerScrollbar.value;
                }
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