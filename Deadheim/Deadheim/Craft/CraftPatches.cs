using BepInEx;
using HarmonyLib;
using Deadheim.agesystem;
using System.Collections.Generic;

namespace Deadheim.Craft
{
    public class CraftPatches
    {
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private class FasterCrafting
        {
            private static void Prefix(ref InventoryGui __instance) => __instance.m_craftDuration = 0.25f;
        }

        [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")]
        private class UpdateKnownRecipesList
        {
            private static void Postfix()
            {
                AgeSystem.RemoveDisabledRecipes();
            }
        }

        [HarmonyPatch(typeof(Player), "GetBuildPieces")]
        private class GetBuildPieces
        {
            private static List<Piece> Postfix(List<Piece> __result)
            {
                return AgeSystem.RemoveDisabledItems(__result);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
        public static class ObjectDB_CopyOtherDB_Patch
        {
            public static void Postfix()
            {
                AgeSystem.AddPortal();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        public static class ObjectDB_Awake_Patch
        {
            public static void Postfix()
            {
                AgeSystem.AddPortal();
            }
        }


        [HarmonyPatch(typeof(Player), "PlacePiece")]
        public static class NoBuild_Patch
        {
            private static bool Prefix(Piece piece, Player __instance)
            {
                int areaInstances = (ZNetScene.m_instance.m_instances.Count);
                if (PrivateArea.CheckInPrivateArea(__instance.transform.position) || piece.gameObject.name == "guard_stone")
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

                return true;
            }
        }
    }
}

