using TMPro;
using Unit.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI.HUD
{
    public class TooltipController : MonoBehaviour
    {
        [SerializeField] RectTransform tooltipRoot;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] TMP_Text hotkeyText;
        [SerializeField] Vector2 screenOffset = new Vector2(18f, -18f);
        [SerializeField] Vector2 defaultMaxSize = new Vector2(320f, 240f);

        Canvas _canvas;
        UnityEngine.Camera _canvasCamera;
        bool _visible;

        void Awake()
        {
            EnsureView();
            CacheCanvas();
            Hide();
        }

        void Update()
        {
            if (!_visible)
                return;

            Vector2 pointer = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)UnityEngine.Input.mousePosition;

            SetScreenPosition(pointer);
        }

        public void Show(CommandEntry entry, Vector2 screenPosition)
        {
            if (tooltipRoot == null)
                return;

            SetText(titleText, entry.Name);
            SetText(bodyText, BuildBodyWithHotkey(entry.Tooltip, entry.HotkeyText));
            SetText(hotkeyText, string.Empty);

            tooltipRoot.gameObject.SetActive(true);
            _visible = true;

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
            SetScreenPosition(screenPosition);
        }

        public void Hide()
        {
            _visible = false;

            if (tooltipRoot != null)
                tooltipRoot.gameObject.SetActive(false);
        }

        void SetScreenPosition(Vector2 screenPosition)
        {
            if (tooltipRoot == null)
                return;

            Vector2 target = ClampToScreen(screenPosition + screenOffset);

            if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                tooltipRoot.position = target;
                return;
            }

            RectTransform canvasRect = _canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                tooltipRoot.position = target;
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, target, _canvasCamera, out Vector2 local))
                tooltipRoot.anchoredPosition = local;
        }

        void EnsureView()
        {
            if (tooltipRoot == null)
                tooltipRoot = transform as RectTransform;

            if (tooltipRoot == null)
                return;

            bool needsDefaultLayout = titleText == null && bodyText == null && hotkeyText == null;
            if (!needsDefaultLayout)
                return;

            tooltipRoot.pivot = new Vector2(0f, 1f);

            Image background = tooltipRoot.GetComponent<Image>();
            if (background == null)
                background = tooltipRoot.gameObject.AddComponent<Image>();

            background.color = new Color(0.03f, 0.035f, 0.045f, 0.94f);
            background.raycastTarget = false;

            VerticalLayoutGroup layout = tooltipRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = tooltipRoot.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = tooltipRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = tooltipRoot.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement layoutElement = tooltipRoot.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = tooltipRoot.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredWidth = defaultMaxSize.x;
            layoutElement.flexibleWidth = 0f;

            titleText = CreateDefaultText("TitleText", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            bodyText = CreateDefaultText("BodyText", 15f, FontStyles.Normal, TextAlignmentOptions.Left);
            hotkeyText = CreateDefaultText("HotkeyText", 13f, FontStyles.Normal, TextAlignmentOptions.Right);
        }

        TMP_Text CreateDefaultText(string objectName, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(tooltipRoot, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;

            LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = Mathf.Max(1f, defaultMaxSize.x - 24f);

            return text;
        }

        void CacheCanvas()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasCamera = _canvas.worldCamera;
        }

        static void SetText(TMP_Text text, string value)
        {
            if (text == null)
                return;

            string resolved = value ?? string.Empty;
            text.text = resolved;
            text.gameObject.SetActive(!string.IsNullOrWhiteSpace(resolved));
        }

        static string BuildBodyWithHotkey(string body, string hotkey)
        {
            string resolvedBody = body ?? string.Empty;
            if (string.IsNullOrWhiteSpace(hotkey))
                return resolvedBody;

            string hotkeyHint = $"({hotkey})";
            if (string.IsNullOrWhiteSpace(resolvedBody))
                return hotkeyHint;

            return $"{resolvedBody}\n{hotkeyHint}";
        }

        Vector2 ClampToScreen(Vector2 screenPosition)
        {
            Vector2 size = tooltipRoot.rect.size;
            Vector2 pivot = tooltipRoot.pivot;
            const float margin = 8f;

            float minX = margin + size.x * pivot.x;
            float maxX = Screen.width - margin - size.x * (1f - pivot.x);
            float minY = margin + size.y * pivot.y;
            float maxY = Screen.height - margin - size.y * (1f - pivot.y);

            if (maxX < minX)
                screenPosition.x = Screen.width * 0.5f;
            else
                screenPosition.x = Mathf.Clamp(screenPosition.x, minX, maxX);

            if (maxY < minY)
                screenPosition.y = Screen.height * 0.5f;
            else
                screenPosition.y = Mathf.Clamp(screenPosition.y, minY, maxY);

            return screenPosition;
        }
    }
}
