namespace Deadheim
{
    internal static class TextosPvpApi
    {
        public static void Registrar()
        {
            L.Add("pvpapi.entre", "Entre no mundo primeiro.",
                "Enter the world first.");
            L.Add("pvpapi.bounty_off", "A bounty está desligada neste servidor.",
                "Bounty is turned off on this server.");
            L.Add("pvpapi.nome_alvo", "Escreva o nome do alvo.",
                "Type the target's name.");
            L.Add("pvpapi.invalido", "Valor inválido.",
                "Invalid amount.");
            L.Add("pvpapi.minima", "A bounty mínima é {0} moedas.",
                "The minimum bounty is {0} coins.");
            L.Add("pvpapi.si_mesmo", "Você não pode colocar bounty em si mesmo.",
                "You cannot place a bounty on yourself.");
            L.Add("pvpapi.sem_bounty", "Não há bounty na sua cabeça.",
                "There is no bounty on your head.");
            L.Add("pvpapi.pagar_off", "Pagar a própria bounty está desligado neste servidor.",
                "Paying off your own bounty is turned off on this server.");
            L.Add("pvpapi.so_admin", "Só admin.",
                "Admin only.");
            L.Add("pvpapi.valor_inv", "Valor inválido para {0}.",
                "Invalid value for {0}.");
            L.Add("pvpapi.desconhecido", "Atributo desconhecido: {0}.",
                "Unknown attribute: {0}.");
            L.Add("pvpapi.valendo", "{0} = {1} (já valendo para todos).",
                "{0} = {1} (already in effect for everyone).");
            L.Add("pvpapi.recusou", "O servidor recusou: {0}",
                "The server refused: {0}");
            L.Add("pvpapi.s_pve_on", "Troca para o PvE liberada",
                "PvE switch allowed");
            L.Add("pvpapi.s_espera", "Espera entre trocas (horas)",
                "Wait between switches (hours)");
            L.Add("pvpapi.s_titulo", "Título de quem é PvE",
                "PvE player title");
            L.Add("pvpapi.s_skill_pve", "Ganho de skill no PvE (x)",
                "Skill gain in PvE (x)");
            L.Add("pvpapi.s_skill_pvp", "Ganho de skill no PvP (x)",
                "Skill gain in PvP (x)");
            L.Add("pvpapi.s_coleta_pve", "Coleta no PvE (x, 0 = a do mundo)",
                "Gathering in PvE (x, 0 = world default)");
            L.Add("pvpapi.s_coleta_pvp", "Coleta no PvP (x, 0 = a do mundo)",
                "Gathering in PvP (x, 0 = world default)");
        }
    }
}
