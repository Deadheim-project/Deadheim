using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    public static class DeadheimMenu
    {
        public static Sprite sprite;

        [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
        public static class SetupGui
        {
            private static void Postfix(ref FejdStartup __instance)
            {
                GameObject logo = GameObject.Find("LOGO");
                logo.GetComponent<Image>().sprite = sprite;
            }
        } 

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), "Update")]
        private static void FejdStartup__Update(GameObject ___m_startGamePanel, Button ___m_worldStart)
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

        public static void Load()
        {
            Stream logoStream = LoadEmbeddedAsset("Assets.logo.png");
            Texture2D logoTexture = LoadPng(logoStream);
            sprite = Sprite.Create(logoTexture, new Rect(0, 0, logoTexture.width, logoTexture.height), new Vector2(0.5f, 0.5f));
            logoStream.Dispose();
        }

        public static Stream LoadEmbeddedAsset(string assetPath)
        {
            Assembly objAsm = Assembly.GetExecutingAssembly();
            string[] embeddedResources = objAsm.GetManifestResourceNames();

            if (objAsm.GetManifestResourceInfo(objAsm.GetName().Name + "." + assetPath) != null)
            {
                return objAsm.GetManifestResourceStream(objAsm.GetName().Name + "." + assetPath);
            }

            return null;
        }

        public static Texture2D LoadPng(Stream fileStream)
        {
            Texture2D texture = null;

            if (fileStream != null)
            {
                using (var memoryStream = new MemoryStream())
                {
                    fileStream.CopyTo(memoryStream);

                    texture = new Texture2D(2, 2);
                    texture.LoadImage(memoryStream.ToArray());
                }
            }

            return texture;
        }
    }
}