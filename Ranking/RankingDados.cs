using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Deadheim.Ranking
{
    // O nucleo do ranking: registros, formato do arquivo, categorias, classificacao, semana e as
    // regras que o servidor usa para aceitar o que o cliente relata. Nada aqui usa o Unity nem o
    // jogo de proposito: e testado fora do jogo (Testing/RankingSemJogo) e roda igual no servidor
    // (que guarda e classifica) e no cliente (que mostra).

    /// <summary>Nome de cada numero guardado por jogador.</summary>
    internal static class Chave
    {
        // ---- do servidor (o cliente nunca escreve)
        public const string PvpAbates = "pvp.k";
        public const string PvpMortes = "pvp.d";
        public const string PvpPk = "pvp.pk";
        public const string Guerra = "guerra";
        public const string GuerraConquistas = "guerra.c";
        public const string Online = "online";
        /// <summary>Arena do NpcValheim: maior rating pessoal, vitorias e partidas da temporada.</summary>
        public const string Arena = "arena";
        public const string ArenaVitorias = "arena.v";
        public const string ArenaPartidas = "arena.j";
        /// <summary>Missoes concluidas (NpcValheim), repetiveis contam cada vez.</summary>
        public const string Missoes = "missoes";

        // ---- do perfil do jogador (o cliente relata; o servidor confere)
        public const string Habilidades = "skills";
        public const string HabilidadeMaior = "skills.max";
        public const string Nivel = "nivel";
        public const string Chefes = "chefes";
        public const string ChefesAbates = "chefes.k";
        public const string Monstros = "monstros";
        public const string Mortes = "mortes";
        public const string Construcoes = "build";
        public const string Fabricacoes = "craft";
        public const string Arvores = "arvores";
        public const string Mineracao = "mina";
        public const string Distancia = "dist";
        public const string Navegacao = "mar";
        public const string Domados = "domados";
        public const string Moedas = "moedas";

        /// <summary>Mortes por causa: "mt:monstro", "mt:queda"... (ver TiposDeMorte).</summary>
        public const string PrefixoMorte = "mt:";

        /// <summary>Abates de um chefe: "chefe:$enemy_eikthyr".</summary>
        public const string PrefixoChefe = "chefe:";

        public static bool EhDoServidor(string chave) =>
            chave == PvpAbates || chave == PvpMortes || chave == PvpPk || chave == Guerra
            || chave == GuerraConquistas || chave == Online || chave == Arena || chave == ArenaVitorias
            || chave == ArenaPartidas || chave == Missoes;
    }

    /// <summary>Causa de morte: id da chave, nome da lista e os contadores DeathBy* do jogo que somam nela.</summary>
    internal sealed class TipoMorte
    {
        public string Id, Titulo, Icone, Descricao;
        public string[] Contadores;
        public string Chave => Deadheim.Ranking.Chave.PrefixoMorte + Id;
    }

    internal static class TiposDeMorte
    {
        public static readonly TipoMorte[] Todos =
        {
            new TipoMorte { Id = "monstro", Titulo = "Mortes por monstros", Icone = "TrophyGreydwarfBrute", Contadores = new[] { "DeathByEnemyHit" },
                Descricao = "Vezes que um monstro ou criatura matou você." },
            new TipoMorte { Id = "jogador", Titulo = "Mortes por jogadores", Icone = "SwordIron", Contadores = new[] { "DeathByPlayerHit" },
                Descricao = "Vezes que outro jogador matou você (PvP, inclusive na arena se o jogo registrar)." },
            new TipoMorte { Id = "queda", Titulo = "Mortes por queda", Icone = "TrophyDeer", Contadores = new[] { "DeathByFall", "DeathByImpact" },
                Descricao = "Quedas e impactos fatais." },
            new TipoMorte { Id = "afogado", Titulo = "Mortes por afogamento", Icone = "TrophySerpent", Contadores = new[] { "DeathByDrowning", "DeathByWater", "DeathByAshlandsOcean" },
                Descricao = "Afogado ou na água/oceano de cinzas." },
            new TipoMorte { Id = "fogo", Titulo = "Mortes por fogo e lava", Icone = "Torch", Contadores = new[] { "DeathByBurning", "DeathByCinderFire", "DeathByIncinerator", "DeathByAshlandsLava", "DeathBySmoke" },
                Descricao = "Queimado, fogo de cinzas, incinerador, lava ou fumaça." },
            new TipoMorte { Id = "clima", Titulo = "Mortes por frio e veneno", Icone = "Ruby", Contadores = new[] { "DeathByFreezing", "DeathByPoisoned" },
                Descricao = "Congelado ou envenenado." },
            new TipoMorte { Id = "outras", Titulo = "Mortes por acidente", Icone = "Hammer", Contadores = new[] { "DeathByTree", "DeathByTreeTier0", "DeathByTreeTier1", "DeathByTreeTier2", "DeathByTreeTier3", "DeathByTreeTier4", "DeathByTreeTier5", "DeathByCart", "DeathByBoat", "DeathByStructural", "DeathByTurret", "DeathByStalagtite", "DeathByCatapult", "DeathByDrawBridge", "DeathByEdgeOfWorld", "DeathBySelf", "DeathByUndefined" },
                Descricao = "Árvore caindo, carroça, barco, construção, torre, estalactite, fim do mundo, suicídio e causas não identificadas." },
        };

        public static TipoMorte Achar(string chave)
        {
            foreach (TipoMorte t in Todos) if (t.Chave == chave) return t;
            return null;
        }
    }

    /// <summary>
    /// Como o servidor aceita um numero relatado pelo cliente. Contador so cresce (abates,
    /// distancia) e tem um teto de quanto pode crescer por segundo; Medida e o estado atual
    /// (soma das habilidades, moedas), que sobe e desce, so com teto absoluto.
    /// </summary>
    internal struct Regra
    {
        public bool Contador;
        public double PorSegundo;
        public double Teto;

        public Regra(bool contador, double porSegundo, double teto)
        {
            Contador = contador;
            PorSegundo = porSegundo;
            Teto = teto;
        }

        /// <summary>Folga minima, em segundos, do teto por segundo: o relato vem a cada minuto, em rajadas.</summary>
        public const double FolgaSeg = 120d;

        private static readonly Dictionary<string, Regra> Regras = new Dictionary<string, Regra>
        {
            [Chave.Habilidades] = new Regra(false, 0d, 20000d),
            [Chave.HabilidadeMaior] = new Regra(false, 0d, 1000d),
            [Chave.Nivel] = new Regra(false, 0d, 10000d),
            [Chave.Chefes] = new Regra(false, 0d, 100d),
            [Chave.ChefesAbates] = new Regra(true, 1d, 1e7),
            [Chave.Monstros] = new Regra(true, 10d, 1e9),
            [Chave.Mortes] = new Regra(true, 1d, 1e7),
            [Chave.Construcoes] = new Regra(true, 25d, 1e9),
            // Fabricacao em lote (Aesir) faz ate 99 de uma vez.
            [Chave.Fabricacoes] = new Regra(true, 100d, 1e9),
            [Chave.Arvores] = new Regra(true, 5d, 1e9),
            [Chave.Mineracao] = new Regra(true, 25d, 1e9),
            // Metros. Correr com vento e barco rapido ficam bem abaixo disto.
            [Chave.Distancia] = new Regra(true, 120d, 1e12),
            [Chave.Navegacao] = new Regra(true, 80d, 1e12),
            [Chave.Domados] = new Regra(true, 1d, 1e7),
            [Chave.Moedas] = new Regra(false, 0d, 1e10),
        };

        private static readonly Regra RegraChefe = new Regra(true, 1d, 1e7);
        private static readonly Regra RegraMorte = new Regra(true, 1d, 1e7);

        public static bool TryGet(string chave, out Regra regra)
        {
            regra = default(Regra);
            if (string.IsNullOrEmpty(chave) || Chave.EhDoServidor(chave)) return false;
            if (chave.StartsWith(Chave.PrefixoChefe, StringComparison.Ordinal))
            {
                if (chave.Length <= Chave.PrefixoChefe.Length || chave.Length > 64) return false;
                regra = RegraChefe;
                return true;
            }
            if (chave.StartsWith(Chave.PrefixoMorte, StringComparison.Ordinal))
            {
                if (TiposDeMorte.Achar(chave) == null) return false;
                regra = RegraMorte;
                return true;
            }
            return Regras.TryGetValue(chave, out regra);
        }

        /// <summary>
        /// O valor aceito para um relato. <paramref name="tinha"/> diz se ja havia valor antes
        /// (o primeiro relato de um jogador e o historico do personagem e entra inteiro, so com o
        /// teto absoluto). Contador nunca desce; sobe no maximo PorSegundo x tempo desde o ultimo
        /// relato (com a folga minima).
        /// </summary>
        public double Aceitar(bool tinha, double antes, double novo, double segundos)
        {
            if (double.IsNaN(novo) || double.IsInfinity(novo)) return tinha ? antes : 0d;
            if (novo < 0d) novo = 0d;
            if (novo > Teto) novo = Teto;
            if (!Contador || !tinha) return novo;
            if (novo <= antes) return antes;
            double tempo = Math.Max(FolgaSeg, segundos);
            return Math.Min(novo, antes + PorSegundo * tempo);
        }
    }

    internal sealed class RankingJogador
    {
        public long Id;
        public string Nome = "";
        public string Guilda = "";
        public bool Aesir;
        /// <summary>Escondido pelo admin (contas de teste, admin, trapaca): fica no arquivo, sai do ranking.</summary>
        public bool Oculto;
        /// <summary>
        /// Esta na adminlist do servidor (conferido quando o jogador esta conectado e guardado no
        /// arquivo): admin nao compete, fica fora das listas e das somas de guilda.
        /// </summary>
        public bool Admin;
        /// <summary>Fora do ranking, por ordem do admin (Oculto) ou por ser admin.</summary>
        public bool Fora => Oculto || Admin;
        public double PrimeiraVez;
        public double UltimaVez;
        /// <summary>Hora (servidor) do ultimo relato aceito: o teto por segundo conta daqui.</summary>
        public double UltimoRelato;
        /// <summary>Semana de BaseSemana (RankingTempo.Semana).</summary>
        public int Semana;
        /// <summary>
        /// Hora (UTC) do arquivo do personagem que o servidor importou por ultimo (ServerCharacters).
        /// 0 = nunca importado. Arquivo com a mesma hora nao e lido de novo.
        /// </summary>
        public double UltimoPerfil;

        public Dictionary<string, double> Valores = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>Valores no comeco da semana: o ranking da semana e a diferenca.</summary>
        public Dictionary<string, double> BaseSemana = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>Posicao em cada categoria (temporada) na virada do dia: as setas de subiu/desceu.</summary>
        public Dictionary<string, int> PosOntem = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>So no cliente: o jogador esta conectado agora.</summary>
        public bool Conectado;

        public double Valor(string chave)
        {
            double v;
            return chave != null && Valores.TryGetValue(chave, out v) ? v : 0d;
        }

        /// <summary>Quanto cresceu desde o comeco da semana (nunca negativo).</summary>
        public double DaSemana(string chave)
        {
            double agora = Valor(chave);
            double base0;
            if (!BaseSemana.TryGetValue(chave, out base0)) return agora;
            return Math.Max(0d, agora - base0);
        }

        public double Le(string chave, bool semana) => semana ? DaSemana(chave) : Valor(chave);
    }

    /// <summary>Feito do mundo: o primeiro a derrotar cada chefe.</summary>
    internal sealed class RankingFeito
    {
        /// <summary>Nome interno do chefe ($enemy_eikthyr).</summary>
        public string Chefe = "";
        public long Id;
        public string Nome = "";
        public string Guilda = "";
        public double Em;
        /// <summary>
        /// Veio no primeiro relato do jogador: o chefe ja tinha caido antes de o ranking existir,
        /// entao "primeiro" e so o primeiro registrado.
        /// </summary>
        public bool Antigo;
    }

    internal sealed class RankingBase
    {
        public List<RankingJogador> Jogadores = new List<RankingJogador>();
        public List<RankingFeito> Feitos = new List<RankingFeito>();
        /// <summary>Dia (RankingTempo.Dia) da ultima foto das posicoes.</summary>
        public int DiaFoto;

        public RankingJogador Achar(long id)
        {
            for (int i = 0; i < Jogadores.Count; i++)
                if (Jogadores[i].Id == id) return Jogadores[i];
            return null;
        }

        public RankingJogador Obter(long id, string nome, double agora)
        {
            RankingJogador j = Achar(id);
            if (j != null)
            {
                if (!string.IsNullOrEmpty(nome)) j.Nome = nome;
                return j;
            }
            j = new RankingJogador { Id = id, Nome = nome ?? id.ToString(CultureInfo.InvariantCulture), PrimeiraVez = agora, UltimaVez = agora };
            Jogadores.Add(j);
            return j;
        }

        public RankingFeito Feito(string chefe)
        {
            for (int i = 0; i < Feitos.Count; i++)
                if (string.Equals(Feitos[i].Chefe, chefe, StringComparison.Ordinal)) return Feitos[i];
            return null;
        }
    }

    // ===================================================================== tempo

    /// <summary>Dia e semana do ranking, no fuso do servidor (FusoHoras), a partir de segundos UTC.</summary>
    internal static class RankingTempo
    {
        public static int Dia(double segundosUtc, double fusoHoras) =>
            (int)Math.Floor((segundosUtc + fusoHoras * 3600d) / 86400d);

        /// <summary>Semana que comeca na segunda-feira, 00:00 do fuso. 01/01/1970 foi quinta.</summary>
        public static int Semana(double segundosUtc, double fusoHoras) =>
            (int)Math.Floor((Dia(segundosUtc, fusoHoras) + 3) / 7d);

        /// <summary>Segundos UTC da segunda-feira 00:00 (no fuso) da semana.</summary>
        public static double InicioSemana(int semana, double fusoHoras) =>
            ((double)semana * 7d - 3d) * 86400d - fusoHoras * 3600d;
    }

    // =============================================================== regras de relato

    internal static class RankingRegras
    {
        /// <summary>
        /// Aplica um relato do cliente ao registro. Devolve os chefes derrotados pela primeira
        /// vez por este jogador (para os feitos do mundo), e se cada um veio no primeiro relato.
        /// Chave desconhecida ou do servidor e ignorada.
        /// </summary>
        public static List<KeyValuePair<string, bool>> Aplicar(RankingJogador j, IDictionary<string, double> relato,
            double agora, int semana)
        {
            var chefesNovos = new List<KeyValuePair<string, bool>>();
            if (j == null || relato == null) return chefesNovos;

            bool primeiroRelato = j.UltimoRelato <= 0d;
            // Sem historico nenhum (nem relato, nem perfil importado): o que chega e o passado.
            bool semHistorico = primeiroRelato && j.UltimoPerfil <= 0d;
            double segundos = primeiroRelato ? 0d : Math.Max(0d, agora - j.UltimoRelato);

            foreach (KeyValuePair<string, double> par in relato)
            {
                Regra regra;
                if (!Regra.TryGet(par.Key, out regra)) continue;

                double antes;
                bool tinha = j.Valores.TryGetValue(par.Key, out antes);
                double aceito = regra.Aceitar(tinha, antes, par.Value, segundos);
                j.Valores[par.Key] = aceito;

                // Chave nova no meio da semana: a semana conta a partir daqui, nao o historico.
                if (!j.BaseSemana.ContainsKey(par.Key)) j.BaseSemana[par.Key] = BaseNova(regra, semHistorico || par.Key.StartsWith(Chave.PrefixoMorte, StringComparison.Ordinal), tinha, antes, aceito);

                if (par.Key.StartsWith(Chave.PrefixoChefe, StringComparison.Ordinal) && (!tinha || antes <= 0d) && aceito > 0d)
                    chefesNovos.Add(new KeyValuePair<string, bool>(par.Key.Substring(Chave.PrefixoChefe.Length), semHistorico));
            }

            if (j.Semana == 0) j.Semana = semana;
            j.UltimoRelato = agora;
            return chefesNovos;
        }

        /// <summary>
        /// O perfil do personagem guardado no servidor (ServerCharacters), lido do disco. E do
        /// proprio servidor, entao nao tem teto por segundo: contador fica com o maior entre o que
        /// ja havia e o arquivo (o relato ao vivo pode estar a frente do arquivo); medida (soma
        /// das habilidades, moedas) so e trocada com o jogador offline, porque com ele online o
        /// relato e mais novo. A primeira importacao e o historico: nao conta na semana, e chefe
        /// que ja estava la vira feito "antes do ranking". Chefe que aparece numa importacao
        /// seguinte foi derrotado de verdade desde a anterior, por volta de
        /// <paramref name="horaArquivo"/>.
        /// </summary>
        public static List<KeyValuePair<string, bool>> AplicarDoPerfil(RankingJogador j, IDictionary<string, double> valores,
            double horaArquivo, bool online, int semana)
        {
            var chefesNovos = new List<KeyValuePair<string, bool>>();
            if (j == null || valores == null) return chefesNovos;
            bool primeira = j.UltimoPerfil <= 0d && j.UltimoRelato <= 0d;

            foreach (KeyValuePair<string, double> par in valores)
            {
                Regra regra;
                if (!Regra.TryGet(par.Key, out regra)) continue;
                double novo = par.Value;
                if (double.IsNaN(novo) || double.IsInfinity(novo) || novo < 0d) continue;
                if (novo > regra.Teto) novo = regra.Teto;

                double antes;
                bool tinha = j.Valores.TryGetValue(par.Key, out antes);
                double aceito = regra.Contador ? Math.Max(tinha ? antes : 0d, novo) : (online && tinha ? antes : novo);
                j.Valores[par.Key] = aceito;
                if (!j.BaseSemana.ContainsKey(par.Key)) j.BaseSemana[par.Key] = BaseNova(regra, primeira || par.Key.StartsWith(Chave.PrefixoMorte, StringComparison.Ordinal), tinha, antes, aceito);

                if (par.Key.StartsWith(Chave.PrefixoChefe, StringComparison.Ordinal) && (!tinha || antes <= 0d) && aceito > 0d)
                    chefesNovos.Add(new KeyValuePair<string, bool>(par.Key.Substring(Chave.PrefixoChefe.Length), primeira));
            }

            if (j.Semana == 0) j.Semana = semana;
            if (horaArquivo > j.UltimoPerfil) j.UltimoPerfil = horaArquivo;
            return chefesNovos;
        }

        /// <summary>
        /// Base da semana de uma chave que ainda nao tinha base. Sem historico, o que chegou e o
        /// passado (nao conta). Contador que aparece pela primeira vez no meio da semana (o jogo
        /// so cria o contador no primeiro animal domado, por exemplo) comeca do zero: conta inteiro.
        /// Medida nova comeca dela mesma.
        /// </summary>
        private static double BaseNova(Regra regra, bool semHistorico, bool tinha, double antes, double aceito)
        {
            if (semHistorico) return aceito;
            if (tinha) return antes;
            return regra.Contador ? 0d : aceito;
        }

        /// <summary>Numero que so o servidor sabe (PvP, guerra, tempo online).</summary>
        public static void DefinirDoServidor(RankingJogador j, string chave, double valor)
        {
            if (j == null || !Chave.EhDoServidor(chave)) return;
            if (double.IsNaN(valor) || double.IsInfinity(valor) || valor < 0d) valor = 0d;
            if (!j.BaseSemana.ContainsKey(chave)) j.BaseSemana[chave] = j.Valores.ContainsKey(chave) ? j.Valor(chave) : valor;
            j.Valores[chave] = valor;
        }

        /// <summary>Semana nova: o que o jogador tem agora vira a base da semana.</summary>
        public static bool VirarSemana(RankingJogador j, int semana)
        {
            if (j == null || j.Semana == semana) return false;
            j.BaseSemana = new Dictionary<string, double>(j.Valores, StringComparer.Ordinal);
            j.Semana = semana;
            return true;
        }

        /// <summary>
        /// Foto das posicoes da temporada em cada categoria: amanha as setas comparam com ela.
        /// Quem nao esta na categoria fica sem posicao (aparece como "novo" quando entrar).
        /// </summary>
        public static void FotoDoDia(RankingBase dados, IEnumerable<Categoria> categorias, int dia)
        {
            foreach (RankingJogador j in dados.Jogadores) j.PosOntem.Clear();
            foreach (Categoria c in categorias)
                foreach (Linha linha in Classificacao.Classificar(dados.Jogadores, c, false))
                    linha.Jogador.PosOntem[c.Id] = linha.Posicao;
            dados.DiaFoto = dia;
        }
    }

    // ================================================================== categorias

    internal sealed class Categoria
    {
        public string Id;
        public string Titulo;
        public string Grupo;
        /// <summary>Prefab do item cujo icone representa a categoria.</summary>
        public string Icone;
        /// <summary>O que a categoria mede, na barra de cima da janela.</summary>
        public string Descricao;
        /// <summary>Como o numero e calculado e de onde vem: o texto da dica ao passar o mouse.</summary>
        public string Calculo;
        public Func<RankingJogador, bool, double> Valor;
        /// <summary>Texto da coluna de detalhe (pode ser null).</summary>
        public Func<RankingJogador, bool, string> Detalhe;
        public Func<double, string> Formato;
        /// <summary>Unidade depois do numero ("pts", "km"), no texto "faltam X".</summary>
        public string Unidade = "";
        /// <summary>Esta categoria tem dados neste servidor (mod instalado, chave ligada).</summary>
        public Func<RankingContexto, bool> Disponivel;

        public string Formatar(double valor) => Formato != null ? Formato(valor) : Numero.Inteiro(valor);
    }

    /// <summary>O que as categorias precisam saber do servidor (config sincronizada, mods presentes).</summary>
    internal sealed class RankingContexto
    {
        public IList<string> OrdemChefes = new List<string>();
        public Dictionary<string, double> PesosLenda = new Dictionary<string, double>(StringComparer.Ordinal);
        public bool TemGuerra;
        public bool TemNivel;
        public bool TemArena;
        public bool TemMissoes;
        public bool MostrarRiqueza = true;

        public double Lenda(RankingJogador j, bool semana)
        {
            double total = 0d;
            foreach (KeyValuePair<string, double> peso in PesosLenda)
            {
                if (peso.Value == 0d || peso.Key == Chave.Domados) continue;
                total += j.Le(peso.Key, semana) * peso.Value;
            }
            return Math.Max(0d, Math.Floor(total));
        }

        /// <summary>"skills=2|chefes=150|..." (ponto ou virgula no decimal). Linha ruim e ignorada.</summary>
        public static Dictionary<string, double> LerPesos(string texto)
        {
            var pesos = new Dictionary<string, double>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(texto)) return pesos;
            foreach (string parte in texto.Split('|', ';', '\n'))
            {
                int igual = parte.LastIndexOf('=');
                if (igual <= 0) continue;
                string chave = parte.Substring(0, igual).Trim();
                double peso;
                if (chave.Length == 0 || !Numero.TentarLer(parte.Substring(igual + 1), out peso)) continue;
                pesos[chave] = peso;
            }
            return pesos;
        }
    }

    internal static class Categorias
    {
        public const string Lenda = "lenda";

        public const string GrupoGeral = "Geral";
        public const string GrupoCombate = "Combate";
        public const string GrupoProgresso = "Progresso";
        public const string GrupoOficio = "Ofícios";
        public const string GrupoExploracao = "Exploração";
        public const string GrupoRiqueza = "Riqueza";
        public const string GrupoMortes = "Mortes";

        public const string PesosPadrao =
            "skills=2|chefes=150|chefes.k=10|monstros=1|pvp.k=15|guerra=5|arena.v=20|missoes=25|build=0.2|craft=0.5|arvores=0.5|mina=0.1|dist=0.002|mar=0.004|online=0.0005";

        /// <summary>Todas as categorias, na ordem da lista da janela.</summary>
        public static List<Categoria> Todas(RankingContexto ctx)
        {
            var lista = new List<Categoria>
            {
                new Categoria
                {
                    Id = Lenda, Titulo = "Pontuação geral", Grupo = GrupoGeral, Icone = "TrophyDragonQueen", Unidade = "pts",
                    Descricao = "Soma de pontos de tudo o que você faz no mundo (como é calculada: passe o mouse).",
                    Calculo = TextoLenda(ctx),
                    Valor = ctx.Lenda,
                    Detalhe = (j, s) => DetalheLenda(j, s, ctx),
                },
                new Categoria
                {
                    Id = "pvp", Titulo = "Abates PvP", Grupo = GrupoCombate, Icone = "SwordIron", Unidade = "abates",
                    Descricao = "Jogadores abatidos fora da arena, conferido pelo servidor.",
                    Calculo = "Jogadores que você abateu fora da arena. Contado e conferido pelo servidor, não pelo seu jogo. K/D = abates ÷ mortes em PvP; PK = abates de inocentes.",
                    Valor = (j, s) => j.Le(Chave.PvpAbates, s),
                    Detalhe = (j, s) =>
                    {
                        double k = j.Le(Chave.PvpAbates, s), d = j.Le(Chave.PvpMortes, s), pk = j.Le(Chave.PvpPk, s);
                        string texto = "K/D " + Numero.Decimal(d <= 0d ? k : k / d, 2) + " · " + Numero.Inteiro(d) + (d == 1d ? " morte" : " mortes");
                        return pk > 0d ? texto + " · PK " + Numero.Inteiro(pk) : texto;
                    },
                },
                new Categoria
                {
                    Id = "guerra", Titulo = "Guerra de guildas", Grupo = GrupoCombate, Icone = "ShieldBronzeBuckler", Unidade = "pts",
                    Descricao = "Pontos de guerra de guildas (categoria oculta: o RaidSystem foi removido).",
                    Calculo = "Sem fonte de dados desde a remocao do RaidSystem.",
                    Valor = (j, s) => j.Le(Chave.Guerra, s),
                    Detalhe = (j, s) =>
                    {
                        double c = j.Le(Chave.GuerraConquistas, s);
                        return c > 0d ? Numero.Inteiro(c) + (c == 1d ? " conquista" : " conquistas") : null;
                    },
                    Disponivel = c => c.TemGuerra,
                },
                new Categoria
                {
                    Id = "arena", Titulo = "Arena (rating)", Grupo = GrupoCombate, Icone = "SwordBlackmetal", Unidade = "de rating",
                    Descricao = "Maior rating pessoal nos times de arena (na semana: vitórias).",
                    Calculo = "Na temporada: seu maior rating pessoal nos times de arena (só entra quem já jogou uma partida). Na semana: vitórias ganhas na semana. Contado pelo servidor.",
                    // Rating sem partida e so o valor inicial: fica fora da lista.
                    Valor = (j, s) => j.Valor(Chave.ArenaPartidas) <= 0d ? 0d : s ? j.DaSemana(Chave.ArenaVitorias) : j.Valor(Chave.Arena),
                    Detalhe = (j, s) =>
                    {
                        double v = j.Valor(Chave.ArenaVitorias), n = j.Valor(Chave.ArenaPartidas);
                        return Numero.Inteiro(v) + (v == 1d ? " vitória" : " vitórias") + " em " + Numero.Inteiro(n) + (n == 1d ? " partida" : " partidas");
                    },
                    Disponivel = c => c.TemArena,
                },
                new Categoria
                {
                    Id = "chefes", Titulo = "Chefes derrotados", Grupo = GrupoCombate, Icone = "TrophyEikthyr", Unidade = "chefes",
                    Descricao = "Chefes diferentes derrotados (na semana: chefes novos).",
                    Calculo = "Quantos chefes DIFERENTES você já derrotou (cada um conta uma vez, não importa quantas vezes o mate). Lido do perfil do personagem. Na semana: chefes novos.",
                    Valor = (j, s) => j.Le(Chave.Chefes, s),
                    Detalhe = (j, s) =>
                    {
                        double abates = j.Le(Chave.ChefesAbates, s);
                        return abates > 0d ? Numero.Inteiro(abates) + (abates == 1d ? " abate de chefe" : " abates de chefe") : null;
                    },
                },
                new Categoria
                {
                    Id = "monstros", Titulo = "Monstros abatidos", Grupo = GrupoCombate, Icone = "TrophyGreydwarfBrute", Unidade = "abates",
                    Descricao = "Monstros e criaturas abatidos.",
                    Calculo = "Monstros e criaturas que você matou (contador EnemyKills do jogo). Chefes NÃO entram aqui, têm categoria própria.",
                    Valor = (j, s) => j.Le(Chave.Monstros, s),
                },
                new Categoria
                {
                    Id = "habilidades", Titulo = "Soma das habilidades", Grupo = GrupoProgresso, Icone = "SilverNecklace", Unidade = "níveis",
                    Descricao = "Soma dos níveis de todas as habilidades (na semana: níveis ganhos).",
                    Calculo = "Soma dos níveis de todas as habilidades do personagem, lida do arquivo do personagem. Cai se você perder nível ao morrer. Na semana: níveis ganhos.",
                    Valor = (j, s) => Math.Floor(j.Le(Chave.Habilidades, s)),
                    Detalhe = (j, s) => s ? null : "maior: " + Numero.Inteiro(j.Valor(Chave.HabilidadeMaior)),
                },
                new Categoria
                {
                    Id = "missoes", Titulo = "Missões dos NPCs", Grupo = GrupoProgresso, Icone = "Feathers", Unidade = "missões",
                    Descricao = "Missões concluídas nos NPCs (as repetíveis contam cada vez).",
                    Calculo = "Missões concluídas nos NPCs. As repetíveis contam a cada vez. Contado pelo servidor.",
                    Valor = (j, s) => j.Le(Chave.Missoes, s),
                    Disponivel = c => c.TemMissoes,
                },
                new Categoria
                {
                    Id = "nivel", Titulo = "Nível do personagem", Grupo = GrupoProgresso, Icone = "Ruby", Unidade = "níveis",
                    Descricao = "Nível do personagem (EpicMMO).",
                    Calculo = "Nível do personagem no EpicMMO. Na semana: níveis ganhos.",
                    Valor = (j, s) => j.Le(Chave.Nivel, s),
                    Disponivel = c => c.TemNivel,
                },
                new Categoria
                {
                    Id = "online", Titulo = "Tempo de jogo", Grupo = GrupoProgresso, Icone = "Torch", Unidade = "",
                    Descricao = "Tempo conectado neste mundo, contado pelo servidor.",
                    Calculo = "Tempo conectado neste mundo, contado pelo servidor de minuto em minuto.",
                    Valor = (j, s) => j.Le(Chave.Online, s),
                    Formato = Numero.Duracao,
                },
                new Categoria
                {
                    Id = "mortes", Titulo = "Mortes (total)", Grupo = GrupoMortes, Icone = "TrophySkeleton", Unidade = "mortes",
                    Descricao = "Quantas vezes você morreu, de qualquer causa.",
                    Calculo = "Todas as mortes do personagem (contador Deaths do jogo), de qualquer causa. Quem morre mais fica em cima. Na semana: mortes da semana.",
                    Valor = (j, s) => j.Le(Chave.Mortes, s),
                    Detalhe = (j, s) => CausaPrincipal(j, s),
                },
                new Categoria
                {
                    Id = "construcoes", Titulo = "Peças construídas", Grupo = GrupoOficio, Icone = "Hammer", Unidade = "peças",
                    Descricao = "Peças construídas com o martelo.",
                    Calculo = "Cada peça colocada com o martelo conta 1 (contador Builds do jogo). Desmanchar não desconta.",
                    Valor = (j, s) => j.Le(Chave.Construcoes, s),
                },
                new Categoria
                {
                    Id = "fabricacoes", Titulo = "Itens fabricados", Grupo = GrupoOficio, Icone = "Bronze", Unidade = "itens",
                    Descricao = "Itens fabricados e melhorados nas bancadas.",
                    Calculo = "Cada vez que você fabrica ou melhora um item numa bancada (conta a operação, não a quantidade do lote).",
                    Valor = (j, s) => j.Le(Chave.Fabricacoes, s),
                },
                new Categoria
                {
                    Id = "lenhador", Titulo = "Árvores derrubadas", Grupo = GrupoOficio, Icone = "AxeBronze", Unidade = "árvores",
                    Descricao = "Árvores derrubadas.",
                    Calculo = "Árvores que você derrubou (contador Tree do jogo: a árvore que cai, não cada machadada).",
                    Valor = (j, s) => j.Le(Chave.Arvores, s),
                },
                new Categoria
                {
                    Id = "mineracao", Titulo = "Mineração (golpes)", Grupo = GrupoOficio, Icone = "PickaxeIron", Unidade = "golpes",
                    Descricao = "Pedra e minério quebrados com a picareta.",
                    Calculo = "Golpes de picareta em pedra e minério (contador MineHits: cada golpe conta, não a rocha inteira).",
                    Valor = (j, s) => j.Le(Chave.Mineracao, s),
                },
                new Categoria
                {
                    Id = "domados", Titulo = "Animais domados", Grupo = GrupoOficio, Icone = "TrophyBoar", Unidade = "animais",
                    Descricao = "Criaturas domadas.",
                    Calculo = "Criaturas que o jogo registrou como domadas no seu personagem (contador CreatureTamed). Atenção: o jogo credita quem é o dono do animal na hora da doma, normalmente o jogador mais próximo, que pode não ser quem o alimentou.",
                    Valor = (j, s) => j.Le(Chave.Domados, s),
                    // Desligada: o jogo credita a doma ao dono do animal na hora, nao a quem domou.
                    Disponivel = c => false,
                },
                new Categoria
                {
                    Id = "distancia", Titulo = "Distância percorrida", Grupo = GrupoExploracao, Icone = "TrophyDeer", Unidade = "",
                    Descricao = "Distância percorrida a pé, de montaria e de barco.",
                    Calculo = "Tudo o que você se deslocou: a pé, correndo, de montaria, de barco e voando (contador DistanceTraveled). Inclui a distância navegada.",
                    Valor = (j, s) => j.Le(Chave.Distancia, s),
                    Formato = Numero.Distancia,
                },
                new Categoria
                {
                    Id = "navegacao", Titulo = "Distância navegada", Grupo = GrupoExploracao, Icone = "TrophySerpent", Unidade = "",
                    Descricao = "Distância navegada.",
                    Calculo = "Distância percorrida dentro de um barco (contador DistanceSail), no leme ou como passageiro.",
                    Valor = (j, s) => j.Le(Chave.Navegacao, s),
                    Formato = Numero.Distancia,
                },
                new Categoria
                {
                    Id = "moedas", Titulo = "Moedas na bolsa", Grupo = GrupoRiqueza, Icone = "Coins", Unidade = "moedas",
                    Descricao = "Moedas na bolsa (na semana: quanto juntou).",
                    Calculo = "Moedas que você carrega agora. Sobe e desce: gastar derruba. Na semana: quanto juntou a mais.",
                    Valor = (j, s) => j.Le(Chave.Moedas, s),
                    Disponivel = c => c.MostrarRiqueza,
                },
            };
            // As listas por causa de morte, logo depois do total.
            int depoisDeMortes = lista.FindIndex(c => c.Id == "mortes") + 1;
            foreach (TipoMorte tipo in TiposDeMorte.Todos)
            {
                TipoMorte t = tipo;
                lista.Insert(depoisDeMortes++, new Categoria
                {
                    Id = "mt_" + t.Id, Titulo = t.Titulo, Grupo = GrupoMortes, Icone = t.Icone, Unidade = "mortes",
                    Descricao = t.Descricao,
                    Calculo = t.Descricao + " Contado pelo jogo no seu personagem (" + string.Join(", ", t.Contadores.Take(3)) + (t.Contadores.Length > 3 ? "..." : "") + "). Na semana: mortes da semana.",
                    Valor = (j, s) => j.Le(t.Chave, s),
                    Detalhe = (j, s) =>
                    {
                        double total = j.Le(Chave.Mortes, s);
                        return total > 0d ? Numero.Inteiro(100d * j.Le(t.Chave, s) / total) + "% das " + Numero.Inteiro(total) + " mortes" : null;
                    },
                });
            }
            return lista.Where(c => c.Disponivel == null || c.Disponivel(ctx)).ToList();
        }

        /// <summary>A formula da pontuacao geral, lida dos pesos que o servidor esta usando.</summary>
        private static string TextoLenda(RankingContexto ctx)
        {
            var linhas = new List<string>();
            foreach (KeyValuePair<string, double> peso in ctx.PesosLenda)
            {
                if (peso.Value <= 0d || peso.Key == Chave.Domados) continue;
                if (peso.Key == Chave.Online)
                    linhas.Add(Numero.Peso(peso.Value * 3600d) + " por hora de jogo");
                else if (peso.Key == Chave.Distancia)
                    linhas.Add(Numero.Peso(peso.Value * 1000d) + " por km percorrido");
                else if (peso.Key == Chave.Navegacao)
                    linhas.Add(Numero.Peso(peso.Value * 1000d) + " por km navegado");
                else
                    linhas.Add(Numero.Peso(peso.Value) + " por " + NomeUnidade(peso.Key));
            }
            return "Pontos: " + string.Join("; ", linhas) + ". Na semana vale só o que você fez desde segunda-feira.";
        }

        /// <summary>A causa que mais matou o jogador, para a coluna de detalhe das mortes.</summary>
        private static string CausaPrincipal(RankingJogador j, bool semana)
        {
            TipoMorte melhor = null;
            double maior = 0d;
            foreach (TipoMorte t in TiposDeMorte.Todos)
            {
                double v = j.Le(t.Chave, semana);
                if (v > maior) { maior = v; melhor = t; }
            }
            if (melhor == null) return null;
            return "mais: " + melhor.Titulo.Replace("Mortes por ", "") + " (" + Numero.Inteiro(maior) + ")";
        }

        public static string NomeUnidade(string chave)
        {
            switch (chave)
            {
                case Chave.Habilidades: return "nível de habilidade";
                case Chave.Chefes: return "chefe diferente derrotado";
                case Chave.ChefesAbates: return "abate de chefe";
                case Chave.Monstros: return "monstro abatido";
                case Chave.PvpAbates: return "abate PvP";
                case Chave.Guerra: return "ponto de guerra";
                case Chave.Construcoes: return "peça construída";
                case Chave.Fabricacoes: return "item fabricado";
                case Chave.Arvores: return "árvore derrubada";
                case Chave.Mineracao: return "golpe de picareta";
                case Chave.Domados: return "animal domado";
                case Chave.Nivel: return "nível de personagem";
                case Chave.ArenaVitorias: return "vitória na arena";
                case Chave.Missoes: return "missão";
                default: return chave;
            }
        }

        private static string DetalheLenda(RankingJogador j, bool semana, RankingContexto ctx)
        {
            // As tres maiores fontes de pontos do jogador: mostra "do que" e feita a lenda dele.
            var partes = new List<KeyValuePair<string, double>>();
            foreach (KeyValuePair<string, double> peso in ctx.PesosLenda)
            {
                if (peso.Value <= 0d || peso.Key == Chave.Domados) continue;
                double pontos = j.Le(peso.Key, semana) * peso.Value;
                if (pontos >= 1d) partes.Add(new KeyValuePair<string, double>(peso.Key, pontos));
            }
            if (partes.Count == 0) return null;
            double total = partes.Sum(p => p.Value);
            // Fatia abaixo de 5% nao diz nada ("viagens 0%").
            return string.Join(" · ", partes.Where(p => p.Value / total >= 0.05d).OrderByDescending(p => p.Value).Take(3)
                .Select(p => NomeCurto(p.Key) + " " + Numero.Inteiro(Math.Round(100d * p.Value / total)) + "%"));
        }

        public static string NomeCurto(string chave)
        {
            switch (chave)
            {
                case Chave.Habilidades: return "habilidades";
                case Chave.Chefes: return "chefes";
                case Chave.ChefesAbates: return "chefes";
                case Chave.Monstros: return "caça";
                case Chave.PvpAbates: return "PvP";
                case Chave.Guerra: return "guerra";
                case Chave.Construcoes: return "construção";
                case Chave.Fabricacoes: return "artesanato";
                case Chave.Arvores: return "lenha";
                case Chave.Mineracao: return "mineração";
                case Chave.Distancia: return "viagens";
                case Chave.Navegacao: return "mar";
                case Chave.Domados: return "domar";
                case Chave.Online: return "tempo";
                case Chave.Nivel: return "nível";
                case Chave.ArenaVitorias: return "arena";
                case Chave.Missoes: return "missões";
                default: return chave;
            }
        }
    }

    // ================================================================= classificacao

    internal sealed class Linha
    {
        public RankingJogador Jogador;
        public double Valor;
        /// <summary>1, 2, 2, 4: empate divide a posicao.</summary>
        public int Posicao;
    }

    internal static class Classificacao
    {
        /// <summary>
        /// Quem tem mais que zero na categoria, do maior para o menor (empate: o nome). Oculto
        /// pelo admin fica de fora.
        /// </summary>
        public static List<Linha> Classificar(IEnumerable<RankingJogador> jogadores, Categoria categoria, bool semana)
        {
            var linhas = new List<Linha>();
            foreach (RankingJogador j in jogadores)
            {
                if (j == null || j.Fora) continue;
                double valor = categoria.Valor(j, semana);
                if (valor <= 0d || double.IsNaN(valor)) continue;
                linhas.Add(new Linha { Jogador = j, Valor = valor });
            }
            linhas.Sort((a, b) =>
            {
                int porValor = b.Valor.CompareTo(a.Valor);
                return porValor != 0 ? porValor : string.Compare(a.Jogador.Nome, b.Jogador.Nome, StringComparison.OrdinalIgnoreCase);
            });
            for (int i = 0; i < linhas.Count; i++)
                linhas[i].Posicao = i > 0 && linhas[i].Valor == linhas[i - 1].Valor ? linhas[i - 1].Posicao : i + 1;
            return linhas;
        }

        /// <summary>Quantas posicoes subiu desde a foto do dia (negativo = desceu; null = nao estava).</summary>
        public static int? Movimento(Linha linha, string categoria)
        {
            int antes;
            if (linha == null || !linha.Jogador.PosOntem.TryGetValue(categoria, out antes)) return null;
            return antes - linha.Posicao;
        }

        public sealed class LinhaGuilda
        {
            public string Guilda;
            public int Membros;
            public double Valor;
            public int Posicao;
            public RankingJogador Destaque;
            public double ValorDestaque;
        }

        /// <summary>Guildas pela soma da categoria entre os membros, com o melhor de cada uma.</summary>
        public static List<LinhaGuilda> Guildas(IEnumerable<RankingJogador> jogadores, Categoria categoria, bool semana)
        {
            var porGuilda = new Dictionary<string, LinhaGuilda>(StringComparer.OrdinalIgnoreCase);
            foreach (RankingJogador j in jogadores)
            {
                if (j == null || j.Fora || string.IsNullOrEmpty(j.Guilda)) continue;
                LinhaGuilda g;
                if (!porGuilda.TryGetValue(j.Guilda, out g))
                {
                    g = new LinhaGuilda { Guilda = j.Guilda };
                    porGuilda[j.Guilda] = g;
                }
                g.Membros++;
                double valor = Math.Max(0d, categoria.Valor(j, semana));
                g.Valor += valor;
                if (g.Destaque == null || valor > g.ValorDestaque)
                {
                    g.Destaque = j;
                    g.ValorDestaque = valor;
                }
            }
            List<LinhaGuilda> lista = porGuilda.Values.Where(g => g.Valor > 0d)
                .OrderByDescending(g => g.Valor).ThenBy(g => g.Guilda, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < lista.Count; i++)
                lista[i].Posicao = i > 0 && lista[i].Valor == lista[i - 1].Valor ? lista[i - 1].Posicao : i + 1;
            return lista;
        }
    }

    // ====================================================================== numeros

    /// <summary>Numeros no jeito brasileiro: 12.345 e 3,5.</summary>
    internal static class Numero
    {
        private static readonly CultureInfo Br = CriarCultura();

        private static CultureInfo CriarCultura()
        {
            var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            c.NumberFormat.NumberGroupSeparator = ".";
            c.NumberFormat.NumberDecimalSeparator = ",";
            return c;
        }

        public static string Inteiro(double v) => Math.Floor(v + 1e-9).ToString("#,0", Br);

        public static string Decimal(double v, int casas) =>
            v.ToString("#,0." + new string('0', Math.Max(1, casas)), Br);

        /// <summary>1.234 / 12,3 mil / 4,56 mi: para caber no podio.</summary>
        public static string Compacto(double v)
        {
            double a = Math.Abs(v);
            if (a >= 1e9) return (v / 1e9).ToString("0.##", Br) + " bi";
            if (a >= 1e6) return (v / 1e6).ToString("0.##", Br) + " mi";
            if (a >= 1e5) return (v / 1e3).ToString("0", Br) + " mil";
            return Inteiro(v);
        }

        /// <summary>Peso de pontuacao sem zeros a toa: 0,5 / 2 / 0,0005.</summary>
        public static string Peso(double v) => v.ToString("0.####", Br);

        public static string Distancia(double metros)
        {
            if (metros < 1000d) return Inteiro(metros) + " m";
            return (metros / 1000d).ToString(metros < 100000d ? "#,0.0" : "#,0", Br) + " km";
        }

        public static string Duracao(double segundos)
        {
            long minutos = (long)Math.Floor(Math.Max(0d, segundos) / 60d);
            long horas = minutos / 60;
            if (horas <= 0) return minutos + " min";
            if (horas < 100) return horas + " h " + (minutos % 60).ToString("00", Br) + " min";
            return Inteiro(horas) + " h";
        }

        public static bool TentarLer(string texto, out double valor)
        {
            valor = 0d;
            if (string.IsNullOrEmpty(texto)) return false;
            return double.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        public static string Gravar(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    }

    // ===================================================================== arquivo

    /// <summary>
    /// Arquivo de texto do ranking: uma linha por registro, campos chave=valor separados por TAB,
    /// como o do PvP. "R" e o cabecalho, "J" um jogador, "F" um feito. Os mapas de numeros vao
    /// num campo so, "chave=valor;chave=valor" (o valor vem depois do ultimo '=').
    /// </summary>
    internal static class RankingFormato
    {
        public const int Versao = 1;

        public static string Escrever(RankingBase dados)
        {
            var sb = new StringBuilder();
            sb.Append("R\tversao=").Append(Versao).Append("\tdiaFoto=").Append(dados.DiaFoto).Append('\n');
            foreach (RankingJogador j in dados.Jogadores)
            {
                sb.Append("J\tid=").Append(j.Id.ToString(CultureInfo.InvariantCulture));
                Campo(sb, "nome", j.Nome);
                Campo(sb, "guilda", j.Guilda);
                if (j.Aesir) sb.Append("\taesir=1");
                if (j.Oculto) sb.Append("\toculto=1");
                Campo(sb, "primeira", Numero.Gravar(j.PrimeiraVez));
                Campo(sb, "ultima", Numero.Gravar(j.UltimaVez));
                Campo(sb, "relato", Numero.Gravar(j.UltimoRelato));
                if (j.UltimoPerfil > 0d) Campo(sb, "perfil", Numero.Gravar(j.UltimoPerfil));
                sb.Append("\tsemana=").Append(j.Semana.ToString(CultureInfo.InvariantCulture));
                Campo(sb, "v", Mapa(j.Valores));
                Campo(sb, "b", Mapa(j.BaseSemana));
                Campo(sb, "p", Mapa(j.PosOntem));
                sb.Append('\n');
            }
            foreach (RankingFeito f in dados.Feitos)
            {
                sb.Append("F");
                Campo(sb, "chefe", f.Chefe);
                Campo(sb, "id", f.Id.ToString(CultureInfo.InvariantCulture));
                Campo(sb, "nome", f.Nome);
                Campo(sb, "guilda", f.Guilda);
                Campo(sb, "em", Numero.Gravar(f.Em));
                if (f.Antigo) sb.Append("\tantigo=1");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static RankingBase Ler(string texto, out int ignoradas)
        {
            ignoradas = 0;
            var dados = new RankingBase();
            if (string.IsNullOrEmpty(texto)) return dados;

            foreach (string bruta in texto.Split('\n'))
            {
                string linha = bruta.TrimEnd('\r');
                if (linha.Length == 0 || linha[0] == '#') continue;
                string[] partes = linha.Split('\t');
                var campos = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 1; i < partes.Length; i++)
                {
                    int igual = partes[i].IndexOf('=');
                    if (igual > 0) campos[partes[i].Substring(0, igual)] = partes[i].Substring(igual + 1);
                }

                try
                {
                    switch (partes[0])
                    {
                        case "R":
                            dados.DiaFoto = Int(campos, "diaFoto");
                            break;
                        case "J":
                        {
                            long id;
                            if (!long.TryParse(Texto(campos, "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || id == 0L)
                            {
                                ignoradas++;
                                break;
                            }
                            if (dados.Achar(id) != null) { ignoradas++; break; }
                            var j = new RankingJogador
                            {
                                Id = id,
                                Nome = Texto(campos, "nome"),
                                Guilda = Texto(campos, "guilda"),
                                Aesir = Texto(campos, "aesir") == "1",
                                Oculto = Texto(campos, "oculto") == "1",
                                Admin = Texto(campos, "admin") == "1",
                                PrimeiraVez = Dbl(campos, "primeira"),
                                UltimaVez = Dbl(campos, "ultima"),
                                UltimoRelato = Dbl(campos, "relato"),
                                UltimoPerfil = Dbl(campos, "perfil"),
                                Semana = Int(campos, "semana"),
                            };
                            LerMapa(Texto(campos, "v"), j.Valores);
                            LerMapa(Texto(campos, "b"), j.BaseSemana);
                            var pos = new Dictionary<string, double>(StringComparer.Ordinal);
                            LerMapa(Texto(campos, "p"), pos);
                            foreach (KeyValuePair<string, double> p in pos) j.PosOntem[p.Key] = (int)p.Value;
                            dados.Jogadores.Add(j);
                            break;
                        }
                        case "F":
                        {
                            string chefe = Texto(campos, "chefe");
                            if (chefe.Length == 0 || dados.Feito(chefe) != null) { ignoradas++; break; }
                            long id;
                            long.TryParse(Texto(campos, "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
                            dados.Feitos.Add(new RankingFeito
                            {
                                Chefe = chefe,
                                Id = id,
                                Nome = Texto(campos, "nome"),
                                Guilda = Texto(campos, "guilda"),
                                Em = Dbl(campos, "em"),
                                Antigo = Texto(campos, "antigo") == "1",
                            });
                            break;
                        }
                        default:
                            ignoradas++;
                            break;
                    }
                }
                catch (Exception)
                {
                    ignoradas++;
                }
            }
            return dados;
        }

        public static string Limpar(string valor) =>
            string.IsNullOrEmpty(valor) ? "" : valor.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        private static void Campo(StringBuilder sb, string nome, string valor) =>
            sb.Append('\t').Append(nome).Append('=').Append(Limpar(valor));

        public static string Mapa(IDictionary<string, double> mapa)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, double> par in mapa)
            {
                if (par.Key.IndexOf(';') >= 0 || par.Key.IndexOf('=') >= 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(par.Key).Append('=').Append(Numero.Gravar(par.Value));
            }
            return sb.ToString();
        }

        private static string Mapa(IDictionary<string, int> mapa) =>
            Mapa(mapa.ToDictionary(p => p.Key, p => (double)p.Value, StringComparer.Ordinal));

        public static void LerMapa(string texto, IDictionary<string, double> destino)
        {
            if (string.IsNullOrEmpty(texto)) return;
            foreach (string parte in texto.Split(';'))
            {
                int igual = parte.LastIndexOf('=');
                if (igual <= 0) continue;
                double v;
                if (double.TryParse(parte.Substring(igual + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    destino[parte.Substring(0, igual)] = v;
            }
        }

        private static string Texto(Dictionary<string, string> campos, string nome)
        {
            string v;
            return campos.TryGetValue(nome, out v) ? v : "";
        }

        private static int Int(Dictionary<string, string> campos, string nome)
        {
            int v;
            return int.TryParse(Texto(campos, nome), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static double Dbl(Dictionary<string, string> campos, string nome)
        {
            double v;
            return double.TryParse(Texto(campos, nome), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0d;
        }
    }
}
