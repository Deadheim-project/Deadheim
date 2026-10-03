using System.Globalization;

namespace Deadheim.Shared
{
    /// <summary>
    /// Numero com casa decimal que o mod grava ou que o jogador digita. Grava sempre com ponto
    /// (InvariantCulture); le com ponto ou com virgula, que e como o formato antigo (cultura do PC)
    /// e o jogador brasileiro escrevem. Sem isto "123,45" virava 12345 ou excecao conforme o
    /// idioma do Windows. Sem Unity: o teste sem o jogo (Testing/PvpSemJogo) usa o mesmo arquivo.
    /// </summary>
    internal static class Numeros
    {
        public static string Escrever(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static bool TryLer(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
