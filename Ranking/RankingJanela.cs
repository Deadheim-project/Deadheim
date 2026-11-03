using Deadheim.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Ranking
{
    /// <summary>
    /// A janela do Ranking, na mesma interface dos NPCs (Shared/ValheimUi: painel de madeira,
    /// fontes e botoes do jogo). Abre pela Central do inventario (Tab), pelo ESC > Ranking e pelo
    /// comando /ranking.
    ///
    ///  - Esquerda: as categorias, agrupadas (Geral, Combate, Progresso, Oficio, Exploracao,
    ///    Riqueza, Mundo), cada uma com a sua posicao nela.
    ///  - Barra de cima: Temporada/Semana, Vikings/Guildas, filtro (todos, online, minha guilda) e
    ///    busca por nome.
    ///  - Podio dos tres primeiros, a tabela com a seta de quanto cada um subiu ou desceu desde a
    ///    meia-noite, e embaixo, sempre visivel, a sua linha: posicao, valor e quanto falta para
    ///    passar quem esta na sua frente.
    ///  - Clicar num nome abre a ficha do jogador: a posicao dele em todas as categorias.
    ///  - "Feitos do mundo": o primeiro a derrotar cada chefe.
    ///
    /// A foto vem do servidor (RankingRede) e e renovada sozinha com a janela aberta; tudo o mais
    /// (ordenar, filtrar, buscar) e feito aqui, sem esperar a rede. Os widgets sao montados uma vez
    /// e reaproveitados (as linhas da tabela vem de um conjunto fixo).
    /// </summary>
    internal sealed class RankingJanela : MonoBehaviour
    {
        public enum Origem
        {
            Central,
            Esc,
            Comando
        }

        private enum Visao { Vikings, Guildas }
        private enum Filtro { Todos, Online, MinhaGuilda }

        private const string IdFeitos = "@feitos";
        private const int Ordem = 5020;
        private const int MaxLinhas = 100;
        private const float Largura = 1120f;
        private const float Altura = 720f;
        private const float AlturaLinha = 38f;
        private const float RenovarSeg = 20f;

        private static readonly Color Ouro = new Color(1f, 0.84f, 0.35f, 1f);
        private static readonly Color Prata = new Color(0.80f, 0.82f, 0.86f, 1f);
        private static readonly Color Bronze = new Color(0.86f, 0.55f, 0.30f, 1f);
        private static readonly Color Verde = new Color(0.50f, 0.82f, 0.54f, 1f);
        private static readonly Color Vermelho = new Color(0.92f, 0.42f, 0.36f, 1f);
        private static readonly Color FundoLinha = new Color(0f, 0f, 0f, 0.30f);
        private static readonly Color FundoLinhaPar = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color FundoMinha = new Color(1f, 0.63f, 0.24f, 0.22f);
        private static readonly Color FundoHover = new Color(1f, 0.70f, 0.30f, 0.30f);
        private static readonly Color Selecionado = new Color(1f, 0.82f, 0.55f, 1f);

        // Escolhas do jogador: valem para a proxima vez que ele abrir.
        private static string _categoria = Categorias.Lenda;
        private static bool _semana;
        private static Visao _visao = Visao.Vikings;
        private static Filtro _filtro = Filtro.Todos;
        private static string _busca = "";

        private static RankingJanela _instancia;
        private static Origem _origem;
        private static int _abertaNoFrame;
        private static float _abertaEm;

        public static bool Aberta => _instancia != null && _instancia._canvas != null;
        /// <summary>Aberta pelo ESC: ao fechar, o menu do ESC volta.</summary>
        public static bool DoEsc => Aberta && _origem == Origem.Esc;

        // ------------------------------------------------------------------ widgets
        private GameObject _canvas;
        private RectTransform _painel;
        private TextMeshProUGUI _subtitulo;
        private RectTransform _listaCategorias;
        private readonly List<BotaoCategoria> _botoesCategoria = new List<BotaoCategoria>();
        private Button _bTemporada, _bSemana, _bVikings, _bGuildas, _bFiltro;
        private TMP_InputField _campoBusca;
        private Image _iconeCabecalho;
        private TextMeshProUGUI _tituloCabecalho, _descricaoCabecalho;
        private RectTransform _podio;
        private readonly CartaoPodio[] _cartoes = new CartaoPodio[3];
        private RectTransform _cabecalhoTabela;
        private TextMeshProUGUI _colValor, _colDetalhe, _colNome;
        private RectTransform _caixaTabela;
        private RectTransform _tabela;
        private ScrollRect _rolagem;
        private readonly List<LinhaUi> _linhas = new List<LinhaUi>();
        private TextMeshProUGUI _vazio;
        private RectTransform _rodape;
        private TextMeshProUGUI _rodapePos, _rodapeTexto;
        private Button _irParaMim;
        private int _indiceMinhaLinha = -1;

        private GameObject _ficha;

        // dica ao passar o mouse (como cada ranking e calculado)
        private RectTransform _dica;
        private TextMeshProUGUI _dicaTexto;
        private string _dicaAtual;

        // ------------------------------------------------------------------ estado
        private int _versaoMostrada = -1;
        private bool _sujo = true;
        private float _proximoSegundo;
        private RankingContexto _ctx;
        private List<Categoria> _cats = new List<Categoria>();
        private readonly Dictionary<string, List<Linha>> _cache = new Dictionary<string, List<Linha>>();

        // ================================================================== API

        public static void Abrir(Origem origem)
        {
            if (Player.m_localPlayer == null) return;
            if (!RankingRede.Ligado)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "O ranking está desligado neste servidor.");
                return;
            }
            if (!ValheimUi.EnsureAssets()) return;

            if (_instancia == null)
            {
                var go = new GameObject("DeadheimRanking");
                DontDestroyOnLoad(go);
                _instancia = go.AddComponent<RankingJanela>();
            }
            if (Aberta) _instancia.Destruir();

            _origem = origem;
            _abertaNoFrame = Time.frameCount;
            _abertaEm = Time.unscaledTime;
            try
            {
                _instancia.Montar();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Ranking: nao consegui montar a janela: " + ex);
                _instancia.Destruir();
                return;
            }
            RankingRede.Pedir(0f);
        }

        public static void Fechar()
        {
            if (_instancia == null || !Aberta) return;
            bool doEsc = _origem == Origem.Esc;
            _instancia.Destruir();
            if (doEsc) Ajustes.AjustesMenu.DevolverMenu();
            else ValheimUi.ReleaseCursor();
        }

        /// <summary>ESC: fecha a ficha aberta, senao a janela. Devolve se fez algo.</summary>
        public static bool Voltar()
        {
            if (!Aberta || Time.frameCount == _abertaNoFrame) return Aberta;
            if (_instancia._ficha != null)
            {
                _instancia.FecharFicha();
                return true;
            }
            Fechar();
            return true;
        }

        private static int _resumoVersao = -1;
        private static string _resumo;

        /// <summary>Linha de baixo do botao "Ranking" da Central: a sua posicao na Pontuacao geral.</summary>
        public static string ResumoCentral()
        {
            RankingRede.Pedir(180f);
            RankingRede.Foto foto = RankingRede.Atual;
            Player player = Player.m_localPlayer;
            if (foto == null || player == null) return "Os melhores do mundo, em cada categoria";
            // A Central pergunta duas vezes por segundo; a resposta so muda com foto nova.
            if (_resumoVersao == RankingRede.Versao && _resumo != null) return _resumo;
            _resumoVersao = RankingRede.Versao;
            _resumo = MontarResumo(foto, player);
            return _resumo;
        }

        private static string MontarResumo(RankingRede.Foto foto, Player player)
        {
            RankingContexto ctx = RankingRede.Contexto(foto.TemGuerra, foto.TemNivel, foto.TemArena, foto.TemMissoes);
            Categoria lenda = Categorias.Todas(ctx).FirstOrDefault(c => c.Id == Categorias.Lenda);
            if (lenda == null) return "Os melhores do mundo, em cada categoria";
            List<Linha> linhas = Classificacao.Classificar(foto.Jogadores, lenda, false);
            Linha minha = linhas.FirstOrDefault(l => l.Jogador.Id == player.GetPlayerID());
            return minha == null
                ? $"{linhas.Count} vikings na Pontuação geral · entre para a lista"
                : $"Você é #{minha.Posicao} de {linhas.Count} na Pontuação geral · {lenda.Formatar(minha.Valor)} pts";
        }

        // ================================================================== ciclo

        private void Update()
        {
            if (!Aberta) return;
            if (Player.m_localPlayer == null)
            {
                Destruir();
                return;
            }

            // ESC: quem trata e o prefixo do Menu.Update (AjustesMenu), para o mesmo ESC nao abrir
            // nem fechar o menu do jogo. Sem aquele patch, a janela cuida sozinha.
            if (!Ajustes.AjustesMenu.MenuCorrigido && Input.GetKeyDown(KeyCode.Escape))
            {
                Voltar();
                return;
            }

            if (!Vanilla.Ui.IsTyping() && (_campoBusca == null || !_campoBusca.isFocused) && _ficha == null)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) PularCategoria(1);
                else if (Input.GetKeyDown(KeyCode.UpArrow)) PularCategoria(-1);
            }

            AtualizarDica();

            RankingRede.Pedir(RenovarSeg);
            if (RankingRede.Versao != _versaoMostrada)
            {
                _versaoMostrada = RankingRede.Versao;
                _cache.Clear();
                _sujo = true;
            }
            if (_sujo)
            {
                _sujo = false;
                try { Redesenhar(); }
                catch (Exception ex) { Debug.LogError("[Deadheim] Ranking: falha ao desenhar: " + ex); }
            }
            if (Time.unscaledTime >= _proximoSegundo)
            {
                _proximoSegundo = Time.unscaledTime + 1f;
                AtualizarSubtitulo();
            }
        }

        // ================================================================== dica

        /// <summary>Liga uma dica (texto sob demanda) ao mouse passar sobre o objeto.</summary>
        private void ComDica(GameObject alvo, Func<string> texto)
        {
            var passar = alvo.GetComponent<AoPassarDica>() ?? alvo.AddComponent<AoPassarDica>();
            passar.Texto = texto;
            passar.Dono = this;
        }

        private void MostrarDica(string texto) => _dicaAtual = string.IsNullOrEmpty(texto) ? null : texto;

        private void EsconderDica(AoPassarDica quem)
        {
            // So apaga se a dica atual ainda e a de quem saiu (o mouse pode ja estar noutro alvo).
            if (_dicaAtual != null && quem.Texto != null && quem.Texto() == _dicaAtual) _dicaAtual = null;
        }

        private void AtualizarDica()
        {
            if (_painel == null) return;
            if (_dica == null)
            {
                _dica = ValheimUi.CreateRect("Dica", _painel);
                _dica.pivot = new Vector2(0f, 1f);
                _dica.anchorMin = _dica.anchorMax = new Vector2(0.5f, 0.5f);
                var fundo = _dica.gameObject.AddComponent<Image>();
                fundo.color = new Color(0.05f, 0.04f, 0.03f, 0.96f);
                fundo.raycastTarget = false;
                _dicaTexto = ValheimUi.CreateLabel(_dica, "", 14, ValheimUi.Beige, TextAlignmentOptions.TopLeft);
                ValheimUi.Stretch((RectTransform)_dicaTexto.transform, 10f, 8f);
                _dica.gameObject.SetActive(false);
            }

            if (_dicaAtual == null || _ficha != null)
            {
                if (_dica.gameObject.activeSelf) _dica.gameObject.SetActive(false);
                return;
            }

            const float larg = 380f;
            _dicaTexto.text = _dicaAtual;
            float alt = _dicaTexto.GetPreferredValues(_dicaAtual, larg - 20f, 0f).y + 18f;
            _dica.sizeDelta = new Vector2(larg, alt);
            if (!_dica.gameObject.activeSelf) _dica.gameObject.SetActive(true);
            _dica.SetAsLastSibling();

            Canvas canvas = _canvas.GetComponent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_painel, Input.mousePosition, cam, out local)) return;
            Rect r = _painel.rect;
            // Pivot do painel no centro: converte para o espaco do painel (origem no centro, ancora no centro).
            float x = local.x + 18f, y = local.y - 22f;
            if (x + larg > r.xMax - 6f) x = local.x - larg - 12f;
            if (y - alt < r.yMin + 6f) y = local.y + alt + 18f;
            _dica.anchoredPosition = new Vector2(x, y);
        }

        private sealed class AoPassarDica : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            public Func<string> Texto;
            public RankingJanela Dono;

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Dono != null) Dono.MostrarDica(Texto != null ? Texto() : null); }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Dono != null) Dono.EsconderDica(this); }
        }

        // Depois do jogo, que trava o cursor de novo a cada frame.
        private void LateUpdate()
        {
            if (Aberta) ValheimUi.HoldCursor();
        }

        private void Destruir()
        {
            FecharFicha();
            if (_canvas != null) Destroy(_canvas);
            _canvas = null;
            _botoesCategoria.Clear();
            _linhas.Clear();
            _cache.Clear();
            _versaoMostrada = -1;
        }

        private void OnDestroy() => Destruir();

        /// <summary>Redesenha no proximo frame. A classificacao em cache so cai com foto nova.</summary>
        private void Invalidar() => _sujo = true;

        // ================================================================== montar

        private void Montar()
        {
            _canvas = ValheimUi.CreateCanvas("Deadheim_Ranking", Ordem);
            if (_canvas == null) throw new InvalidOperationException("GUI do jogo nao encontrada");

            _dicaAtual = null;

            // Fundo escuro atras da janela: o jogo continua, mas o olho vai para o ranking.
            var veu = ValheimUi.CreateRect("Veu", _canvas.transform);
            ValheimUi.Stretch(veu, 0f, 0f);
            veu.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            _painel = ValheimUi.CreatePanel(_canvas.transform, Largura, Altura);
            _painel.anchorMin = _painel.anchorMax = new Vector2(0.5f, 0.5f);
            _painel.anchoredPosition = Vector2.zero;

            var barra = ValheimUi.CreateRect("Titulo", _painel);
            ValheimUi.Anchor(barra, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -58f), Vector2.zero);
            barra.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // so para o arrasto
            barra.gameObject.AddComponent<DragWindow>().Target = _painel;

            var titulo = ValheimUi.CreateLabel(barra, "Ranking", 34, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Stretch((RectTransform)titulo.transform, 80f, 6f);

            var x = ValheimUi.CreateButton(_painel, "X", 36f, 36f, 18);
            ValheimUi.Anchor((RectTransform)x.transform, Vector2.one, Vector2.one, new Vector2(-52f, -52f), new Vector2(-16f, -16f));
            x.onClick.AddListener(Fechar);

            _subtitulo = ValheimUi.CreateLabel(_painel, "", 14, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)_subtitulo.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(60f, -84f), new Vector2(-60f, -58f));

            MontarCategorias();
            MontarDireita();
        }

        private void MontarCategorias()
        {
            var caixa = ValheimUi.CreateInlay(_painel, "Categorias");
            ValheimUi.Anchor(caixa, Vector2.zero, new Vector2(0f, 1f), new Vector2(22f, 22f), new Vector2(272f, -92f));
            var area = ValheimUi.CreateRect("Area", caixa);
            ValheimUi.Stretch(area, 4f, 4f);
            _listaCategorias = ValheimUi.CreateScrollList(area, spacing: 3f);
        }

        /// <summary>A lista de categorias muda com o servidor (guerra, nivel, riqueza): refeita quando muda.</summary>
        private void RefazerCategorias()
        {
            string assinatura = string.Join(",", _cats.Select(c => c.Id));
            if (_listaCategorias.name == assinatura) return;
            _listaCategorias.name = assinatura;

            foreach (Transform filho in _listaCategorias) Destroy(filho.gameObject);
            _botoesCategoria.Clear();

            string grupo = null;
            foreach (Categoria c in _cats)
            {
                if (c.Grupo != grupo)
                {
                    grupo = c.Grupo;
                    Cabecalho(_listaCategorias, grupo.ToUpperInvariant());
                }
                CriarBotaoCategoria(c.Id, c.Titulo, c.Icone);
            }
            Cabecalho(_listaCategorias, "MUNDO");
            CriarBotaoCategoria(IdFeitos, "Feitos do mundo", "TrophyFader");
        }

        private static void Cabecalho(RectTransform lista, string texto)
        {
            var label = ValheimUi.CreateLabel(lista, texto, 14, ValheimUi.Orange, TextAlignmentOptions.BottomLeft, display: true);
            ValheimUi.SetHeight(label.gameObject, 24f);
        }

        private void CriarBotaoCategoria(string id, string titulo, string icone)
        {
            Button botao = ValheimUi.CreateButton(_listaCategorias, titulo, 0f, 40f, 15);
            SemNavegacao(botao);
            var b = new BotaoCategoria { Id = id, Botao = botao };

            b.Titulo = botao.GetComponentInChildren<TextMeshProUGUI>();
            b.Titulo.alignment = TextAlignmentOptions.Left;
            ValheimUi.Anchor((RectTransform)b.Titulo.transform, Vector2.zero, Vector2.one, new Vector2(44f, 2f), new Vector2(-56f, -2f));

            Image img = ValheimUi.CreateItemIcon(botao.transform, icone, 28f);
            ValheimUi.Anchor((RectTransform)img.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(9f, -14f), new Vector2(37f, 14f));
            img.raycastTarget = false;

            b.Posicao = ValheimUi.CreateLabel(botao.transform, "", 13, ValheimUi.Muted, TextAlignmentOptions.Right);
            ValheimUi.Anchor((RectTransform)b.Posicao.transform, new Vector2(1f, 0f), Vector2.one, new Vector2(-58f, 0f), new Vector2(-10f, 0f));

            botao.onClick.AddListener(() =>
            {
                _categoria = id;
                FecharFicha();
                Invalidar();
                if (_rolagem != null) _rolagem.verticalNormalizedPosition = 1f;
            });
            _botoesCategoria.Add(b);
            ComDica(botao.gameObject, () => id == IdFeitos
                ? "O primeiro viking a derrotar cada chefe neste mundo, com a data. Quem já tinha o chefe antes do ranking existir aparece como \"antes do ranking\"."
                : _cats.FirstOrDefault(x => x.Id == id)?.Calculo);
        }

        private void MontarDireita()
        {
            var direita = ValheimUi.CreateRect("Direita", _painel);
            ValheimUi.Anchor(direita, Vector2.zero, Vector2.one, new Vector2(284f, 22f), new Vector2(-22f, -92f));

            // ---- barra de cima
            var barra = ValheimUi.Strip(direita, TextAnchor.MiddleLeft, 6f);
            ValheimUi.Anchor(barra, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -36f), Vector2.zero);

            _bTemporada = Segmento(barra, "Temporada", () => { _semana = false; Invalidar(); });
            _bSemana = Segmento(barra, "Semana", () => { _semana = true; Invalidar(); });
            ComDica(_bTemporada.gameObject, () => "Temporada: o total acumulado de cada jogador desde que o ranking existe neste mundo.");
            ComDica(_bSemana.gameObject, () => "Semana: só o que cada jogador fez desde segunda-feira 00:00 (horário do servidor). Zera toda segunda.");
            Espaco(barra, 10f);
            _bVikings = Segmento(barra, "Vikings", () => { _visao = Visao.Vikings; Invalidar(); });
            _bGuildas = Segmento(barra, "Guildas", () => { _visao = Visao.Guildas; Invalidar(); });
            ComDica(_bVikings.gameObject, () => "Vikings: cada jogador na sua posição. Admins do servidor não entram no ranking.");
            ComDica(_bGuildas.gameObject, () => "Guildas: a soma da categoria entre todos os membros da guilda. Aparece também o melhor membro.");
            Espaco(barra, 10f);
            _bFiltro = Segmento(barra, "Todos", () =>
            {
                _filtro = (Filtro)(((int)_filtro + 1) % 3);
                Invalidar();
            }, 124f);

            _campoBusca = ValheimUi.CreateInputField(direita, _busca, 210f, 34f, 15);
            var rectBusca = (RectTransform)_campoBusca.transform;
            ValheimUi.Anchor(rectBusca, Vector2.one, Vector2.one, new Vector2(-210f, -35f), new Vector2(0f, -1f));
            var dica = ValheimUi.CreateLabel(_campoBusca.textViewport, "Buscar viking...", 15, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);
            ValheimUi.Stretch((RectTransform)dica.transform, 0f, 0f);
            _campoBusca.placeholder = dica;
            _campoBusca.onValueChanged.AddListener(texto =>
            {
                _busca = texto ?? "";
                Invalidar();
            });

            // ---- cabecalho da categoria
            var cab = ValheimUi.CreateRect("Cabecalho", direita);
            ValheimUi.Anchor(cab, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -94f), new Vector2(0f, -44f));
            var fundoCab = cab.gameObject.AddComponent<Image>();
            fundoCab.color = new Color(0f, 0f, 0f, 0f);
            ComDica(cab.gameObject, () => _categoria == IdFeitos ? null : _cats.FirstOrDefault(x => x.Id == _categoria)?.Calculo);
            _iconeCabecalho = ValheimUi.CreateItemIcon(cab, null, 46f);
            ValheimUi.Anchor((RectTransform)_iconeCabecalho.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, -23f), new Vector2(48f, 23f));
            _tituloCabecalho = ValheimUi.CreateLabel(cab, "", 26, ValheimUi.Orange, TextAlignmentOptions.BottomLeft, display: true);
            ValheimUi.Anchor((RectTransform)_tituloCabecalho.transform, new Vector2(0f, 0.45f), Vector2.one, new Vector2(60f, 0f), Vector2.zero);
            _descricaoCabecalho = ValheimUi.CreateLabel(cab, "", 14, ValheimUi.Beige, TextAlignmentOptions.TopLeft);
            ValheimUi.Anchor((RectTransform)_descricaoCabecalho.transform, Vector2.zero, new Vector2(1f, 0.45f), new Vector2(60f, 0f), Vector2.zero);
            _descricaoCabecalho.textWrappingMode = TextWrappingModes.NoWrap;
            _descricaoCabecalho.overflowMode = TextOverflowModes.Ellipsis;

            // ---- podio (2, 1, 3)
            _podio = ValheimUi.CreateRect("Podio", direita);
            ValheimUi.Anchor(_podio, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -236f), new Vector2(0f, -102f));
            _cartoes[1] = CriarCartao(_podio, 0f, 0.32f, 16f, 2, Prata);
            _cartoes[0] = CriarCartao(_podio, 0.34f, 0.66f, 0f, 1, Ouro);
            _cartoes[2] = CriarCartao(_podio, 0.68f, 1f, 16f, 3, Bronze);

            // ---- tabela
            _cabecalhoTabela = ValheimUi.CreateRect("Colunas", direita);
            _colNome = ColunaTitulo("Viking", TextAlignmentOptions.BottomLeft);
            _colValor = ColunaTitulo("Valor", TextAlignmentOptions.BottomRight);
            _colDetalhe = ColunaTitulo("Detalhe", TextAlignmentOptions.BottomLeft);
            var colPos = ColunaTitulo("#", TextAlignmentOptions.Bottom);
            ValheimUi.Anchor((RectTransform)colPos.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(58f, 0f));
            ValheimUi.Anchor((RectTransform)_colNome.transform, Vector2.zero, new Vector2(0.48f, 1f), new Vector2(108f, 0f), Vector2.zero);
            ValheimUi.Anchor((RectTransform)_colValor.transform, new Vector2(0.48f, 0f), new Vector2(0.66f, 1f), Vector2.zero, new Vector2(-8f, 0f));
            ValheimUi.Anchor((RectTransform)_colDetalhe.transform, new Vector2(0.66f, 0f), Vector2.one, new Vector2(10f, 0f), new Vector2(-20f, 0f));

            _caixaTabela = ValheimUi.CreateInlay(direita, "Tabela");
            var area = ValheimUi.CreateRect("Area", _caixaTabela);
            ValheimUi.Stretch(area, 4f, 4f);
            _tabela = ValheimUi.CreateScrollList(area, spacing: 2f);
            _rolagem = _tabela.GetComponentInParent<ScrollRect>();

            _vazio = ValheimUi.CreateLabel(_caixaTabela, "", 17, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)_vazio.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(30f, -60f), new Vector2(-30f, 60f));

            // ---- a sua linha, sempre visivel
            _rodape = ValheimUi.CreateInlay(direita, "Voce");
            ValheimUi.Anchor(_rodape, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 58f));
            var brilho = ValheimUi.CreateRect("Brilho", _rodape);
            ValheimUi.Stretch(brilho, 3f, 3f);
            var imgBrilho = brilho.gameObject.AddComponent<Image>();
            imgBrilho.color = FundoMinha;
            imgBrilho.raycastTarget = false;

            _rodapePos = ValheimUi.CreateLabel(_rodape, "", 24, Ouro, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)_rodapePos.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(100f, 0f));
            _rodapeTexto = ValheimUi.CreateLabel(_rodape, "", 15, ValheimUi.Beige, TextAlignmentOptions.Left);
            ValheimUi.Anchor((RectTransform)_rodapeTexto.transform, Vector2.zero, Vector2.one, new Vector2(108f, 4f), new Vector2(-150f, -4f));
            _rodapeTexto.overflowMode = TextOverflowModes.Ellipsis;

            _irParaMim = ValheimUi.CreateButton(_rodape, "Ir para mim", 128f, 36f, 14);
            ValheimUi.Anchor((RectTransform)_irParaMim.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-138f, -18f), new Vector2(-10f, 18f));
            _irParaMim.onClick.AddListener(IrParaMim);

            PosicionarTabela(true);
        }

        /// <summary>Com podio a tabela comeca embaixo dele; sem (feitos, busca, filtro), ocupa o lugar.</summary>
        private void PosicionarTabela(bool comPodio)
        {
            float topo = comPodio ? -244f : -102f;
            ValheimUi.Anchor(_cabecalhoTabela, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, topo - 22f), new Vector2(0f, topo));
            ValheimUi.Anchor(_caixaTabela, Vector2.zero, Vector2.one, new Vector2(0f, 64f), new Vector2(0f, topo - 24f));
            _podio.gameObject.SetActive(comPodio);
        }

        private static void Espaco(RectTransform barra, float largura)
        {
            var e = ValheimUi.CreateRect("Espaco", barra);
            ValheimUi.SetWidth(e.gameObject, largura);
        }

        private static Button Segmento(RectTransform barra, string texto, Action clique, float largura = 96f)
        {
            Button b = ValheimUi.CreateButton(barra, texto, largura, 34f, 15);
            SemNavegacao(b);
            ValheimUi.SetWidth(b.gameObject, largura);
            b.onClick.AddListener(() => clique());
            return b;
        }

        private static void Marcar(Button b, bool ligado, bool habilitado = true)
        {
            if (b == null) return;
            b.interactable = habilitado;
            b.image.color = ligado ? Selecionado : Color.white;
            var t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.color = !habilitado ? ValheimUi.Muted : ligado ? ValheimUi.Yellow : ValheimUi.Orange;
        }

        private TextMeshProUGUI ColunaTitulo(string texto, TextAlignmentOptions alinhamento) =>
            ValheimUi.CreateLabel(_cabecalhoTabela, texto, 13, ValheimUi.Muted, alinhamento);

        private CartaoPodio CriarCartao(RectTransform podio, float x0, float x1, float rebaixo, int lugar, Color cor)
        {
            var caixa = ValheimUi.CreateInlay(podio, "Lugar" + lugar);
            ValheimUi.Anchor(caixa, new Vector2(x0, 0f), new Vector2(x1, 1f), Vector2.zero, new Vector2(0f, -rebaixo));

            var faixa = ValheimUi.CreateRect("Faixa", caixa);
            ValheimUi.Anchor(faixa, new Vector2(0f, 1f), Vector2.one, new Vector2(3f, -6f), new Vector2(-3f, -3f));
            var imgFaixa = faixa.gameObject.AddComponent<Image>();
            imgFaixa.color = new Color(cor.r, cor.g, cor.b, 0.85f);
            imgFaixa.raycastTarget = false;

            var c = new CartaoPodio { Caixa = caixa };
            c.Medalha = ValheimUi.CreateLabel(caixa, lugar + "º", lugar == 1 ? 34 : 28, cor, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)c.Medalha.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 6f), new Vector2(62f, -8f));

            c.Nome = ValheimUi.CreateLabel(caixa, "", lugar == 1 ? 20 : 18, ValheimUi.Beige, TextAlignmentOptions.BottomLeft);
            ValheimUi.Anchor((RectTransform)c.Nome.transform, new Vector2(0f, 0.62f), Vector2.one, new Vector2(66f, 0f), new Vector2(-8f, -10f));
            c.Nome.textWrappingMode = TextWrappingModes.NoWrap;
            c.Nome.overflowMode = TextOverflowModes.Ellipsis;

            c.Guilda = ValheimUi.CreateLabel(caixa, "", 13, ValheimUi.Muted, TextAlignmentOptions.TopLeft);
            ValheimUi.Anchor((RectTransform)c.Guilda.transform, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f), new Vector2(66f, 0f), new Vector2(-8f, 0f));
            c.Guilda.textWrappingMode = TextWrappingModes.NoWrap;
            c.Guilda.overflowMode = TextOverflowModes.Ellipsis;

            c.Valor = ValheimUi.CreateLabel(caixa, "", lugar == 1 ? 24 : 20, cor, TextAlignmentOptions.Left, display: true);
            ValheimUi.Anchor((RectTransform)c.Valor.transform, Vector2.zero, new Vector2(1f, 0.38f), new Vector2(66f, 6f), new Vector2(-8f, 0f));
            c.Valor.textWrappingMode = TextWrappingModes.NoWrap;
            c.Valor.overflowMode = TextOverflowModes.Ellipsis;

            // O cartao inteiro e clicavel: abre a ficha.
            c.Botao = caixa.gameObject.AddComponent<Button>();
            c.Botao.transition = Selectable.Transition.None;
            SemNavegacao(c.Botao);
            c.Botao.onClick.AddListener(() => { if (c.Jogador != null) AbrirFicha(c.Jogador); });
            return c;
        }

        // ================================================================== desenhar

        private void Redesenhar()
        {
            RankingRede.Foto foto = RankingRede.Atual;
            _ctx = foto != null
                ? RankingRede.Contexto(foto.TemGuerra, foto.TemNivel, foto.TemArena, foto.TemMissoes)
                : RankingRede.Contexto(false, false, false, false);
            _cats = Categorias.Todas(_ctx);
            RefazerCategorias();

            bool feitos = _categoria == IdFeitos;
            Categoria cat = feitos ? null : _cats.FirstOrDefault(c => c.Id == _categoria);
            if (!feitos && cat == null)
            {
                _categoria = Categorias.Lenda;
                cat = _cats[0];
            }

            AtualizarBotoesCategoria(foto);

            Marcar(_bTemporada, !_semana, !feitos);
            Marcar(_bSemana, _semana, !feitos);
            Marcar(_bVikings, _visao == Visao.Vikings, !feitos);
            Marcar(_bGuildas, _visao == Visao.Guildas, !feitos);
            Marcar(_bFiltro, _filtro != Filtro.Todos, !feitos && _visao == Visao.Vikings);
            var tFiltro = _bFiltro.GetComponentInChildren<TextMeshProUGUI>();
            if (tFiltro != null) tFiltro.text = _filtro == Filtro.Online ? "Só online" : _filtro == Filtro.MinhaGuilda ? "Minha guilda" : "Todos";

            if (feitos)
            {
                _iconeCabecalho.sprite = ValheimUi.FindItemIcon("TrophyFader");
                _tituloCabecalho.text = "Feitos do mundo";
                _descricaoCabecalho.text = "O primeiro viking a derrotar cada chefe neste mundo.";
            }
            else
            {
                _iconeCabecalho.sprite = ValheimUi.FindItemIcon(cat.Icone);
                _tituloCabecalho.text = cat.Titulo + " <size=16><color=#9e9480>(?)</color></size>" + (_visao == Visao.Guildas ? " <size=18><color=#9e9480>· por guilda</color></size>" : "");
                _descricaoCabecalho.text = _semana && foto != null
                    ? cat.Descricao + "  <color=#ffd75a>Semana desde " + Data(foto.InicioSemana, false) + ".</color>"
                    : cat.Descricao;
            }
            _iconeCabecalho.enabled = _iconeCabecalho.sprite != null;

            if (foto == null)
            {
                PosicionarTabela(false);
                MostrarLinhas(0);
                _vazio.text = "Carregando o ranking do servidor...";
                Rodape(null, "", false);
                return;
            }

            if (feitos) DesenharFeitos(foto);
            else if (_visao == Visao.Guildas) DesenharGuildas(foto, cat);
            else DesenharVikings(foto, cat);
            AtualizarSubtitulo();
        }

        private void AtualizarBotoesCategoria(RankingRede.Foto foto)
        {
            long eu = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;
            foreach (BotaoCategoria b in _botoesCategoria)
            {
                bool ativo = b.Id == _categoria;
                b.Botao.image.color = ativo ? Selecionado : Color.white;
                b.Titulo.color = ativo ? ValheimUi.Yellow : ValheimUi.Orange;

                string pos = "";
                Categoria c = _cats.FirstOrDefault(x => x.Id == b.Id);
                if (foto != null && c != null)
                {
                    Linha minha = Linhas(foto, c).FirstOrDefault(l => l.Jogador.Id == eu);
                    if (minha != null) pos = minha.Posicao <= 3 ? $"<color={CorHex(CorLugar(minha.Posicao))}>#{minha.Posicao}</color>" : "#" + minha.Posicao;
                }
                else if (foto != null && b.Id == IdFeitos)
                {
                    pos = Math.Max(foto.Feitos.Count, foto.ChefesDoMundo.Count) + "/" + _ctx.OrdemChefes.Count;
                }
                b.Posicao.text = pos;
            }
        }

        private List<Linha> Linhas(RankingRede.Foto foto, Categoria c)
        {
            string chave = c.Id + (_semana ? "|s" : "|t");
            List<Linha> linhas;
            if (!_cache.TryGetValue(chave, out linhas))
            {
                linhas = Classificacao.Classificar(foto.Jogadores, c, _semana);
                _cache[chave] = linhas;
            }
            return linhas;
        }

        private void DesenharVikings(RankingRede.Foto foto, Categoria cat)
        {
            _colNome.text = "Viking";
            _colValor.text = cat.Titulo;
            _colDetalhe.text = "Detalhe";

            Player player = Player.m_localPlayer;
            long eu = player.GetPlayerID();
            RankingJogador euNaFoto = foto.Achar(eu);
            string minhaGuilda = euNaFoto != null ? euNaFoto.Guilda : null;

            List<Linha> todas = Linhas(foto, cat);
            string busca = (_busca ?? "").Trim();
            IEnumerable<Linha> filtradas = todas;
            if (_filtro == Filtro.Online) filtradas = filtradas.Where(l => l.Jogador.Conectado);
            else if (_filtro == Filtro.MinhaGuilda)
                filtradas = filtradas.Where(l => !string.IsNullOrEmpty(minhaGuilda) && string.Equals(l.Jogador.Guilda, minhaGuilda, StringComparison.OrdinalIgnoreCase));
            if (busca.Length > 0)
                filtradas = filtradas.Where(l => l.Jogador.Nome.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0
                                                 || (l.Jogador.Guilda ?? "").IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0);
            List<Linha> mostrar = filtradas.ToList();

            bool comPodio = busca.Length == 0 && _filtro == Filtro.Todos && todas.Count > 0;
            PosicionarTabela(comPodio);
            if (comPodio)
            {
                for (int i = 0; i < 3; i++)
                {
                    Linha l = i < todas.Count ? todas[i] : null;
                    Cartao(_cartoes[i], l == null ? i + 1 : l.Posicao, l?.Jogador, l?.Jogador.Nome, l == null ? "" : GuildaTexto(l.Jogador),
                        l == null ? "" : cat.Formatar(l.Valor), l == null);
                }
            }

            // Com podio, a tabela comeca do 4o: os tres primeiros ja estao em cima.
            int pular = comPodio ? Math.Min(3, mostrar.Count) : 0;
            List<Linha> tabela = mostrar.Skip(pular).Take(MaxLinhas).ToList();
            MostrarLinhas(tabela.Count);
            _indiceMinhaLinha = -1;
            for (int i = 0; i < tabela.Count; i++)
            {
                Linha l = tabela[i];
                bool minha = l.Jogador.Id == eu;
                if (minha) _indiceMinhaLinha = i;
                int? mov = _semana ? null : Classificacao.Movimento(l, cat.Id);
                PreencherLinha(_linhas[i], i, minha, l.Posicao, Movimento(mov, _semana), null,
                    NomeTexto(l.Jogador), cat.Formatar(l.Valor), cat.Detalhe?.Invoke(l.Jogador, _semana) ?? "", l.Jogador);
            }

            if (todas.Count == 0)
                _vazio.text = _semana ? "Ninguém pontuou aqui nesta semana ainda.\nSeja o primeiro!" : "Ninguém pontuou aqui ainda.\nSeja o primeiro!";
            else if (mostrar.Count == 0)
                _vazio.text = busca.Length > 0 ? $"Ninguém com \"{busca}\" no nome ou na guilda."
                    : _filtro == Filtro.Online ? "Ninguém desta lista está online agora."
                    : string.IsNullOrEmpty(minhaGuilda) ? "Você não tem guilda." : "Ninguém da sua guilda nesta lista.";
            else if (tabela.Count == 0) _vazio.text = "Só estes vikings pontuaram: estão todos no pódio.";
            else _vazio.text = "";
            if (mostrar.Count - pular > MaxLinhas)
                _colDetalhe.text = $"Detalhe <color=#7a7262>· os {MaxLinhas} primeiros de {mostrar.Count - pular}; busque um nome</color>";

            // ---- a minha linha
            int indice = todas.FindIndex(l => l.Jogador.Id == eu);
            if (indice < 0)
            {
                Rodape("—", $"<b>{Esc(player.GetPlayerName())}</b>  <color=#9e9480>ainda fora desta lista.</color>  {DicaEntrar(cat)}", false);
                return;
            }
            Linha eu1 = todas[indice];
            string texto = $"<b>{Esc(eu1.Jogador.Nome)}</b>  <color=#ffa13c>{cat.Formatar(eu1.Valor)}</color>";
            int? movimento = _semana ? null : Classificacao.Movimento(eu1, cat.Id);
            if (movimento.HasValue && movimento.Value != 0) texto += "  " + Movimento(movimento, false);
            if (indice > 0)
            {
                // Quem esta logo a frente (com valor maior; empate divide a posicao).
                Linha frente = todas.Take(indice).LastOrDefault(l => l.Valor > eu1.Valor);
                if (frente != null)
                    texto += $"  <color=#9e9480>· faltam</color> {Falta(cat, frente.Valor - eu1.Valor)} <color=#9e9480>para passar</color> {Esc(frente.Jogador.Nome)} <color=#9e9480>(#{frente.Posicao})</color>";
            }
            else texto += "  <color=#ffd75a>· você lidera!</color>";
            texto += $"  <color=#9e9480>· de {todas.Count}</color>";
            Rodape("#" + eu1.Posicao, texto, eu1.Posicao > 3 || !comPodio);
        }

        private void DesenharGuildas(RankingRede.Foto foto, Categoria cat)
        {
            _colNome.text = "Guilda";
            _colValor.text = cat.Titulo;
            _colDetalhe.text = "Destaque";

            List<Classificacao.LinhaGuilda> todas = Classificacao.Guildas(foto.Jogadores, cat, _semana);
            string busca = (_busca ?? "").Trim();
            List<Classificacao.LinhaGuilda> mostrar = busca.Length > 0
                ? todas.Where(g => g.Guilda.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                : todas;

            bool comPodio = busca.Length == 0 && todas.Count > 0;
            PosicionarTabela(comPodio);
            if (comPodio)
            {
                for (int i = 0; i < 3; i++)
                {
                    Classificacao.LinhaGuilda g = i < todas.Count ? todas[i] : null;
                    Cartao(_cartoes[i], g == null ? i + 1 : g.Posicao, null, g?.Guilda, g == null ? "" : Membros(g.Membros), g == null ? "" : cat.Formatar(g.Valor), g == null);
                }
            }

            RankingJogador eu = foto.Achar(Player.m_localPlayer.GetPlayerID());
            string minha = eu?.Guilda;
            int pular = comPodio ? Math.Min(3, mostrar.Count) : 0;
            List<Classificacao.LinhaGuilda> tabela = mostrar.Skip(pular).Take(MaxLinhas).ToList();
            MostrarLinhas(tabela.Count);
            _indiceMinhaLinha = -1;
            for (int i = 0; i < tabela.Count; i++)
            {
                Classificacao.LinhaGuilda g = tabela[i];
                bool daMinha = !string.IsNullOrEmpty(minha) && string.Equals(g.Guilda, minha, StringComparison.OrdinalIgnoreCase);
                if (daMinha) _indiceMinhaLinha = i;
                string destaque = g.Destaque != null ? Esc(g.Destaque.Nome) + " <color=#9e9480>" + cat.Formatar(g.ValorDestaque) + "</color>" : "";
                PreencherLinha(_linhas[i], i, daMinha, g.Posicao, "", null,
                    Esc(g.Guilda) + "  <size=13><color=#9e9480>" + Membros(g.Membros) + "</color></size>",
                    cat.Formatar(g.Valor), destaque, null);
            }

            _vazio.text = todas.Count == 0 ? "Nenhuma guilda pontuou aqui ainda."
                : mostrar.Count == 0 ? $"Nenhuma guilda com \"{busca}\" no nome."
                : tabela.Count == 0 && pular > 0 ? "Só estas guildas pontuaram." : "";

            if (string.IsNullOrEmpty(minha))
            {
                Rodape("—", "<color=#9e9480>Você não tem guilda. Crie ou entre numa no NPC Registrador de Guildas.</color>", false);
                return;
            }
            int indice = todas.FindIndex(g => string.Equals(g.Guilda, minha, StringComparison.OrdinalIgnoreCase));
            if (indice < 0)
            {
                Rodape("—", $"<b>{Esc(minha)}</b>  <color=#9e9480>ainda não pontuou nesta categoria.</color>", false);
                return;
            }
            Classificacao.LinhaGuilda a = todas[indice];
            string texto = $"<b>{Esc(a.Guilda)}</b>  <color=#ffa13c>{cat.Formatar(a.Valor)}</color>  <color=#9e9480>· {Membros(a.Membros)}</color>";
            if (indice > 0)
            {
                Classificacao.LinhaGuilda frente = todas.Take(indice).LastOrDefault(g => g.Valor > a.Valor);
                if (frente != null)
                    texto += $"  <color=#9e9480>· faltam</color> {Falta(cat, frente.Valor - a.Valor)} <color=#9e9480>para passar</color> {Esc(frente.Guilda)}";
            }
            else texto += "  <color=#ffd75a>· sua guilda lidera!</color>";
            Rodape("#" + a.Posicao, texto, a.Posicao > 3);
        }

        private void DesenharFeitos(RankingRede.Foto foto)
        {
            _colNome.text = "Chefe";
            _colValor.text = "Primeiro";
            _colDetalhe.text = "Quando";
            PosicionarTabela(false);

            IList<string> chefes = _ctx.OrdemChefes;
            MostrarLinhas(chefes.Count);
            _indiceMinhaLinha = -1;
            long eu = Player.m_localPlayer.GetPlayerID();
            int meus = 0;
            for (int i = 0; i < chefes.Count; i++)
            {
                string chefe = chefes[i];
                RankingFeito f = foto.Feitos.FirstOrDefault(x => x.Chefe == chefe);
                int derrotaram = foto.Jogadores.Count(j => j.Valor(Chave.PrefixoChefe + chefe) > 0d);
                bool meu = f != null && f.Id == eu;
                if (meu) meus++;

                string nomeChefe = Esc(Localizar(chefe)) + "  <size=13><color=#9e9480>" +
                                   (derrotaram == 0 ? "ninguém derrotou ainda" : derrotaram == 1 ? "1 viking derrotou" : derrotaram + " vikings derrotaram") +
                                   "</color></size>";
                bool mundoDerrotou = foto.ChefesDoMundo.Contains(chefe);
                string quem = f != null
                    ? Esc(f.Nome) + (string.IsNullOrEmpty(f.Guilda) ? "" : " <color=#9e9480>[" + Esc(f.Guilda) + "]</color>")
                    : mundoDerrotou ? "<color=#9e9480>derrotado, sem registro</color>" : "<color=#9e9480>seja o primeiro!</color>";
                string quando = f == null ? (mundoDerrotou ? "antes do ranking" : "")
                    : f.Antigo ? "antes do ranking · " + Data(f.Em, false) : Data(f.Em, true);
                PreencherLinha(_linhas[i], i, meu, i + 1, "", Trofeu(chefe), nomeChefe, quem, quando,
                    f != null ? foto.Achar(f.Id) : null);
            }
            _vazio.text = chefes.Count == 0 ? "Nenhum chefe configurado (PvP > BossOrder)." : "";
            _descricaoCabecalho.text = "O primeiro viking a derrotar cada chefe neste mundo." +
                (foto.DiaDoMundo > 0 ? $"  <color=#ffd75a>Dia {foto.DiaDoMundo} · o mundo já derrotou {foto.ChefesDoMundo.Count} de {chefes.Count} chefes.</color>" : "");

            Rodape(meus > 0 ? meus.ToString() : "—",
                meus > 0
                    ? $"<color=#ffd75a>Você tem {meus} {(meus == 1 ? "feito" : "feitos")} do mundo.</color> <color=#9e9480>Ninguém tira: é seu para sempre.</color>"
                    : "<color=#9e9480>Derrote um chefe antes de todo mundo e o seu nome fica aqui para sempre.</color>",
                false);
        }

        // ------------------------------------------------------------------ linhas

        private void MostrarLinhas(int quantas)
        {
            while (_linhas.Count < quantas) _linhas.Add(CriarLinha());
            for (int i = 0; i < _linhas.Count; i++)
            {
                bool ativa = i < quantas;
                if (_linhas[i].Objeto.activeSelf != ativa) _linhas[i].Objeto.SetActive(ativa);
            }
        }

        private LinhaUi CriarLinha()
        {
            var linha = ValheimUi.Row(_tabela, AlturaLinha, 6f);
            var layout = linha.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 10, 0, 0);

            var fundo = linha.gameObject.AddComponent<Image>();
            fundo.color = Color.white;
            var botao = linha.gameObject.AddComponent<Button>();
            botao.targetGraphic = fundo;
            SemNavegacao(botao);

            var ui = new LinhaUi { Objeto = linha.gameObject, Botao = botao };

            ui.Posicao = ValheimUi.CreateLabel(linha, "", 18, ValheimUi.Beige, TextAlignmentOptions.Center, display: true);
            ValheimUi.SetWidth(ui.Posicao.gameObject, 52f);

            ui.Movimento = ValheimUi.CreateLabel(linha, "", 12, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.SetWidth(ui.Movimento.gameObject, 40f);

            ui.Icone = ValheimUi.CreateItemIcon(linha, null, 30f);
            ui.Icone.raycastTarget = false;

            ui.Nome = ValheimUi.CreateLabel(linha, "", 16, ValheimUi.Beige, TextAlignmentOptions.Left);
            ui.Nome.textWrappingMode = TextWrappingModes.NoWrap;
            ui.Nome.overflowMode = TextOverflowModes.Ellipsis;
            ValheimUi.Flexible(ui.Nome.gameObject, 1f);

            ui.Valor = ValheimUi.CreateLabel(linha, "", 17, ValheimUi.Orange, TextAlignmentOptions.Right, display: true);
            ui.Valor.textWrappingMode = TextWrappingModes.NoWrap;
            ui.Valor.overflowMode = TextOverflowModes.Ellipsis;
            ValheimUi.SetWidth(ui.Valor.gameObject, 150f);

            ui.Detalhe = ValheimUi.CreateLabel(linha, "", 13, ValheimUi.Muted, TextAlignmentOptions.Left);
            ui.Detalhe.textWrappingMode = TextWrappingModes.NoWrap;
            ui.Detalhe.overflowMode = TextOverflowModes.Ellipsis;
            ValheimUi.SetWidth(ui.Detalhe.gameObject, 236f);

            botao.onClick.AddListener(() => { if (ui.Jogador != null) AbrirFicha(ui.Jogador); });
            return ui;
        }

        private static void PreencherLinha(LinhaUi ui, int indice, bool minha, int posicao, string movimento, Sprite icone,
            string nome, string valor, string detalhe, RankingJogador jogador)
        {
            ui.Jogador = jogador;
            ui.Botao.interactable = jogador != null;
            Color fundo = minha ? FundoMinha : indice % 2 == 0 ? FundoLinha : FundoLinhaPar;
            ui.Botao.colors = new ColorBlock
            {
                normalColor = fundo,
                highlightedColor = jogador != null ? FundoHover : fundo,
                pressedColor = FundoMinha,
                selectedColor = fundo,
                disabledColor = fundo,
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };

            ui.Posicao.text = posicao.ToString(CultureInfo.InvariantCulture);
            ui.Posicao.color = posicao <= 3 ? CorLugar(posicao) : minha ? ValheimUi.Yellow : ValheimUi.Beige;
            ui.Movimento.text = movimento ?? "";
            ui.Icone.sprite = icone;
            ui.Icone.enabled = icone != null;
            ui.Icone.gameObject.SetActive(icone != null);
            ui.Nome.text = nome ?? "";
            ui.Nome.color = minha ? ValheimUi.Yellow : ValheimUi.Beige;
            ui.Valor.text = valor ?? "";
            ui.Detalhe.text = detalhe ?? "";
        }

        private void Cartao(CartaoPodio c, int lugar, RankingJogador jogador, string nome, string guilda, string valor, bool vazio)
        {
            c.Jogador = jogador;
            // Empate divide o lugar: dois primeiros mostram "1º" os dois.
            c.Medalha.text = lugar + "º";
            c.Medalha.color = CorLugar(lugar);
            c.Nome.text = vazio ? "<color=#9e9480>—</color>" : Esc(nome) + (jogador != null && jogador.Conectado ? " <color=#7fd08a>•</color>" : "");
            c.Guilda.text = vazio ? "<color=#9e9480>lugar livre</color>" : guilda;
            c.Valor.text = valor;
            bool eu = jogador != null && Player.m_localPlayer != null && jogador.Id == Player.m_localPlayer.GetPlayerID();
            c.Nome.color = eu ? ValheimUi.Yellow : ValheimUi.Beige;
        }

        private void Rodape(string posicao, string texto, bool podeIr)
        {
            _rodapePos.text = posicao ?? "";
            _rodapeTexto.text = texto ?? "";
            _irParaMim.gameObject.SetActive(podeIr && _indiceMinhaLinha >= 0);
        }

        private void IrParaMim()
        {
            if (_rolagem == null || _indiceMinhaLinha < 0) return;
            Canvas.ForceUpdateCanvases();
            float alturaConteudo = _tabela.rect.height;
            float alturaVisivel = ((RectTransform)_rolagem.transform).rect.height;
            if (alturaConteudo <= alturaVisivel) return;
            float y = 6f + _indiceMinhaLinha * (AlturaLinha + 2f) - (alturaVisivel - AlturaLinha) / 2f;
            _rolagem.verticalNormalizedPosition = 1f - Mathf.Clamp01(y / (alturaConteudo - alturaVisivel));
        }

        private void PularCategoria(int passo)
        {
            var ids = _cats.Select(c => c.Id).ToList();
            ids.Add(IdFeitos);
            int i = ids.IndexOf(_categoria);
            if (i < 0) i = 0;
            _categoria = ids[(i + passo + ids.Count) % ids.Count];
            if (_rolagem != null) _rolagem.verticalNormalizedPosition = 1f;
            Invalidar();
        }

        private void AtualizarSubtitulo()
        {
            if (_subtitulo == null) return;
            RankingRede.Foto foto = RankingRede.Atual;
            if (foto == null)
            {
                bool demorou = Time.unscaledTime - _abertaEm > 10f;
                _subtitulo.text = demorou ? "O servidor não respondeu." : "Conectando ao servidor...";
                if (demorou && _vazio != null)
                    _vazio.text = "O servidor não mandou o ranking.\nEle precisa estar com a mesma versão do Deadheim que você.";
                return;
            }
            int idade = Mathf.Max(0, Mathf.RoundToInt(Time.unscaledTime - RankingRede.RecebidaEm));
            string quando = idade < 5 ? "atualizado agora" : idade < 60 ? $"atualizado há {idade} s" : $"atualizado há {idade / 60} min";
            string mundo = (string.IsNullOrEmpty(foto.Mundo) ? "" : Esc(foto.Mundo) + "  ·  ")
                           + (foto.DiaDoMundo > 0 ? "dia " + foto.DiaDoMundo + "  ·  " : "");
            _subtitulo.text = $"{mundo}{foto.Jogadores.Count} vikings  ·  <color=#7fd08a>{foto.Online.Count} online</color>  ·  {quando}  ·  <color=#7a7262>setas do teclado trocam a categoria · clique num nome para ver a ficha</color>";
        }

        // ================================================================== ficha

        private void AbrirFicha(RankingJogador j)
        {
            FecharFicha();
            RankingRede.Foto foto = RankingRede.Atual;
            if (foto == null || j == null) return;

            _ficha = ValheimUi.CreateCanvas("Deadheim_RankingFicha", Ordem + 2);
            if (_ficha == null) return;
            try
            {
                MontarFicha(foto, j);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Ranking: ficha: " + ex);
                FecharFicha();
            }
        }

        private void FecharFicha()
        {
            if (_ficha != null) Destroy(_ficha);
            _ficha = null;
        }

        private void MontarFicha(RankingRede.Foto foto, RankingJogador j)
        {
            // Clicar fora fecha.
            var fora = ValheimUi.CreateRect("Fora", _ficha.transform);
            ValheimUi.Stretch(fora, 0f, 0f);
            fora.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            fora.gameObject.AddComponent<Button>().onClick.AddListener(FecharFicha);

            var painel = ValheimUi.CreatePanel(_ficha.transform, 520f, 640f);
            painel.anchorMin = painel.anchorMax = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = Vector2.zero;
            painel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // clique no painel nao fecha

            var nome = ValheimUi.CreateLabel(painel, Esc(j.Nome), 30, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)nome.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(50f, -64f), new Vector2(-50f, -16f));
            nome.textWrappingMode = TextWrappingModes.NoWrap;
            nome.overflowMode = TextOverflowModes.Ellipsis;

            var x = ValheimUi.CreateButton(painel, "X", 34f, 34f, 17);
            ValheimUi.Anchor((RectTransform)x.transform, Vector2.one, Vector2.one, new Vector2(-48f, -48f), new Vector2(-14f, -14f));
            x.onClick.AddListener(FecharFicha);

            var partes = new List<string>();
            if (!string.IsNullOrEmpty(j.Guilda)) partes.Add("<color=#ffa13c>[" + Esc(j.Guilda) + "]</color>");
            if (j.Aesir) partes.Add("<color=#ffd75a>AESIR</color>");
            partes.Add(j.Conectado ? "<color=#7fd08a>• online</color>" : "visto " + Relativo(foto.Agora - j.UltimaVez));
            if (j.PrimeiraVez > 0d) partes.Add("no mundo desde " + Data(j.PrimeiraVez, false));
            var sub = ValheimUi.CreateLabel(painel, string.Join("  ·  ", partes), 14, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)sub.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -92f), new Vector2(-24f, -64f));

            var caixa = ValheimUi.CreateInlay(painel, "Posicoes");
            ValheimUi.Anchor(caixa, Vector2.zero, Vector2.one, new Vector2(20f, 70f), new Vector2(-20f, -100f));
            var area = ValheimUi.CreateRect("Area", caixa);
            ValheimUi.Stretch(area, 4f, 4f);
            RectTransform lista = ValheimUi.CreateScrollList(area, spacing: 2f);

            int i = 0;
            foreach (Categoria c in _cats)
            {
                List<Linha> linhas = Classificacao.Classificar(foto.Jogadores, c, false);
                Linha minha = linhas.FirstOrDefault(l => l.Jogador.Id == j.Id);
                double valor = c.Valor(j, false);

                var linha = ValheimUi.Row(lista, 36f, 8f);
                linha.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 10, 0, 0);
                linha.gameObject.AddComponent<Image>().color = i++ % 2 == 0 ? FundoLinha : FundoLinhaPar;

                Image icone = ValheimUi.CreateItemIcon(linha, c.Icone, 28f);
                icone.raycastTarget = false;
                var titulo = ValheimUi.CreateLabel(linha, c.Titulo, 15, ValheimUi.Beige, TextAlignmentOptions.Left);
                ValheimUi.Flexible(titulo.gameObject, 1f);
                var v = ValheimUi.CreateLabel(linha, valor > 0d ? c.Formatar(valor) : "—", 15, ValheimUi.Orange, TextAlignmentOptions.Right, display: true);
                ValheimUi.SetWidth(v.gameObject, 130f);
                string pos = minha == null ? "<color=#9e9480>—</color>"
                    : $"<color={CorHex(minha.Posicao <= 3 ? CorLugar(minha.Posicao) : ValheimUi.Beige)}>#{minha.Posicao}</color> <size=12><color=#9e9480>de {linhas.Count}</color></size>";
                var p = ValheimUi.CreateLabel(linha, pos, 16, ValheimUi.Beige, TextAlignmentOptions.Right, display: true);
                ValheimUi.SetWidth(p.gameObject, 110f);
            }

            int feitos = foto.Feitos.Count(f => f.Id == j.Id);
            if (feitos > 0)
            {
                var f = ValheimUi.CreateLabel(lista, $"<color=#ffd75a>{feitos} {(feitos == 1 ? "feito" : "feitos")} do mundo:</color> " +
                    string.Join(", ", foto.Feitos.Where(ft => ft.Id == j.Id).Select(ft => Esc(Localizar(ft.Chefe)))), 14, ValheimUi.Beige, TextAlignmentOptions.Left);
                ValheimUi.SetHeight(f.gameObject, 30f);
            }

            double pvpK = j.Valor(Chave.PvpAbates), pvpD = j.Valor(Chave.PvpMortes), mortes = j.Valor(Chave.Mortes);
            var rodape = ValheimUi.CreateLabel(painel,
                $"Mortes: {Numero.Inteiro(mortes)}  ·  PvP: {Numero.Inteiro(pvpK)} abates / {Numero.Inteiro(pvpD)} mortes  ·  maior habilidade {Numero.Inteiro(j.Valor(Chave.HabilidadeMaior))}",
                13, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)rodape.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 34f), new Vector2(-20f, 66f));

            var fechar = ValheimUi.CreateButton(painel, "Fechar", 150f, 32f, 15);
            ValheimUi.Anchor((RectTransform)fechar.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-75f, 8f), new Vector2(75f, 38f));
            fechar.onClick.AddListener(FecharFicha);
        }

        // ================================================================= textos

        private static string Movimento(int? mov, bool semana)
        {
            if (semana) return "";
            if (!mov.HasValue) return "<color=#ffd75a>novo</color>";
            if (mov.Value > 0) return "<color=#7fd08a>+" + mov.Value + "</color>";
            if (mov.Value < 0) return "<color=#e06a5c>" + mov.Value + "</color>";
            return "<color=#5e584c>=</color>";
        }

        private static string NomeTexto(RankingJogador j)
        {
            string texto = Esc(j.Nome);
            if (j.Conectado) texto += " <color=#7fd08a>•</color>";
            if (j.Aesir) texto += " <size=11><color=#ffd75a>AESIR</color></size>";
            if (!string.IsNullOrEmpty(j.Guilda)) texto += "  <size=13><color=#9e9480>[" + Esc(j.Guilda) + "]</color></size>";
            return texto;
        }

        private static string GuildaTexto(RankingJogador j)
        {
            string texto = string.IsNullOrEmpty(j.Guilda) ? "sem guilda" : "[" + Esc(j.Guilda) + "]";
            if (j.Aesir) texto += "  <color=#ffd75a>AESIR</color>";
            return texto;
        }

        private static string Membros(int n) => n == 1 ? "1 membro" : n + " membros";

        private static string Falta(Categoria c, double diferenca)
        {
            // Valor com "." de milhar ou em km/h ja diz a unidade; contador simples ganha a palavra.
            string numero = c.Formatar(Math.Max(1d, Math.Ceiling(diferenca)));
            return "<color=#ffa13c>" + numero + (string.IsNullOrEmpty(c.Unidade) ? "" : " " + c.Unidade) + "</color>";
        }

        private static string DicaEntrar(Categoria c)
        {
            switch (c.Id)
            {
                case "pvp": return "<color=#9e9480>Abata um jogador fora da arena para entrar.</color>";
                case "guerra": return "<color=#9e9480>Entre numa guilda e lute na guerra de castelos.</color>";
                case "chefes": return "<color=#9e9480>Derrote o primeiro chefe para entrar.</color>";
                default: return _semana ? "<color=#9e9480>Jogue nesta semana para aparecer aqui.</color>" : "<color=#9e9480>Seus números chegam em até um minuto.</color>";
            }
        }

        private static Color CorLugar(int lugar) => lugar == 1 ? Ouro : lugar == 2 ? Prata : Bronze;

        private static string CorHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>O texto vem de outros jogadores: mostrado como esta, sem virar tag de rich text.</summary>
        private static string Esc(string texto) =>
            string.IsNullOrEmpty(texto) ? "" : "<noparse>" + texto.Replace("</noparse>", "") + "</noparse>";

        /// <summary>
        /// Sem navegacao do Unity: as setas trocam a categoria (Update), e nao o botao selecionado.
        /// </summary>
        private static void SemNavegacao(Selectable s) => s.navigation = new Navigation { mode = Navigation.Mode.None };

        private static string Localizar(string token) =>
            Localization.instance != null ? Localization.instance.Localize(token) : token;

        private static Sprite Trofeu(string chefe)
        {
            string prefab;
            switch (chefe)
            {
                case "$enemy_eikthyr": prefab = "TrophyEikthyr"; break;
                case "$enemy_gdking": prefab = "TrophyTheElder"; break;
                case "$enemy_bonemass": prefab = "TrophyBonemass"; break;
                case "$enemy_dragon": prefab = "TrophyDragonQueen"; break;
                case "$enemy_goblinking": prefab = "TrophyGoblinKing"; break;
                case "$enemy_seekerqueen": prefab = "TrophySeekerQueen"; break;
                case "$enemy_fader": prefab = "TrophyFader"; break;
                default: prefab = "TrophyEikthyr"; break;
            }
            return ValheimUi.FindItemIcon(prefab);
        }

        private static string Data(double segundosUtc, bool comHora)
        {
            if (segundosUtc <= 0d) return "?";
            DateTime local = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(segundosUtc).ToLocalTime();
            return local.ToString(comHora ? "dd/MM/yyyy HH:mm" : "dd/MM", CultureInfo.InvariantCulture);
        }

        private static string Relativo(double segundos)
        {
            if (segundos < 120d) return "agora há pouco";
            if (segundos < 3600d) return "há " + Mathf.FloorToInt((float)(segundos / 60d)) + " min";
            if (segundos < 86400d) return "há " + Mathf.FloorToInt((float)(segundos / 3600d)) + " h";
            int dias = Mathf.FloorToInt((float)(segundos / 86400d));
            return dias == 1 ? "ontem" : "há " + dias + " dias";
        }

        // ================================================================== tipos

        private sealed class BotaoCategoria
        {
            public string Id;
            public Button Botao;
            public TextMeshProUGUI Titulo, Posicao;
        }

        private sealed class CartaoPodio
        {
            public RectTransform Caixa;
            public Button Botao;
            public TextMeshProUGUI Medalha, Nome, Guilda, Valor;
            public RankingJogador Jogador;
        }

        private sealed class LinhaUi
        {
            public GameObject Objeto;
            public Button Botao;
            public TextMeshProUGUI Posicao, Movimento, Nome, Valor, Detalhe;
            public Image Icone;
            public RankingJogador Jogador;
        }

        // ============================================================ cursor e teclado

        // Com a janela aberta o jogo se comporta como num menu: cursor livre, e teclado e mouse
        // nao chegam ao personagem nem a camera (mesmo esquema da troca e das janelas dos NPCs).

        [HarmonyPatch(typeof(Menu), nameof(Menu.IsVisible))]
        private static class MenuVisivelPatch
        {
            private static void Postfix(ref bool __result) { if (Aberta) __result = true; }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.IsActive))]
        private static class MenuAtivoPatch
        {
            private static void Postfix(ref bool __result) { if (Aberta) __result = true; }
        }

        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static class ControllerInputPatch
        {
            private static void Postfix(ref bool __result) { if (Aberta) __result = false; }
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static class TakeInputPatch
        {
            private static void Postfix(Player __instance, ref bool __result)
            {
                if (Aberta && __instance == Player.m_localPlayer) __result = false;
            }
        }
    }
}
