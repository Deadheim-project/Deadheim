namespace Deadheim
{
    internal static class TextosPvpBounty
    {
        public static void Registrar()
        {
            L.Add("pvpbounty.so_admin", "Só admin.",
                "Admin only.");
            L.Add("pvpbounty.desligada", "A bounty está desligada neste servidor.",
                "Bounty is turned off on this server.");
            L.Add("pvpbounty.minima", "A bounty mínima é {0} moedas.",
                "The minimum bounty is {0} coins.");
            L.Add("pvpbounty.nao_conheco", "Não conheço nenhum jogador chamado '{0}'.",
                "I don't know any player named '{0}'.");
            L.Add("pvpbounty.si_mesmo", "Você não pode colocar bounty em si mesmo.",
                "You cannot place a bounty on yourself.");
            L.Add("pvpbounty.e_pve", "{0} é PvE: não pode ser caçado.",
                "{0} is PvE: cannot be hunted.");
            L.Add("pvpbounty.invalido", "Valor inválido.",
                "Invalid amount.");
            L.Add("pvpbounty.acabou_sair", "{0} acabou de sair de uma bounty. Outra só em {1}.",
                "{0} just got out of a bounty. Another one only in {1}.");
            L.Add("pvpbounty.teto", "Teto de bounty por dia: {0} moedas por jogador. Hoje você ainda pode colocar {1}.",
                "Daily bounty cap: {0} coins per player. Today you can still place {1}.");
            L.Add("pvpbounty.ate_morrer", "até morrer para um jogador",
                "until killed by a player");
            L.Add("pvpbounty.por_online", "por {0} online",
                "for {0} online");
            L.Add("pvpbounty.vira_cacado", " Vira CAÇADO em {0}.",
                " Becomes HUNTED in {0}.");
            L.Add("pvpbounty.vale", "<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{0}</color> vale <color=#ffd700>{1}</color> moedas para quem matar ({2}).{3}",
                "<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{0}</color> is worth <color=#ffd700>{1}</color> coins to whoever kills them ({2}).{3}");
            L.Add("pvpbounty.colocou", "Você colocou {0} moedas na cabeça de {1}.",
                "You placed {0} coins on {1}'s head.");
            L.Add("pvpbounty.colocaram_delay", "<color=#ff5050>Colocaram {0} moedas na sua cabeça.</color> Em {1} você vira CAÇADO: aparece no mapa e perde a zona segura. Ainda dá tempo de teleportar.{2}",
                "<color=#ff5050>{0} coins were placed on your head.</color> In {1} you become HUNTED: you show on the map and lose the safe zone. There is still time to teleport.{2}");
            L.Add("pvpbounty.ou_pague", " Ou pague {0} no Caçador de Recompensas.",
                " Or pay {0} at the Bounty Hunter.");
            L.Add("pvpbounty.subiu", "<color=#ff5050>Sua bounty subiu para {0} moedas.</color>",
                "<color=#ff5050>Your bounty rose to {0} coins.</color>");
            L.Add("pvpbounty.sem_bounty", "Não há bounty na sua cabeça.",
                "There is no bounty on your head.");
            L.Add("pvpbounty.pagar_off", "Pagar a própria bounty está desligado neste servidor.",
                "Paying off your own bounty is turned off on this server.");
            L.Add("pvpbounty.custo", "Para encerrar a sua bounty são {0} moedas.",
                "Ending your bounty costs {0} coins.");
            L.Add("pvpbounty.encerrada_troco", "Bounty encerrada por {0} moedas; o troco ({1}) voltou para você.",
                "Bounty ended for {0} coins; your change ({1}) was returned.");
            L.Add("pvpbounty.encerrada", "Bounty encerrada por {0} moedas.",
                "Bounty ended for {0} coins.");
            L.Add("pvpbounty.comprou", "<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{0}</color> pagou {1} moedas e comprou a própria cabeça.",
                "<color=#ff8c00>BOUNTY:</color> <color=#ffb347>{0}</color> paid {1} coins and bought their own head.");
            L.Add("pvpbounty.st_delay", "caçado em {0}",
                "hunted in {0}");
            L.Add("pvpbounty.st_morrer", "CAÇADO até morrer",
                "HUNTED until death");
            L.Add("pvpbounty.st_mais", "CAÇADO por mais {0}",
                "HUNTED for {0} more");
            L.Add("pvpbounty.linha", "{0}: {1} moedas para quem matar, {2}{3}",
                "{0}: {1} coins to whoever kills them, {2}{3}");
            L.Add("pvpbounty.offline", " (offline, tempo parado)",
                " (offline, time stopped)");
            L.Add("pvpbounty.nenhuma", "Nenhuma bounty ativa.",
                "No active bounties.");
        }
    }
}
