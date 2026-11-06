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
        private const string Dica =
            "<color=#ffd75a>LIXEIRA</color>\n" +
            "Selecione um item do inventário (passe o mouse ou pegue-o com o clique) e aperte <color=#ffd75a>Delete</color> para apagá-lo.\n" +
            "Apaga a pilha inteira, sem perguntar.\n" +
            "<color=#ff8a7a>Não dá para apagar</color>: moedas, item equipado ou item de baú.";

        private static GameObject _botao;
        private static Sprite _icone;

        [HarmonyPatch(typeof(InventoryGui), "Update")]
        [HarmonyPostfix]
        private static void GuiUpdate(InventoryGui __instance)
        {
            try
            {
                if (!InventoryGui.IsVisible()) return;
                if (_botao == null) Criar(__instance);
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

        private static void Criar(InventoryGui gui)
        {
            if (gui.m_player == null || gui.m_weight == null) return;
            RectTransform pai = gui.m_player;
            RectTransform peso = GrupoDe(gui.m_weight.transform, pai);
            if (peso == null) return;

            Vector3[] cp = new Vector3[4];
            peso.GetWorldCorners(cp);
            Vector3 pMin = pai.InverseTransformPoint(cp[0]), pMax = pai.InverseTransformPoint(cp[2]);
            float altura = Mathf.Clamp(pMax.y - pMin.y, 24f, 34f);
            // Centralizada ACIMA do indicador de peso.
            float centroX = (pMin.x + pMax.x) / 2f;
            float centroY = pMax.y + 4f + altura / 2f;

            var go = new GameObject("DeadheimLixeira", typeof(RectTransform), typeof(Image));
            go.layer = pai.gameObject.layer;
            go.transform.SetParent(pai, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(altura, altura);
            rt.anchoredPosition = new Vector2(centroX, centroY) - pai.rect.center;
            _botao = go;

            var fundo = go.GetComponent<Image>();
            fundo.color = new Color(0f, 0f, 0f, 0.35f);

            var icone = new GameObject("Icone", typeof(RectTransform), typeof(Image));
            icone.layer = go.layer;
            icone.transform.SetParent(go.transform, false);
            var rtI = (RectTransform)icone.transform;
            rtI.anchorMin = Vector2.zero;
            rtI.anchorMax = Vector2.one;
            rtI.offsetMin = new Vector2(altura * 0.12f, altura * 0.12f);
            rtI.offsetMax = new Vector2(-altura * 0.12f, -altura * 0.12f);
            var img = icone.GetComponent<Image>();
            img.sprite = Icone();
            img.color = new Color(1f, 0.89f, 0.66f, 1f);
            img.raycastTarget = false;
            img.preserveAspect = true;

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
            return grade.GetGamepadSelectedItem();
        }

        /// <summary>Tecla Delete: apaga a pilha do item pego (arrastado) ou sob o mouse.</summary>
        private static void ApagarComDelete(InventoryGui gui)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            ItemDrop.ItemData item = LerCampo<ItemDrop.ItemData>(CampoDragItem, gui) ?? ItemSelecionado(gui);
            if (item == null) { Aviso("Selecione um item e aperte Delete."); return; }
            string motivo = Recusa(player, item, player.GetInventory());
            if (motivo != null) { Aviso(motivo); return; }
            Apagar(gui, player, item, item.m_stack);
        }

        private static void Aviso(string texto) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);

        /// <summary>Texto do motivo se o item NAO pode ser apagado; null se pode.</summary>
        private static string Recusa(Player player, ItemDrop.ItemData item, Inventory origem)
        {
            if (origem != player.GetInventory()) return "A lixeira só apaga itens do seu inventário.";
            if (!origem.ContainsItem(item)) return "Esse item não está mais no inventário.";
            if (Moedas.EhMoeda(item)) return "As moedas não podem ser apagadas.";
            if (item.m_equipped) return "Desequipe o item antes de apagá-lo.";
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
            if (!player.GetInventory().RemoveItem(item, qtd)) { Aviso("Não foi possível apagar."); return; }
            Aviso("Apagado: " + (qtd > 1 ? qtd + "x " : "") + nome);
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
