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
            private static bool Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                try
                {
                    if (!PrivateArea.CheckInPrivateArea(__instance.transform.position)) return true;
                    if ((Vector3.Distance(new Vector3(0, 0), Player.m_localPlayer.transform.position) <= Plugin.SafeArea.Value)) return false;
                    if (___m_nview is null) return false;

                    if (__instance.gameObject.name.Contains("guard_stone")) return false;

                    if (!hit.GetAttacker().IsPlayer())
                    {
                        hit.ApplyModifier(1 - (Plugin.MonsterDamageWardReduction.Value / 100));
                        return true;
                    }

                    if (IsRaidBlocked())
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "VAI DORMIR PORRA");
                        return false;
                    }

                    if (hit.GetAttacker().IsPlayer() && !PrivateArea.CheckAccess(__instance.transform.position) && Plugin.PlayersInsideWardForRaid.Value != 0)
                    {
                        List<PrivateArea> areas = PrivateArea.m_allAreas;

                        List<Player> players = new List<Player>();
                        Player.GetPlayersInRange(__instance.transform.position, (float)Plugin.WardRadius.Value * (float)2, players);

                        int allowedPlayersInsideWard = 0;

                        foreach (Player player in players)
                        {
                            foreach (PrivateArea area in areas)
                            {
                                if (area.IsPermitted(player.GetPlayerID()))
                                {
                                    allowedPlayersInsideWard++;
                                    break;
                                }
                            }
                        }

                        if (allowedPlayersInsideWard <= Plugin.PlayersInsideWardForRaid.Value)
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Proteção contra raid ativa.");
                            return false;
                        }
                    }

                    hit.ApplyModifier(1 - (Plugin.WardReductionDamage.Value / 100));
                    return true;
                }
                catch
                {
                    return true;
                }
            }
        }

        private static bool IsRaidBlocked()
        {
            DateTime utcStart = DateTime.UtcNow;
            double startHour = Convert.ToDouble(Plugin.TimeToBlockRaid.Value.Split(',')[0]);
            utcStart.AddHours(-utcStart.Hour);
            utcStart.AddHours(startHour);

            double hoursDuration = Convert.ToDouble(Plugin.TimeToBlockRaid.Value.Split(',')[1]);
            DateTime utcEnd = utcStart.AddHours(hoursDuration);


            return IsDateTimeNowUtcBetween(utcStart, utcEnd);
        }

        public static bool IsDateTimeNowUtcBetween(DateTime date1, DateTime date2)
        {
            DateTime input = DateTime.UtcNow;
            return (input > date1 && input < date2);
        }

        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        public static class CheckCanRemovePiece
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (__instance.m_noPlacementCost) return true;
                if (!piece.gameObject.name.Contains("guard_stone")) return true;
                if (piece.m_creator != __instance.GetPlayerID() && !SynchronizationManager.Instance.PlayerIsAdmin)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas o dono do totem pode destrui-lo", 0, null);
                    return false;
                }

                return true;
            }

            private static void Postfix(Piece piece, Player __instance, bool __result)
            {
                if (!piece.gameObject.name.Contains("guard_stone")) return;
                if (piece.m_creator != __instance.GetPlayerID()) return;

                if (!Player.m_localPlayer.m_knownTexts.ContainsKey("dead_PlayerWards")) return;

                int wards = Convert.ToInt32(Player.m_localPlayer.m_knownTexts["dead_PlayerWards"]);
                wards--;
                if (__result) Player.m_localPlayer.m_knownTexts["dead_PlayerWards"] = wards.ToString();
            }
        }


        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        public static class UpdateFireplace
        {
            private static bool Prefix(Fireplace __instance)
            {
                if (!__instance.gameObject.name.Contains("guard_stone")) return true;

                if (!__instance.m_nview.IsValid()) return false;
                if (__instance.m_nview.IsOwner())
                {
                    if (__instance.m_fuelItem is null)
                    {
                        __instance.m_fuelItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
                        __instance.m_enabledObject = PrefabManager.Instance.GetPrefab("GreydwarfEye");
                        __instance.m_enabledObjectHigh = PrefabManager.Instance.GetPrefab("GreydwarfEye");
                        __instance.m_enabledObjectLow = PrefabManager.Instance.GetPrefab("GreydwarfEye");
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

                    __instance.m_nview.GetZDO().Set("fuel", num3);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                bool isTotem = piece.gameObject.name.Contains("guard_stone");

                int areaInstances = (ZNetScene.m_instance.m_instances.Count);

                bool isInsideArea = false;

                foreach (PrivateArea area in PrivateArea.m_allAreas)
                {
                    isInsideArea = Vector3.Distance(__instance.transform.position, area.transform.position) <= (area.m_radius * 2.5);
                    if (isInsideArea) break;
                }

                if (isTotem)
                {
                    if (!Plugin.Vip.Value.Contains(Plugin.steamId) && areaInstances >= 6500)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem construir em locais com mais de 6500 instâncias", 0, null);
                        return false;
                    }

                    if (Plugin.Vip.Value.Contains(Plugin.steamId) && areaInstances >= 10000)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "O limite é de 10000 instâncias.", 0, null);
                        return false;
                    }
                }

                if (!Plugin.Vip.Value.Contains(Plugin.steamId) && areaInstances >= 6500 && isInsideArea)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem construir em locais com mais de 6500 instâncias", 0, null);
                    return false;
                }

                if (Plugin.Vip.Value.Contains(Plugin.steamId) && areaInstances >= 10000 && isInsideArea)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "O limite é de 10000 instâncias.", 0, null);
                    return false;
                }

                if (!isTotem) return true;

                bool isInNotAllowedArea = false;

                List<PrivateArea> privateAreaList = new List<PrivateArea>();

                foreach (PrivateArea area in PrivateArea.m_allAreas)
                {
                    bool isInsideAreaa = Vector3.Distance(__instance.transform.position, area.transform.position) <= (area.m_radius * 2.5);
                    if (isInsideAreaa) privateAreaList.Add(area);
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

                if (!Player.m_localPlayer.m_knownTexts.ContainsKey("dead_PlayerWards"))
                {
                    Player.m_localPlayer.m_knownTexts.Add("dead_PlayerWards", "1");
                }
                else
                {
                    int wards = Convert.ToInt32(Player.m_localPlayer.m_knownTexts["dead_PlayerWards"]);
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

                    wards++;
                    Player.m_localPlayer.m_knownTexts["dead_PlayerWards"] = wards.ToString();
                }

                return true;
            }

            private static void Postfix(Piece piece, Player __instance)
            {
                if (!piece.gameObject.name.Contains("guard_stone")) return;

                Minimap.instance.AddPin(__instance.transform.position, Minimap.PinType.Boss, "WARD", true, false);
            }
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
                    text.Append("\n[<color=yellow><b>" + fuelWard.ToString() + "</b></color>] Fuel: " + Math.Round(fireplace.m_nview.GetZDO().GetFloat("fuel"), 2) + "/" + fireplace.m_maxFuel);
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

            public static void Interact(Player thes, GameObject go, PrivateArea privateArea)
            {
                thes.m_lastHoverInteractTime = Time.time;

                privateArea.m_nview.InvokeRPC("ToggleEnabled", new object[]
                {
                                privateArea.m_piece.GetCreator()
                });

                Vector3 vector = go.transform.position - thes.transform.position;
                vector.y = 0f;
                vector.Normalize();
                thes.transform.rotation = Quaternion.LookRotation(vector);
                thes.m_zanim.SetTrigger("interact");
            }
        }
    }
}