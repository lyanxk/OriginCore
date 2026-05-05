using UnityEngine;

namespace UI.HUD
{
    [ExecuteAlways]
    public class CrosshairReticle : MonoBehaviour
    {
        [SerializeField] CrosshairDotGraphic dotGraphic;
        [SerializeField] Color dotColor = Color.white;
        [SerializeField, Min(1f)] float dotSize = 6f;

        public Color DotColor => dotColor;
        public float DotSize => dotSize;

        void Reset()
        {
            dotGraphic = GetComponentInChildren<CrosshairDotGraphic>(true);
            Apply();
        }

        void Awake()
        {
            Apply();
        }

        void OnEnable()
        {
            Apply();
        }

        public void Configure(CrosshairDotGraphic graphic, Color color, float size)
        {
            dotGraphic = graphic;
            dotColor = color;
            dotSize = Mathf.Max(1f, size);
            Apply();
        }

        public void SetColor(Color color)
        {
            dotColor = color;
            Apply();
        }

        public void SetSize(float size)
        {
            dotSize = Mathf.Max(1f, size);
            Apply();
        }

        void Apply()
        {
            if (dotGraphic == null)
                dotGraphic = GetComponentInChildren<CrosshairDotGraphic>(true);

            if (dotGraphic == null)
                return;

            dotGraphic.color = dotColor;

            RectTransform rectTransform = dotGraphic.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            dotSize = Mathf.Max(1f, dotSize);
            Apply();
        }
#endif
    }
}
