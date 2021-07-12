using HarmonyLib;
using System;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using System.Reflection;
using BetterWards;
using ValheimRpBr.Discord;
using ValheimRpBr;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    internal class WardPrivateArea
    {
        public static bool ClientInWardArea;
        /// Alter ward range

        [HarmonyPatch]
        public static class ModifyWardRange
        {
            /// When inside ward owned or permitted

            [HarmonyPatch]
            public static class PrivateAreaPatchAwake
            {
                [HarmonyPatch(typeof(Player), "Update")]
                [HarmonyPostfix]
                private static void Postfix(ref Player __instance)
                {
                    if (__instance.InPlaceMode() || !(bool)(UnityEngine.Object)Player.m_localPlayer)
                        return;
                    List<string> stringList = new List<string>();
                    if (PrivateArea.CheckInPrivateArea(Player.m_localPlayer.transform.position) && !WardPrivateArea.ClientInWardArea && BetterWardsPlugin.wardEnabled.Value)
                    {
                        List<GameObject> gameObjectList = new List<GameObject>();
                        double wardDetection = BetterWardsPlugin.wardRange.Value;

                        foreach (Component component in Physics.OverlapSphere(Player.m_localPlayer.transform.position, (float)100))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            bool flag = true;
                            if (!BetterWardsPlugin.wardEnabled.Value && (bool)componentInParent)
                                flag = componentInParent.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || componentInParent.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());

                            if (((!(bool)componentInParent ? 0 : ((double)Vector3.Distance(Player.m_localPlayer.transform.position, componentInParent.transform.position) <= (double)wardDetection ? 1 : 0)) & (flag ? 1 : 0)) != 0)
                            {
                                if (componentInParent.IsPermitted(Player.m_localPlayer.GetPlayerID()) == false && componentInParent.GetCreatorName() != Player.m_localPlayer.GetPlayerName())
                                {
                                    if (BetterWardsPlugin.admin && BetterWardsPlugin.adminAutoPerm.Value)
                                    {
                                        componentInParent.AddPermitted(Player.m_localPlayer.GetPlayerID(), Player.m_localPlayer.GetPlayerName());                                        
                                        stringList.Add(string.Format("Ward in position: {0} now permitted for {1}", (object)componentInParent.transform.position, (object)Player.m_localPlayer.GetPlayerName()));
                                        foreach (string str in stringList)
                                            Chat.instance.AddString("[Better Wards] " + str);

                                        String discordMessage = "**" + Player.m_localPlayer.GetPlayerName() + "** steamId: **" + Plugin.steamId + "** is now permitted the ward at: **" + Player.m_localPlayer.transform.position + "** ward owner: **" + componentInParent.GetCreatorName() + "**";
                                        Discord.DiscordBot.postBetterWardsMessage(discordMessage);
                                    }
                                }
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    //string str = componentInParent.GetCreatorName().Substring(0, Math.Min(2, componentInParent.GetCreatorName().Length));
                                    if (BetterWardsPlugin.wardNotify.Value)
                                    {
                                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, string.Format("Entered {0}'s ward", (object)componentInParent.GetCreatorName(), (object)0, (object)null), 0, (Sprite)null);
                                        String discordMessage = "**" + Player.m_localPlayer.GetPlayerName() + "** steamId: **" + Plugin.steamId + "** entered the ward at: **" + Player.m_localPlayer.transform.position + "** ward owner: **" + componentInParent.GetCreatorName() + "**";
                                        Discord.DiscordBot.postBetterWardsMessage(discordMessage);
                                    }
                                }

                            }

                        }
                        WardPrivateArea.ClientInWardArea = true;
                    }
                    else
                    {
                        if (PrivateArea.CheckInPrivateArea(Player.m_localPlayer.transform.position) || !WardPrivateArea.ClientInWardArea)
                            return;
                        if (BetterWardsPlugin.wardNotify.Value && BetterWardsPlugin.wardEnabled.Value)
                        {
                            
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center, string.Format("{0} you have left the ward", (object)Player.m_localPlayer.GetPlayerName()), 0, (Sprite)null);
                            String discordMessage = "**" + Player.m_localPlayer.GetPlayerName() + "** steamId: **" + Plugin.steamId + "** left the ward at: **" + Player.m_localPlayer.transform.position + "**";
                            Discord.DiscordBot.postBetterWardsMessage(discordMessage);
                        }
                        WardPrivateArea.ClientInWardArea = false;
                    }
                }



                /// Toggle Ward if Owner or Permitted

                [HarmonyPatch]
                public static class ToggleWardPermitted
                {
                    [HarmonyPatch(typeof(Player), "Update")]
                    [HarmonyPostfix]
                    private static void Postfix(ref Player __instance)
                    {
                        StringBuilder text = new StringBuilder(256);

                        if (!BetterWardsPlugin.wardEnabled.Value) return;

                        KeyCode altWardPress = BetterWardsPlugin.wardHotKey.Value;
                        if (__instance.m_hovering)
                        {

                            Interactable componentInParent = __instance.m_hovering.GetComponentInParent<Interactable>();
                            if (componentInParent != null)
                            {
                                __instance.m_lastHoverInteractTime = Time.time;

                                if (componentInParent is PrivateArea)
                                {
                                    //cast it
                                    PrivateArea pa = (PrivateArea)componentInParent;
                                    if (Input.GetKeyDown(KeyCode.DownArrow) && (BetterWardsPlugin.admin))
                                    {
                                        if (pa.IsEnabled())
                                            pa.SetEnabled(false);
                                    }
                                    if (Input.GetKeyDown(KeyCode.UpArrow) && (BetterWardsPlugin.admin))
                                    {
                                        if (!pa.IsEnabled())
                                            pa.SetEnabled(true);
                                    }
                                    //show message when ward is hovered
                                    if (pa.IsPermitted(__instance.GetPlayerID()))
                                    {
                                        text.Append("\n[<color=yellow><b>" + altWardPress.ToString() + "</b></color>] Better Ward");
                                        pa.m_name = text.ToString();
                                        pa.AddUserList(text);
                                    }
                                    else
                                    {
                                        text.Clear();
                                        pa.m_name = "Better Ward";
                                    }
                                }
                            }
                        }

                        if (Input.GetKeyDown(altWardPress))
                        {
                            if (__instance.m_hovering)
                            {
                                WardInteract(__instance, __instance.m_hovering, false);
                            }
                        }
                    }


                    // Ward interact method
                    public static void WardInteract(Player thes, GameObject go, bool hold)
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
                                //cast it
                                PrivateArea pa = (PrivateArea)componentInParent;
                                //new code for interact
                                if (pa.IsPermitted(thes.GetPlayerID()))
                                {
                                    pa.m_nview.InvokeRPC("ToggleEnabled", new object[]
                                    {
                            //override this to creator id so it thinks we are allowed to do this
                                pa.m_piece.GetCreator()
                                    });

                                    //copied code for player animation
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

            // Ward Range Method
            public static void WardRangeEffect(MonoBehaviour parent, EffectArea.Type includedTypes, float newRadius)
            {
                if (parent != null)
                {
                    EffectArea effectArea = parent.GetComponentInChildren<EffectArea>();
                    if (effectArea != null)
                    {
                        if ((effectArea.m_type & includedTypes) != 0)
                        {
                            SphereCollider collision = effectArea.GetComponent<SphereCollider>();
                            if (collision != null)
                            {
                                collision.radius = newRadius;
                                //if (BetterWardsPlugin.wardNotify.Value)
                                //{
                                //    effectArea.m_type = EffectArea.Type.NoMonsters;
                                //}
                            }
                        }
                    }
                }
            }

            // Show area marker patch. Separated this out due to issues of it not showing when put in player update patch above.
            // Also gives more control
            [HarmonyPatch]
            public static class ShowAreaMarker
            {
                [HarmonyPatch(typeof(Player), "Update")]
                [HarmonyPostfix]
                private static void Postfix(ref Player __instance)
                {
                    if (__instance.InPlaceMode() || !(bool)(UnityEngine.Object)Player.m_localPlayer)
                        return;
                    if (PrivateArea.CheckInPrivateArea(Player.m_localPlayer.transform.position) && Game.instance.isActiveAndEnabled && BetterWardsPlugin.wardEnabled.Value)
                    {

                        foreach (PrivateArea allArea in PrivateArea.m_allAreas)
                        {
                            if (allArea.IsEnabled() && allArea.IsInside(Player.m_localPlayer.transform.position, 0.0f) && BetterWardsPlugin.showMarker.Value)
                            {
                                allArea.ShowAreaMarker();
                            }
                        }
                    }
                }


            }
        }
    }
}