using Deadheim.Shared;
using HarmonyLib;
using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    /// <summary>
    /// Lixeira do inventario: icone apenas informativo (tooltip) acima do indicador de peso. Para apagar:
    /// selecione o item (passe o mouse, ou pegue-o com clique) e aperte Delete. Apaga a pilha toda do
    /// inventario do proprio jogador, por Inventory.RemoveItem. Nunca apaga Coins, item equipado ou item de bau.
    /// </summary>
    [HarmonyPatch]
    internal static class Lixeira
    {
        private static string Dica => L.T("lixeira.dica");

        private static GameObject _botao;
        private static RectTransform _peso;
        private static float _dyPeso;
        private static Sprite _icone;

        [HarmonyPatch(typeof(InventoryGui), "Update")]
        [HarmonyPostfix]
        private static void GuiUpdate(InventoryGui __instance)
        {
            try
            {
                if (!InventoryGui.IsVisible()) return;
                if (_botao == null) Criar(__instance);
                if (_botao != null) Alinhar();
                if (Input.GetKeyDown(KeyCode.Delete)) ApagarComDelete(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError("[Deadheim] Lixeira: " + e);
                if (_botao != null) _botao.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- botao

        private static RectTransform GrupoDe(Transform folha, Transform ancestral)
        {
            Transform t = folha;
            while (t != null && t.parent != ancestral) t = t.parent;
            return t as RectTransform;
        }

        /// <summary>Limites (mundo) do bloco inteiro, contando filhos que saem do retangulo (o icone).</summary>
        private static void Limites(RectTransform bloco, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue);
            Vector3[] c = new Vector3[4];
            foreach (RectTransform r in bloco.GetComponentsInChildren<RectTransform>(true))
            {
                r.GetWorldCorners(c);
                min = Vector3.Min(min, c[0]);
                max = Vector3.Max(max, c[2]);
            }
        }

        /// <summary>
        /// Mantem a lixeira com o mesmo tamanho/forma do bloco do peso (o jogo ajusta o bloco depois de
        /// criado) e na mesma coluna, so deslocada para baixo.
        /// </summary>
        private static void Alinhar()
        {
            if (_peso == null) return;
            var rt = (RectTransform)_botao.transform;
            CopiarForma(_peso, rt);
            Vector3 p = _peso.position;
            rt.position = new Vector3(p.x, p.y + _dyPeso, p.z);
        }

        private static void CopiarForma(RectTransform de, RectTransform para)
        {
            para.anchorMin = de.anchorMin;
            para.anchorMax = de.anchorMax;
            para.pivot = de.pivot;
            para.sizeDelta = de.sizeDelta;
            para.localScale = de.localScale;
            CopiarFilhos(de, para);
        }

        private static void CopiarFilhos(RectTransform a, RectTransform b)
        {
            int n = Mathf.Min(a.childCount, b.childCount);
            for (int i = 0; i < n; i++)
            {
                if (!(a.GetChild(i) is RectTransform x) || !(b.GetChild(i) is RectTransform y)) continue;
                y.anchorMin = x.anchorMin;
                y.anchorMax = x.anchorMax;
                y.pivot = x.pivot;
                y.sizeDelta = x.sizeDelta;
                y.anchoredPosition = x.anchoredPosition;
                y.localScale = x.localScale;
                CopiarFilhos(x, y);
            }
        }

        private static void MoverY(RectTransform bloco, float y) =>
            bloco.position = new Vector3(bloco.position.x, bloco.position.y + y, bloco.position.z);

        private static void Criar(InventoryGui gui)
        {
            if (gui.m_player == null || gui.m_weight == null) return;
            RectTransform pai = gui.m_player;
            RectTransform peso = GrupoDe(gui.m_weight.transform, pai);
            if (peso == null) return;
            RectTransform armadura = gui.m_armor != null ? GrupoDe(gui.m_armor.transform, pai) : null;
            if (armadura == peso) armadura = null;

            // Mesmo visual dos indicadores de armadura e peso: clona o bloco do peso (fundo de madeira,
            // icone e texto) e troca o icone pela lixeira.
            GameObject go = UnityEngine.Object.Instantiate(peso.gameObject, pai, false);
            go.name = "DeadheimLixeira";
            // Mesma camada do peso: logo depois dele, por baixo da moldura do inventario (como armadura e peso).
            go.transform.SetSiblingIndex(peso.GetSiblingIndex() + 1);
            var rt = (RectTransform)go.transform;

            // Coluna ao lado da grade: armadura (fica onde esta), peso e lixeira, igualmente espacados e
            // dentro da altura do painel (margem de baixo = margem de cima da armadura). Tudo em mundo.
            Vector3[] cp = new Vector3[4];
            pai.GetWorldCorners(cp);
            Limites(peso, out Vector3 pMin, out Vector3 pMax);
            float h = pMax.y - pMin.y;
            float topo = pMax.y + h, margem = 0f;
            if (armadura != null)
            {
                Limites(armadura, out Vector3 aMin, out Vector3 aMax);
                topo = aMax.y;
                margem = Mathf.Max(0f, cp[2].y - aMax.y);
            }
            float fundoY = cp[0].y + margem;
            float vao = Mathf.Max(4f * pai.lossyScale.y, (topo - fundoY - 3f * h) / 2f);
            float topoPeso = armadura != null ? topo - h - vao : pMax.y;
            MoverY(peso, topoPeso - pMax.y);
            MoverY(rt, topoPeso - h - vao - pMax.y);
            _botao = go;
            _peso = peso;
            _dyPeso = rt.position.y - peso.position.y;

            foreach (UITooltip t in go.GetComponentsInChildren<UITooltip>(true)) UnityEngine.Object.Destroy(t);
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                t.text = "Del";
                t.color = new Color(1f, 0.84f, 0.35f, 1f);
            }

            // Icone = menor Image filha (o fundo de madeira e a maior).
            Image icone = null;
            float menor = float.MaxValue;
            foreach (Image im in go.GetComponentsInChildren<Image>(true))
            {
                if (im.gameObject == go) continue;
                float area = im.rectTransform.rect.width * im.rectTransform.rect.height;
                if (area < menor) { menor = area; icone = im; }
            }
            if (icone != null)
            {
                icone.sprite = Icone();
                icone.overrideSprite = null;
                icone.color = new Color(0.82f, 0.80f, 0.76f, 1f);
                icone.preserveAspect = true;
                icone.type = Image.Type.Simple;
            }
            // Fundo precisa receber o mouse para a dica.
            Image fundo = go.GetComponent<Image>();
            if (fundo != null) fundo.raycastTarget = true;
            else go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            // Moldura inteira (madeira, icone, texto) recebe o mouse; o evento sobe ate a dica na raiz.
            foreach (Graphic g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = true;

            // Dica do jogo: reaproveita o prefab de tooltip de algum UITooltip da propria janela.
            UITooltip modelo = null;
            foreach (UITooltip t in gui.GetComponentsInChildren<UITooltip>(true))
                if (t.m_tooltipPrefab != null) { modelo = t; break; }
            if (modelo != null)
            {
                var dica = go.AddComponent<UITooltip>();
                dica.m_tooltipPrefab = modelo.m_tooltipPrefab;
                dica.m_topic = "Lixeira";
                dica.m_text = Dica;
            }
        }

        // ---------------------------------------------------------------- Delete

        private static readonly FieldInfo CampoDragItem = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo CampoDragInventario = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly FieldInfo CampoDragQuantidade = AccessTools.Field(typeof(InventoryGui), "m_dragAmount");
        private static readonly MethodInfo MetodoHover = AccessTools.Method(typeof(InventoryGrid), "GetHoveredElement");

        private static T LerCampo<T>(FieldInfo campo, InventoryGui gui)
        {
            if (campo == null || gui == null) return default(T);
            object valor = campo.GetValue(gui);
            return valor is T tipado ? tipado : default(T);
        }

        /// <summary>Item sob o mouse na grade do jogador, ou o selecionado por gamepad/teclado; null se nenhum.</summary>
        private static ItemDrop.ItemData ItemSelecionado(InventoryGui gui)
        {
            InventoryGrid grade = gui.m_playerGrid;
            if (grade == null) return null;
            Inventory inv = grade.GetInventory();
            if (inv == null) return null;
            try
            {
                object el = MetodoHover?.Invoke(grade, null);
                if (el != null && AccessTools.Field(el.GetType(), "m_pos")?.GetValue(el) is Vector2i p)
                {
                    ItemDrop.ItemData it = inv.GetItemAt(p.x, p.y);
                    if (it != null) return it;
                }
            }
            catch (Exception e) { Debug.LogError("[Deadheim] Lixeira (hover): " + e); }
            // GetGamepadSelectedItem devolve a casa m_selected (geralmente a 0,0) mesmo com mouse: so vale com gamepad.
            if (!ZInput.IsGamepadActive()) return null;
            return grade.GetGamepadSelectedItem();
        }

        private static ItemDrop.ItemData _pendente;
        private static float _pendenteAte;

        /// <summary>Item que nao e pilha de material (equipamento, cajado, qualidade acima de 1) pede um segundo Delete.</summary>
        private static bool PedeConfirmacao(ItemDrop.ItemData item) =>
            item.m_shared.m_maxStackSize <= 1 || item.m_quality > 1;

        /// <summary>Tecla Delete: apaga a pilha do item pego (arrastado) ou sob o mouse.</summary>
        private static void ApagarComDelete(InventoryGui gui)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            ItemDrop.ItemData item = LerCampo<ItemDrop.ItemData>(CampoDragItem, gui) ?? ItemSelecionado(gui);
            if (item == null) { Aviso("Selecione um item e aperte Delete."); return; }
            string motivo = Recusa(player, item, player.GetInventory());
            if (motivo != null) { Aviso(motivo); return; }
            if (PedeConfirmacao(item))
            {
                if (!ReferenceEquals(_pendente, item) || Time.unscaledTime > _pendenteAte)
                {
                    _pendente = item;
                    _pendenteAte = Time.unscaledTime + 4f;
                    string n = Localization.instance.Localize(item.m_shared.m_name);
                    Aviso(L.T("lixeira.confirmar", n, item.m_quality > 1 ? L.T("lixeira.nivel", item.m_quality) : ""));
                    return;
                }
                _pendente = null;
            }
            Apagar(gui, player, item, item.m_stack);
        }

        private static void Aviso(string texto) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);

        /// <summary>Texto do motivo se o item NAO pode ser apagado; null se pode.</summary>
        private static string Recusa(Player player, ItemDrop.ItemData item, Inventory origem)
        {
            if (origem != player.GetInventory()) return L.T("lixeira.so_inventario");
            if (!origem.ContainsItem(item)) return L.T("lixeira.nao_esta");
            if (Moedas.EhMoeda(item)) return L.T("lixeira.moeda");
            if (item.m_equipped) return L.T("lixeira.equipado");
            return null;
        }

        private static void Apagar(InventoryGui gui, Player player, ItemDrop.ItemData item, int qtd)
        {
            string nome = Localization.instance.Localize(item.m_shared.m_name);
            qtd = Mathf.Clamp(qtd, 1, item.m_stack);
            if (ReferenceEquals(LerCampo<ItemDrop.ItemData>(CampoDragItem, gui), item))
            {
                CampoDragItem?.SetValue(gui, null);
                CampoDragInventario?.SetValue(gui, null);
                CampoDragQuantidade?.SetValue(gui, 0);
                AccessTools.Method(typeof(InventoryGui), "SetupDragItem")?.Invoke(gui, new object[] { null, null, 1 });
            }
            if (!player.GetInventory().RemoveItem(item, qtd)) { Aviso(L.T("lixeira.falhou")); return; }
            Aviso(L.T("lixeira.apagado", qtd > 1 ? qtd + "x " : "", nome));
            Debug.Log($"[Deadheim] Lixeira: {player.GetPlayerName()} apagou {qtd}x {item.m_shared.m_name}.");
        }

        // ---------------------------------------------------------------- icone

        /// <summary>Lixeira desenhada aqui: tampa com alca e corpo com tres ripas.</summary>
        private static Sprite Icone()
        {
            if (_icone != null) return _icone;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[n * n];
            var cheio = new Color32(255, 255, 255, 255);
            for (int k = 0; k < pixels.Length; k++) pixels[k] = new Color32(0, 0, 0, 0);
            void Ret(int x0, int y0, int x1, int y1)
            {
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) pixels[y * n + x] = cheio;
            }
            Ret(12, 26, 20, 29);                 // alca
            Ret(4, 22, 28, 25);                  // tampa
            Ret(7, 3, 25, 22);                   // corpo
            // ripas vazadas no corpo
            for (int i = 0; i < 3; i++)
                for (int y = 6; y < 19; y++) { int x = 11 + i * 5; pixels[y * n + x] = new Color32(0, 0, 0, 0); pixels[y * n + x + 1] = new Color32(0, 0, 0, 0); }
            tex.SetPixels32(pixels);
            tex.Apply();
            _icone = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _icone;
        }
    }
}
