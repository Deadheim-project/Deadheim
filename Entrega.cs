using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Item que o servidor manda ao jogador local (recompensa do PvP, tributo do RaidSystem):
    /// vai para o inventario e o que nao couber cai aos pes, sem duplicar nem sumir. A conta e
    /// do PvpCoinMath.Deliver, que o teste sem o jogo confere. Publico: o RaidSystem usa.
    /// </summary>
    public static class Entrega
    {
        /// <summary>
        /// Entrega pelo nome do prefab, na qualidade pedida (troca entre jogadores), como os NPCs
        /// entregam: AddItem(nome, ...) com cheated false e pickedUp true, que no 1.0 contam o item
        /// como coletado e nao como spawn de trapaca. Devolve quanto caiu no chao.
        /// </summary>
        public static int ParaJogador(Player player, string prefabName, int amount, int quality)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (player == null || item?.m_itemData?.m_shared == null || amount <= 0) return 0;

            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
            int q = Mathf.Clamp(quality, 1, Mathf.Max(1, shared.m_maxQuality));
            Vector3 at = player.transform.position + player.transform.forward + Vector3.up;

            return Pvp.PvpCoinMath.Deliver(amount, Mathf.Max(1, shared.m_maxStackSize),
                chunk =>
                {
                    int before = inventory.CountItems(shared.m_name, q, false);
                    inventory.AddItem(prefabName, chunk, q, 0, 0L, "", false, true);
                    return inventory.CountItems(shared.m_name, q, false) - before;
                },
                pile =>
                {
                    GameObject go = Object.Instantiate(prefab, at + Random.insideUnitSphere * 0.3f, Quaternion.identity);
                    ItemDrop dropped = go != null ? go.GetComponent<ItemDrop>() : null;
                    if (dropped == null) return;
                    dropped.m_itemData.m_quality = q;
                    dropped.m_itemData.m_durability = dropped.m_itemData.GetMaxDurability();
                    dropped.SetStack(pile);
                });
        }

        /// <summary>Devolve quanto caiu no chao.</summary>
        public static int ParaJogador(Player player, GameObject prefab, int amount)
        {
            ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (player == null || item?.m_itemData?.m_shared == null || amount <= 0) return 0;

            Inventory inventory = player.GetInventory();
            string name = item.m_itemData.m_shared.m_name;
            int maxStack = Mathf.Max(1, item.m_itemData.m_shared.m_maxStackSize);
            Vector3 at = player.transform.position + player.transform.forward + Vector3.up;

            return Pvp.PvpCoinMath.Deliver(amount, maxStack,
                chunk =>
                {
                    // Conta antes e depois: o AddItem pode parar no meio e ainda assim ter posto uma parte.
                    int before = inventory.CountItems(name, -1, false);
                    inventory.AddItem(prefab, chunk);
                    return inventory.CountItems(name, -1, false) - before;
                },
                pile =>
                {
                    GameObject go = Object.Instantiate(prefab, at + Random.insideUnitSphere * 0.3f, Quaternion.identity);
                    ItemDrop dropped = go != null ? go.GetComponent<ItemDrop>() : null;
                    if (dropped != null) dropped.SetStack(pile);
                });
        }
    }
}
