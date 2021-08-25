using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Deadheim.EnhancedWards
{
    class Ward
    {
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        public static class RPC_Damage
        {
            private static bool Prefix(WearNTear __instance, ref HitData hit, ZNetView ___m_nview)
            {
                if (PrivateArea.CheckInPrivateArea(__instance.transform.position) && ___m_nview != null)
                {
                    hit.m_damage.m_blunt *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_slash *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_pierce *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_chop *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_pickaxe *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_fire *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_frost *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_lightning *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_poison *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                    hit.m_damage.m_spirit *= (float)(1.0 - Plugin.wardReductionDamage / 100.0);
                }

                return true;
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
                if (thes.InAttack() || thes.InDodge())
                {
                    return;
                }

                if (hold && Time.time - thes.m_lastHoverInteractTime < 0.2f)
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
