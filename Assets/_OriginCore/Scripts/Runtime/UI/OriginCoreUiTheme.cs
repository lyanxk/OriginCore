using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OriginCore.UI
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class OriginCoreUiTheme : MonoBehaviour
    {
        private const float RescanInterval = 0.5f;
        private const string UiFontResourcePath = "Fonts/F_NotoSansSC_SDF";

        private static TMP_FontAsset s_uiFont;

        private static readonly Color CanvasBackground =
            new Color(0.91f, 0.95f, 0.985f, 1f);
        private static readonly Color PanelBackground =
            new Color(0.945f, 0.965f, 0.995f, 0.95f);
        private static readonly Color RaisedBackground =
            new Color(0.86f, 0.91f, 0.97f, 0.98f);
        private static readonly Color ControlBackground =
            new Color(0.8f, 0.87f, 0.94f, 0.98f);
        private static readonly Color Cyan =
            new Color(0.04f, 0.63f, 0.82f, 1f);
        private static readonly Color Blue =
            new Color(0.22f, 0.46f, 0.92f, 1f);
        private static readonly Color Green =
            new Color(0.12f, 0.68f, 0.46f, 1f);
        private static readonly Color Amber =
            new Color(0.91f, 0.55f, 0.1f, 1f);
        private static readonly Color Red =
            new Color(0.84f, 0.2f, 0.3f, 1f);
        private static readonly Color Violet =
            new Color(0.56f, 0.3f, 0.88f, 1f);
        private static readonly Color TextPrimary =
            new Color(0.1f, 0.16f, 0.27f, 1f);
        private static readonly Color TextSecondary =
            new Color(0.32f, 0.4f, 0.54f, 1f);
        private static readonly Color BindingRowBackground =
            new Color(0.82f, 0.89f, 0.96f, 0.98f);
        private static readonly Color BindingButtonBackground =
            new Color(0.1f, 0.3f, 0.54f, 1f);
        private static readonly Color TextOnAccent =
            new Color(0.97f, 0.99f, 1f, 1f);
        private static readonly Color Border =
            new Color(0.25f, 0.5f, 0.7f, 0.88f);

        private readonly HashSet<int> _styledObjects = new HashSet<int>();
        private float _nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            OriginCoreUiTheme existing = FindObjectOfType<OriginCoreUiTheme>();
            if (existing != null)
            {
                return;
            }

            GameObject root = new GameObject(nameof(OriginCoreUiTheme));
            DontDestroyOnLoad(root);
            root.AddComponent<OriginCoreUiTheme>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyToLoadedCanvases();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + RescanInterval;
            ApplyToLoadedCanvases();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _styledObjects.Clear();
            ApplyToLoadedCanvases();
        }

        private void ApplyToLoadedCanvases()
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!IsRuntimeSceneObject(canvas))
                {
                    continue;
                }

                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    StyleWorldCanvas(canvas);
                }
                else
                {
                    StyleScreenCanvas(canvas);
                }
            }
        }

        private void StyleScreenCanvas(Canvas canvas)
        {
            canvas.pixelPerfect = true;
            if (!MarkStyled(canvas))
            {
                StyleNewDescendants(canvas);
                return;
            }

            Image canvasImage = canvas.GetComponent<Image>();
            if (canvasImage != null && !IsSemanticGraphic(canvasImage))
            {
                canvasImage.color = CanvasBackground;
            }

            StyleNewDescendants(canvas);
        }

        private void StyleNewDescendants(Canvas canvas)
        {
            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (MarkStyled(images[i]))
                {
                    StyleImage(images[i], canvas);
                }
            }

            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (MarkStyled(texts[i]))
                {
                    StyleText(texts[i]);
                }
            }

            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (MarkStyled(buttons[i]))
                {
                    StyleButton(buttons[i]);
                }
            }

            Toggle[] toggles = canvas.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                if (MarkStyled(toggles[i]))
                {
                    StyleToggle(toggles[i]);
                }
            }

            Slider[] sliders = canvas.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                if (MarkStyled(sliders[i]))
                {
                    StyleSlider(sliders[i]);
                }
            }

            TMP_Dropdown[] dropdowns = canvas.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                if (MarkStyled(dropdowns[i]))
                {
                    StyleDropdown(dropdowns[i]);
                }
            }

            Scrollbar[] scrollbars = canvas.GetComponentsInChildren<Scrollbar>(true);
            for (int i = 0; i < scrollbars.Length; i++)
            {
                if (MarkStyled(scrollbars[i]))
                {
                    StyleScrollbar(scrollbars[i]);
                }
            }

            ScrollRect[] scrollRects = canvas.GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < scrollRects.Length; i++)
            {
                if (MarkStyled(scrollRects[i]))
                {
                    StyleScrollRect(scrollRects[i]);
                }
            }
        }

        private static void StyleImage(Image image, Canvas canvas)
        {
            if (image == null || IsSemanticGraphic(image) ||
                image.GetComponentInParent<RTS.RtsCommandPanelView>() != null)
            {
                return;
            }

            if (image.GetComponent<Settings.InputBindingRowView>() != null)
            {
                image.color = BindingRowBackground;
                EnsureOutline(
                    image.gameObject,
                    new Color(0.22f, 0.48f, 0.7f, 0.9f),
                    new Vector2(1f, -1f));
                return;
            }

            if (image.GetComponent<Button>() != null ||
                image.GetComponent<Toggle>() != null ||
                image.GetComponent<TMP_Dropdown>() != null)
            {
                return;
            }

            string name = image.name;
            RectTransform rect = image.rectTransform;
            bool fullScreen = rect != null && rect.rect.width >= 900f && rect.rect.height >= 500f;
            bool majorSurface = ContainsAny(
                name,
                "Panel", "Page", "Modal", "Dialog", "VisibleRoot", "WeaponPanel",
                "RadialMenu", "ChoicePanel", "LoadoutPanel", "CommanderPanel");
            bool hudSurface = string.Equals(name, "Content", StringComparison.OrdinalIgnoreCase) ||
                              ContainsAny(name, "ResourceHud", "Roster", "SelectionSummary",
                                  "ProductionPanel");

            if (fullScreen)
            {
                image.color = ContainsAny(name, "Options", "MatchSetup", "VisibleRoot")
                    ? new Color(0.91f, 0.94f, 0.985f, 0.96f)
                    : CanvasBackground;
                return;
            }

            if (!majorSurface && !hudSurface)
            {
                return;
            }

            image.color = majorSurface ? PanelBackground : new Color(
                PanelBackground.r, PanelBackground.g, PanelBackground.b, 0.9f);
            EnsureOutline(image.gameObject, Border, new Vector2(1.5f, -1.5f));
            EnsureExactShadow(
                image.gameObject,
                new Color(0.2f, 0.22f, 0.45f, 0.24f),
                new Vector2(-3f, 3f));
            EnsureEdge(image.transform, "ThemePanelEdge", Cyan, 3f, 0.82f);
        }

        private static void StyleText(TMP_Text text)
        {
            ApplyUiFont(text);
            if (text == null || IsProtectedText(text))
            {
                return;
            }

            // The project UI font is generated from the Thin face. Synthetic bold is
            // required at small sizes so narrow glyphs such as uppercase I remain visible.
            text.fontStyle |= FontStyles.Bold;

            string name = text.name;
            bool title = ContainsAny(name, "Title", "Heading") ||
                         text.fontSize >= 28f;
            bool status = ContainsAny(name, "Status", "Subtitle", "Hint", "Description");

            text.color = title ? TextPrimary : status ? TextSecondary : TextPrimary;
            if (title)
            {
                text.fontStyle |= FontStyles.Bold;
                text.enableVertexGradient = true;
                text.colorGradient = new VertexGradient(
                    new Color(0.12f, 0.25f, 0.46f, 1f),
                    new Color(0.12f, 0.25f, 0.46f, 1f),
                    new Color(0.48f, 0.27f, 0.76f, 1f),
                    new Color(0.48f, 0.27f, 0.76f, 1f));
                EnsureExactShadow(
                    text.gameObject,
                    new Color(0.35f, 0.45f, 0.75f, 0.26f),
                    new Vector2(1.5f, -1.5f));
            }
            else if (text.GetComponentInParent<Button>() != null)
            {
                text.fontStyle |= FontStyles.Bold;
            }
        }

        private static void StyleButton(Button button)
        {
            if (button == null ||
                button.GetComponentInParent<RTS.RtsCommandPanelView>() != null)
            {
                return;
            }

            if (button.GetComponentInParent<Settings.InputBindingRowView>() != null)
            {
                StyleBindingButton(button);
                return;
            }

            Color accent = ResolveButtonAccent(button.name);
            Image background = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (background != null && !IsSemanticGraphic(background))
            {
                background.color = Color.Lerp(ControlBackground, accent, 0.12f);
                background.raycastTarget = true;
                EnsureOutline(
                    background.gameObject,
                    Color.Lerp(Border, accent, 0.38f),
                    new Vector2(1f, -1f));
                EnsureEdge(background.transform, "ThemeControlEdge", accent, 3f, 0.9f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.12f);
            colors.pressedColor = Color.Lerp(new Color(0.76f, 0.82f, 0.9f, 1f), accent, 0.24f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.62f, 0.67f, 0.74f, 0.52f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = TextPrimary;
                label.fontStyle |= FontStyles.Bold;
            }
        }

        private static void StyleBindingButton(Button button)
        {
            Image background = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (background != null)
            {
                background.color = BindingButtonBackground;
                background.raycastTarget = true;
                EnsureOutline(
                    background.gameObject,
                    new Color(0.16f, 0.68f, 0.9f, 1f),
                    new Vector2(1f, -1f));
                EnsureEdge(
                    background.transform,
                    "ThemeControlEdge",
                    new Color(0.55f, 0.9f, 1f, 1f),
                    3f,
                    0.95f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.72f, 0.86f, 0.96f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.62f, 0.7f, 0.56f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableVertexGradient = false;
                label.color = TextOnAccent;
                label.fontStyle |= FontStyles.Bold;
            }
        }

        private static void StyleToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            Image background = toggle.targetGraphic as Image;
            if (background != null)
            {
                background.color = ControlBackground;
                EnsureOutline(background.gameObject, Border, new Vector2(1f, -1f));
            }

            if (toggle.graphic is Image checkmark)
            {
                checkmark.color = Cyan;
            }

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.84f, 0.93f, 1f);
            colors.disabledColor = new Color(0.62f, 0.67f, 0.74f, 0.52f);
            toggle.colors = colors;
        }

        private static void StyleSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            Image background = FindNamedImage(slider.transform, "Background");
            if (background != null)
            {
                AlignSliderBackgroundToFillArea(slider, background.rectTransform);
                background.color = new Color(0.68f, 0.76f, 0.84f, 1f);
                EnsureOutline(background.gameObject, Border, new Vector2(1f, -1f));
            }

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();
                if (fill != null)
                {
                    fill.color = Cyan;
                }
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                {
                    handle.color = TextPrimary;
                    EnsureOutline(handle.gameObject, Cyan, new Vector2(1f, -1f));
                }
            }

            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.98f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.86f, 0.94f, 1f);
            colors.disabledColor = new Color(0.62f, 0.67f, 0.74f, 0.5f);
            slider.colors = colors;
        }

        private static void AlignSliderBackgroundToFillArea(
            Slider slider,
            RectTransform background)
        {
            if (slider == null || background == null || slider.fillRect == null)
            {
                return;
            }

            RectTransform fillArea = slider.fillRect.parent as RectTransform;
            RectTransform sliderRect = slider.transform as RectTransform;
            if (fillArea == null || sliderRect == null || fillArea.parent != sliderRect)
            {
                return;
            }

            Vector2 anchorMin = background.anchorMin;
            Vector2 anchorMax = background.anchorMax;
            Vector2 offsetMin = background.offsetMin;
            Vector2 offsetMax = background.offsetMax;
            anchorMin.x = fillArea.anchorMin.x;
            anchorMax.x = fillArea.anchorMax.x;
            offsetMin.x = fillArea.offsetMin.x;
            offsetMax.x = fillArea.offsetMax.x;
            background.anchorMin = anchorMin;
            background.anchorMax = anchorMax;
            background.offsetMin = offsetMin;
            background.offsetMax = offsetMax;
        }

        private static void StyleDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Image background = dropdown.targetGraphic as Image ?? dropdown.GetComponent<Image>();
            if (background != null)
            {
                background.color = RaisedBackground;
                EnsureOutline(background.gameObject, Border, new Vector2(1f, -1f));
                EnsureEdge(background.transform, "ThemeControlEdge", Blue, 2f, 0.8f);
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.color = TextPrimary;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.color = TextPrimary;
            }

            ColorBlock colors = dropdown.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.92f, 1f);
            colors.disabledColor = new Color(0.62f, 0.67f, 0.74f, 0.52f);
            dropdown.colors = colors;
        }

        private static void StyleScrollbar(Scrollbar scrollbar)
        {
            if (scrollbar == null)
            {
                return;
            }

            Image background = scrollbar.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.68f, 0.76f, 0.84f, 0.9f);
            }

            if (scrollbar.targetGraphic is Image handle)
            {
                handle.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.82f);
            }
        }

        private static void StyleScrollRect(ScrollRect scrollRect)
        {
            if (scrollRect == null)
            {
                return;
            }

            Image frame = scrollRect.GetComponent<Image>();
            if (frame != null)
            {
                frame.color = new Color(0.92f, 0.95f, 0.985f, 0.94f);
                EnsureOutline(frame.gameObject, Border, new Vector2(1f, -1f));
            }

            if (scrollRect.viewport != null)
            {
                Image viewport = scrollRect.viewport.GetComponent<Image>();
                if (viewport != null)
                {
                    viewport.color = new Color(0.86f, 0.91f, 0.97f, 0.84f);
                }
            }
        }

        private static void StyleWorldCanvas(Canvas canvas)
        {
            if (canvas == null || !ContainsAny(canvas.name, "Health", "Vitals"))
            {
                return;
            }

            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || ContainsAny(image.name, "HealthFill", "ShieldFill"))
                {
                    continue;
                }

                if (ContainsAny(image.name, "Background", "Frame"))
                {
                    image.color = new Color(0.84f, 0.9f, 0.96f, 0.94f);
                    EnsureOutline(
                        image.gameObject,
                        new Color(0.18f, 0.3f, 0.46f, 0.92f),
                        new Vector2(0.5f, -0.5f));
                }
            }
        }

        private bool MarkStyled(Component component)
        {
            return component != null && _styledObjects.Add(component.GetInstanceID());
        }

        private static bool IsRuntimeSceneObject(Component component)
        {
            if (component == null || component.gameObject == null ||
                !component.gameObject.scene.IsValid())
            {
                return false;
            }

            HideFlags flags = component.gameObject.hideFlags;
            return (flags & HideFlags.HideAndDontSave) == 0 &&
                   (flags & HideFlags.NotEditable) == 0;
        }

        private static bool IsSemanticGraphic(Image image)
        {
            if (image == null)
            {
                return true;
            }

            string name = image.name;
            if (ContainsAny(
                    name,
                    "HealthFill", "ShieldFill", "Fill", "Crosshair", "Reticle", "Dot",
                    "Icon", "Marker", "Fog", "Minimap", "SelectionBox", "Preview",
                    "Portrait", "MapTexture", "LegacyIcon", "Checkmark"))
            {
                return true;
            }

            return image.sprite != null && image.preserveAspect;
        }

        private static bool IsProtectedText(TMP_Text text)
        {
            return text == null || ContainsAny(
                text.name,
                "CrosshairDot", "Health", "Shield", "DamageNumber");
        }

        private static Color ResolveButtonAccent(string name)
        {
            if (ContainsAny(name, "Quit", "Exit", "Delete", "Remove"))
            {
                return Red;
            }

            if (ContainsAny(name, "Cancel", "Back", "NoButton"))
            {
                return Violet;
            }

            if (ContainsAny(
                    name,
                    "Start", "Continue", "Confirm", "Apply", "YesButton", "NewButton"))
            {
                return Green;
            }

            if (ContainsAny(name, "Save", "Load", "Story", "Map"))
            {
                return Blue;
            }

            if (ContainsAny(name, "Weapon", "Hero", "Commander", "Choice"))
            {
                return Amber;
            }

            return Cyan;
        }

        private static Image FindNamedImage(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null && string.Equals(
                        candidate.name,
                        targetName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate.GetComponent<Image>();
                }
            }

            return null;
        }

        private static void EnsureOutline(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.enabled = false;
            EnsureInsetBorder(
                target.transform,
                "ThemeInsetBorder",
                color,
                Mathf.Max(2f, Mathf.Abs(distance.x), Mathf.Abs(distance.y)));
        }

        public static TMP_FontAsset UiFont
        {
            get
            {
                if (s_uiFont == null)
                {
                    s_uiFont = Resources.Load<TMP_FontAsset>(UiFontResourcePath);
                }

                return s_uiFont;
            }
        }

        public static void ApplyUiFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = UiFont;
            if (font != null && text.font != font)
            {
                text.font = font;
            }
        }

        public static void EnsureInsetBorder(
            Transform parent,
            string rootName,
            Color color,
            float thickness)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(rootName);
            GameObject root;
            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject(rootName, typeof(RectTransform));
                root.transform.SetParent(parent, false);
            }

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
            root.transform.SetAsLastSibling();

            float width = Mathf.Max(1f, thickness);
            ConfigureBorderSide(root.transform, "Top", color,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, width));
            ConfigureBorderSide(root.transform, "Bottom", color,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, width));
            ConfigureBorderSide(root.transform, "Left", color,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0.5f), new Vector2(width, 0f));
            ConfigureBorderSide(root.transform, "Right", color,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(width, 0f));
        }

        private static void ConfigureBorderSide(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            GameObject side;
            if (existing != null)
            {
                side = existing.gameObject;
            }
            else
            {
                side = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                side.transform.SetParent(parent, false);
            }

            RectTransform rect = side.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            Image image = side.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void EnsureExactShadow(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Shadow exactShadow = null;
            Shadow[] shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    exactShadow = shadows[i];
                    break;
                }
            }

            if (exactShadow == null)
            {
                exactShadow = target.AddComponent<Shadow>();
            }

            exactShadow.effectColor = color;
            exactShadow.effectDistance = distance;
            exactShadow.useGraphicAlpha = true;
        }

        private static void EnsureEdge(
            Transform parent,
            string edgeName,
            Color color,
            float height,
            float alpha)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(edgeName);
            GameObject edge;
            if (existing != null)
            {
                edge = existing.gameObject;
            }
            else
            {
                edge = new GameObject(
                    edgeName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                edge.transform.SetParent(parent, false);
            }

            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
            Image image = edge.GetComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, alpha);
            image.raycastTarget = false;
            edge.transform.SetAsLastSibling();
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) &&
                    value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
