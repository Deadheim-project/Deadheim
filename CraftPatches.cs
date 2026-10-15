using HarmonyLib;
using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Deadheim.Vanilla;

namespace Deadheim.Craft
{
    /// <summary>
    /// Bancadas: fabricacao rapida, campo de busca na lista de receitas e quantidade com - e +.
    ///
    /// A quantidade usa a fabricacao em lote que o Valheim 1.0 ja tem (Shift = x5): o jogo
    /// multiplica requisitos, custo, espaco no inventario, bonus e skill por m_multiCraftAmount.
    /// Aqui esse numero vira o escolhido no painel, e o lote e ligado pelo m_touchMultiCrafting
    /// (o mesmo que o toque longo do jogo usa), sem precisar segurar Shift.
    ///
    /// Os controles sao clonados do proprio jogo: o campo de busca do menu de construcao e o
    /// botao Fabricar, para ficarem com a cara do Valheim.
    /// </summary>
    public class CraftPatches
    {
        private const float TempoDeFabricacao = 0.25f;
        internal const int QuantidadeMaxima = 99;
        private const float AlturaDaBusca = 32f;
        private const float EspacoDaBusca = 4f;
        // [-] [numero] [+] na frente do Fabricar, na altura dele.
        private const float LarguraDoBotao = 40f;
        private const float LarguraDoNumero = 54f;
        private const float EspacoEntre = 4f;
        private const float LarguraDaQuantidade = LarguraDoBotao * 2 + LarguraDoNumero + EspacoEntre * 2;
        private const float EspacoDaQuantidade = 6f;

        internal static int Quantidade = 1;
        internal static string Busca = "";

        internal static TMP_InputField Campo;
        internal static GameObject Controles;
        internal static Button Menos;
        internal static Button Mais;
        private static TMP_Text _numero;
        private static RectTransform _botaoFabricar;
        private static float _botaoFabricarEsquerda;
        private static bool _botaoEncolhido;
        private static Recipe _receita;
        private static ItemDrop.ItemData _receitaItem;

        /// <summary>O jogador esta digitando na busca da bancada.</summary>
        internal static bool Digitando => Campo && Campo.isFocused;

        // ------------------------------------------------------------------ montagem

        private static void Montar(InventoryGui gui)
        {
            if (!Campo) MontarBusca(gui);
            if (!Controles) MontarQuantidade(gui);
        }

        private static void MontarBusca(InventoryGui gui)
        {
            // O campo de busca do menu de construcao (BuildUI.m_searchField, um GuiInputField).
            Component construcao = Hud.instance ? Hud.instance.m_buildUi : null;
            FieldInfo busca = construcao ? AccessTools.Field(construcao.GetType(), "m_searchField") : null;
            TMP_InputField origem = busca?.GetValue(construcao) is Component c ? c.GetComponent<TMP_InputField>() : null;
            if (!origem) return; // o Hud ainda nao subiu: tenta de novo na proxima abertura

            ScrollRect scroll = gui.m_recipeListRoot.GetComponentInParent<ScrollRect>();
            RectTransform lista = scroll ? (RectTransform)scroll.transform : (RectTransform)gui.m_recipeListRoot.parent;

            GameObject go = UnityEngine.Object.Instantiate(origem.gameObject, lista.parent, false);
            go.name = "DeadheimBuscaBancada";
            go.SetActive(true);
            LimparClone(go);

            // No topo da lista de receitas, e a lista desce para dar lugar.
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(lista.anchorMin.x, lista.anchorMax.y);
            r.anchorMax = lista.anchorMax;
            r.offsetMin = new Vector2(lista.offsetMin.x, lista.offsetMax.y - AlturaDaBusca);
            r.offsetMax = lista.offsetMax;
            r.localScale = Vector3.one;
            Descer(lista, AlturaDaBusca + EspacoDaBusca);
            if (gui.m_recipeListScroll && !gui.m_recipeListScroll.transform.IsChildOf(lista))
                Descer((RectTransform)gui.m_recipeListScroll.transform, AlturaDaBusca + EspacoDaBusca);

            TMP_InputField campo = go.GetComponent<TMP_InputField>();
            campo.onValueChanged = new TMP_InputField.OnChangeEvent();
            campo.onEndEdit = new TMP_InputField.SubmitEvent();
            campo.onSubmit = new TMP_InputField.SubmitEvent();
            campo.onSelect = new TMP_InputField.SelectionEvent();
            campo.onDeselect = new TMP_InputField.SelectionEvent();
            // O GuiInputField tem um evento proprio de envio, que pode vir ligado ao menu de construcao.
            FieldInfo envio = AccessTools.Field(campo.GetType(), "OnInputSubmit");
            if (envio != null && !envio.FieldType.IsAbstract) envio.SetValue(campo, Activator.CreateInstance(envio.FieldType));
            campo.SetTextWithoutNotify("");
            EstilizarBusca(gui, campo);
            campo.onValueChanged.AddListener(texto =>
            {
                Busca = Normalizar(texto);
                if (InventoryGui.instance) InventoryGui.instance.UpdateCraftingPanel();
            });

            Campo = campo;
            Debug.Log($"[Deadheim] Bancadas: busca montada em '{Caminho(lista.parent)}' (lista '{lista.name}' {lista.rect.size}).");
        }

        private static void MontarQuantidade(InventoryGui gui)
        {
            _botaoFabricar = (RectTransform)gui.m_craftButton.transform;
            _botaoFabricarEsquerda = _botaoFabricar.offsetMin.x;
            _botaoEncolhido = false;

            // Mesma altura do botao Fabricar, ocupando o comeco dele (o botao encolhe quando aparece).
            GameObject root = new GameObject("DeadheimQuantidade", typeof(RectTransform));
            RectTransform r = (RectTransform)root.transform;
            r.SetParent(_botaoFabricar.parent, false);
            r.anchorMin = _botaoFabricar.anchorMin;
            r.anchorMax = new Vector2(_botaoFabricar.anchorMin.x, _botaoFabricar.anchorMax.y);
            r.offsetMin = _botaoFabricar.offsetMin;
            r.offsetMax = new Vector2(_botaoFabricarEsquerda + LarguraDaQuantidade, _botaoFabricar.offsetMax.y);
            r.SetSiblingIndex(_botaoFabricar.GetSiblingIndex());

            Menos = CriarBotao(gui, r, "-", 0f, -1);
            Mais = CriarBotao(gui, r, "+", LarguraDoBotao + EspacoEntre + LarguraDoNumero + EspacoEntre, 1);

            // Numero no meio: a moldura do botao Fabricar, mais escura, na mesma altura dos botoes.
            GameObject caixa = new GameObject("Numero", typeof(RectTransform));
            RectTransform cr = (RectTransform)caixa.transform;
            cr.SetParent(r, false);
            Posicionar(cr, LarguraDoBotao + EspacoEntre, LarguraDoNumero);
            Image moldura = gui.m_craftButton.GetComponent<Image>();
            if (moldura && moldura.sprite)
            {
                Image fundo = caixa.AddComponent<Image>();
                fundo.sprite = moldura.sprite;
                fundo.type = moldura.type;
                fundo.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                fundo.raycastTarget = false;
            }

            TMP_Text modelo = gui.m_craftButton.GetComponentInChildren<TMP_Text>(true);
            GameObject texto = new GameObject("Texto", typeof(RectTransform));
            RectTransform tr = (RectTransform)texto.transform;
            tr.SetParent(cr, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            TextMeshProUGUI numero = texto.AddComponent<TextMeshProUGUI>();
            if (modelo)
            {
                numero.font = modelo.font;
                numero.fontSharedMaterial = modelo.fontSharedMaterial;
                numero.color = modelo.color;
            }
            numero.fontSize = 24f;
            numero.alignment = TextAlignmentOptions.Center;
            numero.raycastTarget = false;
            numero.text = "1";
            _numero = numero;

            Controles = root;
            root.SetActive(false);
        }

        private static Button CriarBotao(InventoryGui gui, Transform pai, string rotulo, float x, int passo)
        {
            GameObject go = UnityEngine.Object.Instantiate(gui.m_craftButton.gameObject, pai, false);
            go.name = passo < 0 ? "Menos" : "Mais";
            go.SetActive(true);
            LimparClone(go);

            RectTransform r = (RectTransform)go.transform;
            Posicionar(r, x, LarguraDoBotao);
            r.localScale = Vector3.one;

            TMP_Text texto = go.GetComponentInChildren<TMP_Text>(true);
            if (texto)
            {
                texto.text = rotulo;
                texto.enableAutoSizing = false;
                texto.fontSize = 30f;
            }

            Button botao = go.GetComponent<Button>();
            botao.onClick = new Button.ButtonClickedEvent();
            botao.interactable = true;
            // Shift: de 10 em 10.
            botao.onClick.AddListener(() => MudarQuantidade(passo * (ShiftApertado() ? 10 : 1)));
            return botao;
        }

        /// <summary>Faixa de largura fixa a partir de x, na altura toda do pai.</summary>
        private static void Posicionar(RectTransform r, float x, float largura)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = new Vector2(0f, 1f);
            r.offsetMin = new Vector2(x, 0f);
            r.offsetMax = new Vector2(x + largura, 0f);
        }

        /// <summary>
        /// O campo do menu de construcao e cinza e em caixa alta. Aqui ele fica com o fundo dos
        /// slots de requisito e a fonte da lista de receitas, como o resto do painel.
        /// </summary>
        private static void EstilizarBusca(InventoryGui gui, TMP_InputField campo)
        {
            Image fundo = campo.GetComponent<Image>();
            Image slot = gui.m_recipeRequirementList != null && gui.m_recipeRequirementList.Length > 0
                ? gui.m_recipeRequirementList[0].GetComponent<Image>() : null;
            if (fundo && slot && slot.sprite)
            {
                fundo.sprite = slot.sprite;
                fundo.type = slot.type;
                fundo.color = slot.color;
            }

            Transform nome = gui.m_recipeElementPrefab ? gui.m_recipeElementPrefab.transform.Find("name") : null;
            TMP_Text fonte = nome ? nome.GetComponent<TMP_Text>() : null;
            if (fonte) campo.fontAsset = fonte.font;
            campo.pointSize = 16f;
            campo.customCaretColor = true;
            campo.caretColor = Ui.ValheimOrange;
            campo.selectionColor = new Color(1f, 0.631f, 0.235f, 0.35f);

            if (campo.textComponent)
            {
                campo.textComponent.fontStyle = FontStyles.Normal;
                campo.textComponent.enableAutoSizing = false;
                campo.textComponent.color = Color.white;
                campo.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (campo.placeholder is TMP_Text dica)
            {
                if (fonte) dica.font = fonte.font;
                dica.text = "Buscar item...";
                dica.fontStyle = FontStyles.Italic;
                dica.enableAutoSizing = false;
                dica.fontSize = 16f;
                dica.color = new Color(0.7f, 0.7f, 0.7f, 0.75f);
                dica.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        /// <summary>Tira do clone o que fazia ele agir como o original (dica de gamepad, tooltip, traducao).</summary>
        private static void LimparClone(GameObject go)
        {
            foreach (UIGamePad pad in go.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint) pad.m_hint.SetActive(false);
                UnityEngine.Object.DestroyImmediate(pad);
            }
            foreach (UITooltip dica in go.GetComponentsInChildren<UITooltip>(true)) UnityEngine.Object.DestroyImmediate(dica);
            foreach (Localize traducao in go.GetComponentsInChildren<Localize>(true)) UnityEngine.Object.DestroyImmediate(traducao);
            foreach (UnityEngine.EventSystems.EventTrigger gatilho in go.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true))
                UnityEngine.Object.DestroyImmediate(gatilho);
        }

        private static void Descer(RectTransform rect, float quanto)
            => rect.offsetMax = new Vector2(rect.offsetMax.x, rect.offsetMax.y - quanto);

        private static string Caminho(Transform t)
        {
            string caminho = t.name;
            for (Transform p = t.parent; p; p = p.parent) caminho = p.name + "/" + caminho;
            return caminho;
        }

        private static bool ShiftApertado() => ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift);

        // ------------------------------------------------------------------ busca

        internal static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposto.Length);
            foreach (char ch in decomposto)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>A receita aparece com a busca atual? Sem acento e sem diferenciar maiuscula.</summary>
        internal static bool Combina(Recipe recipe)
        {
            if (Busca.Length == 0 || recipe == null || !recipe.m_item) return true;
            string nome = recipe.m_item.m_itemData.m_shared.m_name;
            return Normalizar(Localization.instance.Localize(nome)).Contains(Busca)
                || Normalizar(recipe.m_item.name).Contains(Busca);
        }

        // ------------------------------------------------------------------ quantidade

        internal static void MudarQuantidade(int delta)
        {
            Quantidade = Mathf.Clamp(Quantidade + delta, 1, QuantidadeMaxima);
        }

        /// <summary>Aesir 11 (14/10/2026, decisao do usuario): fabricacao em lote (99) so para VIP.</summary>
        private static bool SoVip => !Deadheim.AesirConfig.Aesir11SoVip.Value || VipList.VipListApi.IsLocalPlayerVip();

        /// <summary>Receita nova (nao melhoria) fora da Forja: so ai a quantidade vale.</summary>
        private static bool AceitaQuantidade(InventoryGui gui, Player player)
        {
            if (!SoVip) return false;
            if (!gui.m_selectedRecipe.Recipe || gui.m_selectedRecipe.ItemData != null) return false;
            CraftingStation estacao = player ? player.GetCurrentCraftingStation() : null;
            return !(estacao && estacao.m_upgrader);
        }

        private static void AplicarLote(InventoryGui gui, bool aceita)
        {
            int lote = aceita ? Quantidade : 1;
            gui.m_multiCraftAmount = lote;
            gui.m_touchMultiCrafting = lote > 1;
        }

        private static void MostrarControles(InventoryGui gui, bool mostrar)
        {
            if (!Controles) return;
            if (Controles.activeSelf != mostrar) Controles.SetActive(mostrar);
            if (_botaoFabricar && _botaoEncolhido != mostrar)
            {
                _botaoEncolhido = mostrar;
                float esquerda = mostrar ? _botaoFabricarEsquerda + LarguraDaQuantidade + EspacoDaQuantidade : _botaoFabricarEsquerda;
                _botaoFabricar.offsetMin = new Vector2(esquerda, _botaoFabricar.offsetMin.y);
            }
            if (!mostrar) return;
            _numero.text = Quantidade.ToString();
            Menos.interactable = Quantidade > 1;
            Mais.interactable = Quantidade < QuantidadeMaxima;
        }

        // ------------------------------------------------------------------ patches

        [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
        private static class MontarAoAbrir
        {
            private static void Prefix(InventoryGui __instance) => Montar(__instance);
        }

        [HarmonyPatch(typeof(InventoryGui), "AddRecipeToList")]
        private static class FiltrarLista
        {
            private static bool Prefix(Recipe recipe) => Combina(recipe);
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
        private static class FasterCrafting
        {
            // Duracao original do jogo (antes de qualquer Prefix nosso rodar), para devolver a quem
            // nao e VIP quando a fabricacao rapida esta restrita (Aesir 11).
            private static float? _duracaoOriginal;
            private static float? _duracaoLoteOriginal;

            private static void Prefix(InventoryGui __instance, Player player)
            {
                if (_duracaoOriginal == null)
                {
                    _duracaoOriginal = __instance.m_craftDuration;
                    _duracaoLoteOriginal = __instance.m_multiCraftDuration;
                }
                bool rapido = SoVip;
                __instance.m_craftDuration = rapido ? TempoDeFabricacao : _duracaoOriginal.Value;
                __instance.m_multiCraftDuration = rapido ? TempoDeFabricacao : _duracaoLoteOriginal.Value;

                // Outra receita escolhida: volta para 1.
                if (__instance.m_selectedRecipe.Recipe != _receita || __instance.m_selectedRecipe.ItemData != _receitaItem)
                {
                    _receita = __instance.m_selectedRecipe.Recipe;
                    _receitaItem = __instance.m_selectedRecipe.ItemData;
                    Quantidade = 1;
                }

                bool aceita = AceitaQuantidade(__instance, player);
                bool fabricando = __instance.m_craftTimer >= 0f;
                // Durante a contagem o lote fica o do clique: o DoCrafting le o m_multiCraftAmount no fim.
                if (!fabricando) AplicarLote(__instance, aceita);
                MostrarControles(__instance, aceita && !fabricando);
            }

            private static void Postfix(InventoryGui __instance)
            {
                // Com quantidade 1 e Shift apertado, o jogo escreveria "Fabricar x 1".
                if (Quantidade != 1 || !__instance.m_selectedRecipe.Recipe) return;
                TMP_Text texto = __instance.m_craftButton.GetComponentInChildren<TMP_Text>();
                if (texto && texto.text.EndsWith(" x 1", StringComparison.Ordinal))
                    texto.text = texto.text.Substring(0, texto.text.Length - 4);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
        private static class LoteNoClique
        {
            private static void Prefix(InventoryGui __instance)
                => AplicarLote(__instance, AceitaQuantidade(__instance, Player.m_localPlayer));
        }

        /// <summary>Fechou o inventario: a busca volta vazia.</summary>
        [HarmonyPatch(typeof(InventoryGui), "Hide")]
        private static class LimparAoFechar
        {
            private static void Prefix()
            {
                Busca = "";
                if (!Campo) return;
                Campo.DeactivateInputField();
                Campo.SetTextWithoutNotify("");
            }
        }

        /// <summary>Digitando na busca, Tab e E nao fecham o inventario.</summary>
        [HarmonyPatch(typeof(InventoryGui), "Update")]
        private static class NaoFecharDigitando
        {
            private static void Prefix()
            {
                if (!Digitando) return;
                ZInput.ResetButtonStatus("Inventory");
                ZInput.ResetButtonStatus("Use");
            }
        }

        /// <summary>Digitando na busca, WASD, pulo e companhia nao mexem o personagem.</summary>
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static class NaoAndarDigitando
        {
            private static void Postfix(ref bool __result)
            {
                if (Digitando) __result = false;
            }
        }
    }
}
