using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Deadheim.Shared
{
    /// <summary>
    /// A mesma interface das janelas dos NPCs (NpcValheim/UI/ValheimUi.cs, copiada de la): Unity UI
    /// montada com os assets do proprio jogo -- painel de madeira, botao, campo de texto, fontes
    /// Norse/Averia, icones dos itens e os sons de clique. O IMGUI nao alcanca os sprites do atlas
    /// (por isso os blocos brancos de antes). Os nomes dos assets sao os que o Jotunn usa.
    /// Ao mudar algo aqui, veja se vale tambem para a copia do NpcValheim.
    /// </summary>
    internal static class ValheimUi
    {
        public const int UILayer = 5;

        public static readonly Color Orange = new Color(1f, 0.631f, 0.235f, 1f);
        public static readonly Color Beige = new Color(0.8529f, 0.725f, 0.5331f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.889f, 0f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.58f, 0.50f, 1f);
        public static readonly Color Green = new Color(0.49f, 0.99f, 0f, 1f);

        public static readonly ColorBlock ButtonColors = new ColorBlock
        {
            normalColor = new Color(0.824f, 0.824f, 0.824f, 1f),
            highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f),
            pressedColor = new Color(0.537f, 0.556f, 0.556f, 1f),
            selectedColor = new Color(0.824f, 0.824f, 0.824f, 1f),
            disabledColor = new Color(0.566f, 0.566f, 0.566f, 0.502f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        public static readonly ColorBlock ScrollHandleColors = new ColorBlock
        {
            normalColor = new Color(0.926f, 0.645f, 0.34f, 1f),
            highlightedColor = new Color(1f, 0.786f, 0.088f, 1f),
            pressedColor = new Color(0.838f, 0.647f, 0.03f, 1f),
            selectedColor = new Color(1f, 0.786f, 0.088f, 1f),
            disabledColor = new Color(0.784f, 0.784f, 0.784f, 0.502f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        public static TMP_FontAsset FontBody { get; private set; }
        public static TMP_FontAsset FontDisplay { get; private set; }
        public static Sprite PanelSprite { get; private set; }
        public static Sprite ButtonSprite { get; private set; }
        public static Sprite FieldSprite { get; private set; }
        public static Sprite ScrollHandleSprite { get; private set; }
        public static Sprite ScrollBackSprite { get; private set; }
        public static Material PanelMaterial { get; private set; }

        private static GameObject _sfxButton;
        private static GameObject _sfxSelect;
        private static SpriteAtlas _uiAtlas;
        private static SpriteAtlas _iconAtlas;
        private static bool _loaded;

        /// <summary>Acha os assets quando a UI do jogo ja existe. False enquanto o atlas nao carregou: tente no proximo frame.</summary>
        public static bool EnsureAssets()
        {
            if (_loaded) return true;

            _uiAtlas = FindByName<SpriteAtlas>("UIAtlas");
            _iconAtlas = FindByName<SpriteAtlas>("IconAtlas");

            PanelSprite = GetSprite("woodpanel_trophys") ?? GetSprite("woodpanel_settings");
            ButtonSprite = GetSprite("button");
            FieldSprite = GetSprite("text_field");
            ScrollHandleSprite = GetSprite("UISprite");
            ScrollBackSprite = GetSprite("Background");
            PanelMaterial = FindByName<Material>("litpanel");

            FontBody = FindByName<TMP_FontAsset>("Valheim-AveriaSansLibre");
            FontDisplay = FindByName<TMP_FontAsset>("Valheim-Norse") ?? FontBody;

            _sfxButton = FindByName<GameObject>("sfx_gui_button");
            _sfxSelect = FindByName<GameObject>("sfx_gui_select");

            // Painel e fonte nao tem substituto; o resto degrada para uma cor lisa.
            if (PanelSprite == null || FontBody == null) return false;
            _loaded = true;
            return true;
        }

        public static Sprite GetSprite(string spriteName)
        {
            var fromUi = _uiAtlas != null ? _uiAtlas.GetSprite(spriteName) : null;
            if (fromUi != null) return fromUi;
            var fromIcons = _iconAtlas != null ? _iconAtlas.GetSprite(spriteName) : null;
            if (fromIcons != null) return fromIcons;
            return FindByName<Sprite>(spriteName);
        }

        /// <summary>Nome exato: procurar "button" por substring devolve o icone de uma peca de construcao.</summary>
        private static T FindByName<T>(string wanted) where T : Object =>
            Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(o => o != null && string.Equals(o.name, wanted, System.StringComparison.Ordinal));

        // ---------- construcao ----------

        /// <summary>Canvas proprio debaixo do GUI do jogo: fica por cima do HUD sem mexer nos canvases do jogo.</summary>
        public static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var parent = FindGuiRoot();
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = UILayer };
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            go.GetComponent<CanvasScaler>().referencePixelsPerUnit = 50;
            go.transform.SetAsLastSibling();
            return go;
        }

        private static Transform FindGuiRoot()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "GuiRoot") return root.transform.Find("GUI");
                if (root.name == "_GameMain") return root.transform.Find("LoadingGUI");
            }
            return null;
        }

        public static RectTransform CreateRect(string name, Transform parent, bool active = true)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            if (!active) go.SetActive(false);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>A moldura de madeira, fatiada como o jogo fatia.</summary>
        public static RectTransform CreatePanel(Transform parent, float width, float height)
        {
            var rect = CreateRect("Panel", parent);
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            if (PanelMaterial != null) image.material = PanelMaterial;
            image.color = Color.white;
            return rect;
        }

        /// <summary>Caixa escura afundada (fundo escuro + borda do campo de texto), como as do jogo.</summary>
        public static RectTransform CreateInlay(Transform parent, string name = "Inlay")
        {
            var rect = CreateRect(name, parent);
            var fill = rect.gameObject.AddComponent<Image>();
            fill.color = new Color(0f, 0f, 0f, 0.62f);

            if (FieldSprite != null)
            {
                var frame = CreateRect("Frame", rect);
                Stretch(frame, 0f, 0f);
                var border = frame.gameObject.AddComponent<Image>();
                border.sprite = FieldSprite;
                border.type = Image.Type.Sliced;
                border.pixelsPerUnitMultiplier = 1f;
                border.color = new Color(1f, 1f, 1f, 0.85f);
                border.raycastTarget = false;
            }
            return rect;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool display = false)
        {
            // Criado inativo de proposito: o TextMeshProUGUI resolve a fonte no Awake, e sem a
            // nossa ja atribuida cai na LiberationSans (que o Valheim nao tem) e loga aviso.
            var rect = CreateRect("Label", parent, active: false);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = display ? FontDisplay : FontBody;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.text = text ?? "";
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.Normal;
            rect.gameObject.SetActive(true);
            return label;
        }

        public static Button CreateButton(Transform parent, string text, float width, float height, int fontSize = 16)
        {
            var rect = CreateRect("Button", parent);
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ButtonSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;

            var button = rect.gameObject.AddComponent<Button>();
            button.image = image;
            button.colors = ButtonColors;
            AttachSfx(rect.gameObject);

            var label = CreateLabel(rect, text, fontSize, Orange, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 6f, 2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            // Layout group ignora o sizeDelta e pergunta ao LayoutElement; sem ele o botao vira um fio.
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 0f;
            if (height > 0f) { element.preferredHeight = height; element.minHeight = height; }
            if (width > 0f) { element.preferredWidth = width; element.minWidth = width; }
            return button;
        }

        /// <summary>Os sons de clique/hover do jogo (ButtonSfx, do assembly_guiutils).</summary>
        public static void AttachSfx(GameObject go)
        {
            if (_sfxButton == null) return;
            var sfx = go.GetComponent<ButtonSfx>() ?? go.AddComponent<ButtonSfx>();
            sfx.m_sfxPrefab = _sfxButton;
            sfx.m_selectSfxPrefab = _sfxSelect;
        }

        public static TMP_InputField CreateInputField(Transform parent, string value, float width,
            float height, int fontSize = 15)
        {
            // Inativo durante a montagem, pelo mesmo motivo do CreateLabel.
            var rect = CreateRect("InputField", parent, active: false);
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            if (FieldSprite != null)
            {
                image.sprite = FieldSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                image.color = new Color(0f, 0f, 0f, 0.6f);
            }

            var viewport = CreateRect("TextArea", rect);
            Stretch(viewport, 8f, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateLabel(viewport, value ?? "", fontSize, Beige, TextAlignmentOptions.Left);
            Stretch((RectTransform)text.transform, 0f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.text = value ?? "";
            field.fontAsset = FontBody;
            field.pointSize = fontSize;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretColor = Orange;
            field.selectionColor = new Color(1f, 0.631f, 0.235f, 0.35f);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;

            rect.gameObject.SetActive(true);
            return field;
        }

        /// <summary>Coluna com rolagem. Devolve o conteudo, ja com layout vertical: e so pendurar as linhas.</summary>
        public static RectTransform CreateScrollList(Transform parent, float spacing = 4f)
        {
            const float barWidth = 12f;

            var viewport = CreateRect("Viewport", parent);
            // Espaco fixo para a barra: o AutoHideAndExpandViewport refaz a lista ao cruzar o limite.
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-(barWidth + 3f), 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            // RectTransform novo nasce 100x100: deixaria o conteudo 100px mais largo e cortaria o inicio das linhas.
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            AddScrollbar(scroll, viewport, barWidth);
            return content;
        }

        private static void AddScrollbar(ScrollRect scroll, RectTransform viewport, float barWidth)
        {
            var bar = CreateRect("Scrollbar", viewport.parent);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(barWidth, 0f);
            bar.anchoredPosition = Vector2.zero;

            var back = bar.gameObject.AddComponent<Image>();
            back.sprite = ScrollBackSprite;
            back.color = new Color(0f, 0f, 0f, 0.75f);
            back.pixelsPerUnitMultiplier = 1f;

            var slidingArea = CreateRect("SlidingArea", bar);
            Stretch(slidingArea, 0f, 0f);

            // O Scrollbar reescreve as ancoras do handle a cada frame: ele comeca neutro.
            var handle = CreateRect("Handle", slidingArea);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = ScrollHandleSprite;
            handleImage.pixelsPerUnitMultiplier = 1f;

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.transition = Selectable.Transition.ColorTint;
            scrollbar.colors = ScrollHandleColors;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        /// <summary>O icone do item como o inventario desenha.</summary>
        public static Image CreateItemIcon(Transform parent, string prefabName, float size)
        {
            var rect = CreateRect("Icon", parent);
            rect.sizeDelta = new Vector2(size, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.sprite = FindItemIcon(prefabName);
            image.enabled = image.sprite != null;

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            layout.minWidth = size;
            return image;
        }

        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        public static Sprite FindItemIcon(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            if (IconCache.TryGetValue(prefabName, out var cached)) return cached;

            Sprite icon = null;
            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop?.m_itemData?.m_shared?.m_icons != null && drop.m_itemData.m_shared.m_icons.Length > 0)
                icon = drop.m_itemData.m_shared.m_icons[0];

            if (prefab != null) IconCache[prefabName] = icon;
            return icon;
        }

        /// <summary>Icone do item na ponta esquerda do botao, com o texto afastado dele.</summary>
        public static void Iconify(Button button, string prefabName, float iconSize = 32f)
        {
            var icon = CreateItemIcon(button.transform, prefabName, iconSize);
            Anchor((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, -iconSize / 2f), new Vector2(8f + iconSize, iconSize / 2f));

            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            Anchor((RectTransform)label.transform, Vector2.zero, Vector2.one,
                new Vector2(iconSize + 16f, 2f), new Vector2(-8f, -2f));
            label.alignment = TextAlignmentOptions.Left;
        }

        // ---------- layout ----------

        public static void Stretch(RectTransform rect, float horizontalInset, float verticalInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Altura fixa numa lista. flexibleHeight 0: sem isso a linha de 40px virava 190px.</summary>
        public static LayoutElement SetHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            element.flexibleHeight = 0f;
            return element;
        }

        /// <summary>Largura fixa numa linha, que nem estica nem e espremida pelos vizinhos.</summary>
        public static LayoutElement SetWidth(GameObject go, float width)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.flexibleWidth = 0f;
            return element;
        }

        public static LayoutElement Flexible(GameObject go, float weight = 1f)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.flexibleWidth = weight;
            return element;
        }

        /// <summary>Faixa horizontal dentro de uma lista vertical.</summary>
        public static RectTransform Row(Transform parent, float height, float spacing = 8f)
        {
            var row = CreateRect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetHeight(row.gameObject, height);
            return row;
        }

        /// <summary>Faixa horizontal ancorada (fora de lista), com os filhos no centro ou a esquerda.</summary>
        public static RectTransform Strip(Transform parent, TextAnchor alignment, float spacing = 8f)
        {
            var strip = CreateRect("Strip", parent);
            var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = alignment;
            return strip;
        }

        // ---------- cursor ----------

        /// <summary>Solta o cursor para a janela. No LateUpdate, para ser a ultima palavra do frame.</summary>
        public static void HoldCursor()
        {
            if (ZCursor.LockState != CursorLockMode.None) ZCursor.LockState = CursorLockMode.None;
            if (!ZCursor.IsVisible) ZCursor.Show();
            if (!Cursor.visible) Cursor.visible = true;
        }

        /// <summary>
        /// Devolve o cursor a camera, a nao ser que outro menu ou o inventario ainda precise dele.
        /// Janela de NPC aberta tambem conta: o NpcValheim faz o Menu.IsVisible responder sim.
        /// </summary>
        public static void ReleaseCursor()
        {
            if (Menu.IsVisible() || (InventoryGui.instance != null && InventoryGui.IsVisible())) return;
            ZCursor.LockState = CursorLockMode.Locked;
            ZCursor.Hide();
        }
    }

    /// <summary>Arrastar a janela pela barra de titulo.</summary>
    internal sealed class DragWindow : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public RectTransform Target;
        private Vector2 _grabOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)Target.parent, eventData.position, eventData.pressEventCamera, out var local);
            _grabOffset = Target.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Target.parent, eventData.position, eventData.pressEventCamera, out var local))
                Target.anchoredPosition = local + _grabOffset;
        }
    }
}
