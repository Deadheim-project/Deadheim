using Deadheim.Vanilla;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim
{ internal class ItemService
    {
        // Quantidades originais da mesa de cartografia: com a config em 0 elas voltam, mesmo
        // depois de uma recarga do cfg que ja tinha trocado a receita.
        private static readonly Dictionary<Piece.Requirement, int> _cartographyVanilla = new Dictionary<Piece.Requirement, int>();

        public static void ModifyItemsCost()
        {
            GameObject cartographyTable = Prefabs.Get("piece_cartographytable");
            if (cartographyTable != null)
                foreach (Piece.Requirement requirement in cartographyTable.GetComponent<Piece>().m_resources)
                {
                    if (!_cartographyVanilla.ContainsKey(requirement)) _cartographyVanilla[requirement] = requirement.m_amount;
                    int amount = Plugin.CartographyTableAmount.Value;
                    requirement.m_amount = amount > 0 ? amount : _cartographyVanilla[requirement];
                }

            // O custo do portal vem do cfg (PortalMaterials); antes era fixo aqui e a config
            // nao tinha efeito nenhum.
            Portal.NoBuild_Patch.UpdatePortalMaterials();
        }

        // Pecas so de admin que sairam do martelo: voltam se a adminlist disser que o jogador e admin.
        private static readonly List<GameObject> _hiddenAdminPieces = new List<GameObject>();

        private static PieceTable HammerTable()
        {
            var hammer = ObjectDB.instance != null ? ObjectDB.instance.m_items.FirstOrDefault(x => x != null && x.name == "Hammer") : null;
            return hammer != null ? hammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
        }

        public static void OnlyAdminPieces()
        {
            PieceTable table = HammerTable();
            if (table == null) return;

            foreach (string prefab in Plugin.OnlyAdminPieces.Value.Split(','))
            {
                var item = Prefabs.Get(prefab.Trim());

                if (item is null) continue;

                var piece = item.GetComponent<Piece>();
                foreach (var x in piece.m_resources)
                {
                    x.m_resItem = Prefabs.Get("SwordCheat").GetComponent<ItemDrop>();
                    x.m_recover = false;
                }

                if (Admin.LocalPlayerIsAdmin()) continue;

                if (table.m_pieces.Remove(item) && !_hiddenAdminPieces.Contains(item)) _hiddenAdminPieces.Add(item);
            }
        }

        /// <summary>
        /// A config do servidor chega antes da adminlist (Patches.ReenviaAdminList manda a lista so
        /// depois do RPC_CharacterID), entao OnlyAdminPieces tirava as pecas do martelo do proprio
        /// admin pela sessao inteira (B2). Quando a lista chega e diz admin, as pecas voltam.
        /// </summary>
        public static void RestoreAdminPieces()
        {
            if (_hiddenAdminPieces.Count == 0 || !Admin.LocalPlayerIsAdmin()) return;
            PieceTable table = HammerTable();
            if (table == null) return;
            foreach (GameObject item in _hiddenAdminPieces)
                if (item != null && !table.m_pieces.Contains(item)) table.m_pieces.Add(item);
            _hiddenAdminPieces.Clear();
            Player.m_localPlayer?.UpdateAvailablePiecesList();
        }

        public static void NerfRunicCape()
        {
            GameObject prefab = ObjectDB.instance.GetItemPrefab("rae_CapeHorseHide");

            if (!prefab) return;

            ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();

            SE_Stats stats = (SE_Stats)itemDrop.m_itemData.m_shared.m_equipStatusEffect;
            stats.m_mods = new();
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
