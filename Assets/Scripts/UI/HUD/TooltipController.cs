using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TooltipController : MonoBehaviour
{
    [SerializeField] RectTransform tooltipRoot;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] TMP_Text hotkeyText;
    [SerializeField] Vector2 screenOffset = new Vector2(18f, -18f);

    Canvas _canvas;
    Camera _canvasCamera;
    bool _visible;

    void Awake()
    {
        if (tooltipRoot == null)
            tooltipRoot = transform as RectTransform;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
            _canvasCamera = _canvas.worldCamera;

        Hide();
    }

    void Update()
    {
        if (!_visible)
            return;

        Vector2 pointer = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        SetScreenPosition(pointer);
    }

    public void Show(CommandEntry entry, Vector2 screenPosition)
    {
        if (tooltipRoot == null)
            return;

        if (titleText != null)
            titleText.text = entry.Name ?? string.Empty;

        if (bodyText != null)
            bodyText.text = entry.Tooltip ?? string.Empty;

        if (hotkeyText != null)
            hotkeyText.text = entry.HotkeyText ?? string.Empty;

        tooltipRoot.gameObject.SetActive(true);
        _visible = true;

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

        Vector2 target = screenPosition + screenOffset;

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
}
