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
    /// Lixeira do inventario (03/11/2026): um icone entre o de peso e o de armadura. Pegue o item (clique
    /// nele, como para mover) e clique na lixeira: com Shift apaga na hora; sem Shift pergunta Sim/Nao.
    /// Regras: so apaga do inventario do proprio jogador (nunca bau, que e compartilhado), nunca as
    /// Coins (slot fixo, ver Moedas.cs) e nunca item equipado. Apaga so a quantidade que estava
    /// sendo arrastada, por Inventory.RemoveItem -- o mesmo caminho do jogo, que avisa o resto do mod
    /// (Moedas, Transmog) e salva com o personagem. Tudo local ao dono do inventario, sem RPC.
    /// </summary>
    [HarmonyPatch]
    internal static class Lixeira
    {
        private const string Dica =
            "<color=#ffd75a>LIXEIRA</color>\n" +
            "Pegue um item (clique nele) e clique aqui para apagá-lo.\n" +
            "<color=#7fd08a>Segurando SHIFT</color>: apaga na hora, sem perguntar.\n" +
            "<color=#c9a64a>Sem Shift</color>: pergunta Sim/Não antes.\n" +
            "<color=#ff8a7a>Não dá para apagar</color>: moedas, item equipado ou item de baú.";

        private static Button _botao;
        private static Sprite _icone;
        private static GameObject _dialogo;
        private static TMP_Text _dialogoTexto;
        private static ItemDrop.ItemData _pendente;
        private static int _pendenteQtd;

        // InventoryGui.m_drag* sao privados no assembly original do Valheim (1.0.21).
        private static readonly FieldInfo CampoDragItem = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo CampoDragInventario = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly FieldInfo CampoDragQuantidade = AccessTools.Field(typeof(InventoryGui), "m_dragAmount");

        private static T LerCampo<T>(FieldInfo campo, InventoryGui gui)
        {
            if (campo == null || gui == null) return default(T);
            object valor = campo.GetValue(gui);
            return valor is T tipado ? tipado : default(T);
        }

        [HarmonyPatch(typeof(InventoryGui), "Update")]
        [HarmonyPostfix]
        private static void GuiUpdate(InventoryGui __instance)
        {
            try
            {
                if (!InventoryGui.IsVisible())
                {
                    if (_dialogo != null && _dialogo.activeSelf) FecharDialogo();
                    return;
                }
                if (_botao == null) Criar(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError("[Deadheim] Lixeira: " + e);
                if (_botao != null) _botao.gameObject.SetActive(false);
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
            RectTransform armadura = gui.m_armor != null ? GrupoDe(gui.m_armor.transform, pai) : null;
            if (peso == null) return;

            Vector3[] cp = new Vector3[4];
            peso.GetWorldCorners(cp);
            Vector3 pMin = pai.InverseTransformPoint(cp[0]), pMax = pai.InverseTransformPoint(cp[2]);
            float altura = Mathf.Clamp(pMax.y - pMin.y, 24f, 34f);
            float centroY = (pMin.y + pMax.y) / 2f;
            float centroX;
            if (armadura != null && armadura != peso)
            {
                armadura.GetWorldCorners(cp);
                Vector3 aMin = pai.InverseTransformPoint(cp[0]), aMax = pai.InverseTransformPoint(cp[2]);
                // O meio do espaco entre os dois grupos, qualquer que seja o da esquerda.
                float vazioDe = aMin.x >= pMax.x ? pMax.x : aMax.x;
                float vazioAte = aMin.x >= pMax.x ? aMin.x : pMin.x;
                centroX = (vazioDe + vazioAte) / 2f;
            }
            else centroX = pMax.x + 8f + altura / 2f;

            var go = new GameObject("DeadheimLixeira", typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = pai.gameObject.layer;
            go.transform.SetParent(pai, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(altura, altura);
            rt.anchoredPosition = new Vector2(centroX, centroY) - pai.rect.center;

            var fundo = go.GetComponent<Image>();
            fundo.color = new Color(0f, 0f, 0f, 0.35f);
            _botao = go.GetComponent<Button>();
            _botao.targetGraphic = fundo;
            _botao.onClick.AddListener(Clicar);

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

        // ---------------------------------------------------------------- acao

        private static void Aviso(string texto) =>
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, texto);

        private static bool Shift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static void Clicar()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (gui == null || player == null) return;

            if (CampoDragItem == null || CampoDragInventario == null || CampoDragQuantidade == null)
            {
                Debug.LogError("[Deadheim] Lixeira: campos privados de arrasto nao encontrados.");
                Aviso("A lixeira nao esta disponivel nesta versao do jogo.");
                return;
            }
            ItemDrop.ItemData item = LerCampo<ItemDrop.ItemData>(CampoDragItem, gui);
            Inventory origem = LerCampo<Inventory>(CampoDragInventario, gui);
            if (item == null || origem == null)
            {
                Aviso("Pegue um item (clique nele) e clique na lixeira. Shift = apaga na hora.");
                return;
            }
            string motivo = Recusa(player, item, origem);
            if (motivo != null) { Aviso(motivo); return; }

            int arrastada = LerCampo<int>(CampoDragQuantidade, gui);
            int qtd = Mathf.Clamp(arrastada, 1, item.m_stack);
            if (Shift) Apagar(gui, player, item, qtd);
            else AbrirDialogo(item, qtd);
        }

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
            // Revalida no momento de apagar (o dialogo pode ter ficado aberto enquanto algo mudou).
            string motivo = Recusa(player, item, player.GetInventory());
            if (motivo != null) { Aviso(motivo); return; }

            string nome = Localization.instance.Localize(item.m_shared.m_name);
            qtd = Mathf.Clamp(qtd, 1, item.m_stack);
            CampoDragItem?.SetValue(gui, null);
            CampoDragInventario?.SetValue(gui, null);
            CampoDragQuantidade?.SetValue(gui, 0);
            AccessTools.Method(typeof(InventoryGui), "SetupDragItem")?.Invoke(gui, new object[] { null, null, 1 });
            if (!player.GetInventory().RemoveItem(item, qtd)) { Aviso("Não foi possível apagar."); return; }
            Aviso("Apagado: " + (qtd > 1 ? qtd + "x " : "") + nome);
            Debug.Log($"[Deadheim] Lixeira: {player.GetPlayerName()} apagou {qtd}x {item.m_shared.m_name}.");
        }

        // ---------------------------------------------------------------- confirmacao

        private static void AbrirDialogo(ItemDrop.ItemData item, int qtd)
        {
            if (!ValheimUi.EnsureAssets()) { Aviso("Segure Shift para apagar direto."); return; }
            if (_dialogo == null) CriarDialogo();
            if (_dialogo == null) return;
            _pendente = item;
            _pendenteQtd = qtd;
            string nome = Localization.instance.Localize(item.m_shared.m_name);
            _dialogoTexto.text = $"Apagar <color=#ffd75a>{(qtd > 1 ? qtd + "x " : "")}{nome}</color> para sempre?\n" +
                                 "<size=80%>Isso não tem volta. (Dica: com Shift apaga sem perguntar.)</size>";
            _dialogo.SetActive(true);
            _dialogo.transform.SetAsLastSibling();
        }

        private static void FecharDialogo()
        {
            _pendente = null;
            if (_dialogo != null) _dialogo.SetActive(false);
        }

        private static void CriarDialogo()
        {
            GameObject canvas = ValheimUi.CreateCanvas("Deadheim_Lixeira", 450);
            if (canvas == null) return;
            _dialogo = canvas;

            RectTransform painel = ValheimUi.CreatePanel(canvas.transform, 420f, 170f);
            painel.anchorMin = painel.anchorMax = painel.pivot = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = Vector2.zero;

            var texto = ValheimUi.CreateLabel(painel, "", 20, ValheimUi.Beige, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)texto.transform, new Vector2(0f, 0.38f), Vector2.one,
                new Vector2(18f, 0f), new Vector2(-18f, -16f));
            _dialogoTexto = texto;

            Botao(painel, "Sim", new Vector2(0.27f, 0.2f), () =>
            {
                ItemDrop.ItemData item = _pendente;
                int qtd = _pendenteQtd;
                FecharDialogo();
                if (item != null && InventoryGui.instance != null && Player.m_localPlayer != null)
                    Apagar(InventoryGui.instance, Player.m_localPlayer, item, qtd);
            });
            Botao(painel, "Não", new Vector2(0.73f, 0.2f), FecharDialogo);
        }

        private static void Botao(RectTransform painel, string texto, Vector2 ancora, UnityEngine.Events.UnityAction acao)
        {
            Button b = ValheimUi.CreateButton(painel, texto, 140f, 40f, 20);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = ancora;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            b.onClick.AddListener(acao);
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
