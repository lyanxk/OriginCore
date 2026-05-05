using UnityEngine;
using UnityEngine.UI;

namespace UI.HUD
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CrosshairDotGraphic : MaskableGraphic
    {
        [SerializeField, Min(3)] int segments = 32;

        public int Segments
        {
            get => segments;
            set
            {
                segments = Mathf.Max(3, value);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
                return;

            int segmentCount = Mathf.Max(3, segments);
            Vector2 center = rect.center;
            Color32 vertexColor = color;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = vertexColor;
            vertex.position = center;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vh.AddVert(vertex);

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / segmentCount;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                vertex.position = center + new Vector2(x, y) * radius;
                vertex.uv0 = new Vector2((x + 1f) * 0.5f, (y + 1f) * 0.5f);
                vh.AddVert(vertex);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int current = i + 1;
                int next = i == segmentCount - 1 ? 1 : current + 1;
                vh.AddTriangle(0, current, next);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            segments = Mathf.Max(3, segments);
            SetVerticesDirty();
        }
#endif
    }
}
