namespace Deadheim
{
    internal static class TextosRankingNet
    {
        public static void Registrar()
        {
            L.Add("rankingnet.entre", "Entre no mundo primeiro.",
                "Enter the world first.");
            L.Add("rankingnet.sem_conexao", "Sem conexão com o servidor.",
                "No connection to the server.");
            L.Add("rankingnet.feito", "{0}{1} é o primeiro do mundo a derrotar {2}! Veja em Ranking > Feitos do mundo.",
                "{0}{1} is the first in the world to defeat {2}! See Ranking > World firsts.");
            L.Add("rankingnet.so_admin", "Só admins do servidor podem mexer no ranking.",
                "Only server admins can change the ranking.");
            L.Add("rankingnet.ninguem", "Ninguém chamado '{0}' no ranking.",
                "Nobody named '{0}' in the ranking.");
            L.Add("rankingnet.ocultou", "{0} personagem(ns) '{1}' fora do ranking (os dados ficam guardados).",
                "{0} character(s) '{1}' removed from the ranking (data is kept).");
            L.Add("rankingnet.mostrou", "{0} personagem(ns) '{1}' de volta ao ranking.",
                "{0} character(s) '{1}' back in the ranking.");
            L.Add("rankingnet.apagou", "Dados de {0} personagem(ns) '{1}' apagados; o que o perfil dele registra volta no próximo relato.",
                "Data of {0} character(s) '{1}' deleted; what their profile records returns on the next report.");
            L.Add("rankingnet.use", "Use ocultar, mostrar ou apagar.",
                "Use ocultar, mostrar or apagar.");
        }
    }
}
