using System;
using HarmonyLib;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    public static class AutomaticDoorClose
    {
        private static Dictionary<int, Coroutine> coroutineClose = new Dictionary<int, Coroutine>();
        [HarmonyPatch(typeof(Door), "Interact")]
        [HarmonyPostfix]
        private static void Postfix(ref Door __instance, ZNetView ___m_nview)
        {
            if (!BetterWardsPlugin.wardEnabled.Value || !PrivateArea.CheckInPrivateArea(Player.m_localPlayer.transform.position))
                return;
            if (BetterWardsPlugin.autoClose.Value && BetterWardsPlugin.wardEnabled.Value)
            {
                if (coroutineClose.ContainsKey(___m_nview.GetHashCode()))
                    ___m_nview.StopCoroutine(coroutineClose[___m_nview.GetHashCode()]);
                Coroutine coroutine = ___m_nview.StartCoroutine(AutomaticDoorClose.AutoClose(__instance, ___m_nview));
                coroutineClose[___m_nview.GetHashCode()] = coroutine;
            }
        }

        public static IEnumerator AutoClose(Door __instance, ZNetView ___m_nview)
        {
            coroutineClose.Remove(___m_nview.GetHashCode());
            yield return (object)new WaitForSeconds(5);
            ___m_nview.GetZDO().Set("state", 0);
            coroutineClose.Remove(___m_nview.GetHashCode());
        }
    }

}
