using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    [HarmonyPatch]
    public static class StartButton
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), "Update")]
        private static void FejdStartup__Update(GameObject ___m_startGamePanel, Button ___m_worldStart)
        {
            if (!___m_startGamePanel.activeInHierarchy)
                return;
            GameObject gameObject = GameObject.Find("Start");
            if ((Object)gameObject != (Object)null)
            {
                Text componentInChildren = gameObject.GetComponentInChildren<Text>();
                if ((Object)componentInChildren != (Object)null)
                    componentInChildren.text = "Desativado no Deadheim";
            }
            ___m_worldStart.interactable = false;
        }
    }
}