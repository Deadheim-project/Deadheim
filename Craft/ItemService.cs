using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    internal class ItemService
    {

        public static void ModifyItemsCost()
        {
            try
            {
                GameObject portalwood = PrefabManager.Instance.GetPrefab("portal_wood");
                var portalwoodPiece = portalwood.GetComponent<Piece>();
                portalwoodPiece.m_resources[0].m_amount = 1500;
                portalwoodPiece.m_resources[1].m_amount = 1;
                portalwoodPiece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("PortalToken").GetComponent<ItemDrop>();
                portalwoodPiece.m_resources[2].m_amount = 225;


                GameObject stonePortal = PrefabManager.Instance.GetPrefab("Stone_Portal");
                var stonePortalPiece = stonePortal.GetComponent<Piece>();
                stonePortalPiece.m_resources[0].m_amount = 1500;
                stonePortalPiece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
                stonePortalPiece.m_resources[1].m_amount = 1;
                stonePortalPiece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("PortalToken").GetComponent<ItemDrop>();
                stonePortalPiece.m_resources[2].m_amount = 225;
                stonePortalPiece.m_resources[2].m_resItem = PrefabManager.Instance.GetPrefab("SurtlingCore").GetComponent<ItemDrop>();

                GameObject cartographyTable = PrefabManager.Instance.GetPrefab("piece_cartographytable");
                var blueberries = ObjectDB.instance.GetItemPrefab("Blueberries");
                var linen = ObjectDB.instance.GetItemPrefab("LinenThread");
                var blackmetal = ObjectDB.instance.GetItemPrefab("BlackMetal");
                cartographyTable.GetComponent<Piece>().m_resources[0].m_amount = 800;
                cartographyTable.GetComponent<Piece>().m_resources[1].m_amount = 1500;
                cartographyTable.GetComponent<Piece>().m_resources[2].m_amount = 500;
                cartographyTable.GetComponent<Piece>().m_resources[2].m_resItem = blackmetal.GetComponent<ItemDrop>();
                cartographyTable.GetComponent<Piece>().m_resources[3].m_amount = 800;
                cartographyTable.GetComponent<Piece>().m_resources[3].m_resItem = linen.GetComponent<ItemDrop>();
                cartographyTable.GetComponent<Piece>().m_resources[4].m_amount = 800;
                cartographyTable.GetComponent<Piece>().m_resources[4].m_resItem = blueberries.GetComponent<ItemDrop>();
            }
            catch { }
        }

        public static void SetWardFirePlace()
        {
            GameObject ward = PrefabManager.Instance.GetPrefab("guard_stone");
            Fireplace fireplace = ward.AddComponent<Fireplace>();
            fireplace.m_fuelItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
        }

        public static void SetBoatsToDrop()
        {
            GameObject boat1 = PrefabManager.Instance.GetPrefab("CargoShip");
            GameObject boat2 = PrefabManager.Instance.GetPrefab("BigCargoShip");
            GameObject boat3 = PrefabManager.Instance.GetPrefab("LittleBoat");

            foreach (var x in boat1.GetComponent<Piece>().m_resources)
            {
                x.m_recover = true;
            }

            foreach (var x in boat2.GetComponent<Piece>().m_resources)
            {
                x.m_recover = true;
            }


            foreach (var x in boat3.GetComponent<Piece>().m_resources)
            {
                x.m_recover = true;
            }
        }

        public static void WolvesTameable()
        {
            if (Plugin.WolvesAreTameable.Value) return;

            GameObject prefab = PrefabManager.Instance.GetPrefab("Wolf");
            if (!prefab) return;

            Tameable tameable = prefab.GetComponent<Tameable>();
            Procreation procreation = prefab.GetComponent<Procreation>();
            UnityEngine.Object.Destroy(tameable);
            UnityEngine.Object.Destroy(procreation);
        }

        public static void LoxTameable()
        {
            if (Plugin.LoxTameable.Value) return;

            GameObject prefab = PrefabManager.Instance.GetPrefab("Lox");
            if (!prefab) return;

            Tameable tameable = prefab.GetComponent<Tameable>();
            Procreation procreation = prefab.GetComponent<Procreation>();
            UnityEngine.Object.Destroy(tameable);
            UnityEngine.Object.Destroy(procreation);
        }

        public static void ModififyItemMaxLevel()
        {
            foreach (string item in Plugin.ItemToSetMaxLevel.Value.Split(','))
            {
                string[] array = item.Split(':');
                GameObject prefab = PrefabManager.Instance.GetPrefab(array[0]);

                if (prefab is null) continue;

                ItemDrop itemdrop = prefab.GetComponent<ItemDrop>();

                if (itemdrop is null) continue;

                itemdrop.m_itemData.m_shared.m_maxQuality = Convert.ToInt32(array[1]);
            }
        }

        private static bool recipesAlreadyModified = false;

        public static void ModifyRecipesCost()
        {
            if (recipesAlreadyModified) return;
            foreach (Recipe recipe in Resources.FindObjectsOfTypeAll(typeof(Recipe)))
            {
                if (recipe.name == "Recipe_Bronze") continue;
                if (recipe.name == "Recipe_Bronze5") continue;

                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (recipe.m_item is null) continue;
                    if (recipe.m_item.m_itemData is null) continue;
                    if (recipe.m_item.m_itemData.m_shared is null) continue;

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeArrowCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeArrowCostMultiplier.Value;
                        continue;
                    }

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeTwoHandedCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeTwoHandedCostMultiplier.Value;
                        continue;
                    }

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeShieldCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeShieldCostMultiplier.Value;
                        continue;
                    }

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeBowCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeBowCostMultiplier.Value;
                        continue;
                    }

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeOneHandedCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeOneHandedCostMultiplier.Value;
                        continue;
                    }

                    if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                    {
                        requirement.m_amount = requirement.m_amount * Plugin.RecipeConsumableCostMultiplier.Value;
                        requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeConsumableCostMultiplier.Value;
                        continue;
                    }

                    requirement.m_amount = requirement.m_amount * Plugin.RecipeCostMultiplier.Value;
                    requirement.m_amountPerLevel = requirement.m_amountPerLevel * Plugin.RecipeCostMultiplier.Value;
                }
            }
            recipesAlreadyModified = true;
        }

        public static void StubNoLife()
        {
            List<GameObject> stubList = new();
            stubList.Add(PrefabManager.Instance.GetPrefab("Pinetree_01_Stub"));
            stubList.Add(PrefabManager.Instance.GetPrefab("SwampTree1_Stub"));
            stubList.Add(PrefabManager.Instance.GetPrefab("BirchStub"));
            stubList.Add(PrefabManager.Instance.GetPrefab("FirTree_Stub"));
            stubList.Add(PrefabManager.Instance.GetPrefab("OakStub"));
            stubList.Add(PrefabManager.Instance.GetPrefab("Beech_Stub"));

            foreach (GameObject stub in stubList)
            {
                Destructible destructible = stub.GetComponent<Destructible>();
                destructible.m_health = 1;
            }
        }
    }
}
