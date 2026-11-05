using BepInEx.Configuration;
using Deadheim.Shared;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Menus
{
    /// <summary>
    /// Central Deadheim (7.9.0): com o inventario aberto (Tab, ou ao abrir um bau), um painel ao
    /// lado mostra os menus que o jogador pode usar agora -- montarias, guilda, ranking, missoes,
    /// arena, troca -- e as funcoes Aesir: Loja Deadcoins, retreat, plantas. Depositar, ordenar e
    /// etiqueta nao: sao do bau, com tecla (AesirTeclas).
    /// Quem nao e Aesir ve as funcoes Aesir travadas; clicar abre a janela do Aesir (AesirJanela),
    /// com todos os beneficios e como doar.
    ///
    /// Substitui as teclas que cada mod tinha (U, J, F7, PageUp, PageDown), que agora vem vazias na
    /// config de cada mod (ver Menus/README.md).
    /// O que e de admin fica na aba Admin do ESC -> Deadheim (FerramentasAdmin).
    ///
    /// A janela e montada uma vez e so atualizada (a cada meio segundo e a cada clique). Fica num
    /// canvas proprio por cima do inventario; o cursor ja esta livre por causa do inventario.
    /// </summary>
    internal static class MenuHub
    {
        public enum Lado
        {
            Direita,
            Esquerda
        }

        /// <summary>Icones = barra centralizada embaixo do personagem (02/11/2026, padrao); Lista = o
        /// painel ao lado do inventario, com uma linha de estado por botao.</summary>
        public enum Formato
        {
            Icones,
            Lista
        }

        public static ConfigEntry<bool> Mostrar;
        public static ConfigEntry<Lado> Posicao;
        public static ConfigEntry<Formato> Forma;

        public static ConfigEntry<string> AesirPreco;
        public static ConfigEntry<string> DoacaoComo;
        public static ConfigEntry<string> DoacaoPix;
        public static ConfigEntry<string> DoacaoDiscord;
        public static ConfigEntry<string> DoacaoPacotes;

        internal const int Ordem = 4950;
        private const float Largura = 360f;
        private const float Altura = 660f;
        private const float AlturaBotao = 48f;
        private const float LadoIcone = 76f;
        private const float AvisoSeg = 6f;

        internal static readonly Color Dourado = new Color(1f, 0.84f, 0.35f, 1f);
        private static readonly Color Travado = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color Apagado = new Color(1f, 1f, 1f, 0.6f);

        private static GameObject _canvas;
        private static RectTransform _painel;
        private static TextMeshProUGUI _nome, _selo, _cabAesir, _extras, _status, _rotuloAesir;
        private static readonly List<Entrada> _entradas = new List<Entrada>();
        private static float _proximaAtualizacao;
        private static string _aviso;
        private static float _avisoAte;
        private static bool _semGuiLogado;
        private static bool _icones;
        private static Entrada _sobre;
        /// <summary>O menu do botao Aesir na barra de icones: as funcoes Aesir e o convite.</summary>
        private static RectTransform _menuAesir;
        private static Entrada _botaoAesir;
        /// <summary>O menu do botao Admin na barra de icones: as ferramentas de admin (FerramentasAdmin).</summary>
        private static RectTransform _menuAdmin;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Central";
            Mostrar = config.Bind(secao, "MostrarNoInventario", true,
                "Mostra a Central Deadheim (montarias, guilda, loja, retreat, Aesir...) ao lado do inventario (Tab).");
            Posicao = config.Bind(secao, "Lado", Lado.Direita,
                "Lado da tela em que a Central aparece com o inventario aberto.");
            Posicao.SettingChanged += (_, __) => Posicionar();
            Forma = config.Bind(secao, "Formato", Formato.Icones,
                "Icones = barra de icones centralizada embaixo do personagem; Lista = painel ao lado do inventario (usa o Lado).");
            Forma.SettingChanged += (_, __) => Desmontar();

            AesirPreco = Plugin.Synced(config.Bind(secao, "AesirPreco", "R$ 50 por mês, com 500 Deadcoins de bônus de uma vez só.",
                "Preco do Aesir, mostrado na janela do Aesir (Central Deadheim)."));
            DoacaoComo = Plugin.Synced(config.Bind(secao, "DoacaoComo",
                "Faça a doação e mande o comprovante no Discord com o nome do seu personagem. A administração ativa o seu Aesir e credita as Deadcoins.",
                "Como doar, mostrado na janela do Aesir."));
            DoacaoPix = Plugin.Synced(config.Bind(secao, "DoacaoPix", "",
                "Chave PIX para doacao (vazio = a linha nao aparece). O jogador copia com um clique."));
            DoacaoDiscord = Plugin.Synced(config.Bind(secao, "DoacaoDiscord", "",
                "Convite do Discord para doar e falar com a administracao (vazio = a linha nao aparece)."));
            DoacaoPacotes = Plugin.Synced(config.Bind(secao, "DoacaoPacotes",
                "R$ 10 = 200 DC|R$ 25 = 525 DC (+5%)|R$ 50 = 1.100 DC (+10%)|R$ 100 = 2.300 DC (+15%)|R$ 200 = 4.800 DC (+20%)",
                "Pacotes de Deadcoins, separados por |. Vazio = a lista nao aparece."));
        }

        // ------------------------------------------------------------------ ciclo

        /// <summary>Todo frame, pelo Plugin.Update.</summary>
        public static void Tick()
        {
            if (Application.isBatchMode) return;

            bool mostrar = Mostrar != null && Mostrar.Value
                           && Player.m_localPlayer != null
                           && InventoryGui.instance != null && InventoryGui.IsVisible();
            if (!mostrar)
            {
                if (_canvas != null && _canvas.activeSelf) _canvas.SetActive(false);
                AesirJanela.Fechar();
                return;
            }

            if (!ValheimUi.EnsureAssets()) return; // a UI do jogo ainda nao carregou: tenta no proximo frame
            if (_canvas == null)
            {
                // O canvas fica debaixo da GUI do jogo e some no logout: monta de novo.
                Montar();
                if (_canvas == null) return;
                Atualizar();
            }

            if (!_canvas.activeSelf)
            {
                _canvas.SetActive(true);
                // O inventario abre sempre com o menu Aesir fechado.
                if (_menuAesir != null) _menuAesir.gameObject.SetActive(false);
                if (_menuAdmin != null) _menuAdmin.gameObject.SetActive(false);
                if (_status != null) _status.gameObject.SetActive(true);
                Atualizar();
            }
            else if (Time.unscaledTime >= _proximaAtualizacao)
            {
                Atualizar();
            }
        }

        internal static void Avisar(string texto)
        {
            _aviso = texto;
            _avisoAte = Time.unscaledTime + AvisoSeg;
            if (_status != null && (!_icones || _sobre == null)) _status.text = texto ?? "";
        }

        // ------------------------------------------------------------ o que mostra

        private static List<Entrada> MontarEntradas()
        {
            return new List<Entrada>
            {
                // ---- menus de todos
                new Entrada
                {
                    Titulo = "Ranking", Icone = "TrophyDragonQueen", FechaInventario = true,
                    Visivel = () => Ranking.RankingRede.Ligado,
                    Detalhe = Ranking.RankingJanela.ResumoCentral,
                    Acao = () => { Ranking.RankingJanela.Abrir(Ranking.RankingJanela.Origem.Central); return null; }
                },
                new Entrada
                {
                    Titulo = "Montarias", Icone = "SaddleLox", FechaInventario = true,
                    Visivel = () => Integracoes.TemMontarias,
                    Detalhe = () => Vazio(Integracoes.ResumoMontarias(), "Suas montarias e a habilidade de montar"),
                    Acao = Integracoes.AbrirMontarias
                },
                new Entrada
                {
                    Titulo = "Pets", Icone = "Pet_galinha", FechaInventario = true,
                    Visivel = () => Integracoes.TemMontarias,
                    Detalhe = () => "Seus pets, bônus e invocar/guardar",
                    Acao = Integracoes.AbrirPets
                },
                new Entrada
                {
                    Titulo = "Guilda", Icone = "ShieldWood", FechaInventario = true,
                    Detalhe = () =>
                    {
                        string guilda = Guilda();
                        return string.IsNullOrEmpty(guilda) ? "Sem guilda" : "Guilda: " + guilda;
                    },
                    Indisponivel = () => Integracoes.TemGuilds ? null : "O mod Guilds não está instalado.",
                    Acao = Integracoes.AbrirGuilds
                },
                new Entrada
                {
                    Titulo = "Diário de missões", Icone = "Feathers", FechaInventario = true,
                    Visivel = () => Integracoes.TemNpcValheim,
                    Detalhe = () => "Suas missões em andamento",
                    Acao = Integracoes.AbrirDiario
                },
                new Entrada
                {
                    Titulo = "Arena", Icone = "SwordIron", FechaInventario = true,
                    Visivel = () => Integracoes.TemNpcValheim,
                    Detalhe = () => "Status, times e ranking da arena",
                    Acao = Integracoes.AbrirArena
                },
                new Entrada
                {
                    Titulo = "Troca de moedas", Icone = "Coins", FechaInventario = true,
                    Visivel = () => Troca.TrocaRede.Ativo == null || Troca.TrocaRede.Ativo.Value,
                    Detalhe = () => "Troque moedas com quem está perto",
                    Acao = () => { Troca.TrocaUi.AbrirEscolha(); return null; }
                },

                // ---- Aesir
                new Entrada
                {
                    Titulo = "Loja Deadcoins", Icone = "Ruby", Aesir = true, FechaInventario = true,
                    Visivel = () => Integracoes.TemNpcValheim,
                    Detalhe = () => "Compre de qualquer lugar do mapa",
                    Acao = Integracoes.AbrirLojaDeadcoins
                },
                RetreatEntrada(1),
                RetreatEntrada(2),
                // Depositar, ordenar e etiqueta sao do bau: tecla olhando para ele (AesirTeclas), nao aqui.
                new Entrada
                {
                    Titulo = "Transmog", Icone = "ArmorLeatherChest", Aesir = true, FechaInventario = true,
                    // Com RequireNpc o transmog e feito no Alfaiate: nada a abrir daqui.
                    Visivel = () => Ligado(Transmog.Enabled) && !Ligado(Transmog.RequireNpc),
                    ExigeAesir = () => Ligado(Transmog.RequireVip),
                    Detalhe = () => "Mude a aparência do equipamento",
                    Acao = () => { Transmog.HandleCommand(new[] { "/transmog" }); return null; }
                },
                new Entrada
                {
                    Titulo = "Plantas salvas", Icone = "Hammer", Aesir = true, FechaInventario = true,
                    Visivel = () => Ligado(Plantas.Enabled),
                    Detalhe = () => "Salve e recoloque suas construções",
                    Acao = () => { PlantasUi.Open(); return null; }
                },

                // ---- admin (02/11/2026): na barra de icones o modo icones nao mostra mais a dica
                // "ESC > Deadheim > Admin"; o botao so aparece para quem esta na adminlist.
                new Entrada
                {
                    Titulo = "Admin", Icone = "SledgeIron",
                    Visivel = () => Vanilla.Admin.LocalPlayerIsAdmin(),
                    Detalhe = () => "Painel de Admin (bestiário, arenas), territórios e as outras ferramentas",
                    Acao = () =>
                    {
                        if (_menuAdmin != null) { AlternarMenuAdmin(); return null; }
                        // Na lista: direto o Painel de Admin; o resto continua no ESC > Deadheim > Admin.
                        InventoryGui.instance?.Hide();
                        string resposta = Integracoes.AbrirPainelAdmin();
                        if (resposta != null) Player.m_localPlayer?.Message(MessageHud.MessageType.Center, resposta);
                        return null;
                    }
                },
            };
        }

        private static Entrada RetreatEntrada(int ponto)
        {
            return new Entrada
            {
                Titulo = ponto == 2 ? "Retreat (2º ponto)" : "Retreat",
                Icone = "Wishbone",
                Aesir = true,
                FechaInventario = true,
                Visivel = ponto == 2 ? (Func<bool>)(() => Ligado(AesirConfig.Aesir18DoisPontos)) : null,
                Detalhe = () =>
                {
                    if (Retreat.GetHearthStonePosition(ponto) == Vector3.zero)
                        return ponto == 2 ? "Marque olhando a cama (Shift+P)" : "Marque o ponto olhando a sua cama (P)";
                    double falta = Pvp.PvpModule.Remaining(ponto == 2 ? Pvp.PvpModule.Cooldown.Retreat2 : Pvp.PvpModule.Cooldown.Retreat1);
                    return falta > 0d ? "Recarga: " + Pvp.PvpClient.FormatDuration(falta) : "Volta ao ponto da sua cama";
                },
                Indisponivel = () =>
                {
                    if (Retreat.GetHearthStonePosition(ponto) == Vector3.zero)
                        return ponto == 2
                            ? "Marque o segundo ponto olhando a sua cama com Shift+P."
                            : "Marque o ponto de retreat olhando a sua cama e apertando P.";
                    return Pvp.PvpModule.RetreatRefusal(Player.m_localPlayer);
                },
                Acao = () => Retreat.TentarRetreat(ponto)
            };
        }

        private static string Guilda()
        {
            try { return Pvp.PvpGuilds.GuildOf(Player.m_localPlayer); }
            catch (Exception) { return null; }
        }

        private static bool Ligado(ConfigEntry<bool> chave) => chave != null && chave.Value;

        private static string Vazio(string valor, string padrao) => string.IsNullOrEmpty(valor) ? padrao : valor;

        // ------------------------------------------------------------------ montar

        /// <summary>Some com o que esta montado; o proximo Tick monta no formato da config.</summary>
        private static void Desmontar()
        {
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _painel = null;
            _nome = _selo = _cabAesir = _extras = _status = _rotuloAesir = null;
            _sobre = null;
            _menuAesir = null;
            _botaoAesir = null;
            _menuAdmin = null;
            _entradas.Clear();
        }

        private static void Montar()
        {
            _canvas = ValheimUi.CreateCanvas("Deadheim_Central", Ordem);
            if (_canvas == null)
            {
                if (!_semGuiLogado) Debug.LogError("[Deadheim] Central: GUI do jogo nao encontrada, hub nao montado.");
                _semGuiLogado = true;
                return;
            }

            _icones = Forma == null || Forma.Value == Formato.Icones;
            if (_icones)
            {
                MontarIcones();
                return;
            }

            _painel = ValheimUi.CreatePanel(_canvas.transform, Largura, Altura);
            Posicionar();

            var titulo = ValheimUi.CreateLabel(_painel, "Central Deadheim", 26, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)titulo.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -54f), new Vector2(-16f, -14f));

            _nome = ValheimUi.CreateLabel(_painel, "", 16, ValheimUi.Beige, TextAlignmentOptions.Left);
            ValheimUi.Anchor((RectTransform)_nome.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(26f, -82f), new Vector2(-120f, -56f));
            _nome.textWrappingMode = TextWrappingModes.NoWrap;
            _nome.overflowMode = TextOverflowModes.Ellipsis;

            _selo = ValheimUi.CreateLabel(_painel, "", 14, Dourado, TextAlignmentOptions.Right, display: true);
            ValheimUi.Anchor((RectTransform)_selo.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(26f, -82f), new Vector2(-26f, -56f));

            var filete = ValheimUi.CreateRect("Filete", _painel);
            ValheimUi.Anchor(filete, new Vector2(0f, 1f), Vector2.one, new Vector2(22f, -90f), new Vector2(-22f, -88f));
            var imagemFilete = filete.gameObject.AddComponent<Image>();
            imagemFilete.color = new Color(ValheimUi.Orange.r, ValheimUi.Orange.g, ValheimUi.Orange.b, 0.4f);
            imagemFilete.raycastTarget = false;

            var caixa = ValheimUi.CreateInlay(_painel, "Menus");
            ValheimUi.Anchor(caixa, Vector2.zero, Vector2.one, new Vector2(16f, 134f), new Vector2(-16f, -98f));
            var area = ValheimUi.CreateRect("Area", caixa);
            ValheimUi.Stretch(area, 4f, 4f);
            RectTransform lista = ValheimUi.CreateScrollList(area, spacing: 4f);

            _entradas.Clear();
            _entradas.AddRange(MontarEntradas());

            Cabecalho(lista, "MENUS", ValheimUi.Orange);
            foreach (Entrada e in _entradas)
                if (!e.Aesir) CriarBotao(lista, e);

            _cabAesir = Cabecalho(lista, "AESIR", Dourado);
            foreach (Entrada e in _entradas)
                if (e.Aesir) CriarBotao(lista, e);

            _extras = ValheimUi.CreateLabel(lista, "", 12, ValheimUi.Muted, TextAlignmentOptions.TopLeft);

            Button aesir = ValheimUi.CreateButton(_painel, "Seja Aesir", 0f, 46f, 18);
            ValheimUi.Anchor((RectTransform)aesir.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 78f), new Vector2(-20f, 124f));
            _rotuloAesir = aesir.GetComponentInChildren<TextMeshProUGUI>();
            _rotuloAesir.color = Dourado;
            _rotuloAesir.richText = true;
            aesir.onClick.AddListener(() => AesirJanela.Abrir(null));

            _status = ValheimUi.CreateLabel(_painel, "", 13, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)_status.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 18f), new Vector2(-24f, 74f));
            _status.richText = true;
        }

        /// <summary>
        /// A barra de icones: centralizada embaixo do personagem, acima das dicas de tecla do
        /// inventario. Um icone por entrada (o titulo embaixo), os Aesir depois de um filete
        /// dourado, e por ultimo o do Aesir (beneficios / seja Aesir). Passar o mouse mostra o
        /// titulo e a linha de estado acima da barra; os avisos aparecem no mesmo lugar.
        /// </summary>
        private static void MontarIcones()
        {
            _painel = ValheimUi.CreatePanel(_canvas.transform, 0f, LadoIcone + 24f);
            _painel.anchorMin = _painel.anchorMax = new Vector2(0.5f, 0f);
            _painel.pivot = new Vector2(0.5f, 0f);
            _painel.anchoredPosition = new Vector2(0f, 64f);
            var layout = _painel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var ajuste = _painel.gameObject.AddComponent<ContentSizeFitter>();
            ajuste.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _entradas.Clear();
            _entradas.AddRange(MontarEntradas());
            foreach (Entrada e in _entradas)
                if (!e.Aesir) CriarIcone(e);

            var filete = ValheimUi.CreateRect("Filete", _painel);
            var imagem = filete.gameObject.AddComponent<Image>();
            imagem.color = new Color(Dourado.r, Dourado.g, Dourado.b, 0.55f);
            imagem.raycastTarget = false;
            ValheimUi.SetWidth(filete.gameObject, 2f);
            ValheimUi.SetHeight(filete.gameObject, LadoIcone - 10f);

            // Um botao so de Aesir (02/11/2026, pedido do usuario): as funcoes Aesir e o convite
            // ficam no menu dele, que abre acima da barra.
            _botaoAesir = new Entrada
            {
                Titulo = "Aesir", Icone = "Thunderstone", Destaque = true,
                Detalhe = () => VipList.VipListApi.IsLocalPlayerVip()
                    ? "Suas funções Aesir e os benefícios"
                    : "O que o Aesir dá e como apoiar",
                Acao = () => { AlternarMenuAesir(); return null; }
            };
            _entradas.Add(_botaoAesir);
            CriarIcone(_botaoAesir);
            MontarMenuAesir();
            MontarMenuAdmin();

            // A linha de cima: o que o mouse aponta, ou o aviso / a dica de sempre.
            _status = ValheimUi.CreateLabel(_canvas.transform, "", 16, ValheimUi.Beige, TextAlignmentOptions.Bottom);
            var rtStatus = (RectTransform)_status.transform;
            rtStatus.anchorMin = rtStatus.anchorMax = new Vector2(0.5f, 0f);
            rtStatus.pivot = new Vector2(0.5f, 0f);
            rtStatus.sizeDelta = new Vector2(900f, 48f);
            rtStatus.anchoredPosition = new Vector2(0f, 64f + LadoIcone + 30f);
            _status.richText = true;
        }

        /// <summary>
        /// O menu do Aesir: titulo e situacao (ativo / exclusivo de quem apoia), uma linha por funcao
        /// Aesir (icone, nome e estado, como na lista; travada para quem nao e Aesir e, ao clicar,
        /// abre a janela do Aesir naquela funcao), o que mais o Aesir da e o botao dos beneficios.
        /// </summary>
        private static void MontarMenuAesir()
        {
            _menuAesir = ValheimUi.CreatePanel(_canvas.transform, 400f, 0f);
            _menuAesir.anchorMin = _menuAesir.anchorMax = new Vector2(0.5f, 0f);
            _menuAesir.pivot = new Vector2(1f, 0f);
            var layout = _menuAesir.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var ajuste = _menuAesir.gameObject.AddComponent<ContentSizeFitter>();
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titulo = ValheimUi.CreateLabel(_menuAesir, "Aesir", 24, Dourado, TextAlignmentOptions.Center, display: true);
            ValheimUi.SetHeight(titulo.gameObject, 30f);
            _cabAesir = ValheimUi.CreateLabel(_menuAesir, "", 13, ValheimUi.Muted, TextAlignmentOptions.Center);
            _cabAesir.richText = true;
            ValheimUi.SetHeight(_cabAesir.gameObject, 20f);

            foreach (Entrada e in _entradas)
                if (e.Aesir) CriarBotao(_menuAesir, e);

            _extras = ValheimUi.CreateLabel(_menuAesir, "", 12, ValheimUi.Muted, TextAlignmentOptions.TopLeft);
            _extras.richText = true;
            ValheimUi.SetHeight(_extras.gameObject, 52f);

            Button beneficios = ValheimUi.CreateButton(_menuAesir, "", 0f, 44f, 17);
            _rotuloAesir = beneficios.GetComponentInChildren<TextMeshProUGUI>();
            _rotuloAesir.color = Dourado;
            _rotuloAesir.richText = true;
            beneficios.onClick.AddListener(() => AesirJanela.Abrir(null));

            _menuAesir.gameObject.SetActive(false);
        }

        private static void AlternarMenuAesir()
        {
            if (_menuAesir == null) return;
            bool abrir = !_menuAesir.gameObject.activeSelf;
            if (abrir)
            {
                // Acima da barra, alinhado a direita dela (onde fica o botao Aesir).
                LayoutRebuilder.ForceRebuildLayoutImmediate(_painel);
                _menuAesir.anchoredPosition = new Vector2(_painel.rect.width / 2f, _painel.anchoredPosition.y + _painel.rect.height + 6f);
                Atualizar();
            }
            if (abrir && _menuAdmin != null) _menuAdmin.gameObject.SetActive(false);
            _menuAesir.gameObject.SetActive(abrir);
            if (_status != null) _status.gameObject.SetActive(!abrir);
        }

        /// <summary>
        /// O menu de admin: um botao por ferramenta de FerramentasAdmin (as mesmas da aba Admin do
        /// ESC > Deadheim), com a descricao embaixo. A config do servidor e os comandos continuam
        /// so no ESC.
        /// </summary>
        private static void MontarMenuAdmin()
        {
            _menuAdmin = ValheimUi.CreatePanel(_canvas.transform, 440f, 0f);
            _menuAdmin.anchorMin = _menuAdmin.anchorMax = new Vector2(0.5f, 0f);
            _menuAdmin.pivot = new Vector2(0.5f, 0f);
            var layout = _menuAdmin.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var ajuste = _menuAdmin.gameObject.AddComponent<ContentSizeFitter>();
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titulo = ValheimUi.CreateLabel(_menuAdmin, "Admin", 24, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.SetHeight(titulo.gameObject, 30f);

            foreach (FerramentasAdmin.Ferramenta ferramenta in FerramentasAdmin.Todas())
            {
                bool disponivel = ferramenta.Disponivel == null || ferramenta.Disponivel();
                Button botao = ValheimUi.CreateButton(_menuAdmin, ferramenta.Titulo, 0f, 52f, 16);
                var rotulo = botao.GetComponentInChildren<TextMeshProUGUI>();
                rotulo.alignment = TextAlignmentOptions.BottomLeft;
                ValheimUi.Anchor((RectTransform)rotulo.transform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, -4f));
                if (!disponivel) rotulo.color = ValheimUi.Muted;

                var sub = ValheimUi.CreateLabel(botao.transform, disponivel ? ferramenta.Descricao : ferramenta.Ausente ?? "Indisponível",
                    12, ValheimUi.Muted, TextAlignmentOptions.TopLeft);
                ValheimUi.Anchor((RectTransform)sub.transform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(14f, 3f), new Vector2(-14f, 0f));
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                sub.overflowMode = TextOverflowModes.Ellipsis;

                botao.interactable = disponivel;
                botao.onClick.AddListener(() => UsarFerramenta(ferramenta));
            }

            var nota = ValheimUi.CreateLabel(_menuAdmin, "A config do servidor e os comandos ficam no ESC > Deadheim > Admin.",
                12, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.SetHeight(nota.gameObject, 20f);

            _menuAdmin.gameObject.SetActive(false);
        }

        private static void AlternarMenuAdmin()
        {
            if (_menuAdmin == null) return;
            bool abrir = !_menuAdmin.gameObject.activeSelf;
            if (abrir)
            {
                // Acima da barra, centrado no botao Admin.
                LayoutRebuilder.ForceRebuildLayoutImmediate(_painel);
                Entrada admin = _entradas.Find(e => e.Titulo == "Admin" && !e.Aesir);
                float x = admin?.Objeto != null ? admin.Objeto.transform.localPosition.x : 0f;
                _menuAdmin.anchoredPosition = new Vector2(_painel.anchoredPosition.x + x, _painel.anchoredPosition.y + _painel.rect.height + 6f);
                if (_menuAesir != null) _menuAesir.gameObject.SetActive(false);
            }
            _menuAdmin.gameObject.SetActive(abrir);
            if (_status != null) _status.gameObject.SetActive(!abrir);
        }

        private static void UsarFerramenta(FerramentasAdmin.Ferramenta ferramenta)
        {
            string resposta;
            try
            {
                // As janelas abrem por cima do jogo: fecha o inventario antes, como na aba Admin.
                if (ferramenta.FechaMenu && InventoryGui.instance != null) InventoryGui.instance.Hide();
                else AlternarMenuAdmin();
                resposta = ferramenta.Acao?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Central: ferramenta '{ferramenta.Titulo}' falhou: {ex}");
                resposta = "Não consegui abrir " + ferramenta.Titulo + ".";
            }

            if (string.IsNullOrEmpty(resposta)) return;
            if (ferramenta.FechaMenu) Player.m_localPlayer?.Message(MessageHud.MessageType.Center, resposta);
            else Avisar(resposta);
        }

        private static void CriarIcone(Entrada e)
        {
            Button botao = ValheimUi.CreateButton(_painel, "", LadoIcone, LadoIcone, 12);
            e.Objeto = botao.gameObject;

            e.IconeImg = ValheimUi.CreateItemIcon(botao.transform, e.Icone, 40f);
            ValheimUi.Anchor((RectTransform)e.IconeImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-20f, -48f), new Vector2(20f, -8f));
            e.IconeImg.raycastTarget = false;

            e.Rotulo = botao.GetComponentInChildren<TextMeshProUGUI>();
            e.Rotulo.text = e.Titulo;
            e.Rotulo.fontSize = 12;
            e.Rotulo.alignment = TextAlignmentOptions.Bottom;
            ValheimUi.Anchor((RectTransform)e.Rotulo.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(3f, 4f), new Vector2(-3f, 22f));

            if (e.Destaque)
            {
                // O convite do Aesir: moldura dourada, como o botao "Seja Aesir" da lista.
                var moldura = botao.gameObject.AddComponent<Outline>();
                moldura.effectColor = Dourado;
                moldura.effectDistance = new Vector2(2f, -2f);
            }
            if (e.Aesir)
            {
                e.Marca = ValheimUi.CreateLabel(botao.transform, "A", 12, Dourado, TextAlignmentOptions.TopRight, display: true);
                ValheimUi.Anchor((RectTransform)e.Marca.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(-5f, -3f));
            }

            var gatilho = botao.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entrar = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entrar.callback.AddListener(_ => { _sobre = e; MostrarSobre(); });
            var sair = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            sair.callback.AddListener(_ => { if (_sobre == e) _sobre = null; MostrarSobre(); });
            gatilho.triggers.Add(entrar);
            gatilho.triggers.Add(sair);

            botao.onClick.AddListener(() => Clicar(e));
        }

        /// <summary>A linha acima da barra: o icone sob o mouse, senao o aviso, senao a dica.</summary>
        private static void MostrarSobre()
        {
            if (_status == null || !_icones) return;
            if (_sobre != null)
            {
                bool aesir = VipList.VipListApi.IsLocalPlayerVip();
                string detalhe;
                try
                {
                    detalhe = _sobre.Travado(aesir) ? "Exclusivo Aesir: clique para conhecer"
                        : _sobre.Indisponivel?.Invoke() ?? _sobre.Detalhe?.Invoke() ?? "";
                }
                catch (Exception) { detalhe = ""; }
                _status.text = "<color=#ffa13c>" + _sobre.Titulo + "</color>" + (detalhe.Length > 0 ? "  ·  " + detalhe : "");
                return;
            }
            if (_aviso != null && Time.unscaledTime < _avisoAte) { _status.text = _aviso; return; }
            _aviso = null;
            // Sem nada sob o mouse: a propaganda do Aesir, a mesma linha que a lista mostra.
            _status.text = Propaganda(VipList.VipListApi.IsLocalPlayerVip());
        }

        /// <summary>O que o Aesir da, para quem nao e ("Tambem no Aesir") e para quem e ("Tambem ativo").</summary>
        private static string Propaganda(bool aesir) => aesir
            ? "<color=#7fd08a>Aesir ativo:</color> colheita em área (Shift+E), plantio em área (Shift ao plantar), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir], descanso mais longo e mais."
            : "<color=#ffd75a>Seja Aesir:</color> colheita em área (Shift+E), plantio em área (Shift ao plantar), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir] e mais. <color=#ffd75a>Clique em Seja Aesir.</color>";

        private static void Posicionar()
        {
            if (_painel == null || _icones) return;
            bool esquerda = Posicao != null && Posicao.Value == Lado.Esquerda;
            Vector2 ancora = new Vector2(esquerda ? 0f : 1f, 0.5f);
            _painel.anchorMin = ancora;
            _painel.anchorMax = ancora;
            _painel.pivot = ancora;
            _painel.anchoredPosition = new Vector2(esquerda ? 24f : -24f, 0f);
        }

        private static TextMeshProUGUI Cabecalho(RectTransform lista, string texto, Color cor)
        {
            var label = ValheimUi.CreateLabel(lista, texto, 15, cor, TextAlignmentOptions.BottomLeft, display: true);
            label.richText = true;
            ValheimUi.SetHeight(label.gameObject, 26f);
            return label;
        }

        private static void CriarBotao(RectTransform lista, Entrada e)
        {
            Button botao = ValheimUi.CreateButton(lista, e.Titulo, 0f, AlturaBotao, 16);
            e.Objeto = botao.gameObject;

            e.Rotulo = botao.GetComponentInChildren<TextMeshProUGUI>();
            e.Rotulo.alignment = TextAlignmentOptions.BottomLeft;
            ValheimUi.Anchor((RectTransform)e.Rotulo.transform, new Vector2(0f, 0.45f), Vector2.one, new Vector2(52f, 0f), new Vector2(-62f, -4f));

            e.IconeImg = ValheimUi.CreateItemIcon(botao.transform, e.Icone, 30f);
            ValheimUi.Anchor((RectTransform)e.IconeImg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, -15f), new Vector2(42f, 15f));
            e.IconeImg.raycastTarget = false;

            e.Sub = ValheimUi.CreateLabel(botao.transform, "", 12, ValheimUi.Muted, TextAlignmentOptions.TopLeft);
            ValheimUi.Anchor((RectTransform)e.Sub.transform, Vector2.zero, new Vector2(1f, 0.45f), new Vector2(52f, 3f), new Vector2(-62f, 0f));
            e.Sub.textWrappingMode = TextWrappingModes.NoWrap;
            e.Sub.overflowMode = TextOverflowModes.Ellipsis;

            e.Marca = ValheimUi.CreateLabel(botao.transform, "", 12, Dourado, TextAlignmentOptions.Right, display: true);
            ValheimUi.Anchor((RectTransform)e.Marca.transform, new Vector2(1f, 0f), Vector2.one, new Vector2(-62f, 0f), new Vector2(-10f, 0f));

            botao.onClick.AddListener(() => Clicar(e));
        }

        // --------------------------------------------------------------- atualizar

        private static void Atualizar()
        {
            _proximaAtualizacao = Time.unscaledTime + 0.5f;
            Player player = Player.m_localPlayer;
            if (_canvas == null || player == null) return;

            bool aesir = VipList.VipListApi.IsLocalPlayerVip();
            if (_icones)
            {
                if (_cabAesir != null)
                    _cabAesir.text = aesir ? "<color=#7fd08a>ativo</color>" : "exclusivo de quem apoia o servidor";
                if (_extras != null) _extras.text = Propaganda(aesir);
                if (_rotuloAesir != null) _rotuloAesir.text = aesir ? "Ver todos os benefícios" : "Seja Aesir: como apoiar";
                foreach (Entrada e in _entradas)
                {
                    try
                    {
                        bool visivel = e.Visivel == null || e.Visivel();
                        if (e.Objeto.activeSelf != visivel) e.Objeto.SetActive(visivel);
                        if (!visivel) continue;
                        bool travado = e.Travado(aesir);
                        string motivo = travado ? null : e.Indisponivel?.Invoke();
                        e.Rotulo.color = e.Destaque ? Dourado : travado || motivo != null ? ValheimUi.Muted : ValheimUi.Orange;
                        e.IconeImg.color = travado ? Travado : motivo != null ? Apagado : Color.white;
                        if (e.Destaque) e.Rotulo.text = aesir ? "Aesir" : "Seja Aesir";
                        // As linhas do menu Aesir (montadas como na lista).
                        if (e.Sub != null) e.Sub.text = travado ? "Exclusivo Aesir: clique para conhecer" : motivo ?? e.Detalhe?.Invoke() ?? "";
                        if (e.Marca != null && !e.Destaque && e.Sub != null) e.Marca.text = travado ? "AESIR" : "";
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Deadheim] Central: '" + e.Titulo + "' falhou ao atualizar: " + ex.Message);
                    }
                }
                MostrarSobre();
                return;
            }
            _nome.text = player.GetPlayerName();
            _selo.text = aesir ? "AESIR" : "<color=#9e9480>VIAJANTE</color>";
            _cabAesir.text = aesir
                ? "AESIR  <size=12><color=#7fd08a>ativo</color></size>"
                : "AESIR  <size=12><color=#9e9480>exclusivo de quem apoia</color></size>";

            foreach (Entrada e in _entradas)
            {
                try
                {
                    bool visivel = e.Visivel == null || e.Visivel();
                    if (e.Objeto.activeSelf != visivel) e.Objeto.SetActive(visivel);
                    if (!visivel) continue;

                    bool travado = e.Travado(aesir);
                    string motivo = travado ? null : e.Indisponivel?.Invoke();

                    e.Sub.text = travado ? "Exclusivo Aesir: clique para conhecer" : e.Detalhe?.Invoke() ?? "";
                    e.Rotulo.color = travado || motivo != null ? ValheimUi.Muted : ValheimUi.Orange;
                    e.Marca.text = travado ? "AESIR" : "";
                    e.IconeImg.color = travado ? Travado : motivo != null ? Apagado : Color.white;
                }
                catch (Exception ex)
                {
                    // Uma entrada quebrada (config de outro mod sumiu) nao derruba o hub inteiro.
                    e.Sub.text = "";
                    Debug.LogWarning($"[Deadheim] Central: '{e.Titulo}' falhou ao atualizar: {ex.Message}");
                }
            }

            _extras.text = aesir
                ? "<color=#7fd08a>Também ativo:</color> colheita em área (Shift+E), plantio em área (Shift ao plantar), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir], descanso mais longo e mais."
                : "<color=#ffd75a>Também no Aesir:</color> colheita em área (Shift+E), plantio em área (Shift ao plantar), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir], descanso mais longo e mais.";

            _rotuloAesir.text = aesir ? "Você é Aesir · benefícios" : "Seja Aesir";

            if (_aviso != null && Time.unscaledTime < _avisoAte) _status.text = _aviso;
            else
            {
                _aviso = null;
                _status.text = Vanilla.Admin.LocalPlayerIsAdmin()
                    ? "Admin: as ferramentas ficam no ESC > Deadheim > Admin."
                    : "Seus ajustes ficam no ESC > Deadheim.";
            }
        }

        private static void Clicar(Entrada e)
        {
            try
            {
                if (e.Travado(VipList.VipListApi.IsLocalPlayerVip()))
                {
                    AesirJanela.Abrir(e.Titulo);
                    return;
                }

                string motivo = e.Indisponivel?.Invoke();
                if (motivo != null)
                {
                    Avisar(motivo);
                    return;
                }

                if (e.FechaInventario && InventoryGui.instance != null) InventoryGui.instance.Hide();
                string resposta = e.Acao?.Invoke();
                if (string.IsNullOrEmpty(resposta)) return;

                // Com o inventario fechado a Central some: a resposta vai para o meio da tela.
                if (e.FechaInventario) Player.m_localPlayer?.Message(MessageHud.MessageType.Center, resposta);
                else Avisar(resposta);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Central: '{e.Titulo}' falhou: {ex}");
                Avisar("Não consegui abrir " + e.Titulo + ".");
            }
            finally
            {
                if (_canvas != null && _canvas.activeSelf) Atualizar();
            }
        }

        private sealed class Entrada
        {
            public string Titulo;
            public string Icone;
            public bool Aesir;
            public bool FechaInventario;
            /// <summary>O convite do Aesir na barra de icones (moldura e titulo dourados).</summary>
            public bool Destaque;

            /// <summary>null = sempre. Falso esconde a linha (mod ausente, funcao desligada no servidor).</summary>
            public Func<bool> Visivel;
            /// <summary>null = basta ser Aesir. Senao, decide se a funcao exige Aesir agora (config do servidor).</summary>
            public Func<bool> ExigeAesir;
            /// <summary>Linha de baixo do botao.</summary>
            public Func<string> Detalhe;
            /// <summary>Motivo de nao poder usar agora, ou null.</summary>
            public Func<string> Indisponivel;
            /// <summary>Faz a acao. Devolve uma mensagem para o jogador, ou null.</summary>
            public Func<string> Acao;

            public GameObject Objeto;
            public Image IconeImg;
            public TextMeshProUGUI Rotulo, Sub, Marca;

            public bool Travado(bool aesir) => Aesir && !aesir && (ExigeAesir == null || ExigeAesir());
        }
    }
}
