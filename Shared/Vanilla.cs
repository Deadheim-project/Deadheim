// Substitui o Jotunn nos mods proprios do Deadheim usando so o que o Valheim ja tem.
//
// Por que existe: com o Jotunn como dependencia dura, cada atualizacao do jogo
// travava os nossos mods ate sair uma versao nova dele (no 1.0 o 2.30.0 quebrou em
// cinco pontos). Tudo que usavamos dele cabe aqui: registro de prefabs clonados,
// pecas no martelo, criaturas clonadas, checagem de admin e os construtores de UI.
//
// Este arquivo e compilado dentro de cada mod (Deadheim, RaidSystem e, por copia,
// DonationShop). Os tipos sao internal de proposito: cada assembly tem a sua copia,
// com o seu proprio registro e os seus proprios patches, e elas nao se enxergam.
//
// C# 9 no maximo: o Deadheim.csproj ainda compila com LangVersion 9.0.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Deadheim.Vanilla
{
    /// <summary>
    /// Prefabs clonados de prefabs do jogo. No lugar do PrefabManager do Jotunn.
    /// </summary>
    internal static class Prefabs
    {
        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<int, GameObject>> NamedPrefabs =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<int, GameObject>>("m_namedPrefabs");

        private static readonly Dictionary<string, GameObject> Clones = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static GameObject _container;
        private static int _piecesRaisedForObjectDb;

        /// <summary>
        /// Depois de cada ZNetScene.Awake (menu e mundo). E o momento em que o Jotunn
        /// disparava OnPrefabsRegistered e OnVanillaCreaturesAvailable: os prefabs do
        /// jogo ja estao no dicionario e da para clonar.
        /// </summary>
        public static event Action ZNetSceneReady;

        /// <summary>
        /// Quando ZNetScene e ObjectDB estao prontos, uma vez por ObjectDB. No lugar do
        /// OnPiecesRegistered do Jotunn: os itens (o martelo inclusive) ja existem.
        /// </summary>
        public static event Action PiecesReady;

        /// <summary>Clone registrado aqui, prefab da cena ou item do ObjectDB, nessa ordem.</summary>
        public static GameObject Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Clones.TryGetValue(name, out GameObject clone) && clone) return clone;

            ZNetScene scene = ZNetScene.instance;
            if (scene)
            {
                GameObject fromScene = scene.GetPrefab(name);
                if (fromScene) return fromScene;
            }

            ObjectDB db = ObjectDB.instance;
            return db ? db.GetItemPrefab(name) : null;
        }

        /// <summary>
        /// Copia <paramref name="sourceName"/> com o nome <paramref name="name"/>. Chamar
        /// de novo com o mesmo nome devolve o clone existente em vez de duplicar.
        /// </summary>
        public static GameObject Clone(string name, string sourceName)
        {
            if (Clones.TryGetValue(name, out GameObject existing) && existing) return existing;

            GameObject source = Get(sourceName);
            if (!source)
            {
                Debug.LogWarning($"[{Owner}] Nao foi possivel clonar '{name}': prefab de origem '{sourceName}' nao existe.");
                return null;
            }

            // Filho de um objeto inativo: o Awake dos componentes nao roda no clone
            // guardado, so nas instancias que o jogo criar a partir dele.
            GameObject copy = UnityEngine.Object.Instantiate(source, Container.transform, false);
            copy.name = name;
            Clones[name] = copy;
            AddToScene(ZNetScene.instance, copy, updateNamed: true);
            return copy;
        }

        private static string Owner => typeof(Prefabs).Assembly.GetName().Name;

        private static GameObject Container
        {
            get
            {
                if (_container) return _container;
                _container = new GameObject("VanillaPrefabs." + Owner);
                _container.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_container);
                return _container;
            }
        }

        private static void AddToScene(ZNetScene scene, GameObject prefab, bool updateNamed)
        {
            if (!scene || !prefab) return;

            List<GameObject> list = prefab.GetComponent<ZNetView>() ? scene.m_prefabs : scene.m_nonNetViewPrefabs;
            if (!list.Contains(prefab)) list.Add(prefab);

            if (!updateNamed) return;
            Dictionary<int, GameObject> named = NamedPrefabs(scene);
            if (named != null) named[prefab.name.GetStableHashCode()] = prefab;
        }

        private static void TryRaisePiecesReady()
        {
            ObjectDB db = ObjectDB.instance;
            if (!ZNetScene.instance || !db || db.m_items == null || db.m_items.Count == 0) return;

            int id = db.GetInstanceID();
            if (_piecesRaisedForObjectDb == id) return;
            _piecesRaisedForObjectDb = id;

            Raise(PiecesReady, nameof(PiecesReady));
            Pieces.ReapplyAll();
        }

        private static void Raise(Action handlers, string eventName)
        {
            if (handlers == null) return;
            // Um assinante que estoura nao pode impedir os outros: e o que o Jotunn
            // tambem garantia, e sem isso uma peca quebrada levava as demais junto.
            foreach (Action handler in handlers.GetInvocationList().Cast<Action>())
            {
                try { handler(); }
                catch (Exception error) { Debug.LogError($"[{Owner}] {eventName}: {error}"); }
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class ZNetSceneAwakePatch
        {
            // Antes do Awake: os clones entram na lista e o proprio jogo monta o dicionario.
            private static void Prefix(ZNetScene __instance)
            {
                foreach (GameObject clone in Clones.Values) AddToScene(__instance, clone, updateNamed: false);
            }

            private static void Postfix(ZNetScene __instance)
            {
                foreach (GameObject clone in Clones.Values) AddToScene(__instance, clone, updateNamed: true);
                Raise(ZNetSceneReady, nameof(ZNetSceneReady));
                TryRaisePiecesReady();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static class ObjectDbAwakePatch
        {
            private static void Postfix() => TryRaisePiecesReady();
        }
    }

    /// <summary>Pecas nas tabelas de construcao. No lugar do PieceManager do Jotunn.</summary>
    internal static class Pieces
    {
        private static readonly List<KeyValuePair<GameObject, string>> Registered = new List<KeyValuePair<GameObject, string>>();

        public static void AddToHammer(GameObject piece, string category) => AddToTable(piece, "Hammer", category);

        /// <summary>
        /// Poe a peca na tabela da ferramenta <paramref name="toolItem"/> na categoria
        /// <paramref name="category"/> (nome de Piece.PieceCategory, como no Jotunn).
        /// Fica registrado e e reaplicado a cada ObjectDB novo.
        /// </summary>
        public static void AddToTable(GameObject piece, string toolItem, string category)
        {
            if (!piece) return;

            Piece component = piece.GetComponent<Piece>();
            if (component && !string.IsNullOrEmpty(category))
            {
                if (Enum.TryParse(category, true, out Piece.PieceCategory parsed))
                    component.m_category = parsed;
                else
                    Debug.LogWarning($"[{piece.name}] Categoria de peca desconhecida: '{category}'.");
            }

            if (!Registered.Any(entry => entry.Key == piece && entry.Value == toolItem))
                Registered.Add(new KeyValuePair<GameObject, string>(piece, toolItem));

            Apply(piece, toolItem);
        }

        internal static void ReapplyAll()
        {
            Registered.RemoveAll(entry => !entry.Key);
            foreach (KeyValuePair<GameObject, string> entry in Registered) Apply(entry.Key, entry.Value);
        }

        private static void Apply(GameObject piece, string toolItem)
        {
            GameObject tool = Prefabs.Get(toolItem);
            ItemDrop drop = tool ? tool.GetComponent<ItemDrop>() : null;
            PieceTable table = drop != null && drop.m_itemData?.m_shared != null ? drop.m_itemData.m_shared.m_buildPieces : null;
            if (!table) return;

            if (!table.m_pieces.Contains(piece)) table.m_pieces.Add(piece);
        }
    }

    /// <summary>Criaturas clonadas. No lugar do CreatureManager/CustomCreature do Jotunn.</summary>
    internal static class Creatures
    {
        /// <summary>Um drop como o DropConfig do Jotunn: chance em porcentagem, 0 a 100.</summary>
        internal sealed class Drop
        {
            public string Item;
            public float Chance = 100f;
            public int MinAmount = 1;
            public int MaxAmount = 1;
            public bool OnePerPlayer;
            public bool LevelMultiplier = true;
        }

        public static GameObject Clone(string name, string sourceName, Character.Faction? faction = null, params Drop[] drops)
        {
            GameObject prefab = Prefabs.Clone(name, sourceName);
            if (!prefab) return null;

            if (faction.HasValue)
            {
                Character character = prefab.GetComponent<Character>();
                if (character) character.m_faction = faction.Value;
            }

            if (drops != null && drops.Length > 0)
            {
                CharacterDrop characterDrop = prefab.GetComponent<CharacterDrop>();
                if (!characterDrop) characterDrop = prefab.AddComponent<CharacterDrop>();
                characterDrop.m_drops = drops
                    .Select(ToVanilla)
                    .Where(drop => drop != null)
                    .ToList();
            }

            return prefab;
        }

        private static CharacterDrop.Drop ToVanilla(Drop drop)
        {
            GameObject item = Prefabs.Get(drop.Item);
            if (!item)
            {
                Debug.LogWarning($"[Vanilla.Creatures] Drop ignorado: item '{drop.Item}' nao existe.");
                return null;
            }

            return new CharacterDrop.Drop
            {
                m_prefab = item,
                m_chance = drop.Chance / 100f,
                m_amountMin = drop.MinAmount,
                m_amountMax = drop.MaxAmount,
                m_onePerPlayer = drop.OnePerPlayer,
                m_levelMultiplier = drop.LevelMultiplier
            };
        }
    }

    /// <summary>
    /// Admin segundo o proprio servidor. O Valheim ja manda a adminlist.txt para cada
    /// cliente; e a mesma fonte que o SynchronizationManager do Jotunn usava.
    /// </summary>
    internal static class Admin
    {
        public static bool LocalPlayerIsAdmin()
        {
            ZNet net = ZNet.instance;
            if (!net) return false;
            // Como o Jotunn: o proprio servidor (dedicado ou host) conta como admin.
            if (net.IsServer()) return true;
            return net.LocalPlayerIsAdminOrHost();
        }
    }

    /// <summary>
    /// Construtores de UI no estilo do Valheim. Mesmos nomes de parametro do
    /// GUIManager do Jotunn, para os menus existentes mudarem so o prefixo.
    /// </summary>
    internal static class Ui
    {
        public static readonly Color ValheimOrange = new Color(1f, 0.631f, 0.235f, 1f);

        public static ColorBlock ValheimScrollbarHandleColorBlock => new ColorBlock
        {
            normalColor = new Color(0.926f, 0.645f, 0.34f, 1f),
            highlightedColor = new Color(1f, 0.786f, 0.088f, 1f),
            pressedColor = new Color(0.838f, 0.647f, 0.031f, 1f),
            selectedColor = new Color(1f, 0.786f, 0.088f, 1f),
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };

        private static readonly Color PanelFallback = new Color(0.24f, 0.16f, 0.09f, 0.97f);
        private static readonly Color ButtonFallback = new Color(0.33f, 0.22f, 0.12f, 1f);
        private static readonly Color FieldFallback = new Color(0.1f, 0.07f, 0.05f, 0.9f);

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static Font _averiaSerifBold;
        private static GameObject _front;

        public static Font AveriaSerifBold
        {
            get
            {
                if (_averiaSerifBold) return _averiaSerifBold;
                _averiaSerifBold = Resources.FindObjectsOfTypeAll<Font>()
                    .FirstOrDefault(font => font.name == "AveriaSerifLibre-Bold");
                if (!_averiaSerifBold)
                {
                    // Sem a fonte do jogo, a embutida da Unity: texto feio, mas legivel.
                    try { _averiaSerifBold = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (Exception) { _averiaSerifBold = null; }
                }
                return _averiaSerifBold;
            }
        }

        /// <summary>
        /// Camada de UI acima do HUD. No lugar do GUIManager.CustomGUIFront. Pendurada
        /// no canvas do proprio jogo, entao herda a escala de interface do jogador, e
        /// some com ele no logout: quem guarda um menu criado aqui recebe null e recria.
        /// </summary>
        public static GameObject Front
        {
            get
            {
                if (_front) return _front;

                Canvas root = InventoryGui.instance ? InventoryGui.instance.GetComponentInParent<Canvas>() : null;
                root = root ? root.rootCanvas : null;
                if (!root) root = CreateFallbackCanvas();

                _front = new GameObject("VanillaGUIFront." + typeof(Ui).Assembly.GetName().Name, typeof(RectTransform));
                _front.transform.SetParent(root.transform, false);
                Stretch((RectTransform)_front.transform);

                Canvas canvas = _front.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = root.sortingOrder + 10;
                _front.AddComponent<GraphicRaycaster>();
                _front.transform.SetAsLastSibling();
                return _front;
            }
        }

        public static GameObject CreateWoodpanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            float width = 0f, float height = 0f, bool draggable = true)
        {
            GameObject panel = NewUiObject("Woodpanel", parent, anchorMin, anchorMax, position, width, height);
            Image image = panel.AddComponent<Image>();
            ApplySprite(image, "woodpanel_trophys", PanelFallback);
            if (draggable) panel.AddComponent<DragWindow>();
            return panel;
        }

        public static GameObject CreateText(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            Font font, int fontSize, Color color, bool outline, Color outlineColor, float width, float height, bool addContentSizeFitter)
        {
            GameObject go = NewUiObject("Text", parent, anchorMin, anchorMax, position, width, height);
            Text label = go.AddComponent<Text>();
            label.font = font ? font : AveriaSerifBold;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;

            Outline effect = go.AddComponent<Outline>();
            effect.effectColor = outlineColor;
            effect.effectDistance = new Vector2(1f, -1f);
            effect.enabled = outline;

            if (addContentSizeFitter)
            {
                ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            return go;
        }

        public static GameObject CreateButton(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            float width = 0f, float height = 0f)
        {
            GameObject go = NewUiObject("Button", parent, anchorMin, anchorMax, position, width, height);
            Image image = go.AddComponent<Image>();
            ApplySprite(image, "button", ButtonFallback);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f, 1f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.55f, 1f);
            button.colors = colors;

            GameObject label = CreateText(text, go.transform, Vector2.zero, Vector2.one, Vector2.zero,
                AveriaSerifBold, 16, ValheimOrange, true, Color.black, 0f, 0f, false);
            Stretch((RectTransform)label.transform);
            label.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            return go;
        }

        public static GameObject CreateInputField(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            InputField.ContentType contentType = InputField.ContentType.Standard, string placeholderText = "",
            int fontSize = 16, float width = 0f, float height = 0f)
        {
            GameObject go = NewUiObject("InputField", parent, anchorMin, anchorMax, position, width, height);
            Image image = go.AddComponent<Image>();
            ApplySprite(image, "text_field", FieldFallback);

            Text placeholder = InputText(go.transform, "Placeholder", fontSize, new Color(0.6f, 0.6f, 0.6f, 0.8f));
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = placeholderText;

            Text text = InputText(go.transform, "Text", fontSize, Color.white);
            text.supportRichText = false;

            InputField input = go.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = contentType;
            return go;
        }

        /// <summary>
        /// Mesma arvore do Jotunn: raiz / "Scroll View" / "Viewport" / "Content", com o
        /// Content empilhando os filhos de cima para baixo. Os menus buscam o Content
        /// por esse caminho.
        /// </summary>
        public static GameObject CreateScrollView(Transform parent, bool showHorizontalScrollbar, bool showVerticalScrollbar,
            float handleSize, ColorBlock handleColors, float handleDistanceToBorder, Color slidingAreaBackgroundColor,
            float width, float height)
        {
            Vector2 center = new Vector2(0.5f, 0.5f);
            GameObject root = NewUiObject("ScrollView", parent, center, center, Vector2.zero, width, height);

            GameObject scrollObject = NewUiObject("Scroll View", root.transform, Vector2.zero, Vector2.one, Vector2.zero, 0f, 0f);
            Stretch((RectTransform)scrollObject.transform);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.horizontal = showHorizontalScrollbar;
            scroll.vertical = true;

            float barWidth = showVerticalScrollbar ? handleSize : 0f;

            GameObject viewport = NewUiObject("Viewport", scrollObject.transform, Vector2.zero, Vector2.one, Vector2.zero, 0f, 0f);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            Stretch(viewportRect);
            viewportRect.offsetMax = new Vector2(-barWidth, 0f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            if (showVerticalScrollbar)
            {
                // handleDistanceToBorder existe so para manter a assinatura do Jotunn:
                // a barra fica colada na borda direita da area de rolagem.
                GameObject bar = NewUiObject("Scrollbar Vertical", scrollObject.transform,
                    new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, handleSize, 0f);
                RectTransform barRect = (RectTransform)bar.transform;
                barRect.pivot = new Vector2(1f, 0.5f);
                barRect.anchoredPosition = Vector2.zero;
                barRect.sizeDelta = new Vector2(handleSize, 0f);
                bar.AddComponent<Image>().color = slidingAreaBackgroundColor;

                GameObject area = NewUiObject("Sliding Area", bar.transform, Vector2.zero, Vector2.one, Vector2.zero, 0f, 0f);
                Stretch((RectTransform)area.transform);
                GameObject handle = NewUiObject("Handle", area.transform, Vector2.zero, Vector2.one, Vector2.zero, 0f, 0f);
                Stretch((RectTransform)handle.transform);
                Image handleImage = handle.AddComponent<Image>();

                Scrollbar scrollbar = bar.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect = (RectTransform)handle.transform;
                scrollbar.targetGraphic = handleImage;
                scrollbar.colors = handleColors;

                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            return root;
        }

        private static GameObject NewUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return go;
        }

        private static Text InputText(Transform parent, string name, int fontSize, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);

            Text text = go.AddComponent<Text>();
            text.font = AveriaSerifBold;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplySprite(Image image, string spriteName, Color fallback)
        {
            Sprite sprite = FindSprite(spriteName);
            if (sprite)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.color = fallback;
            }
        }

        private static Sprite FindSprite(string name)
        {
            if (SpriteCache.TryGetValue(name, out Sprite cached) && cached) return cached;
            Sprite found = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite => sprite.name == name);
            if (found) SpriteCache[name] = found;
            return found;
        }

        private static Canvas CreateFallbackCanvas()
        {
            GameObject go = new GameObject("VanillaGUICanvas." + typeof(Ui).Assembly.GetName().Name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private sealed class DragWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
        {
            private RectTransform _rect;
            private Canvas _canvas;

            public void OnBeginDrag(PointerEventData eventData)
            {
                _rect = (RectTransform)transform;
                _canvas = GetComponentInParent<Canvas>();
                transform.SetAsLastSibling();
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (!_rect) return;
                float scale = _canvas ? _canvas.scaleFactor : 1f;
                _rect.anchoredPosition += eventData.delta / (scale > 0f ? scale : 1f);
            }
        }
    }
}
