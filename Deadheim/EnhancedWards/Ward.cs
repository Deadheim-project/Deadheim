using HarmonyLib;
using System.Collections.Generic;
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
                if (PrivateArea.CheckInPrivateArea(__instance.transform.position) && ___m_nview != null)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                if (Plugin.admin) return true;

                if (piece.gameObject.name.ToLower().Contains("mofarm"))
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Somente disponível para administradores", 0, null);

                    return false;
                }

                int areaInstances = (ZNetScene.m_instance.m_instances.Count);

                bool isInsideArea = false;
                bool isTotem = piece.gameObject.name == "guard_stone";

                foreach (PrivateArea area in PrivateArea.m_allAreas)
                {
                    isInsideArea = Vector3.Distance(__instance.transform.position, area.transform.position) <= (area.m_radius * 2.5);
                    if (isInsideArea) break;
                }

                if (isTotem)
                {
                    if (!Plugin.playerIsVip && areaInstances >= 5000)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem construir em locais com mais de 5000 instâncias", 0, null);
                        return false;
                    }

                    if (Plugin.playerIsVip && areaInstances >= 7000)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "O limite é de 7000 instâncias.", 0, null);
                        return false;
                    }
                }

                if (!Plugin.playerIsVip && areaInstances >= 5000 && isInsideArea)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem construir em locais com mais de 5000 instâncias", 0, null);
                    return false;
                }

                if (Plugin.playerIsVip && areaInstances >= 7000 && isInsideArea)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "O limite é de 7000 instâncias.", 0, null);
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
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        public static class Update
        {
            private static void Postfix(ref Player __instance)
            {
                StringBuilder text = new StringBuilder(256);

                KeyCode wardOptionalKey = KeyCode.K;
                if (__instance.m_hovering)
                {
                    Interactable componentInParent = __instance.m_hovering.GetComponentInParent<Interactable>();
                    if (componentInParent != null)
                    {
                        __instance.m_lastHoverInteractTime = Time.time;

                        if (componentInParent is PrivateArea)
                        {
                            PrivateArea pa = (PrivateArea)componentInParent;

                            if (pa.IsPermitted(__instance.GetPlayerID()))
                            {
                                text.Append("\n[<color=yellow><b>" + wardOptionalKey.ToString() + "</b></color>]");
                                pa.m_name = text.ToString();
                                pa.AddUserList(text);
                            }
                        }
                    }
                }

                if (Input.GetKeyDown(wardOptionalKey))
                {
                    if (__instance.m_hovering)
                    {
                        Interact(__instance, __instance.m_hovering, false);
                    }
                }
            }

            public static void Interact(Player thes, GameObject go, bool hold)
            {
                if (hold && Time.time - thes.m_lastHoverInteractTime < 1f)
                {
                    return;
                }

                Interactable componentInParent = go.GetComponentInParent<Interactable>();
                if (componentInParent != null)
                {
                    thes.m_lastHoverInteractTime = Time.time;

                    if (componentInParent is PrivateArea)
                    {
                        PrivateArea pa = (PrivateArea)componentInParent;
                        if (pa.IsPermitted(thes.GetPlayerID()))
                        {
                            pa.m_nview.InvokeRPC("ToggleEnabled", new object[]
                            {
                                pa.m_piece.GetCreator()
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
        }
    }
}

