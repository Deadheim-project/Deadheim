using UnityEngine;

namespace Deadheim.Shared
{
    /// <summary>
    /// Padrao visual das mensagens centrais do Deadheim (territorio, ward, PvP): titulo curto em cor,
    /// com a explicacao na linha de baixo. Cor diz o tipo (vermelho bloqueio, dourado territorio, azul
    /// informacao, verde ok). Os simbolos sao so os que a fonte do jogo tem (Latin-1: » « •): emoji nao
    /// existe na fonte e sairia como quadradinho.
    /// </summary>
    public static class Avisos
    {
        public const string CorBloqueio = "#ff6b5a";
        public const string CorTerritorio = "#ffd24a";
        public const string CorInfo = "#7fd4ff";
        public const string CorOk = "#7CFC00";
        public const string CorPvp = "#ff5050";
        private const string CorDetalhe = "#d8d8d8";

        private static string Montar(string cor, string simbolo, string titulo, string detalhe)
            => "<color=" + cor + "><b>" + simbolo + " " + titulo + "</b></color>"
               + (string.IsNullOrEmpty(detalhe) ? string.Empty : "\n<color=" + CorDetalhe + ">" + detalhe + "</color>");

        /// <summary>Recusa: o jogador tentou algo que nao pode.</summary>
        public static string Bloqueio(string titulo, string detalhe = null) => Montar(CorBloqueio, "!", titulo, detalhe);

        public static string Info(string titulo, string detalhe = null) => Montar(CorInfo, "•", titulo, detalhe);

        public static string Ok(string titulo, string detalhe = null) => Montar(CorOk, "•", titulo, detalhe);

        public static string Territorio(string titulo, string detalhe = null) => Montar(CorTerritorio, "»", titulo, detalhe);

        public static string Pvp(string titulo, string detalhe = null) => Montar(CorPvp, "»", titulo, detalhe);

        /// <summary>
        /// Recusa em texto puro "Titulo: detalhe" (ou so "Titulo") vira o aviso de bloqueio de duas linhas.
        /// Texto que ja vem com cor (comeca com "<") passa como esta.
        /// </summary>
        public static string Recusa(string texto)
        {
            if (string.IsNullOrEmpty(texto) || texto[0] == '<') return texto;
            int corte = texto.IndexOf(": ", System.StringComparison.Ordinal);
            return corte > 0 ? Bloqueio(texto.Substring(0, corte), texto.Substring(corte + 2).TrimEnd('.')) : Bloqueio(texto.TrimEnd('.'));
        }

        public static void Mostrar(Player player, string texto)
        {
            if (player != null && !string.IsNullOrEmpty(texto)) player.Message(MessageHud.MessageType.Center, texto);
        }
    }
}
