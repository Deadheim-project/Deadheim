namespace Deadheim
{
    internal static class TextosLixeira
    {
        public static void Registrar()
        {
            L.Add("lixeira.dica",
                "Selecione um item do inventário (passe o mouse ou pegue-o com o clique) e aperte <color=#ffd75a>Delete</color> para apagá-lo.\nApaga a pilha inteira, sem perguntar.\n<color=#ff8a7a>Não dá para apagar</color>: moedas, item equipado ou item de baú.",
                "Select an inventory item (hover it or pick it up with a click) and press <color=#ffd75a>Delete</color> to remove it.\nRemoves the whole stack, no prompt.\n<color=#ff8a7a>Cannot delete</color>: coins, equipped items or chest items.");
            L.Add("lixeira.confirmar", "Apagar {0}{1}? Aperte Delete de novo em 4 s.", "Delete {0}{1}? Press Delete again within 4 s.");
            L.Add("lixeira.nivel", " (nível {0})", " (level {0})");
            L.Add("lixeira.so_inventario", "A lixeira só apaga itens do seu inventário.", "The trash only deletes items from your own inventory.");
            L.Add("lixeira.nao_esta", "Esse item não está mais no inventário.", "That item is no longer in the inventory.");
            L.Add("lixeira.moeda", "As moedas não podem ser apagadas.", "Coins cannot be deleted.");
            L.Add("lixeira.equipado", "Desequipe o item antes de apagá-lo.", "Unequip the item before deleting it.");
            L.Add("lixeira.falhou", "Não foi possível apagar.", "Could not delete.");
            L.Add("lixeira.apagado", "Apagado: {0}{1}", "Deleted: {0}{1}");
        }
    }
}
