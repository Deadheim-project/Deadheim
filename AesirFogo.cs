using BepInEx.Configuration;
using Deadheim.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadheim
{
    /// <summary>
    /// Aesir 29: fogo personalizado (03/11/2026). A tocha (e qualquer luz com chama) na mao do Aesir
    /// queima de outra cor. Puro visual: so mexe na COR da luz e das particulas de chama, nunca em
    /// alcance, intensidade ou dano. O dono publica a cor na propria ZDO ("dh_chama", como o titulo
    /// e o pet) e cada cliente pinta a chama que enxerga; sem cor publicada, o visual e o do jogo.
    /// O padrao e o fogo do jogo. Com uma tocha equipada, a tecla Aesir29Tecla (Alt+F) abre um menu
    /// de cores (a escolha vale na hora); "Definir como padrao" grava a cor na cfg (Aesir29Cor) para
    /// as proximas sessoes. Quem deixou de ser Aesir volta ao fogo normal.
    /// </summary>
    [HarmonyPatch]
    internal static class AesirFogo
    {
        private const string ZdoChama = "dh_chama";
        private const float Intervalo = 0.5f;

        // Character.m_nview e protegido no assembly original do Valheim; acessar por reflexao.
        private static readonly FieldInfo CampoNView = AccessTools.Field(typeof(Character), "m_nview");
        private static ZNetView Rede(Player player) => CampoNView?.GetValue(player) as ZNetView;

        public static ConfigEntry<bool> Ativo;
        public static ConfigEntry<string> Cor;
        public static ConfigEntry<KeyboardShortcut> Tecla;

        // Cor escolhida no menu nesta sessao (null = vale a da cfg).
        private static string _sessao;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Aesir cosmeticos";
            Ativo = Plugin.Synced(config.Bind(secao, "Aesir29FogoAtivo", true,
                "Aesir 29: o fogo da tocha do Aesir pode ter outra cor (so visual, nao muda luz nem dano)."));
            Cor = config.Bind(secao, "Aesir29Cor", "normal",
                "Cor padrao do seu fogo (so vale se voce e Aesir): roxo, azul, verde, vermelho, dourado, ciano, rosa, ou #RRGGBB. " +
                "'normal' ou vazio = fogo do jogo (padrao). Mais facil pelo menu da tocha.");
            Tecla = config.Bind(secao, "Aesir29Tecla", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Com uma tocha equipada, abre o menu de cor do fogo (so Aesir). Vazio = sem tecla.");
        }

        private static readonly Dictionary<string, string> Nomes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "roxo", "#B04BFF" }, { "azul", "#3C8CFF" }, { "verde", "#3CFF6E" }, { "vermelho", "#FF3C3C" },
            { "dourado", "#FFC83C" }, { "ciano", "#3CF0FF" }, { "rosa", "#FF6EC7" },
        };

        /// <summary>Texto da cfg -> "#RRGGBB" valido, ou "" (fogo normal).</summary>
        internal static string Normalizar(string texto)
        {
            texto = (texto ?? "").Trim();
            if (texto.Length == 0 || texto.Equals("normal", StringComparison.OrdinalIgnoreCase)) return "";
            if (Nomes.TryGetValue(texto, out string hex)) return hex;
            return TentarCor(texto, out _) ? texto.ToUpperInvariant() : "";
        }

        private static bool TentarCor(string hex, out Color cor)
        {
            cor = Color.white;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length != 7) return false;
            if (!int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            cor = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
            // Nada de branco ofuscante: a luz precisa continuar parecendo fogo, e nao um farol.
            Color.RGBToHSV(cor, out float h, out float s, out float val);
            cor = Color.HSVToRGB(h, Mathf.Max(s, 0.45f), Mathf.Min(val, 1f));
            return true;
        }

        private static string Desejada()
        {
            if (Ativo == null || !Ativo.Value || !VipList.VipListApi.IsLocalPlayerVip()) return "";
            return Normalizar(_sessao ?? Cor?.Value);
        }

        // ---------------------------------------------------------------------- pintura

        private sealed class Estado { public string Aplicada = ""; public float Proximo; }
        private static readonly ConditionalWeakTable<Player, Estado> Estados = new ConditionalWeakTable<Player, Estado>();

        // Cor original de cada luz/particula que ja pintamos, para devolver ao normal.
        private static readonly ConditionalWeakTable<Light, object> LuzOriginal = new ConditionalWeakTable<Light, object>();
        private static readonly ConditionalWeakTable<ParticleSystem, object> ParticulaOriginal = new ConditionalWeakTable<ParticleSystem, object>();

        [HarmonyPatch(typeof(Player), "Update")]
        [HarmonyPostfix]
        private static void PlayerUpdate(Player __instance)
        {
            try
            {
                if (__instance == null) return;
                ZNetView nview = Rede(__instance);
                if (nview == null || !nview.IsValid()) return;
                if (__instance == Player.m_localPlayer && AesirTeclas.Apertou(Tecla)) AbrirMenu(__instance);
                Estado e = Estados.GetOrCreateValue(__instance);
                if (Time.unscaledTime < e.Proximo) return;
                e.Proximo = Time.unscaledTime + Intervalo;

                ZDO zdo = nview.GetZDO();
                if (__instance == Player.m_localPlayer && nview.IsOwner())
                {
                    string d = Desejada();
                    if (zdo.GetString(ZdoChama, "") != d) zdo.Set(ZdoChama, d);
                }

                string alvo = zdo.GetString(ZdoChama, "");
                // Sem cor e nunca pintou: nada a fazer (o caso de quase todo mundo). Com a cor ja
                // aplicada repassa a cada meio segundo, porque trocar de tocha cria luzes novas.
                if (alvo.Length == 0 && e.Aplicada.Length == 0) return;
                Pintar(__instance, alvo);
                e.Aplicada = alvo;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Aesir 29 (fogo): " + ex.Message);
            }
        }

        // ---------------------------------------------------------------------- menu

        private static readonly string[] Opcoes = { "roxo", "azul", "verde", "vermelho", "dourado", "ciano", "rosa", "normal" };
        private static GameObject _menu;
        private static TMP_Text _estado;

        private static bool TochaEquipada(Player p)
        {
            foreach (ItemDrop.ItemData it in new[] { p.GetRightItem(), p.GetLeftItem() })
                if (it?.m_dropPrefab != null && it.m_dropPrefab.name.IndexOf("torch", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void AbrirMenu(Player p)
        {
            if (Ativo == null || !Ativo.Value) return;
            if (_menu != null && _menu.activeSelf) { FecharMenu(); return; }
            if (!VipList.VipListApi.IsLocalPlayerVip()) { p.Message(MessageHud.MessageType.Center, L.T("aesirmsg.f_exclusivo")); return; }
            if (!TochaEquipada(p)) { p.Message(MessageHud.MessageType.Center, L.T("aesirmsg.f_tocha")); return; }
            if (Menu.IsVisible() || InventoryGui.IsVisible() || !ValheimUi.EnsureAssets()) return;
            if (_menu == null) CriarMenu();
            if (_menu == null) return;
            AtualizarEstado();
            _menu.SetActive(true);
            _menu.transform.SetAsLastSibling();
        }

        private static void FecharMenu()
        {
            if (_menu != null) _menu.SetActive(false);
            ValheimUi.ReleaseCursor();
        }

        private static string NomeDe(string c)
        {
            string hex = Normalizar(c);
            if (hex.Length == 0) return "normal";
            foreach (var kv in Nomes) if (kv.Value == hex) return kv.Key;
            return hex;
        }

        private static void AtualizarEstado()
        {
            if (_estado == null) return;
            _estado.text = L.T("aesirmsg.f_estado", NomeDe(_sessao ?? Cor?.Value), NomeDe(Cor?.Value));
        }

        private static void CriarMenu()
        {
            GameObject canvas = ValheimUi.CreateCanvas("Deadheim_Fogo", 440);
            if (canvas == null) return;
            _menu = canvas;
            canvas.AddComponent<SegurarCursor>();

            RectTransform painel = ValheimUi.CreatePanel(canvas.transform, 540f, 420f);
            painel.anchorMin = painel.anchorMax = painel.pivot = new Vector2(0.5f, 0.5f);
            painel.anchoredPosition = Vector2.zero;

            // Barra de titulo arrastavel + X, como as demais janelas do Deadheim (Aesir, Central, Troca).
            var barra = ValheimUi.CreateRect("Titulo", painel);
            ValheimUi.Anchor(barra, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -60f), Vector2.zero);
            barra.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            barra.gameObject.AddComponent<DragWindow>().Target = painel;
            var titulo = ValheimUi.CreateLabel(barra, L.T("misc.titulo"), 36, Menus.MenuHub.Dourado, TextAlignmentOptions.Center, display: true);
            ValheimUi.Stretch((RectTransform)titulo.transform, 60f, 8f);
            var x = ValheimUi.CreateButton(painel, "X", 36f, 36f, 18);
            ValheimUi.Anchor((RectTransform)x.transform, Vector2.one, Vector2.one, new Vector2(-52f, -52f), new Vector2(-16f, -16f));
            x.onClick.AddListener(FecharMenu);

            var lema = ValheimUi.CreateLabel(painel, L.T("aesirmsg.f_lema"),
                15, ValheimUi.Beige, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)lema.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(30f, -92f), new Vector2(-30f, -62f));

            var caixa = ValheimUi.CreateInlay(painel, "Cores");
            ValheimUi.Anchor(caixa, Vector2.zero, Vector2.one, new Vector2(24f, 130f), new Vector2(-24f, -96f));
            _estado = ValheimUi.CreateLabel(caixa, "", 17, ValheimUi.Beige, TextAlignmentOptions.Center);
            ValheimUi.Anchor((RectTransform)_estado.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -62f), new Vector2(-10f, -8f));

            // Quatro botoes por linha dentro da caixa.
            for (int i = 0; i < Opcoes.Length; i++)
            {
                string nome = Opcoes[i];
                Button b = ValheimUi.CreateButton(caixa, char.ToUpper(nome[0]) + nome.Substring(1), 104f, 40f, 18);
                PosBotao(b, (i % 4 - 1.5f) * 114f, -96f - (i / 4) * 50f);
                string escolhida = nome;
                b.onClick.AddListener(() => { _sessao = escolhida; AtualizarEstado(); });
                if (Nomes.TryGetValue(nome, out string hex) && TentarCor(hex, out Color c))
                    b.GetComponentInChildren<TMP_Text>().color = c;
            }

            Button padrao = ValheimUi.CreateButton(painel, L.T("aesirmsg.f_padrao"), 230f, 40f, 16);
            ValheimUi.Anchor((RectTransform)padrao.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-250f, 40f), new Vector2(-20f, 80f));
            padrao.onClick.AddListener(() =>
            {
                if (_sessao != null) Cor.Value = _sessao;
                AtualizarEstado();
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, L.T("aesirmsg.f_salva", NomeDe(Cor.Value)));
            });
            Button fechar = ValheimUi.CreateButton(painel, L.T("misc.fechar"), 170f, 40f, 16);
            ValheimUi.Anchor((RectTransform)fechar.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(20f, 40f), new Vector2(190f, 80f));
            fechar.onClick.AddListener(FecharMenu);
        }

        private static void PosBotao(Button b, float x, float y)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); /* topo-centro da caixa */
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private sealed class SegurarCursor : MonoBehaviour
        {
            private void LateUpdate()
            {
                ValheimUi.HoldCursor();
                if (Input.GetKeyDown(KeyCode.Escape)) FecharMenu();
            }
        }

        private static void Pintar(Player player, string hex)
        {
            bool pintar = TentarCor(hex, out Color cor);
            foreach (Light luz in player.GetComponentsInChildren<Light>(true))
            {
                if (luz.type == LightType.Directional) continue;
                if (pintar)
                {
                    if (!LuzOriginal.TryGetValue(luz, out _)) LuzOriginal.Add(luz, luz.color);
                    luz.color = cor;
                }
                else if (LuzOriginal.TryGetValue(luz, out object o))
                {
                    luz.color = (Color)o;
                    LuzOriginal.Remove(luz);
                }
            }
            foreach (ParticleSystem ps in player.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name.IndexOf("flame", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var main = ps.main;
                if (pintar)
                {
                    if (!ParticulaOriginal.TryGetValue(ps, out _)) ParticulaOriginal.Add(ps, main.startColor);
                    main.startColor = new ParticleSystem.MinMaxGradient(cor);
                }
                else if (ParticulaOriginal.TryGetValue(ps, out object o))
                {
                    main.startColor = (ParticleSystem.MinMaxGradient)o;
                    ParticulaOriginal.Remove(ps);
                }
            }
        }
    }
}
