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
