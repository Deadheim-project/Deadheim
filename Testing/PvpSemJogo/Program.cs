// Teste sem o jogo do estado do PvP (Pvp/PvpData.cs). O formato antigo (JsonUtility)
// gravava so o "version" e perdia jogadores e bounties a cada restart: aqui o arquivo vai e
// volta campo por campo.
using Deadheim.Pvp;
using System;
using System.Linq;

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

    private static int Main()
    {
        PvpStoreData data = new PvpStoreData();
        data.players.Add(new PvpPlayerRecord
        {
            id = 403029348, name = "Solo", kills = 3, deaths = 12, pkLeft = 1234.5678, pkStreak = 2, pkPenalty = 1,
            pkKills = 7, bountyReadyAt = 1790000000.25, combatLogPending = true, pendingCoins = 750,
        });
        data.players.Add(new PvpPlayerRecord { id = 424242, name = "Nome\tcom\ttab\nlinha", pkPermanent = true, pvePermanent = true, pveSince = 99.5 });
        data.players.Add(new PvpPlayerRecord { id = -77, name = "id negativo (Steam/Xbox)" });
        PvpBountyRecord bounty = new PvpBountyRecord { targetId = 403029348, targetName = "Solo", pot = 2500, delayLeft = 12.5, elapsed = 600.25 };
        bounty.contributions.Add(new PvpBountyContribution { id = 0, name = "casa", amount = 1000 });
        bounty.contributions.Add(new PvpBountyContribution { id = 424242, name = "Dummy", amount = 1500 });
        data.bounties.Add(bounty);

        string text = PvpStoreFormat.Write(data);
        PvpStoreData back = PvpStoreFormat.Read(text, out int skipped);

        Check("formato/sem-linhas-puladas", skipped == 0, "puladas=" + skipped);
        Check("formato/versao", back.version == PvpStoreFormat.Version, "versao=" + back.version);
        Check("formato/jogadores", back.players.Count == 3, "jogadores=" + back.players.Count);
        PvpPlayerRecord solo = back.players.FirstOrDefault(p => p.id == 403029348);
        Check("formato/campos-do-jogador", solo != null && solo.name == "Solo" && solo.kills == 3 && solo.deaths == 12
                                           && solo.pkLeft == 1234.5678 && solo.pkStreak == 2 && solo.pkPenalty == 1 && solo.pkKills == 7
                                           && solo.bountyReadyAt == 1790000000.25 && solo.combatLogPending && solo.pendingCoins == 750
                                           && !solo.pkPermanent && !solo.pvePermanent,
            solo == null ? "sem Solo" : PvpStoreFormat.Write(new PvpStoreData { players = { solo } }));
        PvpPlayerRecord dummy = back.players.FirstOrDefault(p => p.id == 424242);
        Check("formato/pk-permanente-e-pve", dummy != null && dummy.pkPermanent && dummy.pvePermanent && dummy.pveSince == 99.5);
        Check("formato/nome-sem-tab-nem-quebra", dummy != null && dummy.name == "Nome com tab linha", dummy?.name ?? "");
        Check("formato/id-negativo", back.players.Any(p => p.id == -77));
        Check("formato/bounty", back.bounties.Count == 1 && back.bounties[0].pot == 2500 && back.bounties[0].delayLeft == 12.5
                                && back.bounties[0].elapsed == 600.25 && back.bounties[0].targetName == "Solo");
        Check("formato/contribuicoes", back.bounties.Count == 1 && back.bounties[0].contributions.Count == 2
                                       && back.bounties[0].contributions.Any(c => c.id == 0 && c.amount == 1000 && c.name == "casa")
                                       && back.bounties[0].contributions.Any(c => c.id == 424242 && c.amount == 1500));
        Check("formato/escrever-de-novo-da-o-mesmo-texto", PvpStoreFormat.Write(back) == text);

        // Arquivo editado a mao ou de versao mais nova: o que sobra e ignorado, o que falta e padrao.
        PvpStoreData hand = PvpStoreFormat.Read(
            "# comentario\r\nversion\t9\r\nplayer\tid=5\tname=Ana\tkills=2\tcampoNovo=x\r\n\r\nlixo\tqualquer\r\n" +
            "player\tname=sem-id\r\nplayer\tid=5\tname=duplicado\r\ncontrib\ttarget=123\tid=1\tamount=5\r\n", out int handSkipped);
        Check("leitura/campo-desconhecido-ignorado", hand.players.Count == 1 && hand.players[0].name == "Ana" && hand.players[0].kills == 2
                                                    && hand.players[0].deaths == 0, $"jogadores={hand.players.Count}");
        Check("leitura/linhas-ruins-contadas", handSkipped == 4, "puladas=" + handSkipped);
        Check("leitura/versao-do-arquivo", hand.version == 9);

        // O arquivo antigo do JsonUtility: so o version. Nao e o formato, nao derruba nada.
        PvpStoreData legacy = PvpStoreFormat.Read("{\n    \"version\": 2\n}", out int legacySkipped);
        Check("leitura/json-antigo-vira-vazio", legacy.players.Count == 0 && legacy.bounties.Count == 0, "puladas=" + legacySkipped);
        Check("leitura/vazio", PvpStoreFormat.Read("", out _).players.Count == 0 && PvpStoreFormat.Read(null, out _).players.Count == 0);

        // Relogio de PK: so desconta quem nao e permanente; quando zera, a marca toda some.
        PvpPlayerRecord pk = new PvpPlayerRecord { id = 1, pkLeft = 10d, pkStreak = 3, pkPenalty = 1 };
        Check("pk/marcado", pk.IsPk);
        Check("pk/desconta-sem-acabar", !pk.TickPk(4d) && Math.Abs(pk.pkLeft - 6d) < 1e-9 && pk.IsPk, "resta=" + pk.pkLeft);
        Check("pk/acaba-no-tempo", pk.TickPk(6d) && !pk.IsPk && pk.pkStreak == 0 && pk.pkPenalty == 0 && pk.pkLeft == 0d);
        Check("pk/sem-marca-nao-faz-nada", !pk.TickPk(5d) && !pk.IsPk);
        PvpPlayerRecord permanent = new PvpPlayerRecord { id = 2, pkPermanent = true, pkLeft = 3d };
        Check("pk/permanente-nao-expira", !permanent.TickPk(100d) && permanent.IsPk && permanent.pkLeft == 3d);
        PvpPlayerRecord overshoot = new PvpPlayerRecord { id = 3, pkLeft = 1d, pkStreak = 1 };
        Check("pk/passar-do-tempo-zera", overshoot.TickPk(9d) && overshoot.pkLeft == 0d && !overshoot.IsPk);

        Console.WriteLine($"DONE pass={_pass} fail={_fail}");
        return _fail == 0 ? 0 : 1;
    }
}
