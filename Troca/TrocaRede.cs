using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deadheim.Troca
{
    /// <summary>
    /// Troca de moedas entre jogadores: o servidor so aceita oferta de Coins (o resto passa pelo
    /// chao ou pelo Correio). O servidor e a fonte da verdade (TrocaMotor); o cliente so
    /// mostra a janela e mexe no proprio inventario. Fluxo do item: o cliente manda a oferta, o
    /// servidor guarda em custodia e responde, SO ENTAO o cliente tira o item do inventario. A
    /// entrega ao destino fica pendente no servidor ate o cliente dizer "recebido" (e e reenviada
    /// no login), entao queda de conexao ou restart nao perdem nem duplicam item.
    /// O item vai por nome, como nos NPCs (Correio, Mercador): nome do prefab, qualidade e
    /// quantidade, que o servidor confere no ObjectDB. Da para oferecer parte de uma pilha, ou
    /// varias pilhas juntas. Quem recebe ganha o item novo (durabilidade cheia; estilo e nome de
    /// quem fez ficam para tras). Item equipado nao entra: tirar pelo nome poderia levar justo ele.
    /// </summary>
    [HarmonyPatch]
    internal static class TrocaRede
    {
        // "2": a troca por nome. Cliente e servidor de versoes diferentes nao se falam em vez de se
        // entenderem errado: um cliente antigo leria a entrega nova como item ilegivel, diria
        // "recebido" e o item sumiria. Assim ela espera no servidor ate o jogador atualizar.
        private const string ToServer = "DH_Troca2_ToServer";
        private const string ToClient = "DH_Troca2_ToClient";

        public static ConfigEntry<bool> Ativo;
        public static ConfigEntry<float> Distancia;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Troca";
            Ativo = Plugin.Synced(config.Bind(secao, "TrocaAtiva", true,
                "Liga a troca de moedas entre jogadores (/trocar)."));
            Distancia = Plugin.Synced(config.Bind(secao, "TrocaDistancia", 10f,
                new ConfigDescription("Distancia maxima, em metros, entre os dois jogadores para pedir a troca. Passando de uma vez e meia isto durante a troca, ela e cancelada.",
                    new AcceptableValueRange<float>(2f, 50f))));
        }

        private static readonly Dictionary<string, string> Erros = new Dictionary<string, string>
        {
            ["desligado"] = "A troca esta desligada neste servidor.",
            ["sem_jogador"] = "Nao achei esse jogador.",
            ["longe"] = "Esse jogador esta longe demais.",
            ["si_mesmo"] = "Voce nao pode trocar consigo mesmo.",
            ["voce_ocupado"] = "Voce ja esta numa troca.",
            ["alvo_ocupado"] = "Esse jogador ja esta numa troca.",
            ["sem_pedido"] = "Nao ha pedido de troca para voce.",
            ["sem_sessao"] = "Nao ha troca aberta.",
            ["item_invalido"] = "Item invalido para troca.",
            ["nao_permitido"] = "A troca e so de moedas.",
            ["cheio"] = "A oferta esta cheia.",
            ["item_repetido"] = "Esse item ja esta na troca.",
            ["item_inexistente"] = "Esse item nao esta na oferta.",
            ["versao_antiga"] = "A oferta mudou; confira de novo.",
            ["vazia"] = "Nada foi oferecido.",
            ["sem_trava"] = "Os dois precisam travar a oferta antes de confirmar.",
            ["aguarde"] = "Aguarde: um dos itens ainda esta sendo guardado.",
            ["nome_ambiguo"] = "Ha mais de um jogador com esse nome; chegue perto e use /trocar sem nome.",
            ["pendencias"] = "Voce tem entregas de troca pendentes; entre de novo no servidor para recebe-las.",
            ["devagar"] = "Calma: espere alguns segundos antes de pedir outra troca.",
            ["bloqueado"] = "Esse jogador recusou ha pouco; tente mais tarde.",
            ["bolsa"] = "A oferta passou do que cabe numa bolsa (" + MaxPilhas + " pilhas).",
        };

        private static readonly Dictionary<string, string> Fins = new Dictionary<string, string>
        {
            ["concluida"] = "Troca concluida.",
            ["recusou"] = "A troca foi recusada.",
            ["cancelou"] = "A troca foi cancelada.",
            ["saiu"] = "O outro jogador saiu; seus itens foram devolvidos.",
            ["longe"] = "Voces se afastaram; a troca foi cancelada.",
            ["morreu"] = "A troca foi cancelada.",
            ["expirou"] = "A troca expirou.",
        };

        // ================================================================== servidor

        private static readonly TrocaMotor _motor = new TrocaMotor { Permitido = Moedas.Prefab };
        private static string _path;
        private static bool _carregado;
        private static float _proximoTick;

        // Limites contra abuso (tudo no servidor; o cliente nao e confiavel).
        private const int MaxPendencias = 20;
        private const double PedidoIntervaloSeg = 3;
        private const double RecusaBloqueioSeg = 60;
        private const int RajadaOps = 20;
        private const double OpsPorSegundo = 8;

        private static readonly Dictionary<long, double[]> _balde = new Dictionary<long, double[]>();
        private static readonly Dictionary<long, double> _proxPedido = new Dictionary<long, double>();
        private static readonly Dictionary<string, double> _bloqueio = new Dictionary<string, double>();
        private static bool _sujo;

        private static double Agora => Time.time;

        /// <summary>Balde de fichas por jogador: rajada de 20 operacoes, depois 8 por segundo.</summary>
        private static bool Permitido(long peer)
        {
            double t = Agora;
            if (!_balde.TryGetValue(peer, out double[] b)) _balde[peer] = b = new[] { (double)RajadaOps, t };
            b[0] = Math.Min(RajadaOps, b[0] + (t - b[1]) * OpsPorSegundo);
            b[1] = t;
            if (b[0] < 1) return false;
            b[0] -= 1;
            return true;
        }

        private static int Pendencias(long jogador) => _motor.Pendentes.Count(e => e.Dono == jogador);
        private static bool EhServidor => ZNet.instance != null && ZNet.instance.IsServer();

        private static string ArquivoDoMundo()
        {
            string mundo = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            foreach (char c in Path.GetInvalidFileNameChars()) mundo = mundo.Replace(c, '_');
            return Path.Combine(Path.Combine(Paths.ConfigPath, "Deadheim"), "troca-" + mundo + ".txt");
        }

        private static void Carregar()
        {
            string path = ArquivoDoMundo();
            if (_carregado && path == _path) return;
            _path = path;
            try
            {
                if (File.Exists(path) && !_motor.Carregar(File.ReadAllText(path)))
                    Debug.LogError($"[Deadheim] Troca: {path} quebrado; ficou o que deu para ler. Guarde uma copia do arquivo antes de apagar.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Troca: {path} ilegivel ({ex.Message}); comecando vazio.");
            }
            _carregado = true;
        }

        private static void Salvar()
        {
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string temp = _path + ".tmp";
                File.WriteAllText(temp, _motor.Serializar());
                File.Copy(temp, _path, true);
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Troca: nao foi possivel salvar " + _path + ": " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(ToClient, OnToClient);
            }
        }

        /// <summary>O jogador com esse nome. Dois com o mesmo nome = ambiguo (impostor): ninguem.</summary>
        private static ZNetPeer PeerPorNome(string nome, out bool ambiguo)
        {
            ambiguo = false;
            if (string.IsNullOrWhiteSpace(nome)) return null;
            var achados = ZNet.instance.GetPeers().Where(p => p != null &&
                string.Equals(p.m_playerName, nome.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            ambiguo = achados.Count > 1;
            return achados.Count == 1 ? achados[0] : null;
        }

        /// <summary>Nome para a janela do outro lado: sem angulos, que viram rich text do IMGUI.</summary>
        private static string NomeDe(long uid)
            => (ZNet.instance?.GetPeer(uid)?.m_playerName ?? "?").Replace('<', ' ').Replace('>', ' ');

        /// <summary>Pilhas cheias numa linha da oferta: o inventario inteiro, como numa entrega de NPC.</summary>
        internal const int MaxPilhas = 32;

        /// <summary>O item do jogo com esse nome de prefab, ou null.</summary>
        internal static ItemDrop.ItemData.SharedData Prefab(string nome)
        {
            if (string.IsNullOrEmpty(nome) || nome.Length > TrocaMotor.MaxNome || ObjectDB.instance == null) return null;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(nome);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop?.m_itemData?.m_shared;
        }

        /// <summary>Quanto de um item cabe numa linha da oferta.</summary>
        internal static int MaxQtd(ItemDrop.ItemData.SharedData sh)
        {
            long max = (long)Mathf.Max(1, sh.m_maxStackSize) * MaxPilhas;
            return max > int.MaxValue ? int.MaxValue : (int)max;
        }

        /// <summary>O que o jogo aceita de um item de verdade; recusa o que um cliente adulterado inventaria.</summary>
        private static bool ItemValido(string nome, int qualidade, int qtd)
        {
            ItemDrop.ItemData.SharedData sh = Prefab(nome);
            if (sh == null || string.IsNullOrEmpty(sh.m_name)) return false;
            if (qualidade < 1 || qualidade > Mathf.Max(1, sh.m_maxQuality)) return false;
            return qtd > 0 && qtd <= MaxQtd(sh);
        }

        /// <summary>Pilhas cheias que uma linha ocupa na bolsa (arredonda para cima).</summary>
        internal static int PilhasDe(string nome, int qtd)
        {
            ItemDrop.ItemData.SharedData sh = Prefab(nome);
            long max = sh != null ? Mathf.Max(1, sh.m_maxStackSize) : 1;
            return (int)((Math.Max(0, qtd) + max - 1) / max);
        }

        private static void OnToServer(long sender, ZPackage pkg)
        {
            if (!EhServidor) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L || !Permitido(real)) return;

            string op;
            try { op = pkg.ReadString(); }
            catch (Exception ex) { Debug.LogError("[Deadheim] Troca: pacote de " + real + " falhou: " + ex); return; }

            Carregar();
            _ofertaLida = null;
            string erro;
            try { erro = Tratar(real, op, pkg); }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Troca: '{op}' de {real} falhou: {ex}");
                erro = "item_invalido";
            }

            Drenar();
            if (erro != null && Erros.TryGetValue(erro, out string texto)) Pvp.PvpNet.Message(real, texto);
            // Oferta recusada: o cliente solta a quantidade que tinha reservado para ela.
            if (erro != null && op == "oferecer" && !string.IsNullOrEmpty(_ofertaLida) && ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(real, ToClient, Pacote("recusada", p => p.Write(_ofertaLida)));
        }

        /// <summary>Id da oferta do pacote em tratamento, para avisar a recusa.</summary>
        private static string _ofertaLida;

        /// <summary>Executa um pedido do cliente. Devolve o codigo do erro, ou null.</summary>
        private static string Tratar(long real, string op, ZPackage pkg)
        {
            double agora = Agora;
            switch (op)
            {
                case "hello":
                    _motor.Reenviar(real);
                    return null;

                case "pedir":
                {
                    if (!Ativo.Value) return "desligado";
                    ZNetPeer eu = ZNet.instance.GetPeer(real);
                    ZNetPeer alvo = PeerPorNome(pkg.ReadString(), out bool ambiguo);
                    if (ambiguo) return "nome_ambiguo";
                    if (eu == null || alvo == null) return "sem_jogador";
                    if (alvo.m_uid != real && Vector3.Distance(eu.m_refPos, alvo.m_refPos) > Distancia.Value) return "longe";
                    if (_proxPedido.TryGetValue(real, out double ate) && agora < ate) return "devagar";
                    if (_bloqueio.TryGetValue(real + ">" + alvo.m_uid, out double ateBloqueio) && agora < ateBloqueio) return "bloqueado";
                    if (Pendencias(real) >= MaxPendencias) return "pendencias";
                    _proxPedido[real] = agora + PedidoIntervaloSeg;
                    return _motor.Pedir(real, alvo.m_uid, agora);
                }

                case "aceitar":
                    if (!Ativo.Value) return "desligado";
                    return Pendencias(real) >= MaxPendencias ? "pendencias" : _motor.Aceitar(real, agora);

                case "recusar":
                {
                    TrocaSessao pedido = _motor.SessaoDe(real);
                    string erroRecusa = _motor.Recusar(real);
                    // Quem pediu nao pode insistir no mesmo alvo logo em seguida.
                    if (erroRecusa == null && pedido != null) _bloqueio[pedido.A + ">" + pedido.B] = agora + RecusaBloqueioSeg;
                    return erroRecusa;
                }

                case "cancelar":
                    return _motor.Cancelar(real, "cancelou");

                case "oferecer":
                {
                    string id = pkg.ReadString() ?? "";
                    _ofertaLida = id.Length <= 64 ? id : null;
                    if (!Ativo.Value) return "desligado";
                    string nome = pkg.ReadString() ?? "";
                    int qualidade = pkg.ReadInt();
                    int qtd = pkg.ReadInt();
                    if (id.Length == 0 || id.Length > 64) return "item_invalido";
                    if (Pendencias(real) >= MaxPendencias) return "pendencias";
                    if (!ItemValido(nome, qualidade, qtd)) return "item_invalido";
                    // A oferta inteira cabe na bolsa (8x4): mais que isso ninguem carrega, so um
                    // cliente adulterado pediria. Antes cada linha podia ter 32 pilhas, 256 no total.
                    int ocupadas = _motor.SessaoDe(real)?.Oferta(real).Sum(i => PilhasDe(i.Nome, i.Qtd)) ?? 0;
                    if (ocupadas + PilhasDe(nome, qtd) > MaxPilhas) return "bolsa";
                    return _motor.Oferecer(real, new TrocaItem { Id = id, Nome = nome, Qualidade = qualidade, Qtd = qtd }, agora);
                }

                case "persistido":
                {
                    int n = Mathf.Clamp(pkg.ReadInt(), 0, TrocaMotor.MaxItens);
                    var ids = new List<string>();
                    for (int i = 0; i < n; i++) ids.Add(pkg.ReadString());
                    return _motor.Persistido(real, ids, agora);
                }

                case "retirar": return _motor.Retirar(real, pkg.ReadString(), agora);
                case "descartar": return _motor.Descartar(real, pkg.ReadString(), agora);
                case "travar": return _motor.Travar(real, pkg.ReadInt(), agora);
                case "confirmar": return _motor.Confirmar(real, pkg.ReadInt(), agora);

                case "recebido":
                    if (_motor.Recebido(real, pkg.ReadLong())) _sujo = true;
                    return null;
            }
            return null;
        }

        /// <summary>Manda o que o motor decidiu e grava o estado (custodia e entregas pendentes).</summary>
        private static void Drenar()
        {
            if (ZRoutedRpc.instance == null) return;
            var efeitos = _motor.Efeitos.ToList();
            _motor.Efeitos.Clear();
            if (efeitos.Count > 0 || _sujo) Salvar();
            _sujo = false;
            foreach (TrocaEfeito e in efeitos)
            {
                ZPackage p = new ZPackage();
                switch (e.Tipo)
                {
                    case TrocaEfeitoTipo.Pedido:
                        p.Write("pedido");
                        p.Write(NomeDe(e.Sessao.A));
                        break;
                    case TrocaEfeitoTipo.Sessao:
                        EscreverSessao(p, e.Sessao, e.Para);
                        break;
                    case TrocaEfeitoTipo.Entrega:
                        p.Write("entrega");
                        p.Write(e.Entrega.Id);
                        p.Write(e.Entrega.Itens.Count);
                        foreach (TrocaItem i in e.Entrega.Itens)
                        {
                            p.Write(i.Id);
                            p.Write(i.Origem == e.Entrega.Dono); // devolucao do que o proprio dono ofereceu
                            p.Write(i.Nome);
                            p.Write(i.Qualidade);
                            p.Write(i.Qtd);
                        }
                        break;
                    case TrocaEfeitoTipo.Fim:
                        p.Write("fim");
                        p.Write(e.Motivo ?? "");
                        break;
                }
                ZRoutedRpc.instance.InvokeRoutedRPC(e.Para, ToClient, p);
            }
        }

        private static void EscreverSessao(ZPackage p, TrocaSessao s, long para)
        {
            long outro = s.Outro(para);
            p.Write("sessao");
            p.Write(s.Versao);
            p.Write(NomeDe(outro));
            p.Write(s.Trava(para)); p.Write(s.Conf(para));
            p.Write(s.Trava(outro)); p.Write(s.Conf(outro));
            foreach (List<TrocaItem> lista in new[] { s.Oferta(para), s.Oferta(outro) })
            {
                p.Write(lista.Count);
                foreach (TrocaItem i in lista) { p.Write(i.Id); p.Write(i.Nome); p.Write(i.Qualidade); p.Write(i.Qtd); p.Write(i.Seguro); }
            }
        }

        // Relogio do servidor: pedido que vence, sessao parada, jogador que saiu ou se afastou.
        [HarmonyPatch(typeof(ZNet), "Update")]
        private static class TickPatch
        {
            private static void Postfix(ZNet __instance)
            {
                if (!__instance.IsServer() || Time.time < _proximoTick) return;
                _proximoTick = Time.time + 1f;
                if (ZRoutedRpc.instance == null) return;
                Carregar();

                _motor.Tick(Agora);
                float limite = Mathf.Max(1f, Distancia.Value) * 1.5f;
                foreach (TrocaSessao s in _motor.Sessoes)
                {
                    ZNetPeer a = __instance.GetPeer(s.A), b = __instance.GetPeer(s.B);
                    if (a == null) _motor.Cancelar(s.A, "saiu");
                    else if (b == null) _motor.Cancelar(s.B, "saiu");
                    else if (Vector3.Distance(a.m_refPos, b.m_refPos) > limite) _motor.Cancelar(s.A, "longe");
                }
                if (_motor.Efeitos.Count > 0) Drenar();
            }
        }

        // ================================================================== cliente

        /// <summary>O que a janela mostra. Null = sem troca.</summary>
        internal sealed class Vista
        {
            public int Versao;
            public string Outro;
            public bool MinhaTrava, MinhaConf, OutroTrava, OutroConf;
            public List<TrocaItem> Minha = new List<TrocaItem>();
            public List<TrocaItem> Dele = new List<TrocaItem>();
        }

        internal static Vista Atual;
        /// <summary>Pedido de troca que chegou e espera resposta: nome de quem pediu e quando chegou.</summary>
        internal static string PedidoDe;
        internal static float PedidoEm;

        // Itens oferecidos que o servidor ainda nao confirmou: continuam no inventario ate la, mas
        // ja nao contam como disponiveis (nao da para oferecer a mesma madeira duas vezes).
        private static readonly Dictionary<string, TrocaItem> _aOferecer = new Dictionary<string, TrocaItem>();
        private static readonly HashSet<long> _entregasFeitas = new HashSet<long>();

        // Livro do proprio personagem (m_customData, gravado junto com o inventario no mesmo save):
        // o que sai do inventario e o que ja foi entregue ficam consistentes mesmo se o jogo for
        // morto no meio. Sem isso, matar o processo antes do save devolvia o item (duplicado).
        private const string ChaveSaiu = "DH_Troca_Saiu";
        private const string ChaveFeitas = "DH_Troca_Feitas";
        private const int MaxLivro = 60;

        private static readonly System.Reflection.FieldInfo _customData = AccessTools.Field(typeof(Player), "m_customData");

        /// <summary>m_customData do personagem (por reflexao: nao confia em o campo ser publico no jogo original).</summary>
        private static Dictionary<string, string> Dados(Player p) => (Dictionary<string, string>)_customData.GetValue(p);

        private static List<string> LerLivro(Player p, string chave)
            => Dados(p).TryGetValue(chave, out string v) && !string.IsNullOrEmpty(v) ? v.Split(',').ToList() : new List<string>();

        private static void Anotar(Player p, string chave, string valor)
        {
            List<string> livro = LerLivro(p, chave);
            livro.Add(valor);
            if (livro.Count > MaxLivro) livro.RemoveRange(0, livro.Count - MaxLivro);
            Dados(p)[chave] = string.Join(",", livro);
        }

        /// <summary>Grava o personagem agora (o salvamento normal e a cada tantos minutos).</summary>
        private static void SalvarPerfil()
        {
            try
            {
                System.Reflection.MethodInfo m = AccessTools.Method(typeof(Game), "SavePlayerProfile");
                if (m == null || Game.instance == null) return;
                m.Invoke(Game.instance, m.GetParameters().Select(x => x.ParameterType == typeof(bool) ? (object)false : null).ToArray());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Deadheim] Troca: nao consegui gravar o personagem: " + ex.Message);
            }
        }

        /// <summary>Oferta que nunca saiu do inventario (morte, logout): avisa o servidor para nao devolver.</summary>
        private static void EsquecerPendentes()
        {
            foreach (string id in _aOferecer.Keys.ToList()) Enviar(Pacote("descartar", p => p.Write(id)));
            _aOferecer.Clear();
        }

        // ---- inventario por nome (prefab + qualidade), sem os itens equipados ----

        /// <summary>
        /// As pilhas do inventario que podem ir para a troca: do prefab e da qualidade, fora as
        /// equipadas. Pelo nome do prefab e nao pelo m_shared.m_name (que e o que o
        /// Inventory.RemoveItem(nome) compara): item clonado pode ter o mesmo nome traduzido.
        /// </summary>
        internal static List<ItemDrop.ItemData> Pilhas(Inventory inv, string nome, int qualidade)
            => inv == null ? new List<ItemDrop.ItemData>() : inv.GetAllItems()
                .Where(i => i?.m_shared != null && i.m_dropPrefab != null && !i.m_equipped
                            && i.m_quality == qualidade && i.m_dropPrefab.name == nome)
                .ToList();

        /// <summary>Quanto ja foi oferecido e o servidor ainda nao confirmou.</summary>
        internal static int Reservado(string nome, int qualidade)
            => _aOferecer.Values.Where(i => i.Nome == nome && i.Qualidade == qualidade).Sum(i => i.Qtd);

        /// <summary>Quanto ainda da para oferecer desse item.</summary>
        internal static int Disponivel(string nome, int qualidade)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return 0;
            return Math.Max(0, Pilhas(p.GetInventory(), nome, qualidade).Sum(i => i.m_stack) - Reservado(nome, qualidade));
        }

        /// <summary>Tira tudo ou nada: sem a quantidade inteira (largou, equipou), nao mexe em nada.</summary>
        private static bool Tirar(Inventory inv, string nome, int qualidade, int qtd)
        {
            List<ItemDrop.ItemData> pilhas = Pilhas(inv, nome, qualidade);
            if (qtd <= 0 || pilhas.Sum(i => i.m_stack) < qtd) return false;
            int falta = qtd;
            // As menores primeiro: sobram pilhas cheias.
            foreach (ItemDrop.ItemData pilha in pilhas.OrderBy(i => i.m_stack))
            {
                int n = Math.Min(falta, pilha.m_stack);
                inv.RemoveItem(pilha, n);
                falta -= n;
                if (falta <= 0) break;
            }
            return true;
        }

        /// <summary>Nome do item para a janela ("Madeira"), pelo prefab.</summary>
        internal static string NomeVisivel(string nome)
        {
            ItemDrop.ItemData.SharedData sh = Prefab(nome);
            if (sh == null || Localization.instance == null) return nome ?? "";
            string texto = Localization.instance.Localize(sh.m_name);
            return string.IsNullOrEmpty(texto) ? nome : texto;
        }

        internal static string Rotulo(TrocaItem item)
            => item.Qtd + "x " + NomeVisivel(item.Nome) + (item.Qualidade > 1 ? "  (nv " + item.Qualidade + ")" : "");

        private static void Enviar(ZPackage p)
        {
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ToServer, p);
        }

        private static ZPackage Pacote(string op, Action<ZPackage> corpo = null)
        {
            ZPackage p = new ZPackage();
            p.Write(op);
            corpo?.Invoke(p);
            return p;
        }

        internal static void Pedir(string nome) => Enviar(Pacote("pedir", p => p.Write(nome)));
        internal static void Aceitar() { PedidoDe = null; Enviar(Pacote("aceitar")); }
        internal static void Recusar() { PedidoDe = null; Enviar(Pacote("recusar")); }
        internal static void Cancelar() { PedidoDe = null; Enviar(Pacote("cancelar")); }
        internal static void Retirar(string id) => Enviar(Pacote("retirar", p => p.Write(id)));
        internal static void Travar() { if (Atual != null) Enviar(Pacote("travar", p => p.Write(Atual.Versao))); }
        internal static void Confirmar() { if (Atual != null) Enviar(Pacote("confirmar", p => p.Write(Atual.Versao))); }

        /// <summary>
        /// Oferece <paramref name="qtd"/> do item pelo nome. Devolve o motivo da recusa para a
        /// janela, ou null quando o pedido saiu; o item so sai do inventario quando o servidor guardar.
        /// </summary>
        internal static string Oferecer(string nome, int qualidade, int qtd)
        {
            if (Atual == null) return "Nao ha troca aberta.";
            if (Atual.Minha.Count + _aOferecer.Count >= TrocaMotor.MaxItens) return "A oferta esta cheia.";
            if (nome != Moedas.Prefab) return "A troca e so de moedas.";
            ItemDrop.ItemData.SharedData sh = Prefab(nome);
            if (sh == null) return "Esse item nao pode ser trocado.";
            if (qtd <= 0) return "Quantidade invalida.";
            if (qtd > MaxQtd(sh)) return "No maximo " + MaxQtd(sh) + " por vez.";
            int tem = Disponivel(nome, qualidade);
            if (qtd > tem) return "Voce so tem " + tem + "x " + NomeVisivel(nome) + " para oferecer.";

            string id = Guid.NewGuid().ToString("N");
            _aOferecer[id] = new TrocaItem { Id = id, Nome = nome, Qualidade = qualidade, Qtd = qtd };
            Enviar(Pacote("oferecer", p => { p.Write(id); p.Write(nome); p.Write(qualidade); p.Write(qtd); }));
            return null;
        }

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            try
            {
                switch (pkg.ReadString())
                {
                    case "pedido":
                        PedidoDe = pkg.ReadString();
                        PedidoEm = Time.time;
                        TrocaUi.Ensure();
                        break;
                    case "sessao":
                        LerSessao(pkg);
                        break;
                    case "entrega":
                        ReceberEntrega(pkg);
                        break;
                    case "recusada":
                        _aOferecer.Remove(pkg.ReadString());
                        break;
                    case "fim":
                        Fechar(pkg.ReadString());
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Troca: pacote do servidor falhou: " + ex);
            }
        }

        private static void LerSessao(ZPackage pkg)
        {
            var v = new Vista { Versao = pkg.ReadInt(), Outro = pkg.ReadString() };
            v.MinhaTrava = pkg.ReadBool(); v.MinhaConf = pkg.ReadBool();
            v.OutroTrava = pkg.ReadBool(); v.OutroConf = pkg.ReadBool();
            foreach (List<TrocaItem> lista in new[] { v.Minha, v.Dele })
            {
                int n = pkg.ReadInt();
                for (int i = 0; i < n; i++)
                    lista.Add(new TrocaItem { Id = pkg.ReadString(), Nome = pkg.ReadString(), Qualidade = pkg.ReadInt(), Qtd = pkg.ReadInt(), Seguro = pkg.ReadBool() });
            }
            PedidoDe = null;
            Atual = v;
            TrocaUi.Ensure();

            // Custodia confirmada pelo servidor: agora a quantidade sai do inventario, pelo nome e
            // como o servidor guardou. Se ela ja nao estava la inteira (largou, equipou), o
            // servidor descarta em vez de devolver, ou duplicaria.
            var tirados = new List<string>();
            foreach (TrocaItem t in v.Minha)
            {
                if (!_aOferecer.Remove(t.Id)) continue;
                Player player = Player.m_localPlayer;
                bool tirou = player != null && Tirar(player.GetInventory(), t.Nome, t.Qualidade, t.Qtd);
                if (tirou)
                {
                    Anotar(player, ChaveSaiu, t.Id);
                    tirados.Add(t.Id);
                }
                else Enviar(Pacote("descartar", p => p.Write(t.Id)));
            }

            // So depois de gravar o personagem o servidor libera a troca: o item nao pode voltar
            // do save antigo enquanto o destino ja o recebeu.
            if (tirados.Count > 0)
            {
                SalvarPerfil();
                Enviar(Pacote("persistido", p => { p.Write(tirados.Count); foreach (string id in tirados) p.Write(id); }));
            }
        }

        private static void ReceberEntrega(ZPackage pkg)
        {
            long id = pkg.ReadLong();
            int n = pkg.ReadInt();
            var itens = new List<(string id, bool devolucao, TrocaItem item)>();
            for (int i = 0; i < n; i++)
            {
                string itemId = pkg.ReadString();
                bool devolucao = pkg.ReadBool();
                itens.Add((itemId, devolucao, new TrocaItem { Id = itemId, Nome = pkg.ReadString(), Qualidade = pkg.ReadInt(), Qtd = pkg.ReadInt() }));
            }

            Player player = Player.m_localPlayer;
            if (player == null) return; // sem personagem: o servidor reenvia no proximo login

            // Entrega que o personagem ja registrou (reenvio, "recebido" perdido): so confirma.
            bool feita = _entregasFeitas.Contains(id) || LerLivro(player, ChaveFeitas).Contains(id.ToString());
            if (!feita)
            {
                List<string> saiu = LerLivro(player, ChaveSaiu);
                foreach (var (itemId, devolucao, item) in itens)
                {
                    if (Prefab(item.Nome) == null || item.Qtd <= 0)
                    {
                        Debug.LogError($"[Deadheim] Troca: item desconhecido na entrega {id}: {item.Qtd}x '{item.Nome}' (nv {item.Qualidade}).");
                        continue;
                    }
                    // Devolucao de um item que nunca saiu do save (jogo morto antes de gravar): ele
                    // ainda esta no inventario, devolver de novo duplicaria.
                    if (devolucao && !saiu.Contains(itemId)) continue;
                    int noChao = Entrega.ParaJogador(player, item.Nome, item.Qtd, item.Qualidade);
                    player.Message(MessageHud.MessageType.TopLeft, (devolucao ? "Troca, devolvido: " : "Troca, recebido: ") + Rotulo(item));
                    if (noChao > 0) player.Message(MessageHud.MessageType.Center, $"Inventario cheio: {noChao}x {NomeVisivel(item.Nome)} caiu no chao.");
                }
                _entregasFeitas.Add(id);
                Anotar(player, ChaveFeitas, id.ToString());
                SalvarPerfil(); // grava ANTES de confirmar: confirmado = nunca mais reenviado
            }
            Enviar(Pacote("recebido", p => p.Write(id)));
        }

        private static void Fechar(string motivo)
        {
            Atual = null;
            PedidoDe = null;
            _aOferecer.Clear();
            if (Fins.TryGetValue(motivo ?? "", out string texto))
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class SpawnPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                TrocaUi.Ensure();
                Enviar(Pacote("hello"));
            }
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        private static class MorteLocalPatch
        {
            private static void Prefix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                EsquecerPendentes();
                if (Atual != null || PedidoDe != null) Cancelar();
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class LogoutPatch
        {
            private static void Prefix() => EsquecerPendentes();

            private static void Postfix()
            {
                Atual = null; PedidoDe = null;
                _aOferecer.Clear();
                _entregasFeitas.Clear();
            }
        }

        // ------------------------------------------------------------------------ comando

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandoPatch
        {
            private static void Postfix()
            {
                // Sempre pelo nome: sem nome abre a lista de quem esta perto para escolher, em vez
                // de pedir ao mais proximo (numa cidade cheia, o mais proximo e quase sempre outro).
                new Terminal.ConsoleCommand("trocar", "[nome] - pede uma troca de moedas ao jogador (sem nome: escolher na lista de quem esta perto)",
                    args =>
                    {
                        Player eu = Player.m_localPlayer;
                        if (eu == null || ZRoutedRpc.instance == null) return;
                        if (!Ativo.Value) { args.Context.AddString("A troca esta desligada neste servidor."); return; }
                        if (Atual != null) { args.Context.AddString("Voce ja esta numa troca."); return; }

                        string nome = args.Length >= 2 ? string.Join(" ", args.Args, 1, args.Length - 1).Trim() : null;
                        if (string.IsNullOrEmpty(nome))
                        {
                            TrocaUi.AbrirEscolha();
                            return;
                        }
                        Pedir(nome);
                        args.Context.AddString($"Pedido de troca enviado a {nome}.");
                    });
            }
        }
    }
}
