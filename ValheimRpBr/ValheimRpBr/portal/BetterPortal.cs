
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BetterPortal
{
    [BepInPlugin("com.mod.portal", "Better portal", "1.0.0")]
    [BepInProcess("valheim.exe")]

    public class BetterPortal : BaseUnityPlugin
    {

        public static Harmony harmony = new Harmony("mod.portal");

        void Awake()
        {
            harmony.PatchAll();
        }


        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        public static class ObjectDB_CopyOtherDB_Patch
        {
            public static void Postfix()
            {
                AddPortal();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        public static class ObjectDB_Awake_Patch
        {
            public static void Postfix()
            {
                AddPortal();
            }
        }

        private static void AddPortal()
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
            {
                if (gameObject.name == "portal_wood")
                {
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[0]).m_amount = 150;
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[1]).m_amount = 300;
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[2]).m_amount = 50;
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[0]).m_recover = true;
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[1]).m_recover = true;
                    ((Piece.Requirement)gameObject.GetComponent<Piece>().m_resources[2]).m_recover = true;
                }
            }
        }
    }
}