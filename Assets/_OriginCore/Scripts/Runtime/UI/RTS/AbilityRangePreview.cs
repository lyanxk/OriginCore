using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class AbilityRangePreview : MonoBehaviour
    {
        private const string OverlayShaderName = "OriginCore/Command Queue Overlay";
        private const int DashCount = 48;
        private const float DashFill = 0.62f;
        private const float GroundOffset = 0.06f;
        private const float MinimumEffectRadius = 0.65f;

        private static readonly Color CastRangeColor =
            new Color(0.24f, 0.78f, 1f, 0.82f);
        private static readonly Color ValidEffectColor =
            new Color(1f, 0.86f, 0.34f, 0.92f);
        private static readonly Color InvalidEffectColor =
            new Color(1f, 0.22f, 0.18f, 0.94f);

        private Transform _castRangeTransform;
        private Transform _effectRangeTransform;
        private MeshRenderer _castRangeRenderer;
        private MeshRenderer _effectRangeRenderer;
        private Mesh _ringMesh;
        private Material _castRangeMaterial;
        private Material _effectRangeMaterial;

        public bool IsVisible =>
            (_castRangeRenderer != null && _castRangeRenderer.enabled) ||
            (_effectRangeRenderer != null && _effectRangeRenderer.enabled);
        public float CastRange { get; private set; }
        public float EffectRadius { get; private set; }
        public bool HasTargetPoint { get; private set; }
        public bool IsTargetValid { get; private set; }

        private void Awake()
        {
            EnsureVisuals();
            Hide();
        }

        private void OnDestroy()
        {
            if (_castRangeMaterial != null)
            {
                Destroy(_castRangeMaterial);
            }

            if (_effectRangeMaterial != null)
            {
                Destroy(_effectRangeMaterial);
            }

            if (_ringMesh != null)
            {
                Destroy(_ringMesh);
            }
        }

        public void Show(
            Vector3 casterPosition,
            Vector3 targetPosition,
            float castRange,
            float effectRadius,
            bool hasTargetPoint,
            bool targetValid)
        {
            EnsureVisuals();
            CastRange = Mathf.Max(0f, castRange);
            EffectRadius = Mathf.Max(0f, effectRadius);
            HasTargetPoint = hasTargetPoint;
            IsTargetValid = targetValid;

            bool showCastRange = CastRange > 0.05f;
            if (_castRangeRenderer != null)
            {
                _castRangeRenderer.enabled = showCastRange;
            }

            if (showCastRange && _castRangeTransform != null)
            {
                _castRangeTransform.position = Flatten(casterPosition);
                _castRangeTransform.localScale = Vector3.one * CastRange;
            }

            if (_effectRangeRenderer != null)
            {
                _effectRangeRenderer.enabled = hasTargetPoint;
            }

            if (hasTargetPoint && _effectRangeTransform != null)
            {
                _effectRangeTransform.position = Flatten(targetPosition);
                _effectRangeTransform.localScale = Vector3.one * Mathf.Max(
                    MinimumEffectRadius,
                    EffectRadius);
                SetMaterialColor(
                    _effectRangeMaterial,
                    targetValid ? ValidEffectColor : InvalidEffectColor);
            }
        }

        public void Hide()
        {
            CastRange = 0f;
            EffectRadius = 0f;
            HasTargetPoint = false;
            IsTargetValid = false;
            if (_castRangeRenderer != null)
            {
                _castRangeRenderer.enabled = false;
            }

            if (_effectRangeRenderer != null)
            {
                _effectRangeRenderer.enabled = false;
            }
        }

        private void EnsureVisuals()
        {
            if (_ringMesh == null)
            {
                _ringMesh = BuildDashedRingMesh();
            }

            if (_castRangeTransform == null)
            {
                _castRangeTransform = CreateRing(
                    "CastRange",
                    CastRangeColor,
                    out _castRangeRenderer,
                    out _castRangeMaterial);
            }

            if (_effectRangeTransform == null)
            {
                _effectRangeTransform = CreateRing(
                    "EffectRange",
                    ValidEffectColor,
                    out _effectRangeRenderer,
                    out _effectRangeMaterial);
            }
        }

        private Transform CreateRing(
            string objectName,
            Color color,
            out MeshRenderer meshRenderer,
            out Material material)
        {
            GameObject ring = new GameObject(objectName);
            ring.transform.SetParent(transform, false);
            MeshFilter meshFilter = ring.AddComponent<MeshFilter>();
            meshRenderer = ring.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = _ringMesh;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.allowOcclusionWhenDynamic = false;
            meshRenderer.sortingOrder = short.MaxValue;

            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            material = shader != null
                ? new Material(shader)
                {
                    name = "Runtime_AbilityRange_" + objectName,
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = 5000,
                    mainTexture = Texture2D.whiteTexture
                }
                : null;
            SetMaterialColor(material, color);
            meshRenderer.sharedMaterial = material;
            return ring.transform;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material != null && material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Vector3 Flatten(Vector3 position)
        {
            position.y += GroundOffset;
            return position;
        }

        private static Mesh BuildDashedRingMesh()
        {
            const float innerRadius = 0.965f;
            Vector3[] vertices = new Vector3[DashCount * 4];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[DashCount * 6];
            float step = Mathf.PI * 2f / DashCount;
            for (int i = 0; i < DashCount; i++)
            {
                float start = i * step;
                float end = start + step * DashFill;
                int vertex = i * 4;
                vertices[vertex] = PointOnCircle(innerRadius, start);
                vertices[vertex + 1] = PointOnCircle(1f, start);
                vertices[vertex + 2] = PointOnCircle(1f, end);
                vertices[vertex + 3] = PointOnCircle(innerRadius, end);
                for (int j = 0; j < 4; j++)
                {
                    normals[vertex + j] = Vector3.up;
                }

                uvs[vertex] = new Vector2(0f, 0f);
                uvs[vertex + 1] = new Vector2(0f, 1f);
                uvs[vertex + 2] = new Vector2(1f, 1f);
                uvs[vertex + 3] = new Vector2(1f, 0f);
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            Mesh mesh = new Mesh
            {
                name = "Runtime_AbilityRangeDashedRingMesh",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = normals,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 PointOnCircle(float radius, float angle)
        {
            return new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }
    }
}
