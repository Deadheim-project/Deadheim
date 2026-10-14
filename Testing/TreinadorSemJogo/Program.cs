// Teste sem o jogo das regras dos buffs do NPC Treinador (TreinadorRegras.cs). As contas
// rodam no cliente de quem joga, dentro do RaiseSkill e da morte; aqui elas vao uma a uma.
using Deadheim;
using System;

internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) _pass++;
        else _fail++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (ok || detail.Length == 0 ? "" : "  (" + detail + ")"));
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-5f;

    private static int Main()
    {
        Console.WriteLine("== estudo ==");
        float skillMultiplier = 0.5f;
        Check("com SkillMultiplier 0.5 e +50% o ganho vai a 0.75 do vanilla",
              Near(skillMultiplier * TreinadorRegras.StudyMultiplier(true, 20f, 50f, 0.5f), 0.75f));
        Check("abaixo do teto vale", Near(TreinadorRegras.StudyMultiplier(true, 49.9f, 50f, 0.5f), 1.5f));
        Check("no teto ja nao vale", Near(TreinadorRegras.StudyMultiplier(true, 50f, 50f, 0.5f), 1f));
        Check("acima do teto nao vale", Near(TreinadorRegras.StudyMultiplier(true, 80f, 50f, 0.5f), 1f));
        Check("sem estudo ativo e 1", Near(TreinadorRegras.StudyMultiplier(false, 10f, 50f, 0.5f), 1f));
        Check("bonus negativo vira 0", Near(TreinadorRegras.StudyMultiplier(true, 10f, 50f, -3f), 1f));
        Check("bonus absurdo tem teto (+1000%)", Near(TreinadorRegras.StudyMultiplier(true, 10f, 50f, 999f), 11f));
        Check("NaN nao vira ganho", Near(TreinadorRegras.StudyMultiplier(true, 10f, 50f, float.NaN), 1f) &&
                                    Near(TreinadorRegras.StudyMultiplier(true, 10f, float.NaN, 0.5f), 1f));

        Console.WriteLine("== protecao: quando vale ==");
        // charges, byPlayer, pkPays, pveOnly
        Check("morte PvE com protecao: vale", TreinadorRegras.Covers(1, false, false, true));
        Check("morte para jogador com pveOnly: nao vale", !TreinadorRegras.Covers(1, true, false, true));
        Check("morte para jogador sem pveOnly: vale", TreinadorRegras.Covers(1, true, false, false));
        Check("pena de PK nunca e reduzida, nem em morte PvE",
              !TreinadorRegras.Covers(1, false, true, true) && !TreinadorRegras.Covers(1, false, true, false));
        Check("pena de PK nunca e reduzida, nem com pveOnly desligado", !TreinadorRegras.Covers(3, true, true, false));
        Check("sem carga nao vale", !TreinadorRegras.Covers(0, false, false, true));

        Console.WriteLine("== protecao: quanto corta ==");
        // Exemplo: uma morte que tiraria 5% de cada skill.
        Check("corte de 50% transforma 5% em 2,5%", Near(TreinadorRegras.ProtectedLoss(0.05f, 0.5f), 0.025f));
        Check("corte de 0 nao muda nada", Near(TreinadorRegras.ProtectedLoss(0.05f, 0f), 0.05f));
        Check("corte de 100% zera a perda", Near(TreinadorRegras.ProtectedLoss(0.05f, 1f), 0f));
        Check("corte acima de 1 nao vira ganho de skill", Near(TreinadorRegras.ProtectedLoss(0.05f, 3f), 0f));
        Check("corte negativo nao aumenta a perda", Near(TreinadorRegras.ProtectedLoss(0.05f, -1f), 0.05f));

        Console.WriteLine("== gravado no personagem ==");
        string study = TreinadorRegras.WriteStudy(1234.5, 0.5f, 50f);
        Check("estudo vai e volta",
              TreinadorRegras.TryReadStudy(study, out double left, out float bonus, out float max) &&
              Math.Abs(left - 1234.5) < 1e-9 && Near(bonus, 0.5f) && Near(max, 50f), study);
        Check("estudo gravado usa ponto, nunca virgula", !study.Contains(","), study);
        Check("estudo acima de 24 h e cortado",
              TreinadorRegras.TryReadStudy(TreinadorRegras.WriteStudy(1e9, 0.5f, 50f), out left, out _, out _) &&
              Math.Abs(left - TreinadorRegras.MaxStudySeconds) < 1e-6);
        Check("estudo quebrado e recusado",
              !TreinadorRegras.TryReadStudy("abc;0.5;50", out _, out _, out _) &&
              !TreinadorRegras.TryReadStudy("10;0.5", out _, out _, out _) &&
              !TreinadorRegras.TryReadStudy(null, out _, out _, out _));
        Check("tempo negativo gravado vira 0",
              TreinadorRegras.TryReadStudy("-50;0.5;50", out left, out _, out _) && left == 0d);

        string protection = TreinadorRegras.WriteProtection(2, 0.5f, true);
        Check("protecao vai e volta",
              TreinadorRegras.TryReadProtection(protection, out int charges, out float cut, out bool pveOnly) &&
              charges == 2 && Near(cut, 0.5f) && pveOnly, protection);
        Check("pveOnly desligado volta desligado",
              TreinadorRegras.TryReadProtection(TreinadorRegras.WriteProtection(1, 0.25f, false), out _, out _, out pveOnly) && !pveOnly);
        Check("cargas tem teto",
              TreinadorRegras.TryReadProtection("999;0.5;1", out charges, out _, out _) &&
              charges == TreinadorRegras.MaxProtectionCharges);
        Check("protecao quebrada e recusada",
              !TreinadorRegras.TryReadProtection("1;0.5;sim", out _, out _, out _) &&
              !TreinadorRegras.TryReadProtection("x;0.5;1", out _, out _, out _) &&
              !TreinadorRegras.TryReadProtection("", out _, out _, out _));

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? $"TUDO VERDE: {_pass} checagens" : $"{_fail} FALHARAM, {_pass} passaram");
        return _fail == 0 ? 0 : 1;
    }
}
