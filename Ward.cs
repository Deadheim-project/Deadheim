using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Deadheim.EnhancedWards
{
    [HarmonyPatch]
    public class Ward
    {
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        public static class RPC_Damage
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                try
                {
                    if (___m_nview is null) return false;
                    if (!CheckInPrivateArea(__instance.transform.position)) return true;
                    if (__instance.gameObject.name.Contains("guard_stone")) return false;
                    if (__instance.gameObject.name.Contains("AdminWard")) return false;
                    if (Vector3.Distance(new Vector3(0, 0), Player.m_localPlayer.transform.position) <= Plugin.SafeArea.Value) return false;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message + "    - " + e.StackTrace);
                    return false;
                }
            }
        }

        
        public static bool CheckInPrivateArea(Vector3 point, bool flash = false)
        {
            foreach (PrivateArea allArea in PrivateArea.m_allAreas)
            {
                if (allArea.IsEnabled() && allArea.IsInside(point, 0.0f))
                {
                    if (flash)
                        allArea.FlashShield(false);
                    return true;
                }
            }
            return false;
        }

        [HarmonyPatch(typeof(Door), nameof(Door.CanInteract))]
        public static class DoorCanInteract
        {
            private static void Postfix(Door __instance, ref bool __result)
            {
                string name = __instance.gameObject.name;
                name = name.Replace("(Clone)", "");
                if (Plugin.DungeonPrefabs.Value.Split(',').Contains(__instance.gameObject.name.Replace("(Clone)", ""))) __result = true;
            }
        }

        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.IsEnabled))]
        public static class PrivateAreaCheckAccess
        {
            private static void Postfix(PrivateArea __instance, ref bool __result)
            {
                Player player = Player.m_localPlayer;

                if (!player) return;

                if (!player.m_hovering) return;

                Interactable interactable = player.m_hovering.GetComponentInParent<Interactable>();
                if (interactable is null) return;

                Door door = null;
                Container container = null;

                if (interactable.GetType().Name is "Door") door = (Door)interactable;
                if (interactable.GetType().Name is "Container") container = (Container)interactable;

                if (door is null && container is null) return;

                string name = "";
                if (door is not null) name = door.gameObject.name;
                if (container) name = container.gameObject.name;

                if (name == "") return;

                name = name.Replace("(Clone)", "");
                if (Plugin.DungeonPrefabs.Value.Split(',').Contains(name)) __result = false;
            }
        }

        [HarmonyPatch(typeof(Fireplace), "UpdateState")]
        public static class FireplaceUpdateState
        {
            private static bool Prefix(Fireplace __instance)
            {
                if (!__instance.gameObject.name.Contains("guard_stone")) return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(Fireplace), "Awake")]
        public static class FireplaceAwake
        {
            private static bool Prefix(Fireplace __instance)
            {
                try
                {
                    if (!__instance.gameObject.name.Contains("guard_stone")) return true;
                    __instance.m_nview = __instance.gameObject.GetComponent<ZNetView>();
                    __instance.m_piece = __instance.gameObject.GetComponent<Piece>();
                    if (__instance.m_nview.GetZDO() == null) return false;

                    if (__instance.m_nview.IsOwner() && __instance.m_nview.GetZDO().GetFloat("fuel", -1f) == -1.0)
                    {
                        __instance.m_nview.GetZDO().Set("fuel", __instance.m_startFuel);

                    }
                    __instance.m_nview.Register("AddFuel", new Action<long>(__instance.RPC_AddFuel));
                    __instance.InvokeRepeating("UpdateFireplace", 0.0f, 10f);

                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        public static class UpdateFireplace
        {
            private static bool Prefix(Fireplace __instance)
            {
                if (!__instance.gameObject.name.Contains("guard_stone")) return true;

                if (!__instance.m_nview.IsValid()) return false;

                Piece piece = __instance.gameObject.GetComponent<Piece>();

                if (__instance.m_nview.IsOwner())
                {
                    if (__instance.m_fuelItem is null)
                    {
                        __instance.m_fuelItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
                    }
                    __instance.m_secPerFuel = Plugin.WardChargeDurationInSec.Value;
                    __instance.m_maxFuel = 10;
                    PrivateArea privateArea = __instance.gameObject.GetComponent<PrivateArea>();

                    float num1 = __instance.m_nview.GetZDO().GetFloat("fuel");
                    double timeSinceLastUpdate = __instance.GetTimeSinceLastUpdate();
                    float num2 = (float)timeSinceLastUpdate / __instance.m_secPerFuel;
                    float num3 = num1 - num2;
                    if ((double)num3 <= 0.0)
                    {
                        num3 = 0.0f;
                        if (privateArea.IsEnabled()) privateArea.SetEnabled(false);
                    }

                    if (num3 > 10) num3 = 10;

                    __instance.m_nview.GetZDO().Set("fuel", num3);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (Plugin.IsAdmin) return true;

                bool isTotem = piece.gameObject.name.Contains("guard_stone");

                bool isInsideArea = false;

                Vector3 placementGhost = __instance.m_placementGhost.transform.position;
                if (isTotem)
                {
                    var wardsZDO = GetZDOList(piece.gameObject.name.GetStableHashCode());

                    foreach (ZDO zdo in wardsZDO)
                    {
                        isInsideArea = Vector3.Distance(new Vector3(x: placementGhost.x, y: 0, z: placementGhost.z), new Vector3(x: zdo.m_position.x, y: 0, z: zdo.m_position.z)) <= (piece.GetComponent<PrivateArea>().m_radius * 3);
                        if (isInsideArea)
                        {
                            bool isPermitted = IsBuilderPermitted(zdo, __instance);
                            if (!isPermitted)
                            {
                                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Não é possível construir wards próximo da área de outros wards.", 0, null);
                                return false;
                            }
                        }
                    }
                }

                if (!isTotem) return true;

                int wards = GetWardCount();
                bool isVip = Plugin.Vip.Value.Contains(Plugin.steamId);

                if (isVip)
                {
                    if (wards >= Plugin.WardLimitVip.Value)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você não pode mais colocar wards.", 0, null);
                        return false;
                    }
                }
                else if (wards >= Plugin.WardLimit.Value)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você não pode mais colocar wards.", 0, null);
                    return false;
                }

                Minimap.instance.AddPin(__instance.transform.position, Minimap.PinType.Boss, "WARD", true, false);
                ZPackage pkg = new();
                pkg.Write(Player.m_localPlayer.GetPlayerID());
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
                return true;
            }
        }

        private static int GetWardCount()
        {
            if (Plugin.IsAdmin) return 0;
            ZPackage pkg = new();
            pkg.Write(Player.m_localPlayer.GetPlayerID());
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "DeadheimPortalAndTotemCountServer", pkg);
            return Plugin.PlayerWardCount;
        }

        private static List<ZDO> GetZDOList(int prefabHash)
        {
            List<ZDO> toreturn = new();
            foreach (List<ZDO> zdoList in ZDOMan.instance.m_objectsBySector)
            {
                if (zdoList == null) continue;

                for (int index = 0; index < zdoList.Count; ++index)
                {
                    ZDO zdo2 = zdoList[index];
                    if (zdo2.GetPrefab() == prefabHash)
                    {
                        toreturn.Add(zdo2);
                    }
                }
            }
            return toreturn;
        }

        private static bool IsBuilderPermitted(ZDO zdo, Player player)
        {
            List<KeyValuePair<long, string>> keyValuePairList = new List<KeyValuePair<long, string>>();
            long prefabCreatorHash = zdo.GetLong(Util.CREATORHASH);
            if (prefabCreatorHash == player.GetPlayerID()) return true;

            int num = zdo.GetInt("permitted");
            for (int index = 0; index < num; ++index)
            {
                long key = zdo.GetLong("pu_id" + (object)index);
                string str = zdo.GetString("pu_name" + (object)index);
                if (key != 0L)
                    keyValuePairList.Add(new KeyValuePair<long, string>(key, str));
            }

            foreach (KeyValuePair<long, string> permittedPlayer in keyValuePairList)
            {
                if (permittedPlayer.Key == player.GetPlayerID())
                    return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Player), "Update")]
        public static class Update
        {
            private static void Postfix(ref Player __instance)
            {
                if (!__instance.m_hovering) return;

                Interactable interactable = __instance.m_hovering.GetComponentInParent<Interactable>();
                if (interactable is null) return;

                if (interactable is not PrivateArea privateArea) return;

                Fireplace fireplace = privateArea.GetComponentInParent<Fireplace>();

                StringBuilder text = new StringBuilder(256);

                KeyCode wardOptionalKey = KeyCode.K;
                KeyCode fuelWard = KeyCode.Y;

                if (privateArea.IsPermitted(__instance.GetPlayerID()) || privateArea.m_piece.m_creator == __instance.GetPlayerID())
                {
                    if (fireplace) text.Append("\n[<color=yellow><b>" + fuelWard.ToString() + "</b></color>] Fuel: " + Math.Round(fireplace.m_nview.GetZDO().GetFloat("fuel"), 2) + "/" + fireplace.m_maxFuel);
                    text.Append("\n[<color=yellow><b>" + wardOptionalKey.ToString() + "</b></color>]");
                    privateArea.m_name = text.ToString();
                    privateArea.AddUserList(text);

                    if (Input.GetKeyDown(wardOptionalKey)) Interact(__instance, __instance.m_hovering, privateArea);

                    if (Input.GetKeyDown(fuelWard)) FuelWard(__instance, fireplace);
                }
            }

            public static void FuelWard(Player player, Fireplace fireplace)
            {
                if (fireplace.m_holdRepeatInterval <= 0.0 || Time.time - fireplace.m_lastUseTime < fireplace.m_holdRepeatInterval) return;
                if (!fireplace.m_nview.HasOwner()) fireplace.m_nview.ClaimOwnership();

                if (fireplace.m_fuelItem is null)
                {
                    fireplace.m_fuelItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
                }

                fireplace.Interact(player, false, false);
            }

            public static void Interact(Player player, GameObject go, PrivateArea privateArea)
            {
                player.m_lastHoverInteractTime = Time.time;

                privateArea.m_nview.InvokeRPC("ToggleEnabled", new object[]
                {
                                privateArea.m_piece.GetCreator()
                });

                Vector3 vector = go.transform.position - player.transform.position;
                vector.y = 0f;
                vector.Normalize();
                player.transform.rotation = Quaternion.LookRotation(vector);
                player.m_zanim.SetTrigger("interact");
            }
        }
    }
}
