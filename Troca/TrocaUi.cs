using Deadheim.Shared;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Troca
{
    /// <summary>
    /// As janelas da troca, na mesma interface dos NPCs (Shared/ValheimUi): a lista de quem esta
    /// perto para pedir a troca pelo nome, o aviso do pedido que chegou e a troca em si -- as suas
    /// moedas com a quantidade a oferecer, a sua oferta, a do outro e os passos
    /// Travar -> Confirmar. Monta os widgets uma vez e so atualiza: refazer tudo a cada frame
    /// tiraria o foco do campo de quantidade.
    /// </summary>
    internal class TrocaUi : MonoBehaviour
    {
        private const float PedidoValeSeg = 35f;
        private const int Ordem = 5010;

        private static TrocaUi _instance;
        private static bool _escolhaPedida;

        private JanelaTroca _troca;
        private JanelaPedido _pedido;
        private JanelaEscolha _escolha;

        /// <summary>Janela que prende o cursor e o personagem: a troca aberta ou a lista de jogadores.</summary>
        internal static bool IsOpen => _instance != null && (_instance._troca != null || _instance._escolha != null);

        internal static void Ensure()
        {
            if (_instance != null) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            var go = new GameObject("DeadheimTrocaUi");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrocaUi>();
        }

        /// <summary>/trocar sem nome: escolher o jogador na lista.</summary>
        internal static void AbrirEscolha()
        {
            Ensure();
            _escolhaPedida = true;
        }

        private void Update()
        {
            if (Player.m_localPlayer == null)
            {
                bool presa = IsOpen;
                FecharTudo();
                if (presa) ValheimUi.ReleaseCursor();
                return;
            }
            if (TrocaRede.PedidoDe != null && Time.time - TrocaRede.PedidoEm > PedidoValeSeg) TrocaRede.PedidoDe = null;
            if (!ValheimUi.EnsureAssets()) return; // a UI do jogo ainda nao carregou: tenta no proximo frame

            bool estavaAberta = IsOpen;
            TrocaRede.Vista vista = TrocaRede.Atual;

            if (vista != null)
            {
                _escolhaPedida = false;
                if (_troca == null) _troca = JanelaTroca.Criar();
            }
            else if (_troca != null) { _troca.Destruir(); _troca = null; }

            if (_escolhaPedida && _escolha == null) _escolha = JanelaEscolha.Criar(() => _escolhaPedida = false);
            else if (!_escolhaPedida && _escolha != null) { _escolha.Destruir(); _escolha = null; }

            string pedido = vista == null ? TrocaRede.PedidoDe : null;
            if (_pedido != null && _pedido.De != pedido) { _pedido.Destruir(); _pedido = null; }
            if (pedido != null && _pedido == null) _pedido = JanelaPedido.Criar(pedido);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_troca != null) TrocaRede.Cancelar();
                else if (_escolha != null) _escolhaPedida = false;
            }

            if (vista != null) _troca?.Atualizar(vista);
            _escolha?.Atualizar();

            if (estavaAberta && !IsOpen) ValheimUi.ReleaseCursor();
        }

        // Depois do jogo, que trava o cursor de novo a cada frame.
        private void LateUpdate()
        {
            if (IsOpen) ValheimUi.HoldCursor();
        }

        private void FecharTudo()
        {
            _troca?.Destruir(); _troca = null;
            _pedido?.Destruir(); _pedido = null;
            _escolha?.Destruir(); _escolha = null;
            _escolhaPedida = false;
        }

        private void OnDestroy() => FecharTudo();

        private static RectTransform Janela(GameObject canvas, float largura, float altura, string titulo,
            System.Action fechar, out TextMeshProUGUI rotulo)
        {
            var panel = ValheimUi.CreatePanel(canvas.transform, largura, altura);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var barra = ValheimUi.CreateRect("Titulo", panel);
            ValheimUi.Anchor(barra, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -58f), Vector2.zero);
            barra.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // so para o arrasto
            barra.gameObject.AddComponent<DragWindow>().Target = panel;

            rotulo = ValheimUi.CreateLabel(barra, titulo, 28, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Stretch((RectTransform)rotulo.transform, 60f, 10f);
            rotulo.richText = false;

            var x = ValheimUi.CreateButton(panel, "X", 36f, 36f, 18);
            ValheimUi.Anchor((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-52f, -52f), new Vector2(-16f, -16f));
            x.onClick.AddListener(() => fechar());
            return panel;
        }

        private static TextMeshProUGUI Cabecalho(RectTransform caixa, string texto)
        {
            var label = ValheimUi.CreateLabel(caixa, texto, 16, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)label.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(6f, -36f), new Vector2(-6f, -6f));
            return label;
        }

        private static void Limpar(List<GameObject> linhas)
        {
            foreach (GameObject l in linhas) if (l != null) Object.Destroy(l);
            linhas.Clear();
        }

        // ======================================================================== troca

        private sealed class JanelaTroca
        {
            private sealed class Grupo
            {
                public string Nome;
                public int Qualidade;
                public int Qtd;
                public string Rotulo;
            }

            private readonly GameObject _canvas;
            private TextMeshProUGUI _titulo, _dica, _cabMinha, _cabDele, _selecionado, _vazioInv;
            private RectTransform _listaInv, _listaMinha, _listaDele;
            private TMP_InputField _qtd;
            private Image _iconeSel;
            private Button _oferecer, _travar, _confirmar;
            private readonly List<GameObject> _linhasInv = new List<GameObject>();
            private readonly List<GameObject> _linhasOferta = new List<GameObject>();
            private string _assinInv, _assinOferta;
            private List<Grupo> _grupos = new List<Grupo>();

            private string _selNome;
            private int _selQualidade;
            private string _aviso;
            private float _avisoAte;

            private JanelaTroca(GameObject canvas) { _canvas = canvas; }

            private static bool _semGuiLogado;

            internal static JanelaTroca Criar()
            {
                GameObject canvas = ValheimUi.CreateCanvas("Deadheim_Troca", Ordem);
                if (canvas == null)
                {
                    if (!_semGuiLogado) Debug.LogError("[Deadheim] Troca: GUI do jogo nao encontrada, janela nao aberta.");
                    _semGuiLogado = true;
                    return null;
                }
                var j = new JanelaTroca(canvas);
                j.Montar();
                return j;
            }

            internal void Destruir()
            {
                if (_canvas != null) Object.Destroy(_canvas);
            }

            private void Montar()
            {
                var panel = Janela(_canvas, 940f, 640f, "Troca", TrocaRede.Cancelar, out _titulo);

                var conteudo = ValheimUi.CreateRect("Conteudo", panel);
                ValheimUi.Anchor(conteudo, Vector2.zero, Vector2.one, new Vector2(24f, 102f), new Vector2(-24f, -66f));

                // --- seu inventario, a quantidade e Oferecer ---
                var inv = ValheimUi.CreateInlay(conteudo, "Inventario");
                ValheimUi.Anchor(inv, new Vector2(0f, 0f), new Vector2(0.36f, 1f), Vector2.zero, new Vector2(-6f, 0f));
                Cabecalho(inv, "Suas moedas");

                var areaInv = ValheimUi.CreateRect("Lista", inv);
                ValheimUi.Anchor(areaInv, Vector2.zero, Vector2.one, new Vector2(4f, 136f), new Vector2(-4f, -38f));
                _listaInv = ValheimUi.CreateScrollList(areaInv, spacing: 3f);
                _vazioInv = ValheimUi.CreateLabel(areaInv, "Você não tem moedas para oferecer.\nA troca é só de moedas.", 14,
                    ValheimUi.Muted, TextAlignmentOptions.Top);
                ValheimUi.Anchor((RectTransform)_vazioInv.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(10f, -60f), new Vector2(-10f, -12f));

                var sel = ValheimUi.Strip(inv, TextAnchor.MiddleLeft);
                ValheimUi.Anchor(sel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 94f), new Vector2(-10f, 130f));
                _iconeSel = ValheimUi.CreateItemIcon(sel, null, 32f);
                _selecionado = ValheimUi.CreateLabel(sel, "Sem moedas para oferecer.", 14, ValheimUi.Muted, TextAlignmentOptions.Left);
                ValheimUi.Flexible(_selecionado.gameObject);

                var qtd = ValheimUi.Strip(inv, TextAnchor.MiddleCenter, 5f);
                ValheimUi.Anchor(qtd, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 52f), new Vector2(-8f, 88f));
                var qtdLabel = ValheimUi.CreateLabel(qtd, "Qtd", 15, ValheimUi.Beige, TextAlignmentOptions.Right);
                ValheimUi.SetWidth(qtdLabel.gameObject, 34f);
                ValheimUi.CreateButton(qtd, "−", 34f, 34f, 18).onClick.AddListener(() => Somar(-1));
                _qtd = ValheimUi.CreateInputField(qtd, "1", 72f, 34f);
                _qtd.contentType = TMP_InputField.ContentType.IntegerNumber;
                ValheimUi.SetWidth(_qtd.gameObject, 72f);
                ValheimUi.CreateButton(qtd, "+", 34f, 34f, 18).onClick.AddListener(() => Somar(1));
                ValheimUi.CreateButton(qtd, "Tudo", 62f, 34f, 14).onClick.AddListener(() =>
                {
                    if (_selNome != null) _qtd.text = TrocaRede.Disponivel(_selNome, _selQualidade).ToString();
                });

                _oferecer = ValheimUi.CreateButton(inv, "Oferecer", 0f, 38f, 16);
                ValheimUi.Anchor((RectTransform)_oferecer.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(8f, 8f), new Vector2(-8f, 46f));
                _oferecer.onClick.AddListener(Oferecer);

                // --- as duas ofertas ---
                var minha = ValheimUi.CreateInlay(conteudo, "Minha");
                ValheimUi.Anchor(minha, new Vector2(0.36f, 0f), new Vector2(0.68f, 1f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
                _cabMinha = Cabecalho(minha, "Sua oferta");
                var areaMinha = ValheimUi.CreateRect("Lista", minha);
                ValheimUi.Anchor(areaMinha, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -38f));
                _listaMinha = ValheimUi.CreateScrollList(areaMinha, spacing: 3f);

                var dele = ValheimUi.CreateInlay(conteudo, "Dele");
                ValheimUi.Anchor(dele, new Vector2(0.68f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), Vector2.zero);
                _cabDele = Cabecalho(dele, "Oferta dele");
                var areaDele = ValheimUi.CreateRect("Lista", dele);
                ValheimUi.Anchor(areaDele, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -38f));
                _listaDele = ValheimUi.CreateScrollList(areaDele, spacing: 3f);

                // --- passos e dica ---
                var passos = ValheimUi.Strip(panel, TextAnchor.MiddleCenter, 16f);
                ValheimUi.Anchor(passos, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 52f), new Vector2(-24f, 96f));
                _travar = ValheimUi.CreateButton(passos, "1. Travar oferta", 230f, 42f, 16);
                _travar.onClick.AddListener(TrocaRede.Travar);
                _confirmar = ValheimUi.CreateButton(passos, "2. Confirmar troca", 230f, 42f, 16);
                _confirmar.onClick.AddListener(TrocaRede.Confirmar);
                ValheimUi.CreateButton(passos, "Cancelar troca", 190f, 42f, 16).onClick.AddListener(TrocaRede.Cancelar);

                _dica = ValheimUi.CreateLabel(panel, "", 15, ValheimUi.Yellow, TextAlignmentOptions.Left);
                ValheimUi.Anchor((RectTransform)_dica.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(26f, 14f), new Vector2(-26f, 46f));
            }

            private void Avisar(string texto)
            {
                _aviso = texto;
                _avisoAte = Time.time + 5f;
            }

            private void Somar(int passo)
            {
                if (_selNome == null) return;
                int tem = TrocaRede.Disponivel(_selNome, _selQualidade);
                int n = int.TryParse(_qtd.text, out int atual) ? atual : 0;
                _qtd.text = Mathf.Clamp(n + passo, 1, Mathf.Max(1, tem)).ToString();
            }

            private void Oferecer()
            {
                if (_selNome == null) { Avisar("Você não tem moedas para oferecer."); return; }
                if (!int.TryParse(_qtd.text, out int n) || n <= 0) { Avisar("Quantidade inválida."); return; }
                string erro = TrocaRede.Oferecer(_selNome, _selQualidade, n);
                if (erro != null) { Avisar(erro); return; }
                Avisar($"Oferecendo {n}x {TrocaRede.NomeVisivel(_selNome)}...");
                int sobra = TrocaRede.Disponivel(_selNome, _selQualidade);
                if (sobra > 0) _qtd.text = sobra.ToString();
                else _selNome = null;
                _assinInv = null;
            }

            internal void Atualizar(TrocaRede.Vista v)
            {
                _titulo.text = "Troca com " + v.Outro;
                AtualizarInventario();
                AtualizarSelecao();
                AtualizarOfertas(v);

                bool algo = v.Minha.Count + v.Dele.Count > 0;
                bool guardando = v.Minha.Concat(v.Dele).Any(i => !i.Seguro);
                _oferecer.interactable = _selNome != null && v.Minha.Count < TrocaMotor.MaxItens;
                _travar.interactable = algo && !guardando && !v.MinhaTrava;
                _confirmar.interactable = v.MinhaTrava && v.OutroTrava && !v.MinhaConf && !guardando;

                _dica.text = _aviso != null && Time.time < _avisoAte ? _aviso
                    : guardando ? "Guardando os itens no servidor..."
                    : Dica(v);
            }

            private void AtualizarInventario()
            {
                Inventory inv = Player.m_localPlayer?.GetInventory();
                if (inv == null) return;
                // So as moedas: e o que o servidor aceita na troca.
                _grupos = inv.GetAllItems()
                    .Where(i => i?.m_shared != null && i.m_dropPrefab != null && !i.m_equipped && i.m_dropPrefab.name == Moedas.Prefab)
                    .GroupBy(i => i.m_dropPrefab.name + "|" + i.m_quality)
                    .Select(g =>
                    {
                        ItemDrop.ItemData f = g.First();
                        string nome = f.m_dropPrefab.name;
                        return new Grupo
                        {
                            Nome = nome,
                            Qualidade = f.m_quality,
                            Qtd = g.Sum(i => i.m_stack) - TrocaRede.Reservado(nome, f.m_quality),
                            Rotulo = Localization.instance != null ? Localization.instance.Localize(f.m_shared.m_name) : f.m_shared.m_name,
                        };
                    })
                    .Where(g => g.Qtd > 0)
                    .OrderBy(g => g.Rotulo).ThenBy(g => g.Qualidade)
                    .ToList();
                // Nao ha outra coisa para escolher: as moedas ja vem selecionadas.
                if (_selNome == null && _grupos.Count > 0)
                {
                    _selNome = _grupos[0].Nome;
                    _selQualidade = _grupos[0].Qualidade;
                }

                string assin = _selNome + ":" + _selQualidade + "/" + string.Join("|", _grupos.Select(g => g.Nome + ":" + g.Qualidade + ":" + g.Qtd));
                if (assin == _assinInv) return;
                _assinInv = assin;

                Limpar(_linhasInv);
                foreach (Grupo g in _grupos)
                {
                    bool escolhido = g.Nome == _selNome && g.Qualidade == _selQualidade;
                    var botao = ValheimUi.CreateButton(_listaInv, g.Rotulo + " x" + g.Qtd + (g.Qualidade > 1 ? "  (nv " + g.Qualidade + ")" : ""), 0f, 42f, 14);
                    ValheimUi.Iconify(botao, g.Nome, 34f);
                    var texto = botao.GetComponentInChildren<TextMeshProUGUI>();
                    if (texto != null) { texto.richText = false; texto.color = escolhido ? ValheimUi.Yellow : ValheimUi.Orange; }
                    botao.image.color = escolhido ? Color.white : new Color(0.78f, 0.78f, 0.78f, 1f);
                    _linhasInv.Add(botao.gameObject);

                    string nome = g.Nome;
                    int q = g.Qualidade, total = g.Qtd;
                    botao.onClick.AddListener(() =>
                    {
                        _selNome = nome;
                        _selQualidade = q;
                        _qtd.text = total.ToString();
                    });
                }
                _vazioInv.gameObject.SetActive(_grupos.Count == 0);
            }

            private void AtualizarSelecao()
            {
                Grupo g = _selNome == null ? null : _grupos.FirstOrDefault(x => x.Nome == _selNome && x.Qualidade == _selQualidade);
                if (g == null && _selNome != null) _selNome = null; // acabou (ofereceu tudo, largou, equipou)
                Sprite icone = g != null ? ValheimUi.FindItemIcon(g.Nome) : null;
                if (_iconeSel.sprite != icone) { _iconeSel.sprite = icone; _iconeSel.enabled = icone != null; }
                _selecionado.text = g == null ? "Sem moedas para oferecer."
                    : $"{g.Rotulo}{(g.Qualidade > 1 ? " (nv " + g.Qualidade + ")" : "")}\n<size=12><color=#9a9188>{g.Qtd} para oferecer</color></size>";
                _selecionado.color = g == null ? ValheimUi.Muted : ValheimUi.Beige;
            }

            private void AtualizarOfertas(TrocaRede.Vista v)
            {
                _cabMinha.text = $"Sua oferta ({v.Minha.Count}/{TrocaMotor.MaxItens}) {Estado(v.MinhaTrava, v.MinhaConf)}";
                _cabDele.text = $"Oferta de {v.Outro} {Estado(v.OutroTrava, v.OutroConf)}";

                string assin = v.Versao + "/" + string.Join("|", v.Minha.Select(i => i.Id + (i.Seguro ? "s" : "")))
                               + "/" + string.Join("|", v.Dele.Select(i => i.Id + (i.Seguro ? "s" : "")));
                if (assin == _assinOferta) return;
                _assinOferta = assin;

                Limpar(_linhasOferta);
                foreach (TrocaItem item in v.Minha) LinhaOferta(_listaMinha, item, true);
                foreach (TrocaItem item in v.Dele) LinhaOferta(_listaDele, item, false);
            }

            private void LinhaOferta(RectTransform lista, TrocaItem item, bool minha)
            {
                var linha = ValheimUi.Row(lista, 46f);
                _linhasOferta.Add(linha.gameObject);
                ValheimUi.CreateItemIcon(linha, item.Nome, 36f);
                string extra = item.Seguro ? "" : "\n<size=12><color=#9a9188>guardando...</color></size>";
                var label = ValheimUi.CreateLabel(linha, TrocaRede.Rotulo(item) + extra, 14, ValheimUi.Beige, TextAlignmentOptions.Left);
                ValheimUi.Flexible(label.gameObject);
                if (!minha) return;
                // Mexer na oferta com as travas ligadas zera tudo: de proposito, e o servidor avisa.
                string id = item.Id;
                ValheimUi.CreateButton(linha, "Retirar", 84f, 34f, 14).onClick.AddListener(() => TrocaRede.Retirar(id));
            }

            private static string Estado(bool trava, bool conf)
                => conf ? "<color=#7CFC00>[confirmou]</color>" : trava ? "<color=#ffd700>[travou]</color>" : "";

            private static string Dica(TrocaRede.Vista v)
            {
                if (v.MinhaConf) return "Você confirmou. Aguardando " + v.Outro + "...";
                if (v.MinhaTrava && !v.OutroTrava) return "Aguardando " + v.Outro + " travar a oferta...";
                if (v.MinhaTrava && v.OutroTrava) return "Confira a oferta dele e confirme. Qualquer mudança zera tudo.";
                return "Escolha quantas moedas e Oferecer. Depois trave; se a oferta mudar, as travas voltam a zero.";
            }
        }

        // ======================================================================== pedido

        /// <summary>
        /// O aviso do pedido que chegou. Nao prende o cursor nem o personagem: um pedido nao pode
        /// travar quem esta lutando. Para clicar, abre-se o inventario (o cursor fica livre).
        /// </summary>
        private sealed class JanelaPedido
        {
            internal readonly string De;
            private readonly GameObject _canvas;

            private JanelaPedido(string de, GameObject canvas) { De = de; _canvas = canvas; }

            internal static JanelaPedido Criar(string de)
            {
                GameObject canvas = ValheimUi.CreateCanvas("Deadheim_TrocaPedido", Ordem + 1);
                if (canvas == null) return null;

                var panel = ValheimUi.CreatePanel(canvas.transform, 460f, 170f);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f);
                panel.pivot = new Vector2(0.5f, 1f);
                panel.anchoredPosition = new Vector2(0f, -120f);

                var titulo = ValheimUi.CreateLabel(panel, "Pedido de troca", 22, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
                ValheimUi.Anchor((RectTransform)titulo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -48f), new Vector2(-20f, -12f));

                var texto = ValheimUi.CreateLabel(panel, de + " quer trocar moedas com você.", 17, ValheimUi.Beige, TextAlignmentOptions.Center);
                texto.richText = false;
                ValheimUi.Anchor((RectTransform)texto.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -78f), new Vector2(-20f, -50f));

                var dica = ValheimUi.CreateLabel(panel, "Abra o inventário para clicar.", 13, ValheimUi.Muted, TextAlignmentOptions.Center);
                ValheimUi.Anchor((RectTransform)dica.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -100f), new Vector2(-20f, -80f));

                var botoes = ValheimUi.Strip(panel, TextAnchor.MiddleCenter, 20f);
                ValheimUi.Anchor(botoes, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 16f), new Vector2(-20f, 56f));
                ValheimUi.CreateButton(botoes, "Aceitar", 150f, 40f, 16).onClick.AddListener(TrocaRede.Aceitar);
                ValheimUi.CreateButton(botoes, "Recusar", 150f, 40f, 16).onClick.AddListener(TrocaRede.Recusar);
                return new JanelaPedido(de, canvas);
            }

            internal void Destruir()
            {
                if (_canvas != null) Object.Destroy(_canvas);
            }
        }

        // ======================================================================== escolha

        /// <summary>Quem esta perto, pelo nome: clicar no nome manda o pedido a ele.</summary>
        private sealed class JanelaEscolha
        {
            private readonly GameObject _canvas;
            private readonly System.Action _fechar;
            private TMP_InputField _filtro;
            private TextMeshProUGUI _vazio;
            private RectTransform _lista;
            private readonly List<GameObject> _linhas = new List<GameObject>();
            private string _assin;
            private float _proxima;

            private JanelaEscolha(GameObject canvas, System.Action fechar) { _canvas = canvas; _fechar = fechar; }

            internal static JanelaEscolha Criar(System.Action fechar)
            {
                GameObject canvas = ValheimUi.CreateCanvas("Deadheim_TrocaEscolha", Ordem);
                if (canvas == null) { fechar(); return null; }
                var j = new JanelaEscolha(canvas, fechar);
                j.Montar();
                return j;
            }

            internal void Destruir()
            {
                if (_canvas != null) Object.Destroy(_canvas);
            }

            private void Montar()
            {
                var panel = Janela(_canvas, 480f, 560f, "Trocar com quem?", _fechar, out _);

                var rotulo = ValheimUi.CreateLabel(panel, "Nome do jogador (digite para filtrar)", 13, ValheimUi.Muted, TextAlignmentOptions.Left);
                ValheimUi.Anchor((RectTransform)rotulo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -86f), new Vector2(-24f, -66f));
                _filtro = ValheimUi.CreateInputField(panel, "", 0f, 36f);
                ValheimUi.Anchor((RectTransform)_filtro.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -124f), new Vector2(-24f, -88f));

                var caixa = ValheimUi.CreateInlay(panel, "Jogadores");
                ValheimUi.Anchor(caixa, Vector2.zero, Vector2.one, new Vector2(24f, 56f), new Vector2(-24f, -134f));
                _lista = ValheimUi.CreateScrollList(caixa, spacing: 3f);
                _vazio = ValheimUi.CreateLabel(caixa, "", 14, ValheimUi.Muted, TextAlignmentOptions.Top);
                ValheimUi.Anchor((RectTransform)_vazio.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -70f), new Vector2(-10f, -14f));

                var dica = ValheimUi.CreateLabel(panel, $"Só aparece quem está a até {TrocaRede.Distancia.Value:0} m. Clique no nome para pedir a troca.",
                    13, ValheimUi.Muted, TextAlignmentOptions.Left);
                ValheimUi.Anchor((RectTransform)dica.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 14f), new Vector2(-24f, 50f));
            }

            internal void Atualizar()
            {
                if (Time.time < _proxima) return;
                _proxima = Time.time + 0.5f;

                Player eu = Player.m_localPlayer;
                if (eu == null) return;
                string filtro = (_filtro.text ?? "").Trim();
                List<string> nomes = Player.GetAllPlayers()
                    .Where(p => p != null && p != eu && Vector3.Distance(p.transform.position, eu.transform.position) <= TrocaRede.Distancia.Value)
                    .OrderBy(p => Vector3.Distance(p.transform.position, eu.transform.position))
                    .Select(p => p.GetPlayerName())
                    .Where(n => !string.IsNullOrEmpty(n) && (filtro.Length == 0 || n.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();

                string assin = filtro + "/" + string.Join("|", nomes);
                if (assin == _assin) return;
                _assin = assin;

                Limpar(_linhas);
                foreach (string nome in nomes)
                {
                    var botao = ValheimUi.CreateButton(_lista, nome, 0f, 38f, 16);
                    var texto = botao.GetComponentInChildren<TextMeshProUGUI>();
                    if (texto != null) texto.richText = false;
                    _linhas.Add(botao.gameObject);
                    string alvo = nome;
                    botao.onClick.AddListener(() =>
                    {
                        TrocaRede.Pedir(alvo);
                        Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Pedido de troca enviado a " + alvo + ".");
                        _fechar();
                    });
                }
                _vazio.text = nomes.Count > 0 ? "" : filtro.Length > 0 ? $"Ninguém perto com \"{filtro}\" no nome." : "Ninguém por perto.";
                _vazio.richText = false;
            }
        }

        // ================================================================ cursor e teclado

        // Com a janela aberta o jogo se comporta como num menu vanilla: cursor livre, e teclado e
        // mouse nao chegam ao personagem nem a camera (mesmo esquema das janelas dos NPCs).
        // Menu.IsActive decide a trava do cursor; Menu.IsVisible, se ele aparece.

        [HarmonyPatch(typeof(Menu), nameof(Menu.IsVisible))]
        private static class MenuVisivelPatch
        {
            private static void Postfix(ref bool __result) { if (IsOpen) __result = true; }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.IsActive))]
        private static class MenuAtivoPatch
        {
            private static void Postfix(ref bool __result) { if (IsOpen) __result = true; }
        }

        // TakeInput e privado no jogo original: pelo nome em texto (o Harmony aceita privado).
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static class ControllerInputPatch
        {
            private static void Postfix(ref bool __result) { if (IsOpen) __result = false; }
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static class TakeInputPatch
        {
            private static void Postfix(Player __instance, ref bool __result)
            {
                if (IsOpen && __instance == Player.m_localPlayer) __result = false;
            }
        }
    }
}
