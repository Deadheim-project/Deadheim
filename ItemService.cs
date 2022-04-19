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
                stonePortalPiece.m_resources[0].m_recover = true;
                stonePortalPiece.m_resources[1].m_recover = true;
                stonePortalPiece.m_resources[2].m_recover = true;                
                stonePortalPiece.m_resources[0].m_resItem = PrefabManager.Instance.GetPrefab("GreydwarfEye").GetComponent<ItemDrop>();
                stonePortalPiece.m_resources[1].m_amount = 1;
                stonePortalPiece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("PortalToken").GetComponent<ItemDrop>();
                stonePortalPiece.m_resources[2].m_amount = 225;
                stonePortalPiece.m_resources[2].m_resItem = PrefabManager.Instance.GetPrefab("SurtlingCore").GetComponent<ItemDrop>();

                GameObject cartographyTable = PrefabManager.Instance.GetPrefab("piece_cartographytable");
    
                cartographyTable.GetComponent<Piece>().m_resources[0].m_amount = 800;
                cartographyTable.GetComponent<Piece>().m_resources[1].m_amount = 1500;
                cartographyTable.GetComponent<Piece>().m_resources[2].m_amount = 500;
                cartographyTable.GetComponent<Piece>().m_resources[3].m_amount = 800;
                cartographyTable.GetComponent<Piece>().m_resources[4].m_amount = 800;
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

        public static void PiecesToRemoveResourcesDrop()
        {
            foreach (var item in Plugin.PiecesToRemoveResourceDrop.Value.Split(','))
            {
                var gameObject = PrefabManager.Instance.GetPrefab(item);

                if (gameObject)
                {
                    var piece = gameObject.GetComponent<Piece>();
                    foreach (var x in piece.m_resources)
                    {
                        x.m_recover = false;
                    }
                }
            }   
        }

        public static void NerfRunicCape(Player player)
        {            
            GameObject prefab = PrefabManager.Instance.GetPrefab("CapeRunic");

            if (!prefab) return;

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();

            SE_Stats stats = (SE_Stats)itemDrop.m_itemData.m_shared.m_equipStatusEffect;
            stats.m_speedModifier = Plugin.CapeRunicSpeed.Value;
            stats.m_healthRegenMultiplier = Plugin.CapeRunicRegen.Value;
            stats.m_staminaRegenMultiplier = Plugin.CapeRunicRegen.Value;
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

        public static void TurnWandsIntoTwoHandeds()
        {
            List<string> wands = new() { "Wand_Mountain_DoD", "WarlockWand_DoD", "ShamanWand_DoD", "MageWand_DoD" };

            foreach (string wand in wands)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(wand);
                ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
                itemDrop.m_itemData.m_shared.m_itemType =  ItemDrop.ItemData.ItemType.TwoHandedWeapon;
            }
        }
    }
}
