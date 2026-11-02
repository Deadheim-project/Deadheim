using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Ranking
{
    /// <summary>
    /// Servidor: traz para o ranking o que o mundo ja tem guardado, sem esperar ninguem entrar.
    ///
    /// - Perfis dos personagens (ServerCharacters): todo .fch da pasta de personagens do servidor
    ///   (SaveSystem.GetCharacterFolderPath(Local), nomes Steam_&lt;id&gt;_&lt;nome&gt;.fch, do jeito que o
    ///   ServerCharacters grava e le). De cada um: os contadores do jogo, os chefes, as habilidades,
    ///   as moedas, quando o personagem chegou (criacao do arquivo) e quando jogou por ultimo
    ///   (gravacao), e se e Aesir (o id do arquivo na VipList). Le na subida e a cada
    ///   <see cref="VarrerCadaSeg"/> so os arquivos que mudaram, alguns por segundo.
    /// - NpcValheim: arena (rating pessoal, vitorias e partidas da temporada), missoes concluidas e
    ///   o diretorio de jogadores (ultima vez visto), por reflexao.
    /// - Mundo: o dia e os chefes que o mundo ja derrotou (chaves globais defeated_*).
    /// </summary>
    internal static class RankingImportar
    {
        private const float VarrerCadaSeg = 300f;
        private const float NpcCadaSeg = 120f;
        private const int PerfisPorSegundo = 8;

        private static float _proximaVarredura;
        private static float _proximoLote;
        private static float _proximoNpc;
        private static readonly Queue<string> _fila = new Queue<string>();
        private static readonly Dictionary<string, DateTime> _lidos = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static int _importados, _falhas;
        private static bool _semPastaLogado;

        /// <summary>O NpcValheim respondeu (as categorias Arena e Missoes aparecem).</summary>
        public static bool TemArena { get; private set; }
        public static bool TemMissoes { get; private set; }

        public static void Limpar()
        {
            _fila.Clear();
            _lidos.Clear();
            _proximaVarredura = _proximoLote = _proximoNpc = 0f;
            _importados = _falhas = 0;
            _pasta = null;
            _semPastaLogado = false;
            TemArena = TemMissoes = false;
        }

        /// <summary>Todo frame no servidor (RankingRede.TickServidor). Devolve true se mudou algo.</summary>
        public static bool Tick(RankingBase dados, double agora, int semana, Func<long, bool> online, Action<RankingJogador, string, bool, double> feito)
        {
            bool mudou = false;
            float t = Time.realtimeSinceStartup;

            if (t >= _proximaVarredura)
            {
                _proximaVarredura = t + VarrerCadaSeg;
                Varrer();
            }

            if (_fila.Count > 0 && t >= _proximoLote)
            {
                _proximoLote = t + 1f;
                for (int i = 0; i < PerfisPorSegundo && _fila.Count > 0; i++)
                    if (Importar(_fila.Dequeue(), dados, agora, semana, online, feito)) mudou = true;
                if (_fila.Count == 0 && (_importados > 0 || _falhas > 0))
                {
                    Debug.Log($"[Deadheim] Ranking: {_importados} perfil(is) de personagem importado(s)" +
                              (_falhas > 0 ? $", {_falhas} ilegivel(is) (tenta de novo na proxima varredura)." : "."));
                    _importados = _falhas = 0;
                }
            }

            if (t >= _proximoNpc)
            {
                _proximoNpc = t + NpcCadaSeg;
                try { if (NpcValheim(dados)) mudou = true; }
                catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: falha ao ler o NpcValheim: " + Raiz(ex)); }
            }
            return mudou;
        }

        // ================================================================ perfis

        private static object _fonteLocal;
        private static ConstructorInfo _construtor;
        private static MethodInfo _carregar;

        /// <summary>A pasta onde o ServerCharacters guarda os personagens (a do jogo, fonte Local).</summary>
        private static string _pasta;

        private static string Pasta()
        {
            if (_pasta != null) return _pasta;
            try
            {
                // Pelo nome: GetTypes() num assembly de jogo com mods pode lancar ReflectionTypeLoadException.
                Type fonte = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => { try { return a.GetType("FileHelpers+FileSource", false); } catch (Exception) { return null; } })
                    .FirstOrDefault(x => x != null && x.IsEnum);
                if (fonte == null) return null;
                _fonteLocal = Enum.Parse(fonte, "Local");

                Type save = typeof(PlayerProfile).Assembly.GetType("SaveSystem", false);
                MethodInfo pasta = save?.GetMethod("GetCharacterFolderPath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { fonte }, null);
                string caminho = pasta?.Invoke(null, new[] { _fonteLocal }) as string;
                _construtor = typeof(PlayerProfile).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), fonte }, null);
                _carregar = typeof(PlayerProfile).GetMethod("LoadPlayerFromDisk", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)
                            ?? typeof(PlayerProfile).GetMethod("Load", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                _pasta = caminho;
                return caminho;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim] Ranking: nao achei a pasta de personagens: " + Raiz(ex));
                return null;
            }
        }

        /// <summary>O padrao de nome do ServerCharacters: plataforma_id_nome.fch, sem os backups.</summary>
        internal static bool EhPerfilDoServidor(string arquivo)
        {
            string nome = Path.GetFileName(arquivo ?? "");
            return nome.EndsWith(".fch", StringComparison.Ordinal) && nome.Split('_').Length >= 3 && !nome.Contains("_backup_");
        }

        private static void Varrer()
        {
            string pasta = Pasta();
            if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta) || _construtor == null || _carregar == null)
            {
                if (!_semPastaLogado)
                    Debug.Log("[Deadheim] Ranking: sem pasta de personagens do servidor (" + (pasta ?? "?") + "): o ranking usa so os relatos de quem entra.");
                _semPastaLogado = true;
                return;
            }

            int novos = 0;
            foreach (string arquivo in Directory.GetFiles(pasta, "*.fch", SearchOption.TopDirectoryOnly))
            {
                if (!EhPerfilDoServidor(arquivo)) continue;
                DateTime gravado;
                try { gravado = File.GetLastWriteTimeUtc(arquivo); }
                catch (Exception) { continue; }
                DateTime antes;
                if (_lidos.TryGetValue(arquivo, out antes) && antes == gravado) continue;
                if (_fila.Contains(arquivo)) continue;
                _fila.Enqueue(arquivo);
                novos++;
            }
            if (novos > 0) Debug.Log($"[Deadheim] Ranking: {novos} perfil(is) novo(s) ou alterado(s) em {pasta}.");
        }

        private static bool Importar(string arquivo, RankingBase dados, double agora, int semana, Func<long, bool> online,
            Action<RankingJogador, string, bool, double> feito)
        {
            try
            {
                if (!File.Exists(arquivo)) return false;
                DateTime gravado = File.GetLastWriteTimeUtc(arquivo);
                DateTime criado = File.GetCreationTimeUtc(arquivo);
                string nomeArquivo = Path.GetFileNameWithoutExtension(arquivo);

                // Igual ao ServerCharacters: new PlayerProfile(nome, Local) + LoadPlayerFromDisk.
                var perfil = (PlayerProfile)_construtor.Invoke(new[] { nomeArquivo, _fonteLocal });
                object ok = _carregar.Invoke(perfil, null);
                if (ok is bool b && !b)
                {
                    // Gravando agora (hash nao bate): fica para a proxima varredura.
                    _falhas++;
                    return false;
                }

                long id = perfil.GetPlayerID();
                string nome = perfil.GetName();
                if (id == 0L || string.IsNullOrEmpty(nome))
                {
                    _lidos[arquivo] = gravado;
                    return false;
                }

                var valores = new Dictionary<string, double>(StringComparer.Ordinal);
                RankingPerfil.LerEstatisticas(perfil, valores);
                RankingPerfil.LerPersonagem(perfil, valores);

                double horaArquivo = Segundos(gravado);
                RankingJogador j = dados.Achar(id);
                if (j != null && j.UltimoPerfil >= horaArquivo)
                {
                    _lidos[arquivo] = gravado;
                    return false;
                }
                j = dados.Obter(id, nome, horaArquivo);

                double chegou = Segundos(criado);
                if (chegou > 0d && (j.PrimeiraVez <= 0d || chegou < j.PrimeiraVez)) j.PrimeiraVez = chegou;
                if (horaArquivo > j.UltimaVez) j.UltimaVez = horaArquivo;

                // Steam_7656..._Nome: a VipList aceita "Steam_7656...".
                string[] partes = nomeArquivo.Split('_');
                if (partes.Length >= 3 && !online(id))
                {
                    try { j.Aesir = VipList.VipListApi.IsVip(partes[0] + "_" + partes[1]); }
                    catch (Exception) { }
                }

                string guilda = Pvp.PvpBridge.KnownGuild(id);
                if (!string.IsNullOrEmpty(guilda)) j.Guilda = guilda;

                RankingRegras.VirarSemana(j, semana);
                foreach (KeyValuePair<string, bool> chefe in RankingRegras.AplicarDoPerfil(j, valores, horaArquivo, online(id), semana))
                    feito(j, chefe.Key, chefe.Value, horaArquivo);

                _lidos[arquivo] = gravado;
                _importados++;
                return true;
            }
            catch (Exception ex)
            {
                _falhas++;
                Debug.LogWarning($"[Deadheim] Ranking: perfil {Path.GetFileName(arquivo)} ilegivel: {Raiz(ex)}");
                return false;
            }
        }

        private static double Segundos(DateTime utc) =>
            utc.Year < 2000 ? 0d : (utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        // ============================================================== NpcValheim

        private const BindingFlags Tudo = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static Assembly _npc;
        private static bool _npcProcurado;

        private static Type TipoNpc(string nome)
        {
            if (!_npcProcurado)
            {
                _npcProcurado = true;
                _npc = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "NpcValheim");
            }
            return _npc?.GetType(nome, false);
        }

        private static object Prop(object dono, string nome)
        {
            if (dono == null) return null;
            Type t = dono as Type ?? dono.GetType();
            object alvo = dono is Type ? null : dono;
            PropertyInfo p = t.GetProperty(nome, Tudo);
            if (p != null) return p.GetValue(alvo, null);
            return t.GetField(nome, Tudo)?.GetValue(alvo);
        }

        private static double Num(object dono, string nome)
        {
            object v = Prop(dono, nome);
            try { return v == null ? 0d : Convert.ToDouble(v); }
            catch (Exception) { return 0d; }
        }

        /// <summary>Id de personagem e long grande: passar por double perderia os ultimos digitos.</summary>
        private static long Long(object dono, string nome)
        {
            object v = Prop(dono, nome);
            try { return v == null ? 0L : Convert.ToInt64(v); }
            catch (Exception) { return 0L; }
        }

        private static bool NpcValheim(RankingBase dados)
        {
            if (TipoNpc("NpcValheim.Plugin") == null && _npc == null) return false;
            bool mudou = false;

            // ---- diretorio: quando cada personagem foi visto por ultimo
            Type diretorio = TipoNpc("NpcValheim.Persistence.PlayerDirectory");
            if (diretorio?.GetMethod("All", Tudo)?.Invoke(null, null) is IEnumerable conhecidos)
            {
                foreach (object k in conhecidos)
                {
                    long id = Long(k, "Id");
                    RankingJogador j = id != 0L ? dados.Achar(id) : null;
                    if (j == null) continue;
                    if (Prop(k, "LastSeenUtc") is DateTime visto)
                    {
                        double s = Segundos(DateTime.SpecifyKind(visto, DateTimeKind.Utc));
                        if (s > j.UltimaVez) { j.UltimaVez = s; mudou = true; }
                    }
                }
            }

            // ---- arena: os times e os registros de jogador ficam em memoria no motor da arena
            Type servidorArena = TipoNpc("NpcValheim.Arena.ArenaServer");
            if (servidorArena != null)
            {
                servidorArena.GetMethod("EnsureEngine", Tudo)?.Invoke(null, null);
                object motor = Prop(servidorArena, "Engine");
                if (motor != null && Prop(motor, "Teams") is IEnumerable times)
                {
                    TemArena = true;
                    var porJogador = new Dictionary<long, double[]>(); // rating, vitorias, partidas
                    foreach (object time in times)
                    {
                        if (!(Prop(time, "Members") is IEnumerable membros)) continue;
                        foreach (object m in membros)
                        {
                            long id = Long(m, "PlayerId");
                            if (id == 0L) continue;
                            double[] soma;
                            if (!porJogador.TryGetValue(id, out soma)) porJogador[id] = soma = new double[3];
                            double jogos = Num(m, "SeasonGames");
                            if (jogos > 0d) soma[0] = Math.Max(soma[0], Num(m, "PersonalRating"));
                            soma[1] += Num(m, "SeasonWins");
                            soma[2] += jogos;
                        }
                    }
                    foreach (KeyValuePair<long, double[]> par in porJogador)
                    {
                        RankingJogador j = dados.Achar(par.Key);
                        if (j == null) continue;
                        RankingRegras.DefinirDoServidor(j, Chave.Arena, par.Value[0]);
                        RankingRegras.DefinirDoServidor(j, Chave.ArenaVitorias, par.Value[1]);
                        RankingRegras.DefinirDoServidor(j, Chave.ArenaPartidas, par.Value[2]);
                        mudou = true;
                    }
                }
            }

            // ---- missoes concluidas (as repetiveis contam cada vez)
            Type missoes = TipoNpc("NpcValheim.Persistence.QuestDatabase");
            MethodInfo todas = missoes?.GetMethod("GetAll", Tudo, null, new[] { typeof(long) }, null);
            if (todas != null)
            {
                foreach (RankingJogador j in dados.Jogadores)
                {
                    IEnumerable progresso;
                    try { progresso = todas.Invoke(null, new object[] { j.Id }) as IEnumerable; }
                    catch (Exception) { break; } // banco de missoes ainda fechado: tenta no proximo ciclo
                    if (progresso == null) continue;
                    TemMissoes = true;
                    double concluidas = 0d;
                    foreach (object q in progresso)
                    {
                        double vezes = Num(q, "TimesCompleted");
                        if (vezes <= 0d && string.Equals(Prop(q, "Status")?.ToString(), "Completed", StringComparison.Ordinal)) vezes = 1d;
                        concluidas += vezes;
                    }
                    if (concluidas > 0d || j.Valores.ContainsKey(Chave.Missoes))
                    {
                        RankingRegras.DefinirDoServidor(j, Chave.Missoes, concluidas);
                        mudou = true;
                    }
                }
            }
            return mudou;
        }

        // ================================================================== mundo

        private static readonly Dictionary<string, string> ChaveGlobal = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["$enemy_eikthyr"] = "defeated_eikthyr",
            ["$enemy_gdking"] = "defeated_gdking",
            ["$enemy_bonemass"] = "defeated_bonemass",
            ["$enemy_dragon"] = "defeated_dragon",
            ["$enemy_goblinking"] = "defeated_goblinking",
            ["$enemy_seekerqueen"] = "defeated_queen",
            ["$enemy_fader"] = "defeated_fader",
        };

        /// <summary>Chefes da BossOrder que o mundo ja derrotou (chave global do jogo).</summary>
        public static List<string> ChefesDoMundo()
        {
            var lista = new List<string>();
            if (ZoneSystem.instance == null) return lista;
            foreach (string chefe in Pvp.PvpBosses.Order)
            {
                string chave;
                if (!ChaveGlobal.TryGetValue(chefe, out chave)) chave = "defeated_" + chefe.Replace("$enemy_", "");
                try { if (ZoneSystem.instance.GetGlobalKey(chave)) lista.Add(chefe); }
                catch (Exception) { }
            }
            return lista;
        }

        /// <summary>O dia do mundo, como o jogo conta (tempo do mundo / duracao do dia).</summary>
        public static int DiaDoMundo()
        {
            try
            {
                if (ZNet.instance == null || EnvMan.instance == null || EnvMan.instance.m_dayLengthSec <= 0L) return 0;
                return (int)(ZNet.instance.GetTimeSeconds() / EnvMan.instance.m_dayLengthSec);
            }
            catch (Exception) { return 0; }
        }

        private static string Raiz(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
        }
    }
}
