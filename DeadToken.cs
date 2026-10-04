using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Dead Token: o token unico do servidor. Toda construcao paga custa Dead Tokens, na quantidade
    /// do preco dela (portal 1, spawner 1, Ward de Territorio 5).
    ///
    /// Antes era um token por construcao (PortalToken, SpawnerToken, TerritoryToken). Os antigos
    /// continuam registrados, para nao sumir o que esta no chao, num bau ou na loja que ainda os
    /// vende, mas viram Dead Token assim que entram num inventario, na quantidade que a construcao
    /// deles custa. No cfg, um custo antigo ("PortalToken:1") tambem vale como Dead Token.
    /// </summary>
    internal static class DeadToken
    {
        public const string Prefab = "DeadToken";
        public const string Nome = "Dead Token";

        public const int CustoPortal = 1;
        public const int CustoSpawner = 1;
        public const int CustoTerritorio = 5;

        /// <summary>Token antigo e quantos Dead Tokens ele vale: o custo da construcao dele.</summary>
        private static readonly Dictionary<string, int> Antigos = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "PortalToken", CustoPortal },
            { "SpawnerToken", CustoSpawner },
            { Wards.WardProfiles.TerritoryToken, CustoTerritorio },
        };

        /// <summary>
        /// Item de um custo do cfg ("Item:Quantidade"). Um token antigo vira o Dead Token, com a
        /// quantidade multiplicada: o cfg do servidor guarda o valor antigo ate alguem trocar.
        /// </summary>
        internal static string Custo(string prefab, ref int amount)
        {
            if (prefab == null || !Antigos.TryGetValue(prefab, out int each)) return prefab;
            amount *= each;
            return Prefab;
        }

        /// <summary>
        /// Troca no lugar cada token antigo do inventario pelo Dead Token equivalente. Roda em todo
        /// Changed: carregar (personagem, bau, tumba), pegar do chao, comprar e mover entre baus.
        /// </summary>
        internal static void ConverterAntigos(Inventory inventory)
        {
            List<ItemDrop.ItemData> items = inventory?.m_inventory;
            if (items == null) return;

            GameObject token = null;
            int sobra = 0;
            foreach (ItemDrop.ItemData item in items)
            {
                if (item?.m_dropPrefab == null || !Antigos.TryGetValue(item.m_dropPrefab.name, out int each)) continue;
                if (token == null)
                {
                    // Sem o item no ObjectDB (menu, antes do Awake) fica para o proximo Changed.
                    token = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Prefab) : null;
                    if (token == null) return;
                }

                int total = item.m_stack * each;
                item.m_shared = token.GetComponent<ItemDrop>().m_itemData.m_shared;
                item.m_dropPrefab = token;
                item.m_stack = Mathf.Min(total, item.m_shared.m_maxStackSize);
                sobra += total - item.m_stack;
            }

            // So com pilha antiga maior que a do jogo (spawn de admin): o resto vai em pilhas novas.
            int pilha = token != null ? token.GetComponent<ItemDrop>().m_itemData.m_shared.m_maxStackSize : 1;
            while (sobra > 0)
            {
                int n = Mathf.Min(sobra, pilha);
                if (!inventory.AddItem(token, n))
                {
                    Debug.LogWarning($"[Deadheim] Inventario cheio: {sobra} {Nome} da troca dos tokens antigos nao couberam.");
                    break;
                }
                sobra -= n;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
        private static class ConverterAoMudar
        {
            private static void Prefix(Inventory __instance) => ConverterAntigos(__instance);
        }
    }
}
