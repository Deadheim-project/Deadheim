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
    /// arena, troca -- e as funcoes Aesir: Loja Deadcoins, retreat, depositar, ordenar, plantas.
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

        public static ConfigEntry<bool> Mostrar;
        public static ConfigEntry<Lado> Posicao;

        public static ConfigEntry<string> AesirPreco;
        public static ConfigEntry<string> DoacaoComo;
        public static ConfigEntry<string> DoacaoPix;
        public static ConfigEntry<string> DoacaoDiscord;
        public static ConfigEntry<string> DoacaoPacotes;

        internal const int Ordem = 4950;
        private const float Largura = 360f;
        private const float Altura = 660f;
        private const float AlturaBotao = 48f;
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

        public static void Bind(ConfigFile config)
        {
            const string secao = "Central";
            Mostrar = config.Bind(secao, "MostrarNoInventario", true,
                "Mostra a Central Deadheim (montarias, guilda, loja, retreat, Aesir...) ao lado do inventario (Tab).");
            Posicao = config.Bind(secao, "Lado", Lado.Direita,
                "Lado da tela em que a Central aparece com o inventario aberto.");
            Posicao.SettingChanged += (_, __) => Posicionar();

            AesirPreco = Plugin.Synced(config.Bind(secao, "AesirPreco", "R$ 20 por mês, com 100 Deadcoins de bônus todo mês.",
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
            if (_status != null) _status.text = texto ?? "";
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
                    Titulo = "Guilda", Icone = "ShieldWood", FechaInventario = true,
                    Detalhe = () =>
                    {
                        string guilda = Guilda();
                        return string.IsNullOrEmpty(guilda) ? "Sem guilda" : "Guilda: " + guilda;
                    },
                    Indisponivel = () =>
                    {
                        if (MenuBridge.AbrirGuilda == null) return "O painel da guilda vem do RaidSystem, que não está instalado.";
                        return string.IsNullOrEmpty(Guilda())
                            ? "Você ainda não tem guilda. Crie ou entre numa no NPC Registrador de Guildas."
                            : null;
                    },
                    Acao = () => { MenuBridge.AbrirGuilda?.Invoke(); return null; }
                },
                new Entrada
                {
                    Titulo = "Ranking de Guerra", Icone = "TrophyEikthyr", FechaInventario = true,
                    Detalhe = () => "Melhores jogadores e guildas da temporada",
                    Indisponivel = () => MenuBridge.AbrirRanking == null ? "O Ranking de Guerra vem do RaidSystem, que não está instalado." : null,
                    Acao = () => { MenuBridge.AbrirRanking?.Invoke(); return null; }
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
                new Entrada
                {
                    Titulo = "Depositar nos baús", Icone = "FineWood", Aesir = true,
                    Visivel = () => Ligado(AesirConfig.Aesir14DepositarAtivo),
                    Detalhe = () => $"Completa as pilhas dos baús em até {AesirConfig.Aesir14DepositarRaio?.Value ?? 10f:0} m",
                    Acao = () =>
                    {
                        string resposta = null;
                        AesirBaus.Depositar(texto => resposta = texto);
                        return resposta;
                    }
                },
                new Entrada
                {
                    Titulo = "Ordenar baú aberto", Icone = "Wood", Aesir = true,
                    Visivel = () => Ligado(AesirConfig.Aesir15OrdenarAtivo),
                    Detalhe = () => AesirBaus.BauAberto() ? "Organiza o baú aberto por nome" : "Abra um baú para ordenar",
                    Indisponivel = () => AesirBaus.BauAberto() ? null : "Abra um baú (E) e use o botão com ele aberto.",
                    Acao = () =>
                    {
                        string resposta = null;
                        AesirBaus.Ordenar(texto => resposta = texto);
                        return resposta;
                    }
                },
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
                    double falta = Pvp.PvpModule.RetreatRemaining;
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

        private static void Montar()
        {
            _canvas = ValheimUi.CreateCanvas("Deadheim_Central", Ordem);
            if (_canvas == null)
            {
                if (!_semGuiLogado) Debug.LogError("[Deadheim] Central: GUI do jogo nao encontrada, hub nao montado.");
                _semGuiLogado = true;
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

        private static void Posicionar()
        {
            if (_painel == null) return;
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
                ? "<color=#7fd08a>Também ativo:</color> colheita em área (Shift+E), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir], descanso mais longo e mais."
                : "<color=#ffd75a>Também no Aesir:</color> colheita em área (Shift+E), fabricação em lote, Baú Aesir 6×6, portais VIP, tag [Aesir], descanso mais longo e mais.";

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
