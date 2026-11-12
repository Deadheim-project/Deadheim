using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    /// <summary>
    /// Aesir 15 no proprio bau (02/11/2026, pedido do usuario): um botao com icone de ordenar logo a
    /// direita do "Pegar tudo" do jogo, dentro do painel do bau. E uma copia do botao do
    /// jogo, entao tem o mesmo fundo e o mesmo som; so aparece para Aesir com o Aesir15 ligado.
    /// A tecla (AesirTeclas.Ordenar, Alt+O) continua.
    /// </summary>
    [HarmonyPatch]
    internal static class AesirBotaoOrdenar
    {
        private const float Folga = 6f;

        private static Button _botao;
        private static Sprite _icone;

        private static readonly AccessTools.FieldRef<InventoryGui, Container> ContainerAtual =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(InventoryGui), "UpdateContainer");

        private static void Postfix(InventoryGui __instance)
        {
            try { Atualizar(__instance); }
            catch (Exception e)
            {
                AesirLog.Erro(15, e);
                if (_botao != null) _botao.gameObject.SetActive(false);
            }
        }

        private static void Atualizar(InventoryGui gui)
        {
            bool mostrar = gui.m_container != null && gui.m_container.gameObject.activeSelf && ContainerAtual(gui) != null
                           && AesirConfig.Aesir15OrdenarAtivo != null && AesirConfig.Aesir15OrdenarAtivo.Value
                           && VipList.VipListApi.IsLocalPlayerVip();
            if (!mostrar)
            {
                if (_botao != null && _botao.gameObject.activeSelf) _botao.gameObject.SetActive(false);
                return;
            }
            if (_botao == null) Criar(gui);
            if (_botao != null && !_botao.gameObject.activeSelf) _botao.gameObject.SetActive(true);
        }

        /// <summary>Copia o botao do jogo mais a esquerda e poe o icone logo a direita dele, do tamanho da
        /// altura. A esquerda dele ficava fora do painel (teste local de 02/11/2026).</summary>
        private static void Criar(InventoryGui gui)
        {
            Button modelo = gui.m_stackAllButton != null ? gui.m_stackAllButton : gui.m_takeAllButton;
            if (modelo == null) return;
            RectTransform pai = (RectTransform)modelo.transform.parent;

            // O mais a esquerda dos dois botoes do jogo, no espaco do pai.
            float esquerda = float.MaxValue, direita = 0f, centroY = 0f, altura = 0f;
            foreach (Button b in new[] { gui.m_takeAllButton, gui.m_stackAllButton })
            {
                if (b == null || b.transform.parent != pai) continue;
                var rt = (RectTransform)b.transform;
                var cantos = new Vector3[4];
                rt.GetWorldCorners(cantos);
                Vector3 min = pai.InverseTransformPoint(cantos[0]), max = pai.InverseTransformPoint(cantos[2]);
                if (min.x < esquerda)
                {
                    esquerda = min.x;
                    direita = max.x;
                    centroY = (min.y + max.y) / 2f;
                    altura = max.y - min.y;
                }
            }
            if (esquerda == float.MaxValue) return;

            GameObject go = UnityEngine.Object.Instantiate(modelo.gameObject, pai);
            go.name = "DeadheimOrdenar";
            _botao = go.GetComponent<Button>();
            _botao.onClick = new Button.ButtonClickedEvent();
            _botao.onClick.AddListener(Clicar);

            // Sem o texto do botao copiado: so o icone.
            foreach (var texto in go.GetComponentsInChildren<TMPro.TMP_Text>(true)) UnityEngine.Object.Destroy(texto.gameObject);
            foreach (var texto in go.GetComponentsInChildren<Text>(true)) UnityEngine.Object.Destroy(texto.gameObject);
            DicaDoJogo(go, L.T("aesirmsg.b_ordenar"));

            var rtBotao = (RectTransform)go.transform;
            rtBotao.anchorMin = rtBotao.anchorMax = new Vector2(0.5f, 0.5f);
            rtBotao.pivot = new Vector2(0.5f, 0.5f);
            rtBotao.sizeDelta = new Vector2(altura, altura);
            rtBotao.anchoredPosition = new Vector2(direita + Folga + altura / 2f, centroY) - pai.rect.center;

            var icone = new GameObject("Icone", typeof(RectTransform), typeof(Image));
            icone.transform.SetParent(go.transform, false);
            var rtIcone = (RectTransform)icone.transform;
            rtIcone.anchorMin = Vector2.zero;
            rtIcone.anchorMax = Vector2.one;
            rtIcone.offsetMin = new Vector2(altura * 0.2f, altura * 0.2f);
            rtIcone.offsetMax = new Vector2(-altura * 0.2f, -altura * 0.2f);
            var imagem = icone.GetComponent<Image>();
            imagem.sprite = Icone();
            imagem.color = new Color(1f, 0.89f, 0.66f, 1f);
            imagem.raycastTarget = false;
            imagem.preserveAspect = true;
        }

        private static void Clicar()
        {
            Player player = Player.m_localPlayer;
            AesirBaus.Ordenar(texto => player?.Message(MessageHud.MessageType.Center, texto));
        }

        /// <summary>O texto ao passar o mouse, se o botao do jogo tiver um (UITooltip copiado com ele).</summary>
        private static void DicaDoJogo(GameObject go, string texto)
        {
            var dica = go.GetComponent<UITooltip>();
            if (dica == null) return;
            FieldInfo campo = AccessTools.Field(typeof(UITooltip), "m_text");
            if (campo != null && campo.FieldType == typeof(string)) campo.SetValue(dica, texto);
        }

        /// <summary>O icone de ordenar desenhado aqui: tres barras crescendo, como uma lista ordenada.</summary>
        private static Sprite Icone()
        {
            if (_icone != null) return _icone;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var limpo = new Color32(0, 0, 0, 0);
            var cheio = new Color32(255, 255, 255, 255);
            var pixels = new Color32[n * n];
            for (int k = 0; k < pixels.Length; k++) pixels[k] = limpo;
            // Barras de baixo para cima: curta, media, longa (a lista de A a Z).
            int[] larguras = { 12, 20, 28 };
            for (int b = 0; b < 3; b++)
            {
                int y0 = 4 + b * 10;
                for (int y = y0; y < y0 + 6; y++)
                    for (int x = 2; x < 2 + larguras[b]; x++)
                        pixels[y * n + x] = cheio;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _icone = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _icone;
        }
    }
}
