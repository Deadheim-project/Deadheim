using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System;
using Deadheim;

namespace BetterWards.PatchClasses
{

    public static class ChatPatchAwake
    {

        [HarmonyPatch]
        public static class Chat_Patch_Commands
        {
            [HarmonyPatch(typeof(Chat), "InputText")]
            [HarmonyPostfix]
            private static void Postfix(Chat __instance)
            {
                if (!BetterWardsPlugin.valid_server || !BetterWardsPlugin.admin)
                    return;
                ZPackage zpackage = new ZPackage();
                string text = __instance.m_input.text;
                string[] strArray = text.Split(' ');

                Player localPlayer = Player.m_localPlayer;
                Vector3 pos = localPlayer.transform.position;
                PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
                if (BetterWards.BetterWardsPlugin.wardRange.Value > 0 && (text.ToLower() == "/permit" || text.ToLower() == "/unpermit" || text.ToLower() == "/disable" || text.ToLower() == "/flash" || text.ToLower() == "/enable" || text.ToLower() == "sync" || text.ToLower() == "/destroy" || text.ToLower() == "/pos"))
                {
                    if (text == "/pos")
                    {
                        Chat.instance.AddString($"Position = {pos}");
                    }
                    if (text.ToLower() == "sync")
                    {
                        ZPackage pkg = new ZPackage();
                        pkg.Write(Plugin.steamId);
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RequestSync", (object)new ZPackage());
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RequestAdminSync", pkg);
                        return;
                    }

                    List<GameObject> gameObjectList = new List<GameObject>();
                    if (text.ToLower() == "/permit")
                    {
                        if (!BetterWards.BetterWardsPlugin.wardEnabled.Value)
                        {
                            __instance.AddString("<color=red>You're not allowed to do that!</color>");

                            return;
                        }
                        foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            if ((bool)componentInParent && (double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value)
                            {
                                componentInParent.AddPermitted(playerProfile.m_playerID, playerProfile.m_playerName);
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    __instance.AddString(string.Format("<color=green>Ward in position: {0} now permitted for {1}</color>", (object)componentInParent.transform.position, (object)playerProfile.m_playerName));
                                }
                            }
                        }
                    }

                    if (text.ToLower() == "/unpermit")
                    {
                        if (!BetterWards.BetterWardsPlugin.wardEnabled.Value)
                        {
                            __instance.AddString("<color=red>You're not allowed to do that!</color>");
                            return;
                        }
                        foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            if ((bool)componentInParent && (double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value)
                            {
                                List<KeyValuePair<long, string>> permittedPlayers = componentInParent.GetPermittedPlayers();
                                if (permittedPlayers.RemoveAll((Predicate<KeyValuePair<long, string>>)(x => x.Key == Player.m_localPlayer.GetPlayerID())) <= 0)
                                    return;
                                componentInParent.SetPermittedPlayers(permittedPlayers);
                                componentInParent.m_removedPermittedEffect.Create(((Component)componentInParent).transform.position, ((Component)componentInParent).transform.rotation);
                                //componentInParent.m_activateEffect.Create(((Component)componentInParent).transform.position, ((Component)componentInParent).transform.rotation, null, 4);
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    __instance.AddString(string.Format("<color=yellow>Ward in position: {0} removed permitted for {1}</color>", (object)componentInParent.transform.position, (object)playerProfile.m_playerName));
                                }
                            }
                        }
                    }
                    if (text.ToLower() == "/disable")
                    {
                        foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            bool flag = true;
                            if (!BetterWards.BetterWardsPlugin.wardEnabled.Value && (bool)componentInParent)
                                flag = componentInParent.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || componentInParent.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());
                            if (((!(bool)componentInParent ? 0 : ((double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value ? 1 : 0)) & (flag ? 1 : 0)) != 0)
                            {
                                componentInParent.SetEnabled(false);
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    __instance.AddString(string.Format("<color=yellow>Ward in position: {0} now disabled</color>", (object)componentInParent.transform.position));
                                }
                            }
                        }
                    }

                    if (text.ToLower() == "/destroy")
                    {
                        foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            bool flag = true;
                            if (!BetterWards.BetterWardsPlugin.wardEnabled.Value && (bool)componentInParent)
                                flag = componentInParent.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || componentInParent.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());
                            if (((!(bool)componentInParent ? 0 : ((double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value ? 1 : 0)) & (flag ? 1 : 0)) != 0)
                            {

                                componentInParent.m_piece.SetCreator(Game.instance.GetPlayerProfile().GetPlayerID()); // must make owner of object in order for this to update to all players.
                                PrivateArea.m_allAreas.Remove(componentInParent);
                                componentInParent.m_nview.Destroy();
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    __instance.AddString(string.Format("<color=red>Ward in position: {0} now destroyed</color>", (object)componentInParent.transform.position));
                                }
                            }
                        }
                    }

                    if (text.ToLower() == "/flash")
                    {
                        foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                        {
                            PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                            bool flag = true;
                            if (!BetterWards.BetterWardsPlugin.wardEnabled.Value && (bool)componentInParent)
                                flag = componentInParent.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || componentInParent.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());
                            if (((!(bool)componentInParent ? 0 : ((double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value ? 1 : 0)) & (flag ? 1 : 0)) != 0)
                            {
                                componentInParent.FlashShield(true);
                                if (!gameObjectList.Contains(componentInParent.gameObject))
                                {
                                    gameObjectList.Add(componentInParent.gameObject);
                                    __instance.AddString(string.Format("<color=yellow>Flashing Shield for ward in position: {0}</color>", (object)componentInParent.transform.position));
                                }
                            }
                        }
                    }

                    if (!(text.ToLower() == "/enable"))
                        return;
                    foreach (Component component in Physics.OverlapSphere(localPlayer.transform.position, (float)BetterWards.BetterWardsPlugin.wardRange.Value))
                    {
                        PrivateArea componentInParent = component.GetComponentInParent<PrivateArea>();
                        bool flag = true;
                        if (!BetterWards.BetterWardsPlugin.wardEnabled.Value && (bool)componentInParent)
                            flag = componentInParent.m_piece.GetCreator() == Game.instance.GetPlayerProfile().GetPlayerID() || componentInParent.IsPermitted(Game.instance.GetPlayerProfile().GetPlayerID());
                        if (((!(bool)componentInParent ? 0 : ((double)Vector3.Distance(localPlayer.transform.position, componentInParent.transform.position) <= (double)BetterWards.BetterWardsPlugin.wardRange.Value ? 1 : 0)) & (flag ? 1 : 0)) != 0)
                        {
                            componentInParent.SetEnabled(true);
                            if (!gameObjectList.Contains(componentInParent.gameObject))
                            {
                                gameObjectList.Add(componentInParent.gameObject);
                                __instance.AddString(string.Format("<color=green>Ward in position: {0} now enabled</color>", (object)componentInParent.transform.position));
                            }
                        }
                    }
                    if (gameObjectList.Count == 0)
                        __instance.AddString("<color=red>No wards near</color>");
                }
            }
        }
    }
}