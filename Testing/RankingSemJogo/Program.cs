using Deadheim.Ranking;
using System;
using System.Collections.Generic;
using System.Linq;

// Teste sem o jogo do nucleo do ranking (Ranking/RankingDados.cs): regras de relato (teto por
// segundo, contador que nao desce, chave do servidor recusada), semana, foto do dia e setas,
// classificacao com empate, guildas, Lenda, feitos e o arquivo (ida e volta, linha quebrada).
internal static class Program
{
    private static int _falhas;
    private static int _total;

    private static void Checar(bool ok, string nome)
    {
        _total++;
        if (ok) Console.WriteLine("PASS " + nome);
        else
        {
            _falhas++;
            Console.WriteLine("FAIL " + nome);
        }
    }

    private static Dictionary<string, double> R(params object[] pares)
    {
        var d = new Dictionary<string, double>();
        for (int i = 0; i < pares.Length; i += 2) d[(string)pares[i]] = Convert.ToDouble(pares[i + 1]);
        return d;
    }

    private static int Main()
    {
        const double fuso = -3d;
        // Segunda, 03/11/2025 12:00 em Brasilia (15:00 UTC).
        double segunda = new DateTimeOffset(2025, 11, 3, 15, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        int semana = RankingTempo.Semana(segunda, fuso);

        // ---------------------------------------------------------------- tempo
        double domingoNoite = segunda - 13 * 3600 - 60; // domingo 02/11 22:59 em Brasilia
        Checar(RankingTempo.Semana(domingoNoite, fuso) == semana - 1, "domingo a noite ainda e a semana anterior");
        double segundaZero = segunda - 12 * 3600; // segunda 00:00 em Brasilia
        Checar(RankingTempo.Semana(segundaZero, fuso) == semana && RankingTempo.Semana(segundaZero - 1, fuso) == semana - 1,
            "a semana vira na segunda 00:00 do fuso");
        Checar(Math.Abs(RankingTempo.InicioSemana(semana, fuso) - segundaZero) < 0.5, "inicio da semana bate com segunda 00:00");
        Checar(RankingTempo.Dia(segunda, fuso) == RankingTempo.Dia(segundaZero, fuso), "mesmo dia no fuso");

        // ---------------------------------------------------------------- relatos
        var dados = new RankingBase();
        RankingJogador ana = dados.Obter(1, "Ana", segunda);
        var novos = RankingRegras.Aplicar(ana, R(Chave.Monstros, 5000, Chave.Distancia, 1e6, Chave.Habilidades, 800,
            Chave.PrefixoChefe + "$enemy_eikthyr", 3, Chave.PvpAbates, 999, "inventado", 5), segunda, semana);
        Checar(ana.Valor(Chave.Monstros) == 5000, "primeiro relato entra inteiro (historico do personagem)");
        Checar(ana.Valor(Chave.PvpAbates) == 0 && !ana.Valores.ContainsKey("inventado"), "chave do servidor e chave desconhecida sao ignoradas");
        Checar(ana.DaSemana(Chave.Monstros) == 0, "o historico nao conta na semana");
        Checar(novos.Count == 1 && novos[0].Key == "$enemy_eikthyr" && novos[0].Value, "chefe no primeiro relato vira feito 'antigo'");

        RankingRegras.Aplicar(ana, R(Chave.Monstros, 4000), segunda + 60, semana);
        Checar(ana.Valor(Chave.Monstros) == 5000, "contador nao desce");

        RankingRegras.Aplicar(ana, R(Chave.Monstros, 999999), segunda + 120, semana);
        Checar(ana.Valor(Chave.Monstros) == 5000 + 10 * Regra.FolgaSeg, "contador sobe no maximo o teto por segundo (com a folga)");

        RankingRegras.Aplicar(ana, R(Chave.Habilidades, 700), segunda + 180, semana);
        Checar(ana.Valor(Chave.Habilidades) == 700, "medida pode descer (perda de habilidade na morte)");
        Checar(ana.DaSemana(Chave.Habilidades) == 0, "semana nunca fica negativa");

        RankingRegras.Aplicar(ana, R(Chave.Moedas, double.NaN, Chave.Construcoes, -5), segunda + 240, semana);
        Checar(ana.Valor(Chave.Moedas) == 0 && ana.Valor(Chave.Construcoes) == 0, "NaN e negativo viram zero");

        var novos2 = RankingRegras.Aplicar(ana, R(Chave.PrefixoChefe + "$enemy_gdking", 1), segunda + 300, semana);
        Checar(novos2.Count == 1 && novos2[0].Value, "chave de chefe nova no meio do jogo conta como 'antigo' (nao havia valor)");
        var novos3 = RankingRegras.Aplicar(ana, R(Chave.PrefixoChefe + "$enemy_bonemass", 0), segunda + 360, semana);
        var novos4 = RankingRegras.Aplicar(ana, R(Chave.PrefixoChefe + "$enemy_bonemass", 1), segunda + 420, semana);
        Checar(novos3.Count == 0 && novos4.Count == 1 && !novos4[0].Value, "chefe que vai de 0 a 1 com o ranking ligado e feito de verdade");

        Checar(!Regra.TryGet(Chave.PrefixoChefe, out _) && !Regra.TryGet(Chave.PrefixoChefe + new string('x', 80), out _),
            "chave de chefe vazia ou enorme e recusada");

        // ---------------------------------------------------------------- semana
        RankingRegras.Aplicar(ana, R(Chave.Monstros, 6300), segunda + 3600, semana);
        Checar(ana.DaSemana(Chave.Monstros) == 1300, "a semana conta o que cresceu");
        Checar(RankingRegras.VirarSemana(ana, semana + 1) && ana.DaSemana(Chave.Monstros) == 0, "semana nova zera a semana");
        Checar(!RankingRegras.VirarSemana(ana, semana + 1), "virar a mesma semana de novo nao faz nada");

        RankingRegras.DefinirDoServidor(ana, Chave.PvpAbates, 7);
        Checar(ana.Valor(Chave.PvpAbates) == 7 && ana.DaSemana(Chave.PvpAbates) == 0, "numero do servidor novo comeca a semana nele");
        RankingRegras.DefinirDoServidor(ana, Chave.PvpAbates, 9);
        Checar(ana.DaSemana(Chave.PvpAbates) == 2, "numero do servidor conta na semana");
        RankingRegras.DefinirDoServidor(ana, Chave.Monstros, 1);
        Checar(ana.Valor(Chave.Monstros) == 6300, "DefinirDoServidor nao mexe em chave do cliente");

        // ---------------------------------------------------------------- classificacao
        var ctx = new RankingContexto { PesosLenda = RankingContexto.LerPesos(Categorias.PesosPadrao), TemGuerra = false };
        List<Categoria> cats = Categorias.Todas(ctx);
        Checar(cats.All(c => c.Id != "guerra" && c.Id != "nivel"), "categoria sem dados no servidor some (guerra, nivel)");
        Checar(cats[0].Id == Categorias.Lenda, "Lenda e a primeira");
        Categoria caca = cats.First(c => c.Id == "monstros");

        RankingJogador bia = dados.Obter(2, "Bia", segunda);
        RankingRegras.Aplicar(bia, R(Chave.Monstros, 6300), segunda, semana);
        RankingJogador caio = dados.Obter(3, "Caio", segunda);
        RankingRegras.Aplicar(caio, R(Chave.Monstros, 9000), segunda, semana);
        RankingJogador dani = dados.Obter(4, "Dani", segunda);
        RankingRegras.Aplicar(dani, R(Chave.Monstros, 0), segunda, semana);
        RankingJogador edu = dados.Obter(5, "Edu", segunda);
        RankingRegras.Aplicar(edu, R(Chave.Monstros, 20000), segunda, semana);
        edu.Oculto = true;

        List<Linha> linhas = Classificacao.Classificar(dados.Jogadores, caca, false);
        Checar(linhas.Count == 3, "zero e oculto ficam fora");
        Checar(linhas[0].Jogador.Nome == "Caio" && linhas[0].Posicao == 1, "maior primeiro");
        Checar(linhas[1].Posicao == 2 && linhas[2].Posicao == 2 && linhas[1].Jogador.Nome == "Ana", "empate divide a posicao e desempata pelo nome");

        // foto do dia e setas
        RankingRegras.FotoDoDia(dados, cats, RankingTempo.Dia(segunda, fuso));
        RankingRegras.Aplicar(bia, R(Chave.Monstros, 6300 + 5000), segunda + 3600, semana);
        RankingRegras.Aplicar(dani, R(Chave.Monstros, 10), segunda + 3600, semana);
        linhas = Classificacao.Classificar(dados.Jogadores, caca, false);
        Linha linhaBia = linhas.First(l => l.Jogador.Nome == "Bia");
        Linha linhaAna = linhas.First(l => l.Jogador.Nome == "Ana");
        Linha linhaDani = linhas.First(l => l.Jogador.Nome == "Dani");
        Checar(linhaBia.Posicao == 1 && Classificacao.Movimento(linhaBia, caca.Id) == 1, "subiu uma posicao desde ontem");
        Checar(Classificacao.Movimento(linhaAna, caca.Id) == -1, "desceu uma posicao desde ontem");
        Checar(Classificacao.Movimento(linhaDani, caca.Id) == null, "quem nao estava ontem e 'novo'");

        // guildas
        ana.Guilda = "Lobos"; bia.Guilda = "lobos"; caio.Guilda = "Corvos";
        var guildas = Classificacao.Guildas(dados.Jogadores, caca, false);
        Checar(guildas.Count == 2 && guildas[0].Membros == 2 && guildas[0].Valor == 6300 + 11300, "guilda soma os membros, sem diferenciar maiuscula");
        Checar(guildas[0].Destaque == bia, "destaque e o melhor da guilda");

        // lenda
        RankingJogador f = new RankingJogador { Id = 9, Nome = "F" };
        f.Valores[Chave.Habilidades] = 100; f.Valores[Chave.Chefes] = 2; f.Valores[Chave.Distancia] = 1000;
        Checar(ctx.Lenda(f, false) == 100 * 2 + 2 * 150 + 1000 * 0.002, "Lenda soma os pesos");
        Categoria lenda = cats[0];
        string detalhe = lenda.Detalhe(f, false);
        Checar(detalhe != null && detalhe == "chefes 60% · habilidades 40%", "detalhe da Lenda mostra as maiores fontes (" + detalhe + ")");
        var pesos = RankingContexto.LerPesos("skills=1,5|ruim|=3|mar=x|pvp.k= 2 ");
        Checar(pesos.Count == 2 && pesos["skills"] == 1.5 && pesos["pvp.k"] == 2, "pesos aceitam virgula e ignoram linha ruim");

        // ---------------------------------------------------------------- numeros
        Checar(Numero.Inteiro(1234567) == "1.234.567", "milhar com ponto");
        Checar(Numero.Decimal(2.5, 2) == "2,50", "decimal com virgula");
        Checar(Numero.Compacto(123456) == "123 mil" && Numero.Compacto(4560000) == "4,56 mi", "compacto");
        Checar(Numero.Distancia(950) == "950 m" && Numero.Distancia(12345) == "12,3 km" && Numero.Distancia(250000) == "250 km", "distancia");
        Checar(Numero.Duracao(3600 * 5 + 60 * 7) == "5 h 07 min" && Numero.Duracao(59 * 60) == "59 min" && Numero.Duracao(3600 * 150) == "150 h", "duracao");

        // ---------------------------------------------------------------- arquivo
        dados.Feitos.Add(new RankingFeito { Chefe = "$enemy_eikthyr", Id = 1, Nome = "Ana\tTab", Guilda = "Lobos", Em = segunda, Antigo = true });
        ana.Nome = "Ana\nQuebra";
        string arquivo = RankingFormato.Escrever(dados);
        int ignoradas;
        RankingBase lido = RankingFormato.Ler(arquivo + "lixo\tsem=nada\nJ\tid=abc\nJ\tid=1\tnome=duplicado\n", out ignoradas);
        Checar(ignoradas == 3, "linhas ruins e jogador repetido sao contados como ignorados (" + ignoradas + ")");
        Checar(lido.Jogadores.Count == dados.Jogadores.Count && lido.DiaFoto == dados.DiaFoto, "ida e volta: jogadores e dia da foto");
        RankingJogador ana2 = lido.Achar(1);
        Checar(ana2.Nome == "Ana Quebra" && ana2.Guilda == "Lobos", "nome com quebra de linha e limpo");
        Checar(ana2.Valor(Chave.Monstros) == ana.Valor(Chave.Monstros) && ana2.DaSemana(Chave.PvpAbates) == ana.DaSemana(Chave.PvpAbates),
            "ida e volta: valores e base da semana");
        Checar(ana2.Valor(Chave.PrefixoChefe + "$enemy_eikthyr") == 3, "ida e volta: chave com $ e :");
        Checar(ana2.PosOntem.Count == ana.PosOntem.Count && ana2.PosOntem[caca.Id] == ana.PosOntem[caca.Id], "ida e volta: posicoes de ontem");
        Checar(lido.Achar(5).Oculto && !lido.Achar(2).Oculto, "ida e volta: oculto");
        Checar(lido.Feitos.Count == 1 && lido.Feitos[0].Nome == "Ana Tab" && lido.Feitos[0].Antigo && lido.Feitos[0].Em == segunda,
            "ida e volta: feito");
        Checar(RankingFormato.Escrever(lido) == RankingFormato.Escrever(RankingFormato.Ler(RankingFormato.Escrever(lido), out _)),
            "escrever o que foi lido e estavel");
        Checar(RankingFormato.Ler("", out ignoradas).Jogadores.Count == 0 && ignoradas == 0, "arquivo vazio");

        Console.WriteLine();
        Console.WriteLine(_falhas == 0 ? $"OK: {_total} checagens" : $"FALHOU: {_falhas} de {_total}");
        return _falhas == 0 ? 0 : 1;
    }
}
