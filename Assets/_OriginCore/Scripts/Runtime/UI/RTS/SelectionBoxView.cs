using OriginCore.RTS;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SelectionBoxView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _boxRect;
        [SerializeField] private Graphic _boxGraphic;

        public Canvas Canvas => _canvas;
        public RectTransform BoxRect => _boxRect;
        public Graphic BoxGraphic => _boxGraphic;
        public bool IsVisible => _boxRect != null && _boxRect.gameObject.activeSelf;
        public Rect ScreenRect { get; private set; }
        public Rect LocalRect { get; private set; }

        private void OnDisable()
        {
            Hide();
        }

        public void Configure(
            Canvas canvas,
            RectTransform boxRect,
            Graphic boxGraphic)
        {
            _canvas = canvas;
            _boxRect = boxRect;
            _boxGraphic = boxGraphic;
            if (_boxGraphic != null)
            {
                _boxGraphic.raycastTarget = false;
            }

            Hide();
        }

        public bool Show(Rect screenRect)
        {
            if (_canvas == null || _boxRect == null)
            {
                return false;
            }

            RectTransform canvasRect = _canvas.transform as RectTransform;
            Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            if (!SelectionBoxCalculator.TryConvertToLocalRect(
                    screenRect,
                    canvasRect,
                    eventCamera,
                    out Rect localRect))
            {
                Hide();
                return false;
            }

            ScreenRect = screenRect;
            LocalRect = localRect;
            _boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            _boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            _boxRect.pivot = new Vector2(0.5f, 0.5f);
            _boxRect.anchoredPosition = localRect.center;
            _boxRect.sizeDelta = localRect.size;
            if (!_boxRect.gameObject.activeSelf)
            {
                _boxRect.gameObject.SetActive(true);
            }

            return true;
        }

        public void Hide()
        {
            ScreenRect = default(Rect);
            LocalRect = default(Rect);
            if (_boxRect != null && _boxRect.gameObject.activeSelf)
            {
                _boxRect.gameObject.SetActive(false);
            }
        }
    }
}
