using BepInEx.Configuration;
using Deadheim.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Menus
{
    /// <summary>
    /// Janela do Aesir, aberta pela Central Deadheim (botao "Seja Aesir" ou uma funcao Aesir travada):
    /// o que o Aesir tem, agrupado, e como apoiar -- preco, instrucoes, PIX e Discord com botao de
    /// copiar, pacotes de Deadcoins. O texto da doacao vem da config sincronizada [Central], que o
    /// admin edita na aba Admin do ESC -> Deadheim sem reiniciar.
    ///
    /// Os beneficios que dependem de uma chave do servidor (AesirConfig, Transmog, Plantas) so
    /// aparecem com a chave ligada: a janela nao promete o que o servidor desligou.
    ///
    /// Vive junto com o inventario: fecha quando ele fecha (MenuHub.Tick).
    /// </summary>
    internal static class AesirJanela
    {
        private static readonly Color Verde = new Color(0.5f, 0.82f, 0.54f, 1f);

        private static GameObject _canvas;
        private static TextMeshProUGUI _copiado;

        public static bool Aberta => _canvas != null;

        /// <param name="destaque">Funcao travada que trouxe o jogador ate aqui (ou null).</param>
        public static void Abrir(string destaque)
        {
            Fechar();
            if (!ValheimUi.EnsureAssets()) return;

            _canvas = ValheimUi.CreateCanvas("Deadheim_Aesir", MenuHub.Ordem + 5);
            if (_canvas == null) return;

            try
            {
                Montar(destaque);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Deadheim] Janela do Aesir: {ex}");
                Fechar();
            }
        }

        public static void Fechar()
        {
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _copiado = null;
        }

        // ------------------------------------------------------------------ montar

        private static void Montar(string destaque)
        {
            bool aesir = VipList.VipListApi.IsLocalPlayerVip();

            var painel = ValheimUi.CreatePanel(_canvas.transform, 860f, 640f);
            painel.anchorMin = painel.anchorMax = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = Vector2.zero;

            var barra = ValheimUi.CreateRect("Titulo", painel);
            ValheimUi.Anchor(barra, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -60f), Vector2.zero);
            barra.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // so para o arrasto
            barra.gameObject.AddComponent<DragWindow>().Target = painel;

            var titulo = ValheimUi.CreateLabel(barra, "AESIR", 36, MenuHub.Dourado, TextAlignmentOptions.Center, display: true);
            ValheimUi.Stretch((RectTransform)titulo.transform, 60f, 8f);

            var x = ValheimUi.CreateButton(painel, "X", 36f, 36f, 18);
            ValheimUi.Anchor((RectTransform)x.transform, Vector2.one, Vector2.one, new Vector2(-52f, -52f), new Vector2(-16f, -16f));
            x.onClick.AddListener(Fechar);

            var lema = ValheimUi.CreateLabel(painel,
                "Conforto e visual. Nada de dano, vida ou velocidade: no combate, o Aesir joga igual a todo mundo.",
                15, ValheimUi.Beige, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)lema.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -88f), new Vector2(-40f, -60f));

            string estado = aesir
                ? "<color=#7fd08a>Você é Aesir. Obrigado por manter o Deadheim vivo!</color>"
                : "Você ainda não é Aesir.";
            if (!aesir && !string.IsNullOrEmpty(destaque))
                estado = $"<color=#ffd75a>{destaque} é exclusivo Aesir.</color>  " + estado;
            var rotuloEstado = ValheimUi.CreateLabel(painel, estado, 16, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)rotuloEstado.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -118f), new Vector2(-40f, -90f));
            rotuloEstado.richText = true;

            // --- beneficios (esquerda) ---
            var caixaBeneficios = ValheimUi.CreateInlay(painel, "Beneficios");
            ValheimUi.Anchor(caixaBeneficios, Vector2.zero, new Vector2(0.58f, 1f), new Vector2(24f, 70f), new Vector2(-6f, -126f));
            Cabecalho(caixaBeneficios, aesir ? "O que você tem" : "O que o Aesir tem");
            var areaBeneficios = ValheimUi.CreateRect("Lista", caixaBeneficios);
            ValheimUi.Anchor(areaBeneficios, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -38f));
            RectTransform listaBeneficios = ValheimUi.CreateScrollList(areaBeneficios, spacing: 3f);

            foreach (IGrouping<string, Beneficio> grupo in Beneficios().GroupBy(b => b.Grupo))
            {
                Linha(listaBeneficios, grupo.Key, 15, ValheimUi.Orange, display: true, altura: 26f);
                foreach (Beneficio b in grupo)
                {
                    string marca = aesir ? "<color=#7fd08a>•</color>" : "<color=#c9a64a>•</color>";
                    Linha(listaBeneficios, $"{marca} {b.Texto}", 14, aesir ? ValheimUi.Beige : ValheimUi.Muted);
                }
            }
            Linha(listaBeneficios, "Os valores podem mudar durante a temporada. Vale o que estiver no jogo.", 12, ValheimUi.Muted);

            // --- como apoiar (direita) ---
            var caixaApoio = ValheimUi.CreateInlay(painel, "Apoiar");
            ValheimUi.Anchor(caixaApoio, new Vector2(0.58f, 0f), Vector2.one, new Vector2(6f, 70f), new Vector2(-24f, -126f));
            Cabecalho(caixaApoio, aesir ? "Renovar ou comprar Deadcoins" : "Como ser Aesir");
            var areaApoio = ValheimUi.CreateRect("Lista", caixaApoio);
            ValheimUi.Anchor(areaApoio, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -38f));
            RectTransform listaApoio = ValheimUi.CreateScrollList(areaApoio, spacing: 6f);

            string preco = Texto(MenuHub.AesirPreco);
            if (preco.Length > 0) Linha(listaApoio, preco, 18, MenuHub.Dourado);
            string como = Texto(MenuHub.DoacaoComo);
            if (como.Length > 0) Linha(listaApoio, como, 14, ValheimUi.Beige);

            string pix = Texto(MenuHub.DoacaoPix);
            if (pix.Length > 0) Copiavel(listaApoio, "PIX", pix);
            string discord = Texto(MenuHub.DoacaoDiscord);
            if (discord.Length > 0) Copiavel(listaApoio, "Discord", discord);
            if (pix.Length == 0 && discord.Length == 0)
                Linha(listaApoio, "Fale com a administração no Discord do servidor.", 14, ValheimUi.Beige);

            string[] pacotes = Texto(MenuHub.DoacaoPacotes).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (pacotes.Length > 0)
            {
                Linha(listaApoio, "Deadcoins", 15, ValheimUi.Orange, display: true, altura: 26f);
                Linha(listaApoio, "O saldo fica no servidor: não cai na morte e não passa para outro jogador.", 12, ValheimUi.Muted);
                foreach (string pacote in pacotes) Linha(listaApoio, "<color=#c9a64a>•</color> " + pacote, 14, ValheimUi.Beige);
            }

            // --- rodape ---
            _copiado = ValheimUi.CreateLabel(painel, "", 14, Verde, TextAlignmentOptions.Left);
            ValheimUi.Anchor((RectTransform)_copiado.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 18f), new Vector2(-220f, 58f));

            var fechar = ValheimUi.CreateButton(painel, "Fechar", 170f, 40f, 16);
            ValheimUi.Anchor((RectTransform)fechar.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-200f, 18f), new Vector2(-30f, 58f));
            fechar.onClick.AddListener(Fechar);
        }

        private static void Cabecalho(RectTransform caixa, string texto)
        {
            var label = ValheimUi.CreateLabel(caixa, texto, 17, ValheimUi.Orange, TextAlignmentOptions.Center, display: true);
            ValheimUi.Anchor((RectTransform)label.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(6f, -36f), new Vector2(-6f, -6f));
        }

        private static TextMeshProUGUI Linha(RectTransform lista, string texto, int tamanho, Color cor, bool display = false, float altura = 0f)
        {
            var label = ValheimUi.CreateLabel(lista, texto, tamanho, cor, TextAlignmentOptions.TopLeft, display);
            label.richText = true;
            if (altura > 0f) ValheimUi.SetHeight(label.gameObject, altura);
            return label;
        }

        /// <summary>"PIX: chave" com um botao que copia a chave para a area de transferencia.</summary>
        private static void Copiavel(RectTransform lista, string rotulo, string valor)
        {
            var linha = ValheimUi.Row(lista, 36f, 8f);
            var texto = ValheimUi.CreateLabel(linha, $"<color=#ffa13c>{rotulo}:</color> {valor}", 14, ValheimUi.Beige, TextAlignmentOptions.Left);
            texto.richText = true;
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            texto.overflowMode = TextOverflowModes.Ellipsis;
            ValheimUi.Flexible(texto.gameObject);

            var copiar = ValheimUi.CreateButton(linha, "Copiar", 84f, 32f, 14);
            ValheimUi.SetWidth(copiar.gameObject, 84f);
            copiar.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = valor;
                if (_copiado != null) _copiado.text = rotulo + " copiado: cole onde precisar (Ctrl+V).";
            });
        }

        private static string Texto(ConfigEntry<string> chave) => (chave?.Value ?? "").Trim();

        // -------------------------------------------------------------- beneficios

        private readonly struct Beneficio
        {
            public readonly string Grupo, Texto;
            public Beneficio(string grupo, string texto) { Grupo = grupo; Texto = texto; }
        }

        private static bool Ligado(ConfigEntry<bool> chave) => chave == null || chave.Value;

        private static IEnumerable<Beneficio> Beneficios()
        {
            const string viagem = "Viagem e acesso";
            const string fazenda = "Coleta e fazenda";
            const string bancada = "Bancada e baús";
            const string conforto = "Conforto";
            const string visual = "Visual";

            float recarga = (Pvp.PvpConfig.Retreat1CooldownSeconds?.Value ?? 1800f) / 60f;
            yield return new Beneficio(viagem, recarga > 0f
                ? $"Retreat: volta ao ponto da sua cama, com recarga de {recarga:0.#} min"
                : "Retreat: volta ao ponto da sua cama");
            if (Ligado(AesirConfig.Aesir18DoisPontos))
                yield return new Beneficio(viagem, "Segundo ponto de retorno (Shift+P na cama)");
            yield return new Beneficio(viagem, "Loja Deadcoins de qualquer lugar do mapa, pela Central");
            yield return new Beneficio(viagem, "Portais VIP");

            yield return new Beneficio(fazenda, "Shift+E colhe tudo igual num raio de 10 m");
            if (Ligado(AesirConfig.Aesir6ReplantarAtivo))
                yield return new Beneficio(fazenda, "A plantação é replantada sozinha ao colher");
            if (Ligado(AesirConfig.Aesir7AbastecerAreaAtivo))
                yield return new Beneficio(fazenda, "Abastecer uma fundição abastece as iguais por perto (Shift)");
            if (Ligado(AesirConfig.Aesir8SemDesgasteAtivo))
                yield return new Beneficio(fazenda, "Foice e enxada não gastam");
            if (Ligado(AesirConfig.Aesir9TempoRestanteAtivo))
                yield return new Beneficio(fazenda, "Ao mirar um recurso colhido, veja quanto falta para voltar");

            yield return new Beneficio(bancada, "Fabricação em lote (até 99) e mais rápida");
            yield return new Beneficio(bancada, "Baú Aesir, de 6×6");
            if (Ligado(AesirConfig.Aesir12RepararAreaAtivo))
                yield return new Beneficio(bancada, "Reparar com o martelo conserta tudo por perto (Shift)");
            if (Ligado(AesirConfig.Aesir14DepositarAtivo))
                yield return new Beneficio(bancada, "Depositar: completa as pilhas dos baús próximos (Alt+G olhando o baú)");
            if (Ligado(AesirConfig.Aesir15OrdenarAtivo))
                yield return new Beneficio(bancada, "Ordenar o baú aberto (Alt+O)");
            if (Ligado(AesirConfig.Aesir16EtiquetaAtivo))
                yield return new Beneficio(bancada, "Dar nome aos baús (/etiqueta)");
            if (Ligado(Plantas.Enabled))
                yield return new Beneficio(bancada, "Plantas de construção salvas");

            if (Ligado(AesirConfig.Aesir17RestedAtivo))
                yield return new Beneficio(conforto, "O efeito Descansado dura mais");
            yield return new Beneficio(conforto, "Mais anúncios no leilão e mais anexos no correio");
            if (Ligado(AesirConfig.Aesir26Ativo))
                yield return new Beneficio(conforto, "Mensagem especial ao entrar no mundo");

            yield return new Beneficio(visual, "Tag [Aesir] no nome e no chat");
            if (Ligado(Transmog.Enabled) && Ligado(Transmog.RequireVip))
                yield return new Beneficio(visual, "Transmog: a aparência que quiser no seu equipamento");
            yield return new Beneficio(visual, "Velas exclusivas, nome e skins na montaria");
            if (Ligado(AesirFogo.Ativo))
                yield return new Beneficio(visual, "Fogo da tocha de outra cor (Alt+F com a tocha na mão)");
            if (Ligado(AesirLapide.Ativo))
                yield return new Beneficio(visual, "Tumba com cruz dourada ao morrer");
        }
    }
}
