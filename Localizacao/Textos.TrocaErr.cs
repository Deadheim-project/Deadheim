namespace Deadheim
{
    internal static class TextosTrocaErr
    {
        public static void Registrar()
        {
            L.Add("trocaerr.e_desligado", "A troca está desligada neste servidor.",
                "Trading is turned off on this server.");
            L.Add("trocaerr.e_sem_jogador", "Não achei esse jogador.",
                "I could not find that player.");
            L.Add("trocaerr.e_longe", "Esse jogador está longe demais.",
                "That player is too far away.");
            L.Add("trocaerr.e_si_mesmo", "Você não pode trocar consigo mesmo.",
                "You cannot trade with yourself.");
            L.Add("trocaerr.e_voce_ocupado", "Você já está numa troca.",
                "You are already in a trade.");
            L.Add("trocaerr.e_alvo_ocupado", "Esse jogador já está numa troca.",
                "That player is already in a trade.");
            L.Add("trocaerr.e_sem_pedido", "Não há pedido de troca para você.",
                "There is no trade request for you.");
            L.Add("trocaerr.e_sem_sessao", "Não há troca aberta.",
                "There is no open trade.");
            L.Add("trocaerr.e_item_invalido", "Item inválido para troca.",
                "Invalid item for trade.");
            L.Add("trocaerr.e_nao_permitido", "A troca é só de moedas.",
                "Trading is coins only.");
            L.Add("trocaerr.e_cheio", "A oferta está cheia.",
                "The offer is full.");
            L.Add("trocaerr.e_item_repetido", "Esse item já está na troca.",
                "That item is already in the trade.");
            L.Add("trocaerr.e_item_inexistente", "Esse item não está na oferta.",
                "That item is not in the offer.");
            L.Add("trocaerr.e_versao_antiga", "A oferta mudou; confira de novo.",
                "The offer changed; check it again.");
            L.Add("trocaerr.e_vazia", "Nada foi oferecido.",
                "Nothing was offered.");
            L.Add("trocaerr.e_sem_trava", "Os dois precisam travar a oferta antes de confirmar.",
                "Both must lock the offer before confirming.");
            L.Add("trocaerr.e_aguarde", "Aguarde: um dos itens ainda está sendo guardado.",
                "Wait: one of the items is still being stored.");
            L.Add("trocaerr.e_nome_ambiguo", "Há mais de um jogador com esse nome; chegue perto e use /trocar sem nome.",
                "More than one player has that name; get close and use /trocar without a name.");
            L.Add("trocaerr.e_pendencias", "Você tem entregas de troca pendentes; entre de novo no servidor para recebê-las.",
                "You have pending trade deliveries; rejoin the server to receive them.");
            L.Add("trocaerr.e_devagar", "Calma: espere alguns segundos antes de pedir outra troca.",
                "Easy: wait a few seconds before requesting another trade.");
            L.Add("trocaerr.e_bloqueado", "Esse jogador recusou há pouco; tente mais tarde.",
                "That player declined recently; try later.");
            L.Add("trocaerr.e_bolsa", "A oferta passou do que cabe numa bolsa ({0} pilhas).",
                "The offer exceeds what fits in a bag ({0} stacks).");
        }
    }
}
