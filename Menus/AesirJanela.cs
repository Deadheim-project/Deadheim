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
                L.T("aesirui.lema"),
                15, ValheimUi.Beige, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)lema.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -88f), new Vector2(-40f, -60f));

            string estado = aesir
                ? L.T("aesirui.e_aesir")
                : L.T("aesirui.nao_aesir");
            if (!aesir && !string.IsNullOrEmpty(destaque))
                estado = L.T("aesirui.destaque", destaque) + estado;
            var rotuloEstado = ValheimUi.CreateLabel(painel, estado, 16, ValheimUi.Muted, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)rotuloEstado.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -118f), new Vector2(-40f, -90f));
            rotuloEstado.richText = true;

            // --- beneficios (esquerda) ---
            var caixaBeneficios = ValheimUi.CreateInlay(painel, "Beneficios");
            ValheimUi.Anchor(caixaBeneficios, Vector2.zero, new Vector2(0.58f, 1f), new Vector2(24f, 70f), new Vector2(-6f, -126f));
            Cabecalho(caixaBeneficios, aesir ? L.T("aesirui.o_que_tem") : L.T("aesirui.o_que_aesir"));
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
            Linha(listaBeneficios, L.T("aesirui.valores"), 12, ValheimUi.Muted);

            // --- como apoiar (direita) ---
            var caixaApoio = ValheimUi.CreateInlay(painel, "Apoiar");
            ValheimUi.Anchor(caixaApoio, new Vector2(0.58f, 0f), Vector2.one, new Vector2(6f, 70f), new Vector2(-24f, -126f));
            Cabecalho(caixaApoio, aesir ? L.T("aesirui.renovar") : L.T("aesirui.como_ser"));
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
                Linha(listaApoio, L.T("aesirui.fale_adm"), 14, ValheimUi.Beige);

            string[] pacotes = Texto(MenuHub.DoacaoPacotes).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            if (pacotes.Length > 0)
            {
                Linha(listaApoio, "Deadcoins", 15, ValheimUi.Orange, display: true, altura: 26f);
                Linha(listaApoio, L.T("aesirui.saldo"), 12, ValheimUi.Muted);
                foreach (string pacote in pacotes) Linha(listaApoio, "<color=#c9a64a>•</color> " + pacote, 14, ValheimUi.Beige);
            }

            // --- rodape ---
            _copiado = ValheimUi.CreateLabel(painel, "", 14, Verde, TextAlignmentOptions.Left);
            ValheimUi.Anchor((RectTransform)_copiado.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 18f), new Vector2(-220f, 58f));

            var fechar = ValheimUi.CreateButton(painel, L.T("aesirui.fechar"), 170f, 40f, 16);
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

            var copiar = ValheimUi.CreateButton(linha, L.T("aesirui.copiar"), 84f, 32f, 14);
            ValheimUi.SetWidth(copiar.gameObject, 84f);
            copiar.onClick.AddListener(() =>
            {
                GUIUtility.systemCopyBuffer = valor;
                if (_copiado != null) _copiado.text = L.T("aesirui.copiado", rotulo);
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
            string viagem = L.T("aesirui.g_viagem");
            string fazenda = L.T("aesirui.g_fazenda");
            string bancada = L.T("aesirui.g_bancada");
            string conforto = L.T("aesirui.g_conforto");
            string visual = L.T("aesirui.g_visual");

            float recarga = (Pvp.PvpConfig.Retreat1CooldownSeconds?.Value ?? 1800f) / 60f;
            yield return new Beneficio(viagem, recarga > 0f
                ? L.T("aesirui.b_retreat_rec", recarga.ToString("0.#"))
                : L.T("aesirui.b_retreat"));
            if (Ligado(AesirConfig.Aesir18DoisPontos))
                yield return new Beneficio(viagem, L.T("aesirui.b_ponto2"));
            yield return new Beneficio(viagem, L.T("aesirui.b_loja"));
            yield return new Beneficio(viagem, L.T("aesirui.b_portais"));

            yield return new Beneficio(fazenda, L.T("aesirui.b_colhe"));
            if (Ligado(AesirConfig.Aesir6ReplantarAtivo))
                yield return new Beneficio(fazenda, L.T("aesirui.b_replanta"));
            if (Ligado(AesirConfig.Aesir7AbastecerAreaAtivo))
                yield return new Beneficio(fazenda, L.T("aesirui.b_fundicao"));
            if (Ligado(AesirConfig.Aesir8SemDesgasteAtivo))
                yield return new Beneficio(fazenda, L.T("aesirui.b_foice"));
            if (Ligado(AesirConfig.Aesir9TempoRestanteAtivo))
                yield return new Beneficio(fazenda, L.T("aesirui.b_respawn"));

            yield return new Beneficio(bancada, L.T("aesirui.b_lote"));
            yield return new Beneficio(bancada, L.T("aesirui.b_bau"));
            if (Ligado(AesirConfig.Aesir12RepararAreaAtivo))
                yield return new Beneficio(bancada, L.T("aesirui.b_reparar"));
            if (Ligado(AesirConfig.Aesir14DepositarAtivo))
                yield return new Beneficio(bancada, L.T("aesirui.b_depositar"));
            if (Ligado(AesirConfig.Aesir15OrdenarAtivo))
                yield return new Beneficio(bancada, L.T("aesirui.b_ordenar"));
            if (Ligado(AesirConfig.Aesir16EtiquetaAtivo))
                yield return new Beneficio(bancada, L.T("aesirui.b_etiqueta"));
            if (Ligado(Plantas.Enabled))
                yield return new Beneficio(bancada, L.T("aesirui.b_plantas"));

            if (Ligado(AesirConfig.Aesir17RestedAtivo))
                yield return new Beneficio(conforto, L.T("aesirui.b_descansado"));
            yield return new Beneficio(conforto, L.T("aesirui.b_leilao"));
            if (Ligado(AesirConfig.Aesir26Ativo))
                yield return new Beneficio(conforto, L.T("aesirui.b_msg"));

            yield return new Beneficio(visual, L.T("aesirui.b_tag"));
            if (Ligado(Transmog.Enabled) && Ligado(Transmog.RequireVip))
                yield return new Beneficio(visual, L.T("aesirui.b_transmog"));
            yield return new Beneficio(visual, L.T("aesirui.b_velas"));
            if (Ligado(AesirFogo.Ativo))
                yield return new Beneficio(visual, L.T("aesirui.b_tocha"));
            if (Ligado(AesirLapide.Ativo))
                yield return new Beneficio(visual, L.T("aesirui.b_tumba"));
        }
    }
}
