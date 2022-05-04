using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Jotunn.Managers;
using static Deadheim.Plugin;
using System.IO;

namespace Deadheim
{
    [HarmonyPatch]
    public class Patches : MonoBehaviour
    {
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private static class ZNet__OnNewConnection
        {
            public static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (!__instance.IsServer())
                {
                    Plugin.steamId = SteamUser.GetSteamID().ToString();
                    ItemService.NerfRunicCape();
                }
            }
        }

        [HarmonyPatch(typeof(Chat), "OnNewChatMessage")]
        internal class OnNewChatMessage
        {
            private static bool Prefix(string user, string text)
            {
                if (text.ToLower().Contains("i have arrived")) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "HaveSeenTutorial")]
        public class Player_HaveSeenTutorial_Patch
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, ref string name)
            {
                if (!__instance.m_shownTutorials.Contains(name))
                {
                    __instance.m_shownTutorials.Add(name);
                }
            }
        }

        [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.Setup))]
        public static class SE_Stats_Setup_Patch
        {
            private static void Postfix(ref SE_Stats __instance)
            {
                int meginjordbuff = 200;
                if (__instance.m_addMaxCarryWeight > 0)
                    __instance.m_addMaxCarryWeight = (__instance.m_addMaxCarryWeight - 150) + meginjordbuff;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), "Update")]
        private static void GameUpdate()
        {
            if (Player.m_localPlayer)
            {
                Vector3 playerPos = Player.m_localPlayer.transform.position;

                ZNet.instance.SetPublicReferencePosition(true);

                float playerDistanceFromCenter = Vector3.Distance(new Vector3(0, 0), playerPos);
                if (playerDistanceFromCenter <= Plugin.SafeArea.Value || IsInsideAnyCity())
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

        private static bool IsInsideAnyCity()
        {
            Vector3 playerPos = Player.m_localPlayer.transform.position;

            foreach (City city in Plugin.cities)
            {
                float playerDistanceFromCity = Vector3.Distance(city.Position, playerPos);

                if (playerDistanceFromCity <= city.Radius) return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        public static class PlayerUpdate
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Player __instance)
            {
                if (Plugin.StaffMessage.Value != "") Player.m_localPlayer.Message(MessageHud.MessageType.Center, Plugin.StaffMessage.Value);
            }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.UpdatePlayerPins))]
        private class UpdatePlayerPins
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(Minimap __instance)
            {
                if (Plugin.IsAdmin) return;

                foreach (Minimap.PinData playerPin in __instance.m_playerPins)
                {
                    var group = Groups.API.GroupPlayers();

                    if (group.Exists(x => x.name == playerPin.m_name)) continue;

                    __instance.RemovePin(playerPin);
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), MethodType.Constructor, new Type[] { typeof(string), typeof(Sprite), typeof(int), typeof(int) })]
        public static class Inventory_Constructor_Patch
        {
            public static void Prefix(string name, ref int w, ref int h)
            {
                if (h == 4 && w == 8 || name == "Inventory") h = 6;
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

                int playerInventoryBackgroundSize = Math.Min(6, Math.Max(4, 8));
                float containerNewY = containerOriginalY - oneRowSize * playerInventoryBackgroundSize;
                player.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, playerInventoryBackgroundSize * oneRowSize);
                container.offsetMax = new Vector2(610, containerNewY);
                container.offsetMin = new Vector2(40, containerNewY + containerHeight);

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

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Awake_Postfix(ref Player __instance)
        {
            if (ZRoutedRpc.instance == null)
                return;

            __instance.m_maxCarryWeight = 450;
            ItemService.SetWardFirePlace();

            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "Sync", new ZPackage());
        }

        [HarmonyPatch(typeof(Player), nameof(Player.EdgeOfWorldKill))]
        [HarmonyPrefix]
        public static bool EdgeOfWorldKill()
        {
            return false;
        }

        [HarmonyPatch(typeof(SteamGameServer), "SetMaxPlayerCount")]
        public static class ChangeSteamServerVariables
        {
            private static void Prefix(ref int cPlayersMax)
            {
                int maxPlayers = Plugin.maxPlayers;
                if (maxPlayers >= 1)
                {
                    cPlayersMax = maxPlayers;
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), "Awake")]
        public static class ZNetAwake
        {
            private static void Postfix(ref ZNet __instance)
            {

                int maxPlayers = Plugin.maxPlayers;
                if (maxPlayers >= 1)
                {
                    __instance.m_serverPlayerLimit = maxPlayers;
                }
            }
        }

        [HarmonyPatch(typeof(CraftingStation), "Start")]
        public static class WorkbenchRangeIncrease
        {
            public static void Prefix(ref CraftingStation __instance, ref float ___m_rangeBuild, GameObject ___m_areaMarker)
            {
                try
                {
                    ___m_rangeBuild = 30;
                    ___m_areaMarker.GetComponent<CircleProjector>().m_radius = ___m_rangeBuild;
                }
                catch
                {
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        [HarmonyPriority(Priority.First)]
        static class OnDeath
        {
            static bool Prefix(Player __instance, Inventory ___m_inventory, ZNetView ___m_nview, Skills ___m_skills)
            {
                List<ItemDrop.ItemData> itemsToDrop = new List<ItemDrop.ItemData>();
                List<ItemDrop.ItemData> itemsToKeep = Traverse.Create(___m_inventory).Field("m_inventory").GetValue<List<ItemDrop.ItemData>>();

                ___m_nview.GetZDO().Set("dead", true);
                ___m_nview.InvokeRPC(ZNetView.Everybody, "OnDeath", new object[] { });
                Traverse.Create(__instance).Method("CreateDeathEffects").GetValue();

                var random = new System.Random();
                for (int i = itemsToKeep.Count - 1; i >= 0; i--)
                {
                    ItemDrop.ItemData item = itemsToKeep[i];

                    if (item.m_equiped)
                        continue;

                    if (item.m_shared.m_questItem)
                        continue;

                    if (random.Next(1, 100) <= Plugin.DropPercentagePerItem.Value && item.m_durability > 0 || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material)
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
                    Minimap.instance.AddPin(__instance.transform.position, Minimap.PinType.Death, string.Format("$hud_mapday {0}", (object)EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds())), true, false);
                }


                float factor = SkillDeathFactor.Value;

                if (factor > 0.1f) factor = 0.01f;

                ___m_skills.LowerAllSkills(factor);

                Player.m_localPlayer.ClearFood();
                Game.instance.GetPlayerProfile().m_playerStats.m_deaths++;
                Game.instance.GetPlayerProfile().SetDeathPoint(__instance.transform.position);
                Game.instance.RequestRespawn(10f);
                return false;
            }
        }

        [HarmonyPatch(typeof(Skills), "RaiseSkill")]
        public static class RaiseSkill
        {
            private static bool Prefix(ref Skills __instance, ref Skills.SkillType skillType, ref float factor)
            {
                Skills.Skill skill = __instance.GetSkill(skillType);

                if (skill.m_level >= Plugin.SkillCap.Value) return false;

                factor *= Plugin.SkillMultiplier.Value;
                return true;
            }
        }

        [HarmonyPatch(typeof(EnvMan), "SetEnv")]
        public static class EnvMan_SetEnv_Patch
        {
            private static void Prefix(ref EnvMan __instance, ref EnvSetup env)
            {
                env.m_fogDensityNight = 0.0001f;
                env.m_fogDensityMorning = 0.0001f;
                env.m_fogDensityDay = 0.0001f;
                env.m_fogDensityEvening = 0.0001f;
            }
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        public static class TeleportWorldAesir
        {
            private static bool Prefix(TeleportWorld __instance)
            {
                if (Plugin.Vip.Value.Contains(Plugin.steamId)) return true;

                if (!Plugin.VipPortalNames.Value.Contains(__instance.GetText())) return true;

                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Only Aesir's can access this portal.");
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (Plugin.Vip.Value.Contains(Plugin.steamId)) return true;

                if (piece.gameObject.name == "AesirChest") return false;

                return true;
            }
        }
    }
}
