using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{ internal class ItemService
    {
        public static void ModifyItemsCost()
        {
            GameObject cartographyTable = PrefabManager.Instance.GetPrefab("piece_cartographytable");

            cartographyTable.GetComponent<Piece>().m_resources[0].m_amount = 100;
            cartographyTable.GetComponent<Piece>().m_resources[1].m_amount = 100;
            cartographyTable.GetComponent<Piece>().m_resources[2].m_amount = 100;
            cartographyTable.GetComponent<Piece>().m_resources[3].m_amount = 100;
            cartographyTable.GetComponent<Piece>().m_resources[4].m_amount = 100;

            GameObject portalwood = PrefabManager.Instance.GetPrefab("portal_wood");
            var portalwoodPiece = portalwood.GetComponent<Piece>();
            portalwoodPiece.m_resources[0].m_amount = 1500;
            portalwoodPiece.m_resources[1].m_amount = 1;
            portalwoodPiece.m_resources[1].m_resItem = PrefabManager.Instance.GetPrefab("PortalToken").GetComponent<ItemDrop>();
            portalwoodPiece.m_resources[2].m_amount = 225;
        }

        public static void OnlyAdminPieces()
        {
            var hammer = ObjectDB.instance.m_items.FirstOrDefault(x => x.name == "Hammer");
            PieceTable table = hammer.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;

            foreach (string prefab in Plugin.OnlyAdminPieces.Value.Split(','))
            {
                var item = PrefabManager.Instance.GetPrefab(prefab);

                if (item is null) continue;

                var piece = item.GetComponent<Piece>();
                foreach (var x in piece.m_resources)
                {
                    x.m_resItem = PrefabManager.Instance.GetPrefab("SwordCheat").GetComponent<ItemDrop>();
                    x.m_recover = false;
                }

                if (SynchronizationManager.Instance.PlayerIsAdmin) continue;

                table.m_pieces.Remove(item);
            }
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
            GameObject boat4 = PrefabManager.Instance.GetPrefab("WarShip");
            GameObject boat5 = PrefabManager.Instance.GetPrefab("FishingBoat");
            GameObject boat6 = PrefabManager.Instance.GetPrefab("TransporterShip");
            GameObject karve = PrefabManager.Instance.GetPrefab("Karve");
            Piece karveRecipe = karve.GetComponent<Piece>();

            foreach (GameObject gameObject in new List<GameObject> { boat1, boat2, boat3, boat4, boat5, boat6 })
            {
                Piece recipe = gameObject.GetComponent<Piece>();
                recipe.m_craftingStation = karveRecipe.m_craftingStation;
            }
        }

        public static void NerfRunicCape()
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab("rae_CapeHorseHide");

            if (!prefab) return;

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();

            SE_Stats stats = (SE_Stats)itemDrop.m_itemData.m_shared.m_equipStatusEffect;
            stats.m_mods = new();
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

        public static void ChangeMontersFaction()
        {
            GameObject wendigo = PrefabManager.Instance.GetPrefab("BrownBear");
            wendigo.GetComponent<Character>().m_faction = Character.Faction.ForestMonsters;
            
            GameObject voidling = PrefabManager.Instance.GetPrefab("Fox");
            voidling.GetComponent<Character>().m_faction = Character.Faction.MountainMonsters;
            
            GameObject birchElder = PrefabManager.Instance.GetPrefab("Ibex");
            birchElder.GetComponent<Character>().m_faction = Character.Faction.MountainMonsters;

            GameObject yeti = PrefabManager.Instance.GetPrefab("Casuar");
            yeti.GetComponent<Character>().m_faction = Character.Faction.PlainsMonsters;                      
        }
    }
}
