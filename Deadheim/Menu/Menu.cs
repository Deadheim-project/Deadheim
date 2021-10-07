using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    public static class DeadheimMenu
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), "Update")]
        private static void Update(GameObject ___m_startGamePanel, Button ___m_worldStart)
        {
            if (!___m_startGamePanel.activeInHierarchy)
                return;
            GameObject gameObject = GameObject.Find("Start");
            if (gameObject != null)
            {
                Text componentInChildren = gameObject.GetComponentInChildren<Text>();
                if (componentInChildren != null)
                    componentInChildren.text = "Desativado no Deadheim";
            }
            ___m_worldStart.interactable = false;
        }
    }
}