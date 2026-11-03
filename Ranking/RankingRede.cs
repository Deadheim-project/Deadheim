using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.Ranking
{
    /// <summary>
    /// Ranking do mundo. O servidor guarda um registro por personagem
    /// (BepInEx/config/Deadheim/ranking-&lt;mundo&gt;.txt) e junta tres fontes:
    ///
    /// - o que so ele sabe: abates e mortes de PvP (PvpStore), e o tempo conectado, que ele mesmo conta;
    /// - o que o perfil do jogador ja registra (o jogo conta abates, construcoes, distancia,
    ///   arvores, chefes...) e as habilidades, que cada cliente relata a cada minuto. O servidor
    ///   confere cada numero (RankingRegras): contador nao desce nem sobe mais rapido do que da
    ///   para jogar, e nada que e do servidor e aceito do cliente;
    /// - o primeiro a derrotar cada chefe (feitos do mundo).
    ///
    /// Quem abre a janela recebe uma foto inteira (comprimida, guardada por alguns segundos e
    /// dividida entre quem pede) e classifica no proprio PC: trocar de categoria, de periodo ou
    /// buscar um nome e instantaneo. Com a janela aberta a foto e renovada sozinha.
    /// </summary>
    [HarmonyPatch]
    internal static class RankingRede
    {
        private const string ParaServidor = "DH_Ranking_ToServer";
        private const string ParaCliente = "DH_Ranking_ToClient";

        private const string OpRelato = "relato";
        private const string OpPedir = "pedir";
        private const string OpAdmin = "admin";
        private const string OpFoto = "foto";
        private const string OpMensagem = "msg";
        private const string OpFeito = "feito";

        private const int VersaoFoto = 2;
        private const float RelatoCadaSeg = 60f;
        private const float RelatoMinimoSeg = 15f;
        private const float PedidoMinimoSeg = 3f;
        private const float FotoValeSeg = 8f;

        public static ConfigEntry<bool> Ativo;
        public static ConfigEntry<string> PesosLenda;
        public static ConfigEntry<bool> MostrarRiqueza;
        public static ConfigEntry<float> FusoHoras;
        public static ConfigEntry<int> DiasInativo;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Ranking";
            Ativo = Plugin.Synced(config.Bind(secao, "RankingAtivo", true,
                "Liga o Ranking (Central do inventario e ESC > Ranking)."));
            PesosLenda = Plugin.Synced(config.Bind(secao, "PesosLenda", Categorias.PesosPadrao,
                "Pontos da categoria Lenda: quanto vale cada unidade de cada numero, separados por |. " +
                "skills (nivel de habilidade), chefes (chefe diferente), chefes.k (abate de chefe), monstros, pvp.k (abate PvP), " +
                "guerra (ponto de guerra), build (peca), craft (item), arvores, mina (golpe de picareta), dist e mar (metro), " +
                "domados, online (segundo), nivel (EpicMMO). Chave fora da lista ou peso 0 nao conta."));
            MostrarRiqueza = Plugin.Synced(config.Bind(secao, "MostrarRiqueza", true,
                "Mostra a categoria Fortuna (moedas na bolsa)."));
            FusoHoras = Plugin.Synced(config.Bind(secao, "FusoHoras", -3f,
                new ConfigDescription("Fuso do servidor em horas (Brasilia = -3): a semana do ranking vira na segunda 00:00 dele e as setas comparam com a meia-noite.",
                    new AcceptableValueRange<float>(-12f, 14f))));
            DiasInativo = Plugin.Synced(config.Bind(secao, "DiasInativo", 60,
                new ConfigDescription("Quem nao entra ha mais que estes dias sai do ranking (os dados ficam guardados e voltam quando ele entrar). 0 = nunca sai.",
                    new AcceptableValueRange<int>(0, 3650))));
        }

        public static bool Ligado => Ativo == null || Ativo.Value;

        private static bool _registrado;

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegistroPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ParaServidor, NoServidor);
                ZRoutedRpc.instance.Register<ZPackage>(ParaCliente, NoCliente);
                _registrado = true;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Postfix()
            {
                _registrado = false;
                LimparCliente();
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandosPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("ranking", "abre o Ranking do mundo", args =>
                {
                    if (Player.m_localPlayer == null) { args.Context?.AddString("Entre no mundo primeiro."); return; }
                    RankingJanela.Abrir(RankingJanela.Origem.Comando);
                });
                new Terminal.ConsoleCommand("rankingadmin", "(admin) ocultar <nome> | mostrar <nome> | apagar <nome>: tira do ranking, devolve ou apaga os dados", args =>
                {
                    if (args.Length < 3)
                    {
                        args.Context?.AddString("Uso: /rankingadmin ocultar|mostrar|apagar <nome do personagem>");
                        return;
                    }
                    ZPackage pkg = Pacote(OpAdmin);
                    pkg.Write(args[1].ToLowerInvariant());
                    pkg.Write(string.Join(" ", args.Args.Skip(2)));
                    if (!Enviar(pkg)) args.Context?.AddString("Sem conexao com o servidor.");
                });
            }
        }

        /// <summary>Todo frame, pelo Plugin.Update.</summary>
        public static void Tick()
        {
            if (ZNet.instance == null) return;
            if (ZNet.instance.IsServer()) TickServidor();
            if (Player.m_localPlayer != null && !Application.isBatchMode) TickCliente();
        }

        // =================================================================== cliente

        /// <summary>A ultima foto recebida (null antes da primeira).</summary>
        public static Foto Atual { get; private set; }
        /// <summary>Time.unscaledTime da chegada da foto atual.</summary>
        public static float RecebidaEm { get; private set; }
        /// <summary>Muda a cada foto nova: a janela redesenha quando ve outro numero.</summary>
        public static int Versao { get; private set; }
        public static event Action<string> Mensagem;

        private static float _proximoRelato = 5f;
        private static float _ultimoPedido = -999f;

        private static void LimparCliente()
        {
            Atual = null;
            Versao++;
            _proximoRelato = 5f;
            _ultimoPedido = -999f;
        }

        private static void TickCliente()
        {
            if (!Ligado || !_registrado) return;
            if (Time.unscaledTime < _proximoRelato) return;
            _proximoRelato = Time.unscaledTime + RelatoCadaSeg;
            Relatar();
        }

        /// <summary>
        /// Pede a foto ao servidor, se a atual tem mais de <paramref name="idadeMaxima"/> segundos.
        /// Antes, relata os proprios numeros: quem abre a janela se ve atualizado.
        /// </summary>
        public static void Pedir(float idadeMaxima = 0f)
        {
            if (!Ligado || !_registrado) return;
            if (Atual != null && Time.unscaledTime - RecebidaEm < idadeMaxima) return;
            if (Time.unscaledTime - _ultimoPedido < PedidoMinimoSeg) return;
            _ultimoPedido = Time.unscaledTime;

            if (Time.unscaledTime >= _proximoRelato - RelatoCadaSeg + RelatoMinimoSeg)
            {
                _proximoRelato = Time.unscaledTime + RelatoCadaSeg;
                Relatar();
            }
            Enviar(Pacote(OpPedir));
        }

        private static void Relatar()
        {
            Dictionary<string, double> relato = ColetarRelato();
            if (relato == null || relato.Count == 0) return;

            ZPackage pkg = Pacote(OpRelato);
            pkg.Write(relato.Count);
            foreach (KeyValuePair<string, double> par in relato)
            {
                pkg.Write(par.Key);
                pkg.Write(BitConverter.DoubleToInt64Bits(par.Value));
            }
            string guilda = null;
            try { guilda = Pvp.PvpGuilds.GuildOf(Player.m_localPlayer); }
            catch (Exception) { }
            pkg.Write(guilda ?? "");
            Enviar(pkg);
        }

        /// <summary>
        /// Os numeros do jogador local. O perfil do jogo (PlayerProfile.m_playerStats) e lido por
        /// reflexao: ele mudou de forma entre versoes (dicionario ou lista de dicionarios por
        /// preset de mundo) e um nome que sumir zera so aquela categoria, sem derrubar o resto.
        /// </summary>
        internal static Dictionary<string, double> ColetarRelato()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return null;
            var relato = new Dictionary<string, double>(StringComparer.Ordinal);

            try
            {
                double soma = 0d, maior = 0d;
                foreach (Skills.Skill skill in player.GetSkills().GetSkillList())
                {
                    if (skill == null) continue;
                    soma += skill.m_level;
                    if (skill.m_level > maior) maior = skill.m_level;
                }
                relato[Chave.Habilidades] = Math.Floor(soma);
                relato[Chave.HabilidadeMaior] = Math.Floor(maior);
            }
            catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: habilidades ilegiveis: " + ex.Message); }

            try
            {
                double moedas = 0d;
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                    if (Moedas.EhMoeda(item)) moedas += item.m_stack;
                relato[Chave.Moedas] = moedas;
            }
            catch (Exception) { }

            try
            {
                int nivel = EpicMMOApi.GetLevel();
                if (nivel > 0) relato[Chave.Nivel] = nivel;
            }
            catch (Exception) { }

            try
            {
                PlayerProfile perfil = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
                RankingPerfil.LerEstatisticas(perfil, relato);
            }
            catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: perfil ilegivel: " + ex.Message); }

            return relato;
        }

        public static bool PedirAdmin(string acao, string nome)
        {
            ZPackage pkg = Pacote(OpAdmin);
            pkg.Write(acao);
            pkg.Write(nome ?? "");
            return Enviar(pkg);
        }

        private static void NoCliente(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            try
            {
                string op = pkg.ReadString();
                if (op == OpFoto)
                {
                    Foto foto = Foto.Ler(new ZPackage(Descomprimir(pkg.ReadByteArray())));
                    if (foto == null) return;
                    Atual = foto;
                    RecebidaEm = Time.unscaledTime;
                    Versao++;
                }
                else if (op == OpMensagem || op == OpFeito)
                {
                    string texto = pkg.ReadString();
                    // O nome do chefe vem como token ($enemy_eikthyr): cada um le na propria lingua.
                    if (Localization.instance != null) texto = Localization.instance.Localize(texto);
                    Chat.instance?.AddString("[Ranking] " + texto);
                    if (op == OpFeito) Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);
                    Mensagem?.Invoke(texto);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Ranking: resposta do servidor ilegivel: " + ex);
            }
        }

        // ================================================================== servidor

        private static RankingBase _dados;
        private static string _caminho;
        private static bool _sujo;
        private static float _proximoSalvar;
        private static float _proximoMinuto;
        private static float _proximoOnline;
        private static float _ultimoOnline;
        private static byte[] _fotoPronta;
        private static float _fotoProntaEm = -999f;
        // Sem o RaidSystem nao ha Ranking de Guerra: a categoria fica sempre oculta (campo mantido no pacote de rede).
        private static bool _temGuerra;
        private static readonly Dictionary<long, float> _ultimoRelatoDe = new Dictionary<long, float>();
        private static readonly Dictionary<long, float> _ultimoPedidoDe = new Dictionary<long, float>();

        private static double Agora => Pvp.PvpState.Now;
        private static double Fuso => FusoHoras != null ? FusoHoras.Value : -3f;

        private static string Arquivo()
        {
            string mundo = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) mundo = mundo.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "ranking-" + mundo + ".txt");
        }

        private static RankingBase Dados
        {
            get
            {
                string caminho = Arquivo();
                if (_dados != null && caminho == _caminho) return _dados;
                if (_dados != null && _sujo) Salvar();
                _caminho = caminho;
                _dados = null;
                int ignoradas = 0;
                try
                {
                    if (File.Exists(caminho)) _dados = RankingFormato.Ler(File.ReadAllText(caminho), out ignoradas);
                }
                catch (Exception ex)
                {
                    string copia = caminho + ".corrompido-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    try { File.Copy(caminho, copia, true); } catch { }
                    Debug.LogError($"[Deadheim] Ranking: {caminho} ilegivel ({ex.Message}); copia em {copia}, comecando vazio.");
                }
                if (_dados == null) _dados = new RankingBase();
                if (ignoradas > 0)
                {
                    string copia = caminho + ".linhas-ignoradas-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    try { File.Copy(caminho, copia, true); } catch { }
                    Debug.LogWarning($"[Deadheim] Ranking: {ignoradas} linha(s) ilegivel(is) em {caminho}; o original ficou em {copia}.");
                }
                _sujo = false;
                Debug.Log($"[Deadheim] Ranking carregado de {caminho}: {_dados.Jogadores.Count} jogador(es), {_dados.Feitos.Count} feito(s).");
                return _dados;
            }
        }

        private static void Salvar()
        {
            if (_dados == null || string.IsNullOrEmpty(_caminho)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_caminho));
                string temp = _caminho + ".tmp";
                File.WriteAllText(temp, RankingFormato.Escrever(_dados));
                // Copy + Delete e nao File.Replace: o servidor roda Mono em Linux (igual ao PvpStore).
                File.Copy(temp, _caminho, true);
                File.Delete(temp);
                _sujo = false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Ranking: nao foi possivel salvar " + _caminho + ": " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SaveWorld))]
        private static class SalvarMundoPatch
        {
            private static void Postfix()
            {
                if (ZNet.instance != null && ZNet.instance.IsServer() && _sujo) Salvar();
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        private static class DesligarPatch
        {
            private static void Prefix()
            {
                if (ZNet.instance != null && ZNet.instance.IsServer() && _sujo) Salvar();
                _dados = null;
                _caminho = null;
                _fotoPronta = null;
                _ultimoRelatoDe.Clear();
                _ultimoPedidoDe.Clear();
                RankingImportar.Limpar();
            }
        }

        /// <summary>O contexto das categorias, com a config de agora (pesos, chefes, mods presentes).</summary>
        internal static RankingContexto Contexto(bool temGuerra, bool temNivel, bool temArena, bool temMissoes)
        {
            return new RankingContexto
            {
                OrdemChefes = Pvp.PvpBosses.Order,
                PesosLenda = RankingContexto.LerPesos(PesosLenda != null ? PesosLenda.Value : Categorias.PesosPadrao),
                TemGuerra = temGuerra,
                TemNivel = temNivel,
                TemArena = temArena,
                TemMissoes = temMissoes,
                MostrarRiqueza = MostrarRiqueza == null || MostrarRiqueza.Value,
            };
        }

        private static void TickServidor()
        {
            if (!Ligado) return;
            float agora = Time.realtimeSinceStartup;

            // Tempo conectado: a cada 10 s, para cada personagem online.
            if (agora >= _proximoOnline)
            {
                float passou = _ultimoOnline > 0f ? Mathf.Min(agora - _ultimoOnline, 30f) : 0f;
                _ultimoOnline = agora;
                _proximoOnline = agora + 10f;
                ContarOnline(passou);
            }

            // O que o mundo ja tem guardado: perfis dos personagens, arena e missoes do NpcValheim.
            try
            {
                double hora = Agora;
                if (RankingImportar.Tick(Dados, hora, RankingTempo.Semana(hora, Fuso), EstaOnline, RegistrarFeito)) _sujo = true;
            }
            catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: falha ao importar: " + ex.Message); }

            if (agora >= _proximoMinuto)
            {
                _proximoMinuto = agora + 60f;
                try { AtualizarDoServidor(); }
                catch (Exception ex) { Debug.LogWarning("[Deadheim] Ranking: falha ao juntar PvP e guerra: " + ex.Message); }
                VirarPeriodos();
            }

            if (_sujo && agora >= _proximoSalvar)
            {
                _proximoSalvar = agora + 120f;
                Salvar();
            }
        }

        private static HashSet<long> _online = new HashSet<long>();
        private static float _onlineEm = -999f;

        private static bool EstaOnline(long id)
        {
            if (Time.realtimeSinceStartup - _onlineEm > 2f)
            {
                _onlineEm = Time.realtimeSinceStartup;
                _online = new HashSet<long>(Pvp.PvpPeer.Online().Select(p => p.PlayerId));
            }
            return _online.Contains(id);
        }

        /// <summary>
        /// Primeiro do mundo a derrotar um chefe (relato ao vivo ou perfil importado). Feito de
        /// verdade e anunciado; o que ja vinha no historico fica marcado "antes do ranking".
        /// </summary>
        private static void RegistrarFeito(RankingJogador j, string chefe, bool antigo, double em)
        {
            RankingBase dados = Dados;
            if (j == null || string.IsNullOrEmpty(chefe) || dados.Feito(chefe) != null) return;
            dados.Feitos.Add(new RankingFeito { Chefe = chefe, Id = j.Id, Nome = j.Nome, Guilda = j.Guilda, Em = em, Antigo = antigo });
            Debug.Log($"[Deadheim] Ranking: feito do mundo: {j.Nome} e o primeiro a derrotar {chefe}" + (antigo ? " (antes do ranking)." : "."));
            if (!antigo) AnunciarFeito(j, chefe);
            _sujo = true;
        }

        private static void ContarOnline(float segundos)
        {
            RankingBase dados = Dados;
            double agora = Agora;
            foreach (Pvp.PvpPeer peer in Pvp.PvpPeer.Online())
            {
                RankingJogador j = dados.Obter(peer.PlayerId, peer.Name, agora);
                j.UltimaVez = agora;
                j.Admin = Pvp.PvpServer.IsAdminPeer(peer.PeerId);
                // Quem entrou no ranking so pelo historico de PvP passa a ter data de chegada agora.
                if (j.PrimeiraVez <= 0d) j.PrimeiraVez = agora;
                if (segundos > 0f) RankingRegras.DefinirDoServidor(j, Chave.Online, j.Valor(Chave.Online) + segundos);
                _sujo = true;
            }
        }

        /// <summary>PvP (PvpStore) e guerra (RaidSystem) para dentro do ranking, a cada minuto.</summary>
        private static void AtualizarDoServidor()
        {
            RankingBase dados = Dados;
            double agora = Agora;

            if (Pvp.PvpConfig.Active)
            {
                foreach (Pvp.PvpPlayerRecord p in Pvp.PvpStore.Data.players)
                {
                    if (p == null || p.id == 0L) continue;
                    RankingJogador j = dados.Achar(p.id);
                    if (j == null)
                    {
                        // Quem so tem historico de PvP (de antes do ranking) entra pelo PvP.
                        if (p.kills <= 0 && p.deaths <= 0) continue;
                        j = dados.Obter(p.id, p.name, 0d);
                    }
                    if (!string.IsNullOrEmpty(p.guild)) j.Guilda = p.guild;
                    RankingRegras.DefinirDoServidor(j, Chave.PvpAbates, p.kills);
                    RankingRegras.DefinirDoServidor(j, Chave.PvpMortes, p.deaths);
                    RankingRegras.DefinirDoServidor(j, Chave.PvpPk, p.pkKills);
                }
            }

            _sujo = true;
        }

        /// <summary>Semana nova (base da semana) e dia novo (foto das posicoes para as setas).</summary>
        private static void VirarPeriodos()
        {
            RankingBase dados = Dados;
            double agora = Agora;
            int semana = RankingTempo.Semana(agora, Fuso);
            int dia = RankingTempo.Dia(agora, Fuso);

            int viradas = 0;
            foreach (RankingJogador j in dados.Jogadores)
                if (RankingRegras.VirarSemana(j, semana)) viradas++;
            if (viradas > 0)
            {
                _sujo = true;
                Debug.Log($"[Deadheim] Ranking: semana nova para {viradas} jogador(es).");
            }

            if (dados.DiaFoto != dia)
            {
                bool temNivel = dados.Jogadores.Any(j => j.Valor(Chave.Nivel) > 0d);
                RankingRegras.FotoDoDia(dados, Categorias.Todas(Contexto(_temGuerra, temNivel, RankingImportar.TemArena, RankingImportar.TemMissoes)), dia);
                _sujo = true;
                Debug.Log("[Deadheim] Ranking: posicoes do dia guardadas (setas de subiu/desceu).");
            }
        }

        private static void NoServidor(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Ligado) return;
            try
            {
                string op = pkg.ReadString();
                switch (op)
                {
                    case OpRelato: ReceberRelato(sender, pkg); break;
                    case OpPedir: ResponderFoto(sender); break;
                    case OpAdmin: Admin(sender, pkg.ReadString(), pkg.ReadString()); break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Ranking: pacote ilegivel de {sender}: {ex.Message}");
            }
        }

        private static void ReceberRelato(long sender, ZPackage pkg)
        {
            float agora = Time.realtimeSinceStartup;
            float ultimo;
            if (_ultimoRelatoDe.TryGetValue(sender, out ultimo) && agora - ultimo < RelatoMinimoSeg - 1f) return;
            _ultimoRelatoDe[sender] = agora;

            // O id vem da ZDO do personagem (o RemetenteRpc ja garantiu quem mandou), nunca do pacote.
            if (!Pvp.PvpPeer.TryResolve(sender, out Pvp.PvpPeer peer)) return;

            int total = pkg.ReadInt();
            if (total < 0 || total > 200) return;
            var relato = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < total; i++)
            {
                string chave = pkg.ReadString();
                double valor = BitConverter.Int64BitsToDouble(pkg.ReadLong());
                if (!string.IsNullOrEmpty(chave)) relato[chave] = valor;
            }
            string guildaRelatada = pkg.ReadString();

            RankingBase dados = Dados;
            double hora = Agora;
            RankingJogador j = dados.Obter(peer.PlayerId, peer.Name, hora);
            j.UltimaVez = hora;
            j.Guilda = Pvp.PvpBridge.KnownGuild(peer.PlayerId) ?? (string.IsNullOrEmpty(guildaRelatada) ? j.Guilda : RankingFormato.Limpar(guildaRelatada));
            j.Aesir = EhAesir(sender);
            j.Admin = Pvp.PvpServer.IsAdminPeer(sender);

            int semana = RankingTempo.Semana(hora, Fuso);
            RankingRegras.VirarSemana(j, semana);
            bool primeiro = j.UltimoRelato <= 0d;
            List<KeyValuePair<string, bool>> chefesNovos = RankingRegras.Aplicar(j, relato, hora, semana);
            // O primeiro relato entra inteiro (o historico do personagem): fica no log para o admin conferir.
            if (primeiro)
                Debug.Log($"[Deadheim] Ranking: primeiro relato de {j.Nome} ({j.Id}): " +
                          string.Join(" ", j.Valores.Where(p => !p.Key.StartsWith(Chave.PrefixoChefe, StringComparison.Ordinal))
                              .Select(p => p.Key + "=" + Numero.Inteiro(p.Value))));
            foreach (KeyValuePair<string, bool> chefe in chefesNovos) RegistrarFeito(j, chefe.Key, chefe.Value, hora);
            _sujo = true;
        }

        private static bool EhAesir(long peerId)
        {
            try
            {
                if (ZRoutedRpc.instance != null && peerId == ZRoutedRpc.instance.m_id) return VipList.VipListApi.IsLocalPlayerVip();
                string host = ZNet.instance?.GetPeer(peerId)?.m_socket?.GetHostName();
                return !string.IsNullOrEmpty(host) && VipList.VipListApi.IsVip(host);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Primeiro do mundo a derrubar um chefe: todo mundo conectado fica sabendo.</summary>
        private static void AnunciarFeito(RankingJogador j, string chefe)
        {
            ZPackage pkg = Pacote(OpFeito);
            string guilda = string.IsNullOrEmpty(j.Guilda) ? "" : " [" + j.Guilda + "]";
            pkg.Write($"{j.Nome}{guilda} é o primeiro do mundo a derrotar {chefe}! Veja em Ranking > Feitos do mundo.");
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZRoutedRpc.Everybody, ParaCliente, pkg);
        }

        private static void ResponderFoto(long sender)
        {
            float agora = Time.realtimeSinceStartup;
            float ultimo;
            if (_ultimoPedidoDe.TryGetValue(sender, out ultimo) && agora - ultimo < PedidoMinimoSeg - 0.5f) return;
            _ultimoPedidoDe[sender] = agora;

            // A foto guardada e dividida entre quem pede, menos para quem relatou depois dela:
            // quem abre a janela tem que se ver com os numeros que acabou de mandar.
            float relatou;
            bool relatouDepois = _ultimoRelatoDe.TryGetValue(sender, out relatou) && relatou > _fotoProntaEm;
            if (_fotoPronta == null || agora - _fotoProntaEm > FotoValeSeg || relatouDepois)
            {
                _fotoPronta = Comprimir(MontarFoto().GetArray());
                _fotoProntaEm = agora;
            }
            ZPackage pkg = Pacote(OpFoto);
            pkg.Write(_fotoPronta);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ParaCliente, pkg);
        }

        private static ZPackage MontarFoto()
        {
            RankingBase dados = Dados;
            double agora = Agora;
            int dias = DiasInativo != null ? DiasInativo.Value : 0;
            var online = new HashSet<long>(Pvp.PvpPeer.Online().Select(p => p.PlayerId));

            List<RankingJogador> visiveis = dados.Jogadores
                .Where(j => !j.Fora && (online.Contains(j.Id) || dias <= 0 || agora - j.UltimaVez <= dias * 86400d))
                .ToList();

            var foto = new Foto
            {
                Agora = agora,
                Mundo = ZNet.instance != null ? ZNet.instance.GetWorldName() : "",
                InicioSemana = RankingTempo.InicioSemana(RankingTempo.Semana(agora, Fuso), Fuso),
                TemGuerra = _temGuerra,
                TemNivel = visiveis.Any(j => j.Valor(Chave.Nivel) > 0d),
                TemArena = RankingImportar.TemArena,
                TemMissoes = RankingImportar.TemMissoes,
                DiaDoMundo = RankingImportar.DiaDoMundo(),
                ChefesDoMundo = RankingImportar.ChefesDoMundo(),
                Jogadores = visiveis,
                Feitos = dados.Feitos,
                Online = online,
            };
            return foto.Escrever();
        }

        private static void Admin(long sender, string acao, string nome)
        {
            if (!Pvp.PvpServer.IsAdminPeer(sender))
            {
                Responder(sender, "So admins do servidor podem mexer no ranking.");
                return;
            }
            RankingBase dados = Dados;
            List<RankingJogador> achados = dados.Jogadores
                .Where(j => string.Equals(j.Nome, (nome ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (achados.Count == 0)
            {
                Responder(sender, $"Ninguem chamado '{nome}' no ranking.");
                return;
            }

            switch (acao)
            {
                case "ocultar":
                    foreach (RankingJogador j in achados) j.Oculto = true;
                    Responder(sender, $"{achados.Count} personagem(ns) '{nome}' fora do ranking (os dados ficam guardados).");
                    break;
                case "mostrar":
                    foreach (RankingJogador j in achados) j.Oculto = false;
                    Responder(sender, $"{achados.Count} personagem(ns) '{nome}' de volta ao ranking.");
                    break;
                case "apagar":
                    foreach (RankingJogador j in achados) dados.Jogadores.Remove(j);
                    Responder(sender, $"Dados de {achados.Count} personagem(ns) '{nome}' apagados; o que o perfil dele registra volta no proximo relato.");
                    break;
                default:
                    Responder(sender, "Use ocultar, mostrar ou apagar.");
                    return;
            }
            Debug.Log($"[Deadheim] Ranking: admin {sender} -> {acao} '{nome}'.");
            _sujo = true;
            _fotoPronta = null;
        }

        private static void Responder(long peer, string texto)
        {
            ZPackage pkg = Pacote(OpMensagem);
            pkg.Write(texto);
            ZRoutedRpc.instance?.InvokeRoutedRPC(peer, ParaCliente, pkg);
        }

        // ==================================================================== comum

        private static ZPackage Pacote(string op)
        {
            var pkg = new ZPackage();
            pkg.Write(op);
            return pkg;
        }

        private static bool Enviar(ZPackage pkg)
        {
            if (!_registrado || ZRoutedRpc.instance == null) return false;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ParaServidor, pkg);
            return true;
        }

        private static byte[] Comprimir(byte[] dados)
        {
            using (var saida = new MemoryStream())
            {
                using (var deflate = new DeflateStream(saida, System.IO.Compression.CompressionLevel.Optimal))
                    deflate.Write(dados, 0, dados.Length);
                return saida.ToArray();
            }
        }

        private static byte[] Descomprimir(byte[] dados)
        {
            using (var entrada = new MemoryStream(dados))
            using (var deflate = new DeflateStream(entrada, CompressionMode.Decompress))
            using (var saida = new MemoryStream())
            {
                deflate.CopyTo(saida);
                return saida.ToArray();
            }
        }

        // ===================================================================== foto

        /// <summary>
        /// O que o cliente recebe: todos os jogadores visiveis com os numeros, a base da semana e
        /// as posicoes de ontem, e os feitos. As chaves vao numa tabela no comeco e cada numero
        /// leva so o indice dela.
        /// </summary>
        internal sealed class Foto
        {
            public double Agora;
            public string Mundo = "";
            public double InicioSemana;
            public bool TemGuerra;
            public bool TemNivel;
            public bool TemArena;
            public bool TemMissoes;
            /// <summary>Dia do mundo (como o jogo conta) e chefes que o mundo ja derrotou (chaves globais).</summary>
            public int DiaDoMundo;
            public List<string> ChefesDoMundo = new List<string>();
            public List<RankingJogador> Jogadores = new List<RankingJogador>();
            public List<RankingFeito> Feitos = new List<RankingFeito>();
            public HashSet<long> Online = new HashSet<long>();

            public RankingJogador Achar(long id) => Jogadores.FirstOrDefault(j => j.Id == id);

            public ZPackage Escrever()
            {
                var chaves = new List<string>();
                var indice = new Dictionary<string, int>(StringComparer.Ordinal);
                Func<string, int> idx = k =>
                {
                    int i;
                    if (!indice.TryGetValue(k, out i))
                    {
                        i = chaves.Count;
                        chaves.Add(k);
                        indice[k] = i;
                    }
                    return i;
                };
                foreach (RankingJogador j in Jogadores)
                {
                    foreach (string k in j.Valores.Keys) idx(k);
                    foreach (string k in j.BaseSemana.Keys) idx(k);
                    foreach (string k in j.PosOntem.Keys) idx(k);
                }

                var pkg = new ZPackage();
                pkg.Write(VersaoFoto);
                pkg.Write(BitConverter.DoubleToInt64Bits(Agora));
                pkg.Write(Mundo ?? "");
                pkg.Write(BitConverter.DoubleToInt64Bits(InicioSemana));
                pkg.Write(TemGuerra);
                pkg.Write(TemNivel);
                pkg.Write(TemArena);
                pkg.Write(TemMissoes);
                pkg.Write(DiaDoMundo);
                pkg.Write(ChefesDoMundo.Count);
                foreach (string c in ChefesDoMundo) pkg.Write(c ?? "");

                pkg.Write(chaves.Count);
                foreach (string k in chaves) pkg.Write(k);

                pkg.Write(Jogadores.Count);
                foreach (RankingJogador j in Jogadores)
                {
                    pkg.Write(j.Id);
                    pkg.Write(j.Nome ?? "");
                    pkg.Write(j.Guilda ?? "");
                    pkg.Write(j.Aesir);
                    pkg.Write(Online.Contains(j.Id));
                    pkg.Write(BitConverter.DoubleToInt64Bits(j.PrimeiraVez));
                    pkg.Write(BitConverter.DoubleToInt64Bits(j.UltimaVez));
                    EscreverMapa(pkg, j.Valores, idx);
                    EscreverMapa(pkg, j.BaseSemana, idx);
                    pkg.Write(j.PosOntem.Count);
                    foreach (KeyValuePair<string, int> p in j.PosOntem)
                    {
                        pkg.Write(idx(p.Key));
                        pkg.Write(p.Value);
                    }
                }

                pkg.Write(Feitos.Count);
                foreach (RankingFeito f in Feitos)
                {
                    pkg.Write(f.Chefe ?? "");
                    pkg.Write(f.Id);
                    pkg.Write(f.Nome ?? "");
                    pkg.Write(f.Guilda ?? "");
                    pkg.Write(BitConverter.DoubleToInt64Bits(f.Em));
                    pkg.Write(f.Antigo);
                }
                return pkg;
            }

            private static void EscreverMapa(ZPackage pkg, Dictionary<string, double> mapa, Func<string, int> idx)
            {
                pkg.Write(mapa.Count);
                foreach (KeyValuePair<string, double> p in mapa)
                {
                    pkg.Write(idx(p.Key));
                    pkg.Write(BitConverter.DoubleToInt64Bits(p.Value));
                }
            }

            public static Foto Ler(ZPackage pkg)
            {
                int versao = pkg.ReadInt();
                if (versao != VersaoFoto)
                {
                    Debug.LogWarning($"[Deadheim] Ranking: foto na versao {versao}, esta e a {VersaoFoto}. Atualize o Deadheim.");
                    return null;
                }
                var foto = new Foto
                {
                    Agora = BitConverter.Int64BitsToDouble(pkg.ReadLong()),
                    Mundo = pkg.ReadString(),
                    InicioSemana = BitConverter.Int64BitsToDouble(pkg.ReadLong()),
                    TemGuerra = pkg.ReadBool(),
                    TemNivel = pkg.ReadBool(),
                    TemArena = pkg.ReadBool(),
                    TemMissoes = pkg.ReadBool(),
                    DiaDoMundo = pkg.ReadInt(),
                };
                int nChefesMundo = pkg.ReadInt();
                for (int i = 0; i < nChefesMundo; i++) foto.ChefesDoMundo.Add(pkg.ReadString());

                int nChaves = pkg.ReadInt();
                var chaves = new string[nChaves];
                for (int i = 0; i < nChaves; i++) chaves[i] = pkg.ReadString();
                Func<int, string> chave = i => i >= 0 && i < chaves.Length ? chaves[i] : "?";

                int nJogadores = pkg.ReadInt();
                for (int n = 0; n < nJogadores; n++)
                {
                    var j = new RankingJogador
                    {
                        Id = pkg.ReadLong(),
                        Nome = pkg.ReadString(),
                        Guilda = pkg.ReadString(),
                        Aesir = pkg.ReadBool(),
                        Conectado = pkg.ReadBool(),
                        PrimeiraVez = BitConverter.Int64BitsToDouble(pkg.ReadLong()),
                        UltimaVez = BitConverter.Int64BitsToDouble(pkg.ReadLong()),
                    };
                    LerMapa(pkg, j.Valores, chave);
                    LerMapa(pkg, j.BaseSemana, chave);
                    int nPos = pkg.ReadInt();
                    for (int i = 0; i < nPos; i++)
                    {
                        string k = chave(pkg.ReadInt());
                        j.PosOntem[k] = pkg.ReadInt();
                    }
                    if (j.Conectado) foto.Online.Add(j.Id);
                    foto.Jogadores.Add(j);
                }

                int nFeitos = pkg.ReadInt();
                for (int n = 0; n < nFeitos; n++)
                {
                    foto.Feitos.Add(new RankingFeito
                    {
                        Chefe = pkg.ReadString(),
                        Id = pkg.ReadLong(),
                        Nome = pkg.ReadString(),
                        Guilda = pkg.ReadString(),
                        Em = BitConverter.Int64BitsToDouble(pkg.ReadLong()),
                        Antigo = pkg.ReadBool(),
                    });
                }
                return foto;
            }

            private static void LerMapa(ZPackage pkg, Dictionary<string, double> mapa, Func<int, string> chave)
            {
                int n = pkg.ReadInt();
                for (int i = 0; i < n; i++)
                {
                    string k = chave(pkg.ReadInt());
                    mapa[k] = BitConverter.Int64BitsToDouble(pkg.ReadLong());
                }
            }
        }
    }
}
