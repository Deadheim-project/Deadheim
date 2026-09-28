using Deadheim.Vanilla;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadheim.Ajustes
{
    /// <summary>
    /// A janela "Deadheim" do menu do ESC, no visual do Valheim (painel de madeira, fonte
    /// Averia, botoes e caixas de marcar do jogo).
    ///
    /// - Meus ajustes: a config deste PC, de todos os mods carregados, menos o que o
    ///   servidor controla. Grava direto no BepInEx/config do jogador.
    /// - Servidor (so admin): a config que o servidor esta usando, pedida a ele pela rede.
    ///   Cada mudanca e aplicada e salva no servidor na hora; o que e sincronizado o
    ///   ServerSync entrega aos jogadores conectados.
    /// </summary>
    internal static class AjustesJanela
    {
        private enum Aba
        {
            Meus,
            Servidor
        }

        private const float Largura = 1060f;
        private const float Altura = 700f;
        private const float LarguraLinha = 720f;
        private const float AlturaLinha = 54f;
        private const float AlturaSecao = 34f;
        private const float LarguraEditor = 250f;
        private const float LarguraPadrao = 70f;
        private const float EsperaServidor = 8f;

        private static readonly Color CorTexto = new Color(0.93f, 0.9f, 0.84f, 1f);
        private static readonly Color CorDescricao = new Color(0.78f, 0.74f, 0.68f, 1f);
        private static readonly Color CorOk = new Color(0.6f, 0.95f, 0.55f, 1f);
        private static readonly Color CorErro = new Color(1f, 0.45f, 0.38f, 1f);
        private static readonly Color CorSelecionado = new Color(1f, 0.95f, 0.8f, 1f);
        private static readonly Color FundoLista = new Color(0f, 0f, 0f, 0.3f);
        private static readonly Color FundoLinhaPar = new Color(0f, 0f, 0f, 0.3f);
        private static readonly Color FundoLinhaImpar = new Color(0f, 0f, 0f, 0.15f);

        private static GameObject _janela;
        private static Text _cabecalho;
        private static Text _info;
        private static Text _status;
        private static Text _vazio;
        private static GameObject _botaoMeus;
        private static GameObject _botaoServidor;
        private static Transform _listaMods;
        private static Transform _listaItens;
        private static InputField _busca;

        private static Aba _aba = Aba.Meus;
        private static string _modMeus;
        private static string _modServidor;
        private static List<ModAjustes> _mods = new List<ModAjustes>();
        private static readonly Dictionary<string, Text> _rotulosMods = new Dictionary<string, Text>();
        private static readonly List<Secao> _secoes = new List<Secao>();
        private static readonly Dictionary<string, Linha> _linhas = new Dictionary<string, Linha>();
        private static float _esperaAte;
        private static bool _fechando;

        public static bool Aberta => _janela != null && _janela.activeSelf;

        private static string ModAtual => _aba == Aba.Meus ? _modMeus : _modServidor;

        // ------------------------------------------------------------ abrir/fechar

        public static void Mostrar(Transform pai)
        {
            if (_janela == null || _janela.transform.parent != pai)
            {
                if (_janela != null) UnityEngine.Object.Destroy(_janela);
                Construir(pai);
            }

            _janela.transform.SetAsLastSibling();
            _janela.SetActive(true);

            bool admin = Admin.LocalPlayerIsAdmin();
            _botaoServidor.SetActive(admin);
            ((RectTransform)_botaoMeus.transform).anchoredPosition = new Vector2(admin ? -115f : 0f, 262f);

            MostrarAba(_aba == Aba.Servidor && admin ? Aba.Servidor : Aba.Meus);
        }

        public static void Esconder()
        {
            _esperaAte = 0f;
            if (_janela == null) return;

            // Um campo em edicao solta o onEndEdit ao ser desativado. Fechar (ESC, menu
            // fechando) descarta o que estava sendo digitado em vez de gravar.
            _fechando = true;
            try { _janela.SetActive(false); }
            finally { _fechando = false; }
        }

        /// <summary>Todo frame com a janela aberta.</summary>
        public static void Tick()
        {
            if (_esperaAte <= 0f || Time.unscaledTime < _esperaAte) return;
            _esperaAte = 0f;
            Status("O servidor nao respondeu. Ele precisa do Deadheim 7.1.0 ou mais novo.", CorErro);
            if (_linhas.Count == 0) Vazio("Sem resposta do servidor.");
        }

        // ------------------------------------------------------------------- abas

        private static void MostrarAba(Aba aba)
        {
            _aba = aba;
            PintarAba(_botaoMeus, aba == Aba.Meus);
            PintarAba(_botaoServidor, aba == Aba.Servidor);
            _esperaAte = 0f;
            _busca.text = "";
            Status("", CorTexto);
            LimparMods();
            LimparItens();
            InfoPadrao();

            if (aba == Aba.Meus)
            {
                PreencherMods(AjustesDados.Mods(local: true));
                return;
            }

            Cabecalho("Servidor");
            Vazio("Carregando a config do servidor...");
            // Esperar antes de pedir: no host a resposta chega dentro do proprio PedirMods.
            Esperar();
            if (!AjustesRede.PedirMods()) SemConexao();
        }

        private static void PintarAba(GameObject botao, bool ativa)
        {
            Text rotulo = botao.GetComponentInChildren<Text>();
            rotulo.color = ativa ? CorSelecionado : Ui.ValheimOrange;
            rotulo.fontSize = ativa ? 19 : 17;
        }

        private static void InfoPadrao()
        {
            _info.text = _aba == Aba.Meus
                ? "Suas preferencias, salvas neste PC (BepInEx/config). O que o servidor controla nao aparece aqui."
                : "Config do servidor, ao vivo: cada mudanca vale na hora e fica salva no cfg do servidor. " +
                  "<color=#8fd0ff>sincronizado</color> = vai para todos os jogadores; " +
                  "<color=#c8b89a>so no servidor</color> = regra que roda no servidor. Algumas opcoes so pegam quando o mod reinicia.";
        }

        // ------------------------------------------------------------------- mods

        private static void PreencherMods(List<ModAjustes> mods)
        {
            LimparMods();
            _mods = mods ?? new List<ModAjustes>();

            if (_mods.Count == 0)
            {
                Cabecalho("");
                Vazio(_aba == Aba.Meus ? "Nenhum mod com ajustes locais." : "O servidor nao tem mods com config.");
                return;
            }

            foreach (ModAjustes mod in _mods)
            {
                string guid = mod.Guid;
                GameObject botao = Ui.CreateButton(mod.Nome, _listaMods, Vector2.zero, Vector2.zero, Vector2.zero, 226f, 34f);
                Text rotulo = botao.GetComponentInChildren<Text>();
                rotulo.fontSize = 15;
                rotulo.resizeTextForBestFit = true;
                rotulo.resizeTextMinSize = 11;
                rotulo.resizeTextMaxSize = 15;
                botao.GetComponent<Button>().onClick.AddListener(() => SelecionarMod(guid));
                _rotulosMods[guid] = rotulo;
            }

            string escolhido = ModAtual;
            if (string.IsNullOrEmpty(escolhido) || _mods.All(m => m.Guid != escolhido)) escolhido = _mods[0].Guid;
            SelecionarMod(escolhido);
            RolarParaOTopo(_listaMods);
        }

        private static void SelecionarMod(string guid)
        {
            if (_aba == Aba.Meus) _modMeus = guid;
            else _modServidor = guid;

            foreach (KeyValuePair<string, Text> rotulo in _rotulosMods)
                rotulo.Value.color = rotulo.Key == guid ? CorSelecionado : Ui.ValheimOrange;

            ModAjustes mod = _mods.FirstOrDefault(m => m.Guid == guid);
            Cabecalho(mod == null ? guid : $"{mod.Nome} <size=14><color=#c8b89a>{mod.Versao}</color></size>");
            LimparItens();
            InfoPadrao();

            if (_aba == Aba.Meus)
            {
                PreencherItens(AjustesDados.Itens(guid, local: true));
                return;
            }

            Vazio("Carregando...");
            Esperar();
            if (!AjustesRede.PedirItens(guid)) SemConexao();
        }

        // ------------------------------------------------------------------ opcoes

        private static void PreencherItens(List<ItemAjuste> itens)
        {
            LimparItens();
            if (itens.Count == 0)
            {
                Vazio("Nenhuma opcao.");
                return;
            }

            Vazio("");
            Secao secao = null;
            int indice = 0;
            foreach (ItemAjuste item in itens)
            {
                if (secao == null || secao.Nome != item.Secao)
                {
                    secao = CriarSecao(item.Secao);
                    _secoes.Add(secao);
                    indice = 0;
                }

                Linha linha = CriarLinha(item, indice++ % 2 == 0);
                secao.Linhas.Add(linha);
                _linhas[item.Id] = linha;
            }

            Filtrar(_busca.text);
            RolarParaOTopo(_listaItens);
        }

        private static Secao CriarSecao(string nome)
        {
            GameObject go = Retangulo("Secao", _listaItens, LarguraLinha, AlturaSecao);

            Text titulo = CriarTexto(go.transform, nome, 19, Ui.ValheimOrange, TextAnchor.LowerLeft);
            Colocar(titulo.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 4f), new Vector2(LarguraLinha - 12f, 28f));

            // Filete embaixo do titulo, como os cabecalhos das janelas do jogo.
            GameObject filete = Retangulo("Filete", go.transform, LarguraLinha, 2f);
            Colocar(filete, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(LarguraLinha, 2f));
            Image imagem = filete.AddComponent<Image>();
            imagem.color = new Color(Ui.ValheimOrange.r, Ui.ValheimOrange.g, Ui.ValheimOrange.b, 0.45f);
            imagem.raycastTarget = false;

            return new Secao { Nome = nome, Objeto = go };
        }

        private static Linha CriarLinha(ItemAjuste item, bool par)
        {
            GameObject go = Retangulo("Opcao", _listaItens, LarguraLinha, AlturaLinha);
            go.AddComponent<Image>().color = par ? FundoLinhaPar : FundoLinhaImpar;
            Linha linha = new Linha { Item = item, Objeto = go };
            go.AddComponent<AoPassar>().Acao = () => MostrarInfo(item);

            float larguraTexto = LarguraLinha - LarguraEditor - LarguraPadrao - 40f;
            Text chave = CriarTexto(go.transform, item.Chave, 16, Ui.ValheimOrange, TextAnchor.MiddleLeft);
            Colocar(chave.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -5f), new Vector2(larguraTexto, 22f));
            Text descricao = CriarTexto(go.transform, Resumo(item), 13, CorDescricao, TextAnchor.MiddleLeft);
            Colocar(descricao.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -28f), new Vector2(larguraTexto, 20f));

            float direita = -(LarguraPadrao + 18f);
            switch (item.Editor)
            {
                case TipoEditor.LigaDesliga:
                    CriarCaixa(linha, direita);
                    break;
                case TipoEditor.Lista:
                    CriarLista(linha, direita);
                    break;
                default:
                    CriarCampo(linha, direita);
                    break;
            }

            GameObject padrao = Ui.CreateButton("Padrao", go.transform, Vector2.zero, Vector2.zero, Vector2.zero, LarguraPadrao, 30f);
            Colocar(padrao, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(LarguraPadrao, 30f));
            padrao.GetComponentInChildren<Text>().fontSize = 13;
            linha.Padrao = padrao.GetComponent<Button>();
            linha.Padrao.onClick.AddListener(() => Aplicar(linha, linha.Item.Padrao));

            linha.Mostrar();
            return linha;
        }

        private static void CriarCaixa(Linha linha, float direita)
        {
            GameObject caixa = Ui.CreateToggle(linha.Objeto.transform, 28f, 28f);
            Colocar(caixa, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita - LarguraEditor + 28f, 0f), new Vector2(28f, 28f));
            linha.Caixa = caixa.GetComponent<Toggle>();
            linha.Caixa.interactable = !linha.Item.SomenteLeitura;
            linha.Caixa.onValueChanged.AddListener(ligado =>
            {
                if (!linha.Atualizando) Aplicar(linha, ligado ? "true" : "false");
            });

            linha.RotuloCaixa = CriarTexto(linha.Objeto.transform, "", 15, CorTexto, TextAnchor.MiddleLeft);
            Colocar(linha.RotuloCaixa.gameObject, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita, 0f), new Vector2(LarguraEditor - 40f, 28f));
        }

        private static void CriarLista(Linha linha, float direita)
        {
            Transform pai = linha.Objeto.transform;

            GameObject mais = Ui.CreateButton(">", pai, Vector2.zero, Vector2.zero, Vector2.zero, 32f, 30f);
            Colocar(mais, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita, 0f), new Vector2(32f, 30f));

            GameObject caixaValor = Retangulo("Valor", pai, LarguraEditor - 72f, 30f);
            Colocar(caixaValor, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita - 36f, 0f), new Vector2(LarguraEditor - 72f, 30f));
            caixaValor.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            linha.ValorLista = CriarTexto(caixaValor.transform, "", 14, CorTexto, TextAnchor.MiddleCenter);
            Esticar(linha.ValorLista.gameObject);
            linha.ValorLista.resizeTextForBestFit = true;
            linha.ValorLista.resizeTextMinSize = 10;
            linha.ValorLista.resizeTextMaxSize = 14;

            GameObject menos = Ui.CreateButton("<", pai, Vector2.zero, Vector2.zero, Vector2.zero, 32f, 30f);
            Colocar(menos, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita - LarguraEditor + 32f, 0f), new Vector2(32f, 30f));

            bool editavel = !linha.Item.SomenteLeitura;
            Button botaoMais = mais.GetComponent<Button>();
            Button botaoMenos = menos.GetComponent<Button>();
            botaoMais.interactable = editavel;
            botaoMenos.interactable = editavel;
            botaoMais.onClick.AddListener(() => Girar(linha, 1));
            botaoMenos.onClick.AddListener(() => Girar(linha, -1));
        }

        private static void CriarCampo(Linha linha, float direita)
        {
            GameObject campo = Ui.CreateInputField(linha.Objeto.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                InputField.ContentType.Standard, "", 14, LarguraEditor, 32f);
            Colocar(campo, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(direita, 0f), new Vector2(LarguraEditor, 32f));
            linha.Campo = campo.GetComponent<InputField>();
            linha.Campo.lineType = InputField.LineType.SingleLine;
            linha.Campo.interactable = !linha.Item.SomenteLeitura;
            // Enter ou clicar fora aplica; ESC cancela a edicao (e fecha a janela).
            linha.Campo.onEndEdit.AddListener(valor =>
            {
                if (!linha.Atualizando) Aplicar(linha, valor);
            });
        }

        private static void Girar(Linha linha, int passo)
        {
            string[] opcoes = linha.Item.Opcoes;
            if (opcoes.Length == 0) return;

            int atual = Array.FindIndex(opcoes, o => string.Equals(o, linha.Item.Valor, StringComparison.OrdinalIgnoreCase));
            int proximo = atual < 0 ? 0 : ((atual + passo) % opcoes.Length + opcoes.Length) % opcoes.Length;
            Aplicar(linha, opcoes[proximo]);
        }

        // ---------------------------------------------------------------- aplicar

        private static void Aplicar(Linha linha, string valor)
        {
            if (_fechando) return;

            ItemAjuste item = linha.Item;
            if (item.SomenteLeitura || valor == item.Valor)
            {
                linha.Mostrar();
                return;
            }

            if (_aba == Aba.Meus)
            {
                bool ok = AjustesDados.Aplicar(_modMeus, item.Secao, item.Chave, valor, local: true,
                    out _, out string atual, out string erro);
                if (!string.IsNullOrEmpty(atual)) item.Valor = atual;
                linha.Mostrar();
                if (ok) Status($"Salvo: {item.Chave} = {Curto(item.Valor)}", CorOk);
                else Status(erro, CorErro);
                return;
            }

            Status($"Enviando {item.Chave} para o servidor...", CorTexto);
            Esperar();
            if (!AjustesRede.Definir(_modServidor, item.Secao, item.Chave, valor))
            {
                linha.Mostrar();
                SemConexao();
            }
        }

        // --------------------------------------------------- respostas do servidor

        public static void ReceberModsDoServidor(List<ModAjustes> mods)
        {
            if (!Aberta || _aba != Aba.Servidor) return;
            _esperaAte = 0f;
            PreencherMods(mods);
        }

        public static void ReceberItensDoServidor(string guid, List<ItemAjuste> itens)
        {
            if (!Aberta || _aba != Aba.Servidor || guid != _modServidor) return;
            _esperaAte = 0f;
            PreencherItens(itens);
        }

        public static void ReceberResultadoDoServidor(bool ok, string guid, string secao, string chave, string valor, string mensagem)
        {
            if (!Aberta || _aba != Aba.Servidor) return;
            _esperaAte = 0f;

            if (guid == _modServidor && _linhas.TryGetValue(secao + "\n" + chave, out Linha linha))
            {
                if (!string.IsNullOrEmpty(valor)) linha.Item.Valor = valor;
                linha.Mostrar();
            }

            if (ok) Status($"Servidor: {chave} = {Curto(valor)} (ja valendo)", CorOk);
            else Status("Servidor recusou: " + mensagem, CorErro);
        }

        public static void ReceberNegado(string mensagem)
        {
            if (!Aberta) return;
            _esperaAte = 0f;
            if (_aba == Aba.Servidor)
            {
                LimparMods();
                LimparItens();
                Vazio(mensagem);
            }
            Status(mensagem, CorErro);
        }

        // ------------------------------------------------------------------ busca

        private static void Filtrar(string texto)
        {
            string filtro = (texto ?? "").Trim();
            foreach (Secao secao in _secoes)
            {
                bool secaoBate = Contem(secao.Nome, filtro);
                bool algumaVisivel = false;
                foreach (Linha linha in secao.Linhas)
                {
                    bool visivel = filtro.Length == 0 || secaoBate || Contem(linha.Item.Chave, filtro) || Contem(linha.Item.Descricao, filtro);
                    linha.Objeto.SetActive(visivel);
                    algumaVisivel |= visivel;
                }
                secao.Objeto.SetActive(algumaVisivel);
            }

            if (_secoes.Count > 0) Vazio(_secoes.Any(s => s.Objeto.activeSelf) ? "" : "Nada encontrado.");
        }

        private static bool Contem(string texto, string filtro)
            => !string.IsNullOrEmpty(texto) && texto.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0;

        // ---------------------------------------------------------------- textos

        private static void MostrarInfo(ItemAjuste item)
        {
            string detalhes = $"Padrao: {Curto(item.Padrao)}";
            if (item.Editor == TipoEditor.Lista && item.Opcoes.Length > 0) detalhes += "   Opcoes: " + Curto(string.Join(", ", item.Opcoes));
            if (!string.IsNullOrEmpty(item.Faixa)) detalhes += "   Faixa: " + item.Faixa;
            if (item.SomenteLeitura) detalhes += "   (somente leitura)";

            string descricao = string.IsNullOrEmpty(item.Descricao) ? "" : item.Descricao.Replace("\r", "").Replace("\n", " ");
            _info.text = $"<color=#ffa13c>[{item.Secao}] {item.Chave}</color>  <color=#c8b89a>({item.Tipo})</color>  {descricao}\n" +
                         $"<color=#c8b89a>{detalhes}</color>";
        }

        private static string Resumo(ItemAjuste item)
        {
            string marca = "";
            if (_aba == Aba.Servidor)
                marca = item.Sincronizado ? "<color=#8fd0ff>sincronizado</color>  " : "<color=#c8b89a>so no servidor</color>  ";

            string faixa = string.IsNullOrEmpty(item.Faixa) ? "" : $"({item.Faixa})  ";
            string descricao = item.Descricao ?? "";
            int quebra = descricao.IndexOfAny(new[] { '\r', '\n' });
            if (quebra >= 0) descricao = descricao.Substring(0, quebra);
            if (descricao.Length > 90) descricao = descricao.Substring(0, 87) + "...";
            return marca + faixa + descricao;
        }

        private static string Curto(string valor)
        {
            valor = valor ?? "";
            return valor.Length > 60 ? valor.Substring(0, 57) + "..." : valor;
        }

        private static void Cabecalho(string texto) => _cabecalho.text = texto;

        private static void Vazio(string texto)
        {
            _vazio.text = texto;
            _vazio.gameObject.SetActive(!string.IsNullOrEmpty(texto));
        }

        private static void Status(string texto, Color cor)
        {
            _status.text = texto;
            _status.color = cor;
        }

        private static void Esperar() => _esperaAte = Time.unscaledTime + EsperaServidor;

        private static void SemConexao()
        {
            _esperaAte = 0f;
            Status("Sem conexao com o servidor.", CorErro);
            if (_linhas.Count == 0) Vazio("Sem conexao com o servidor.");
        }

        // --------------------------------------------------------------- limpeza

        private static void LimparMods()
        {
            _rotulosMods.Clear();
            Destruir(_listaMods);
        }

        private static void LimparItens()
        {
            _secoes.Clear();
            _linhas.Clear();
            Destruir(_listaItens);
        }

        private static void Destruir(Transform conteudo)
        {
            if (conteudo == null) return;
            // Tira do pai ja, para o layout nao contar quem so some no fim do frame.
            List<Transform> filhos = conteudo.Cast<Transform>().ToList();
            foreach (Transform filho in filhos)
            {
                filho.SetParent(null, false);
                UnityEngine.Object.Destroy(filho.gameObject);
            }
        }

        private static void RolarParaOTopo(Transform conteudo)
        {
            ScrollRect rolagem = conteudo != null ? conteudo.GetComponentInParent<ScrollRect>() : null;
            if (rolagem == null) return;
            Canvas.ForceUpdateCanvases();
            rolagem.verticalNormalizedPosition = 1f;
        }

        // --------------------------------------------------------------- montagem

        private static void Construir(Transform pai)
        {
            Vector2 centro = new Vector2(0.5f, 0.5f);
            _janela = Ui.CreateWoodpanel(pai, centro, centro, Vector2.zero, Largura, Altura, draggable: false);
            _janela.name = "DeadheimAjustes";

            Text titulo = CriarTexto(_janela.transform, "DEADHEIM", 36, Ui.ValheimOrange, TextAnchor.MiddleCenter);
            Colocar(titulo.gameObject, centro, centro, new Vector2(0f, 310f), new Vector2(600f, 48f));

            _botaoMeus = Ui.CreateButton("Meus ajustes", _janela.transform, centro, centro, new Vector2(-115f, 262f), 210f, 38f);
            _botaoMeus.GetComponent<Button>().onClick.AddListener(() => MostrarAba(Aba.Meus));
            _botaoServidor = Ui.CreateButton("Servidor", _janela.transform, centro, centro, new Vector2(115f, 262f), 210f, 38f);
            _botaoServidor.GetComponent<Button>().onClick.AddListener(() => MostrarAba(Aba.Servidor));

            GameObject mods = CriarRolagem(250f, 460f, new Vector2(-385f, -5f));
            _listaMods = mods.transform.Find("Scroll View/Viewport/Content");
            VerticalLayoutGroup layoutMods = _listaMods.GetComponent<VerticalLayoutGroup>();
            layoutMods.spacing = 4f;
            layoutMods.padding = new RectOffset(0, 0, 6, 6);

            _cabecalho = CriarTexto(_janela.transform, "", 22, CorSelecionado, TextAnchor.MiddleLeft);
            Colocar(_cabecalho.gameObject, centro, centro, new Vector2(-5f, 203f), new Vector2(460f, 36f));

            GameObject busca = Ui.CreateInputField(_janela.transform, centro, centro, new Vector2(385f, 203f),
                InputField.ContentType.Standard, "Buscar...", 15, 250f, 32f);
            _busca = busca.GetComponent<InputField>();
            _busca.onValueChanged.AddListener(Filtrar);

            GameObject itens = CriarRolagem(750f, 410f, new Vector2(135f, -30f));
            _listaItens = itens.transform.Find("Scroll View/Viewport/Content");
            VerticalLayoutGroup layoutItens = _listaItens.GetComponent<VerticalLayoutGroup>();
            layoutItens.spacing = 3f;
            layoutItens.padding = new RectOffset(0, 0, 4, 8);

            _vazio = CriarTexto(itens.transform, "", 18, CorDescricao, TextAnchor.MiddleCenter);
            Colocar(_vazio.gameObject, centro, centro, Vector2.zero, new Vector2(680f, 80f));

            _info = CriarTexto(_janela.transform, "", 14, CorTexto, TextAnchor.UpperLeft);
            Colocar(_info.gameObject, centro, centro, new Vector2(0f, -272f), new Vector2(1020f, 54f));

            _status = CriarTexto(_janela.transform, "", 15, CorTexto, TextAnchor.MiddleLeft);
            Colocar(_status.gameObject, centro, centro, new Vector2(-105f, -320f), new Vector2(810f, 30f));

            GameObject voltar = Ui.CreateButton("Voltar", _janela.transform, centro, centro, new Vector2(425f, -320f), 160f, 40f);
            voltar.GetComponent<Button>().onClick.AddListener(AjustesMenu.FecharJanela);
        }

        private static GameObject CriarRolagem(float largura, float altura, Vector2 posicao)
        {
            GameObject rolagem = Ui.CreateScrollView(_janela.transform, false, true, 8f, Ui.ValheimScrollbarHandleColorBlock, 0f,
                new Color(0.157f, 0.102f, 0.063f, 1f), largura, altura);
            ((RectTransform)rolagem.transform).anchoredPosition = posicao;
            Image fundo = rolagem.AddComponent<Image>();
            fundo.color = FundoLista;
            fundo.raycastTarget = false;
            rolagem.transform.SetAsFirstSibling();
            return rolagem;
        }

        private static Text CriarTexto(Transform pai, string texto, int tamanho, Color cor, TextAnchor alinhamento)
        {
            GameObject go = Ui.CreateText(texto, pai, Vector2.zero, Vector2.zero, Vector2.zero, Ui.AveriaSerifBold, tamanho, cor,
                true, Color.black, 100f, 20f, false);
            Text componente = go.GetComponent<Text>();
            componente.alignment = alinhamento;
            componente.horizontalOverflow = HorizontalWrapMode.Wrap;
            componente.verticalOverflow = VerticalWrapMode.Truncate;
            componente.supportRichText = true;
            componente.raycastTarget = false;
            return componente;
        }

        private static GameObject Retangulo(string nome, Transform pai, float largura, float altura)
        {
            GameObject go = new GameObject(nome, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(pai, false);
            rect.sizeDelta = new Vector2(largura, altura);
            return go;
        }

        private static void Colocar(GameObject go, Vector2 ancora, Vector2 pivo, Vector2 posicao, Vector2 tamanho)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = ancora;
            rect.anchorMax = ancora;
            rect.pivot = pivo;
            rect.sizeDelta = tamanho;
            rect.anchoredPosition = posicao;
        }

        private static void Esticar(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
        }

        // ----------------------------------------------------------------- tipos

        private sealed class Secao
        {
            public string Nome;
            public GameObject Objeto;
            public readonly List<Linha> Linhas = new List<Linha>();
        }

        private sealed class Linha
        {
            public ItemAjuste Item;
            public GameObject Objeto;
            public InputField Campo;
            public Toggle Caixa;
            public Text RotuloCaixa;
            public Text ValorLista;
            public Button Padrao;

            /// <summary>Ligado enquanto a janela escreve no controle: nao e o jogador mexendo.</summary>
            public bool Atualizando;

            public void Mostrar()
            {
                Atualizando = true;
                try
                {
                    if (Campo != null) Campo.text = Item.Valor;
                    if (Caixa != null)
                    {
                        bool ligado = string.Equals(Item.Valor, "true", StringComparison.OrdinalIgnoreCase);
                        Caixa.isOn = ligado;
                        RotuloCaixa.text = ligado ? "Ligado" : "Desligado";
                    }
                    if (ValorLista != null) ValorLista.text = Item.Valor;
                    if (Padrao != null) Padrao.interactable = !Item.SomenteLeitura && Item.Valor != Item.Padrao;
                }
                finally
                {
                    Atualizando = false;
                }
            }
        }

        /// <summary>Mostra a descricao completa da opcao embaixo quando o mouse passa na linha.</summary>
        private sealed class AoPassar : MonoBehaviour, IPointerEnterHandler
        {
            public Action Acao;

            public void OnPointerEnter(PointerEventData eventData) => Acao?.Invoke();
        }
    }
}
