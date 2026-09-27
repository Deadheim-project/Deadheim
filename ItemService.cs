using Deadheim.Vanilla;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{ internal class ItemService
    {
        public static void ModifyItemsCost()
        {
            GameObject cartographyTable = Prefabs.Get("piece_cartographytable");
            if (cartographyTable != null)
                foreach (Piece.Requirement requirement in cartographyTable.GetComponent<Piece>().m_resources)
                    requirement.m_amount = Plugin.CartographyTableAmount.Value;

            // O custo do portal vem do cfg (PortalMaterials); antes era fixo aqui e a config
            // nao tinha efeito nenhum.
            Portal.NoBuild_Patch.UpdatePortalMaterials();
        }

        public static void OnlyAdminPieces()
        {
            var hammer = ObjectDB.instance.m_items.FirstOrDefault(x => x.name == "Hammer");
            PieceTable table = hammer.GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces;

            foreach (string prefab in Plugin.OnlyAdminPieces.Value.Split(','))
            {
                var item = Prefabs.Get(prefab);

                if (item is null) continue;

                var piece = item.GetComponent<Piece>();
                foreach (var x in piece.m_resources)
                {
                    x.m_resItem = Prefabs.Get("SwordCheat").GetComponent<ItemDrop>();
                    x.m_recover = false;
                }

                if (Admin.LocalPlayerIsAdmin()) continue;

                table.m_pieces.Remove(item);
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

            GameObject prefab = Prefabs.Get("Wolf");
            if (!prefab) return;

            Tameable tameable = prefab.GetComponent<Tameable>();
            Procreation procreation = prefab.GetComponent<Procreation>();
            UnityEngine.Object.Destroy(tameable);
            UnityEngine.Object.Destroy(procreation);
        }

        public static void LoxTameable()
        {
            if (Plugin.LoxTameable.Value) return;

            GameObject prefab = Prefabs.Get("Lox");
            if (!prefab) return;

            Tameable tameable = prefab.GetComponent<Tameable>();
            Procreation procreation = prefab.GetComponent<Procreation>();
            UnityEngine.Object.Destroy(tameable);
            UnityEngine.Object.Destroy(procreation);
        }

        public static void StubNoLife()
        {
            List<GameObject> stubList = new();
            stubList.Add(Prefabs.Get("Pinetree_01_Stub"));
            stubList.Add(Prefabs.Get("SwampTree1_Stub"));
            stubList.Add(Prefabs.Get("BirchStub"));
            stubList.Add(Prefabs.Get("FirTree_Stub"));
            stubList.Add(Prefabs.Get("OakStub"));
            stubList.Add(Prefabs.Get("Beech_Stub"));

            foreach (GameObject stub in stubList)
            {
                Destructible destructible = stub.GetComponent<Destructible>();
                destructible.m_health = 1;
            }
        }
    }
}
