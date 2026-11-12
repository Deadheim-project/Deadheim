namespace Deadheim
{
    internal static class TextosTrocaMsg
    {
        public static void Registrar()
        {
            L.Add("trocamsg.nv", "  (nv {0})",
                "  (lv {0})");
            L.Add("trocamsg.nao_ha", "Não há troca aberta.",
                "There is no open trade.");
            L.Add("trocamsg.cheia", "A oferta está cheia.",
                "The offer is full.");
            L.Add("trocamsg.so_moedas", "A troca é só de moedas.",
                "Trading is coins only.");
            L.Add("trocamsg.nao_troca", "Esse item não pode ser trocado.",
                "That item cannot be traded.");
            L.Add("trocamsg.qtd_inv", "Quantidade inválida.",
                "Invalid quantity.");
            L.Add("trocamsg.no_max", "No máximo {0} por vez.",
                "At most {0} at a time.");
            L.Add("trocamsg.so_tem", "Você só tem {0}x {1} para oferecer.",
                "You only have {0}x {1} to offer.");
            L.Add("trocamsg.devolvido", "Troca, devolvido: ",
                "Trade, returned: ");
            L.Add("trocamsg.recebido", "Troca, recebido: ",
                "Trade, received: ");
            L.Add("trocamsg.inv_cheio", "Inventário cheio: {0}x {1} caiu no chão.",
                "Inventory full: {0}x {1} dropped on the ground.");
            L.Add("trocamsg.desligada", "A troca está desligada neste servidor.",
                "Trading is turned off on this server.");
            L.Add("trocamsg.ja_troca", "Você já está numa troca.",
                "You are already in a trade.");
        }
    }
}
