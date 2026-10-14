using System;
using System.Globalization;

namespace Deadheim
{
    /// <summary>
    /// As contas dos dois buffs que o NPC Treinador (NpcValheim) vende, e o formato em que eles
    /// ficam gravados no personagem. Sem Unity: o teste sem o jogo (Testing/TreinadorSemJogo)
    /// compila este arquivo sozinho.
    ///
    /// Estudo: +bonus de XP de skill enquanto dura, so nas skills abaixo do teto. Multiplica o
    /// ganho que ja passou pelo SkillMultiplier do servidor e pelo multiplicador de PvE/PvP: com
    /// 0.5 de SkillMultiplier e +50% de estudo, o ganho vai de 0.5 para 0.75 do vanilla.
    ///
    /// Protecao: corta uma fracao da perda de skill de uma morte. Nunca reduz a pena de PK, e
    /// com pveOnly (o padrao) so vale em morte PvE (monstro, queda, afogamento). Morte que nao
    /// tira skill (arena, castelo, janela sem perda do vanilla) nao gasta a protecao.
    /// </summary>
    internal static class TreinadorRegras
    {
        /// <summary>Teto do estudo acumulado: comprar de novo com um ativo soma o tempo, ate isto.</summary>
        public const double MaxStudySeconds = 24d * 3600d;

        /// <summary>Teto de protecoes guardadas (cada uma vale uma morte).</summary>
        public const int MaxProtectionCharges = 10;

        /// <summary>Bonus aceito no maximo (+1000%): um numero absurdo na config nao vira skill 100 num golpe.</summary>
        public const float MaxStudyBonus = 10f;

        public static float ClampBonus(float bonus) => IsNumber(bonus) ? Math.Max(0f, Math.Min(MaxStudyBonus, bonus)) : 0f;

        public static float ClampLevel(float level) => IsNumber(level) ? Math.Max(0f, Math.Min(100f, level)) : 0f;

        public static float ClampFactor(float factor) => IsNumber(factor) ? Math.Max(0f, Math.Min(1f, factor)) : 0f;

        public static double ClampSeconds(double seconds) =>
            double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0d : Math.Max(0d, Math.Min(MaxStudySeconds, seconds));

        /// <summary>Quanto o estudo multiplica o ganho de uma skill neste nivel: 1 + bonus abaixo do teto, 1 no resto.</summary>
        public static float StudyMultiplier(bool active, float level, float maxSkill, float bonus)
        {
            if (!active || !(level < ClampLevel(maxSkill))) return 1f;
            return 1f + ClampBonus(bonus);
        }

        /// <summary>
        /// Se a protecao vale nesta morte. Quem chama ja sabe que a morte tira skill; aqui so
        /// entra quem matou e se o PK paga. A pena de PK nunca e reduzida, mesmo com pveOnly
        /// desligado.
        /// </summary>
        public static bool Covers(int charges, bool byPlayer, bool pkPays, bool pveOnly) =>
            charges > 0 && !pkPays && (!byPlayer || !pveOnly);

        /// <summary>A fracao de skill perdida depois do corte: 0.05 com corte de 0.5 vira 0.025.</summary>
        public static float ProtectedLoss(float lossFactor, float cut) =>
            Math.Max(0f, Math.Min(1f, lossFactor)) * (1f - ClampFactor(cut));

        // ------------------------------------------------------------ gravado no personagem

        /// <summary>"segundos;bonus;teto": o que falta do estudo, gravado como segundos (o tempo so corre com o jogo aberto).</summary>
        public static string WriteStudy(double secondsLeft, float bonus, float maxSkill) =>
            ClampSeconds(secondsLeft).ToString("R", CultureInfo.InvariantCulture) + ";" +
            ClampBonus(bonus).ToString("R", CultureInfo.InvariantCulture) + ";" +
            ClampLevel(maxSkill).ToString("R", CultureInfo.InvariantCulture);

        public static bool TryReadStudy(string text, out double secondsLeft, out float bonus, out float maxSkill)
        {
            secondsLeft = 0d;
            bonus = 0f;
            maxSkill = 0f;
            string[] parts = (text ?? string.Empty).Split(';');
            if (parts.Length != 3
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double left)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float b)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float max))
                return false;
            secondsLeft = ClampSeconds(left);
            bonus = ClampBonus(b);
            maxSkill = ClampLevel(max);
            return true;
        }

        /// <summary>"cargas;corte;pveOnly": quantas mortes a protecao ainda cobre e como.</summary>
        public static string WriteProtection(int charges, float factor, bool pveOnly) =>
            Math.Max(0, Math.Min(MaxProtectionCharges, charges)).ToString(CultureInfo.InvariantCulture) + ";" +
            ClampFactor(factor).ToString("R", CultureInfo.InvariantCulture) + ";" + (pveOnly ? "1" : "0");

        public static bool TryReadProtection(string text, out int charges, out float factor, out bool pveOnly)
        {
            charges = 0;
            factor = 0f;
            pveOnly = true;
            string[] parts = (text ?? string.Empty).Split(';');
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int c)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                || (parts[2] != "0" && parts[2] != "1"))
                return false;
            charges = Math.Max(0, Math.Min(MaxProtectionCharges, c));
            factor = ClampFactor(f);
            pveOnly = parts[2] == "1";
            return true;
        }

        private static bool IsNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
