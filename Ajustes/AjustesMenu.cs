using HarmonyLib;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim.Ajustes
{
    /// <summary>
    /// Poe a opcao "Deadheim" no menu do ESC, logo abaixo de Configuracoes. O botao e uma
    /// copia do proprio botao do jogo (mesma arte, fonte, som e realce), so com o texto e o
    /// clique trocados. A janela abre no lugar do menu e fecha com ESC, voltando ao menu.
    /// </summary>
    internal static class AjustesMenu
    {
        public const string TextoBotao = "Deadheim";

        private static GameObject _botao;
        private static Transform _dialogo;
        private static Menu _menuTentado;

        private static CanvasGroup _grupoOculto;
        private static float _alfaAntes;
        private static bool _interagiaAntes;
        private static bool _bloqueavaAntes;

        /// <summary>
        /// O Menu.Update e corrigido a mao, e nao por atributo: se o nome mudar num patch do
        /// jogo, perde-se so o "ESC volta ao menu", e nao o PatchAll do Deadheim inteiro.
        /// </summary>
        public static void Init(Harmony harmony)
        {
            try
            {
                var update = AccessTools.Method(typeof(Menu), "Update");
                if (update == null)
                {
                    Debug.LogWarning("[Deadheim] Menu.Update nao existe: ESC na janela Deadheim fecha o menu inteiro.");
                    return;
                }
                harmony.Patch(update, prefix: new HarmonyMethod(typeof(AjustesMenu), nameof(AntesDoMenuUpdate)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Nao consegui corrigir o Menu.Update: {ex.Message}");
            }
        }

        /// <summary>Chamado todo frame pelo Plugin.Update.</summary>
        public static void Update()
        {
            Menu menu = Menu.instance;
            if (menu == null)
            {
                if (AjustesJanela.Aberta) FecharJanela();
                return;
            }

            bool visivel = Menu.IsVisible();
            // Uma tentativa por Menu: cada mundo carregado traz um Menu novo.
            if (visivel && _botao == null && _menuTentado != menu)
            {
                _menuTentado = menu;
                try
                {
                    CriarBotao(menu);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Deadheim] Nao consegui por a opcao Deadheim no menu: {ex}");
                }
            }

            if (AjustesJanela.Aberta)
            {
                if (!visivel) FecharJanela();
                else AjustesJanela.Tick();
            }
        }

        public static void AbrirJanela()
        {
            Menu menu = Menu.instance;
            if (menu == null) return;

            _dialogo = Dialogo(menu);
            Transform pai = _dialogo != null && _dialogo.parent != null ? _dialogo.parent : menu.transform;
            OcultarDialogo(true);
            AjustesJanela.Mostrar(pai);
        }

        public static void FecharJanela()
        {
            AjustesJanela.Esconder();
            OcultarDialogo(false);
        }

        /// <summary>
        /// Com a janela aberta, o ESC fecha so ela e o menu continua na tela, como a janela
        /// de Configuracoes do jogo faz. Pular o Update do menu nesse frame e o que impede o
        /// mesmo ESC de fechar o menu tambem.
        /// </summary>
        private static bool AntesDoMenuUpdate()
        {
            if (!AjustesJanela.Aberta) return true;
            if (!Input.GetKeyDown(KeyCode.Escape)) return true;

            FecharJanela();
            return false;
        }

        // ------------------------------------------------------------------ botao

        private static void CriarBotao(Menu menu)
        {
            Transform dialogo = Dialogo(menu);
            if (dialogo == null)
            {
                Debug.LogWarning("[Deadheim] Menu sem m_menuDialog: a opcao Deadheim fica fora do menu do ESC.");
                return;
            }

            Button modelo = EscolherModelo(dialogo);
            if (modelo == null)
            {
                Debug.LogWarning("[Deadheim] Nenhum botao no menu do ESC para copiar.");
                return;
            }

            RectTransform rectModelo = (RectTransform)modelo.transform;
            LayoutGroup layout = rectModelo.parent.GetComponent<LayoutGroup>();
            float passo = Passo(rectModelo, layout);

            GameObject copia = UnityEngine.Object.Instantiate(modelo.gameObject, rectModelo.parent, false);
            copia.name = "Deadheim";
            copia.transform.SetSiblingIndex(rectModelo.GetSiblingIndex() + 1);
            LimparCopia(copia);
            TrocarTexto(copia, TextoBotao);

            Button botao = copia.GetComponent<Button>();
            // Um evento novo derruba tambem os listeners persistentes da copia (o OnSettings).
            botao.onClick = new Button.ButtonClickedEvent();
            botao.onClick.AddListener(AbrirJanela);

            AjustarNavegacao(modelo, botao);
            AbrirEspaco(rectModelo, (RectTransform)copia.transform, layout, passo, dialogo);

            _botao = copia;
            _dialogo = dialogo;
        }

        /// <summary>m_menuDialog e Transform em umas versoes e GameObject em outras.</summary>
        private static Transform Dialogo(Menu menu)
        {
            object valor = AccessTools.Field(typeof(Menu), "m_menuDialog")?.GetValue(menu);
            if (valor is Component componente) return componente.transform;
            if (valor is GameObject objeto) return objeto.transform;
            return null;
        }

        /// <summary>O botao de Configuracoes; sem ele, o ultimo antes de Sair/Logout.</summary>
        private static Button EscolherModelo(Transform dialogo)
        {
            Button[] botoes = dialogo.GetComponentsInChildren<Button>(false);
            Button configuracoes = botoes.FirstOrDefault(b => b.name.IndexOf("settings", StringComparison.OrdinalIgnoreCase) >= 0);
            if (configuracoes != null) return configuracoes;

            return botoes.LastOrDefault(b =>
                       b.name.IndexOf("logout", StringComparison.OrdinalIgnoreCase) < 0 &&
                       b.name.IndexOf("exit", StringComparison.OrdinalIgnoreCase) < 0 &&
                       b.name.IndexOf("quit", StringComparison.OrdinalIgnoreCase) < 0)
                   ?? botoes.FirstOrDefault();
        }

        /// <summary>
        /// Tira da copia o que pertence ao botao original: a dica de botao do controle (senao
        /// o mesmo botao do controle clicaria os dois) e a traducao automatica do texto.
        /// </summary>
        private static void LimparCopia(GameObject copia)
        {
            foreach (MonoBehaviour componente in copia.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (componente == null) continue;
                string nome = componente.GetType().Name;
                if (nome == "UIGamePad")
                {
                    if (AccessTools.Field(componente.GetType(), "m_hint")?.GetValue(componente) is GameObject dica && dica != null)
                        dica.SetActive(false);
                    UnityEngine.Object.DestroyImmediate(componente);
                }
                else if (nome.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.DestroyImmediate(componente);
                }
            }
        }

        private static void TrocarTexto(GameObject copia, string texto)
        {
            TMP_Text tmp = copia.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = texto;
                return;
            }

            Text legado = copia.GetComponentInChildren<Text>(true);
            if (legado != null) legado.text = texto;
        }

        private static void AjustarNavegacao(Button modelo, Button novo)
        {
            Navigation navModelo = modelo.navigation;
            if (navModelo.mode != Navigation.Mode.Explicit) return;

            Selectable abaixo = navModelo.selectOnDown;

            Navigation navNovo = navModelo;
            navNovo.selectOnUp = modelo;
            navNovo.selectOnDown = abaixo;
            novo.navigation = navNovo;

            navModelo.selectOnDown = novo;
            modelo.navigation = navModelo;

            if (abaixo != null)
            {
                Navigation navAbaixo = abaixo.navigation;
                navAbaixo.selectOnUp = novo;
                abaixo.navigation = navAbaixo;
            }
        }

        /// <summary>Altura de uma linha do menu: botao + espaco ate o proximo.</summary>
        private static float Passo(RectTransform modelo, LayoutGroup layout)
        {
            float altura = modelo.rect.height;
            if (layout is VerticalLayoutGroup vertical) return altura + vertical.spacing;
            if (layout != null) return altura;

            float menor = float.MaxValue;
            foreach (Transform filho in modelo.parent)
            {
                if (filho == modelo || !filho.gameObject.activeSelf || filho.GetComponent<Button>() == null) continue;
                float distancia = Mathf.Abs(((RectTransform)filho).anchoredPosition.y - modelo.anchoredPosition.y);
                if (distancia > 1f && distancia < menor) menor = distancia;
            }
            return menor < float.MaxValue ? menor : altura + 4f;
        }

        /// <summary>
        /// Abre uma linha para o botao novo. Com LayoutGroup o proprio layout empilha; sem
        /// ele, o novo vai para a vaga do de baixo e os de baixo descem uma linha. Depois,
        /// o que tem altura fixa cresce uma linha, da lista ate a moldura do menu.
        /// </summary>
        private static void AbrirEspaco(RectTransform modelo, RectTransform novo, LayoutGroup layout, float passo, Transform dialogo)
        {
            RectTransform lista = modelo.parent as RectTransform;
            if (lista == null) return;

            if (layout == null)
            {
                float yModelo = modelo.anchoredPosition.y;
                foreach (Transform filho in lista)
                {
                    RectTransform irmao = filho as RectTransform;
                    if (irmao == null || irmao == novo || irmao == modelo) continue;
                    if (irmao.anchoredPosition.y < yModelo - 0.5f) irmao.anchoredPosition -= new Vector2(0f, passo);
                }
                novo.anchoredPosition = modelo.anchoredPosition - new Vector2(0f, passo);
            }

            for (Transform t = lista; t != null; t = t.parent)
            {
                RectTransform r = t as RectTransform;
                bool alturaFixa = r != null && Mathf.Approximately(r.anchorMin.y, r.anchorMax.y);
                if (alturaFixa && r.GetComponent<ContentSizeFitter>() == null)
                    r.sizeDelta += new Vector2(0f, passo);
                if (t == dialogo) break;
            }

            if (layout != null) LayoutRebuilder.ForceRebuildLayoutImmediate(lista);
        }

        /// <summary>
        /// Esconde o menu do ESC sem desativar nada (o Menu.Update continua achando tudo
        /// onde deixou): so transparencia e clique, por um CanvasGroup.
        /// </summary>
        private static void OcultarDialogo(bool ocultar)
        {
            if (!ocultar)
            {
                // Se o menu ja foi destruido (logout), nao ha o que devolver.
                if (_grupoOculto != null)
                {
                    _grupoOculto.alpha = _alfaAntes;
                    _grupoOculto.interactable = _interagiaAntes;
                    _grupoOculto.blocksRaycasts = _bloqueavaAntes;
                }
                _grupoOculto = null;
                return;
            }

            if (_dialogo == null) return;
            CanvasGroup grupo = _dialogo.GetComponent<CanvasGroup>();
            if (grupo == null) grupo = _dialogo.gameObject.AddComponent<CanvasGroup>();
            if (grupo == _grupoOculto) return;

            _alfaAntes = grupo.alpha;
            _interagiaAntes = grupo.interactable;
            _bloqueavaAntes = grupo.blocksRaycasts;
            grupo.alpha = 0f;
            grupo.interactable = false;
            grupo.blocksRaycasts = false;
            _grupoOculto = grupo;
        }
    }
}
