using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    internal class ItemService
    {  

        public static List<string> GetDisabledCrafts()
        {
            if (Plugin.Age.Value == "bronze") return Plugin.Iron.Value.Split(',').Concat(Plugin.Silver.Value.Split(',')).Concat(Plugin.Blackmetal.Value.Split(',')).Concat(Plugin.Fire.Value.Split(',')).ToList();
            if (Plugin.Age.Value == "iron") return (Plugin.Silver.Value.Split(',')).Concat(Plugin.Blackmetal.Value.Split(',')).Concat(Plugin.Fire.Value.Split(',')).ToList();
            if (Plugin.Age.Value == "silver") return (Plugin.Blackmetal.Value.Split(',')).Concat(Plugin.Fire.Value.Split(',')).ToList();
            if (Plugin.Age.Value == "blackmetal") return (Plugin.Fire.Value.Split(',')).ToList();
            if (Plugin.Age.Value == "fire") return new List<string>();

            return Plugin.Bronze.Value.Split(',').Concat(Plugin.Iron.Value.Split(',')).Concat(Plugin.Silver.Value.Split(',')).Concat(Plugin.Blackmetal.Value.Split(',')).Concat(Plugin.Fire.Value.Split(',')).ToList();
        }

        public static bool IsDisabled(string recipeName)
        {
            List<string> disabledCrafts = GetDisabledCrafts();

            foreach (string x in disabledCrafts)
            {
                if (string.IsNullOrWhiteSpace(x)) continue;
                if (recipeName.ToLower().Contains(x.ToLower())) return true;
            }
            return false;
        }

        public static List<Piece> RemoveDisabledItems(List<Piece> pieces)
        {
            foreach (Piece piece in pieces)
            {
                if (IsDisabled(piece.name))
                {
                    foreach (Piece.Requirement requirement in piece.m_resources.ToList())
                    {
                        requirement.m_amount = 9999;
                        requirement.m_recover = false;
                    }
                }
            }

            return pieces;
        }

        public static void AddPortal()
        {
            try
            {
                foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
                {
                    if (gameObject.name == "portal_wood")
                    {
                        var piece = gameObject.GetComponent<Piece>();
                        piece.m_resources[0].m_amount = 1500;
                        piece.m_resources[1].m_amount = 1;
                        piece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("PortalToken").GetComponent<ItemDrop>();
                        piece.m_resources[2].m_amount = 225;
                    }

                    if (gameObject.name == "piece_cartographytable")
                    {
                        var blueberries = ObjectDB.instance.GetItemPrefab("Blueberries");
                        var linen = ObjectDB.instance.GetItemPrefab("LinenThread");
                        var blackmetal = ObjectDB.instance.GetItemPrefab("BlackMetal");
                        gameObject.GetComponent<Piece>().m_resources[0].m_amount = 800;
                        gameObject.GetComponent<Piece>().m_resources[1].m_amount = 1500;
                        gameObject.GetComponent<Piece>().m_resources[2].m_amount = 500;
                        gameObject.GetComponent<Piece>().m_resources[2].m_resItem = blackmetal.GetComponent<ItemDrop>();
                        gameObject.GetComponent<Piece>().m_resources[3].m_amount = 800;
                        gameObject.GetComponent<Piece>().m_resources[3].m_resItem = linen.GetComponent<ItemDrop>();
                        gameObject.GetComponent<Piece>().m_resources[4].m_amount = 800;
                        gameObject.GetComponent<Piece>().m_resources[4].m_resItem = blueberries.GetComponent<ItemDrop>();
                    }
                }
            }
            catch { }
        }

        public static void RemoveDisabledRecipes()
        {
            var recipes = ObjectDB.instance.m_recipes;

            if (!recipes.Any()) return;

            foreach (Recipe recipe in recipes)
            {
                if (IsDisabled(recipe.name))
                {
                    foreach (Piece.Requirement requirement in recipe.m_resources.ToList())
                    {
                        requirement.m_amount = 9999;
                        requirement.m_recover = false;
                    }
                }
            }
        }
    }
}
