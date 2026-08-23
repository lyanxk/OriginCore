using OriginCore.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class UnitHoverIndicatorView : MonoBehaviour
    {
        private const string OverlayShaderName = "OriginCore/Command Queue Overlay";
        private const int DashCount = 16;
        private const float InnerRadius = 0.82f;
        private const float DashFill = 0.56f;
        private const float GroundOffset = 0.045f;
        private const float RotationDegreesPerSecond = 82f;
        private const float MinimumRadius = 0.62f;
        private const float MaximumRadius = 2.2f;

        public static readonly Color FriendlyHoverColor =
            new Color(0.28f, 0.88f, 1f, 0.92f);
        public static readonly Color HostileHoverColor =
            new Color(1f, 0.2f, 0.18f, 0.95f);

        private Selectable _target;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private Color _indicatorColor = FriendlyHoverColor;

        public Selectable Target => _target;
        public bool IsVisible => _target != null && _meshRenderer != null &&
                                 _meshRenderer.enabled;
        public Color IndicatorColor => _indicatorColor;

        private void Awake()
        {
            EnsureVisual();
            Hide();
        }

        private void LateUpdate()
        {
            if (_target == null || !_target.isActiveAndEnabled || !_target.IsAlive)
            {
                Hide();
                return;
            }

            UpdatePlacement();
            transform.Rotate(
                0f,
                RotationDegreesPerSecond * Time.unscaledDeltaTime,
                0f,
                Space.World);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        public void Show(Selectable target, Color color)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive)
            {
                Hide();
                return;
            }

            EnsureVisual();
            _target = target;
            _indicatorColor = color;
            ApplyColor();
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = true;
            }

            UpdatePlacement();
        }

        public void Hide()
        {
            _target = null;
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }

        private void EnsureVisual()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer == null || _meshFilter == null)
                {
                    return;
                }

                _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _meshRenderer.receiveShadows = false;
                _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                _meshRenderer.allowOcclusionWhenDynamic = false;
                _meshRenderer.sortingOrder = short.MaxValue;
            }

            if (_mesh == null)
            {
                _mesh = BuildDashedRingMesh();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_material == null)
            {
                Shader shader = Shader.Find(OverlayShaderName);
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader != null)
                {
                    _material = new Material(shader)
                    {
                        name = "Runtime_UnitHoverDashedRing",
                        hideFlags = HideFlags.HideAndDontSave,
                        renderQueue = 5000,
                        mainTexture = Texture2D.whiteTexture
                    };
                    if (_material.HasProperty("_Color"))
                    {
                        _material.SetColor("_Color", _indicatorColor);
                    }

                    _meshRenderer.sharedMaterial = _material;
                }
            }
        }

        private void ApplyColor()
        {
            if (_material != null && _material.HasProperty("_Color"))
            {
                _material.SetColor("_Color", _indicatorColor);
            }
        }

        private void UpdatePlacement()
        {
            Bounds bounds;
            Collider selectionCollider = _target.SelectionCollider;
            if (selectionCollider != null && selectionCollider.enabled)
            {
                bounds = selectionCollider.bounds;
            }
            else
            {
                Renderer targetRenderer = _target.GetComponentInChildren<Renderer>();
                if (targetRenderer == null)
                {
                    transform.position = _target.transform.position +
                                         Vector3.up * GroundOffset;
                    transform.localScale = Vector3.one * MinimumRadius;
                    return;
                }

                bounds = targetRenderer.bounds;
            }

            transform.position = new Vector3(
                bounds.center.x,
                bounds.min.y + GroundOffset,
                bounds.center.z);
            float radius = Mathf.Clamp(
                Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.22f,
                MinimumRadius,
                MaximumRadius);
            transform.localScale = Vector3.one * radius;
        }

        private static Mesh BuildDashedRingMesh()
        {
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
                vertices[vertex] = PointOnCircle(InnerRadius, start);
                vertices[vertex + 1] = PointOnCircle(1f, start);
                vertices[vertex + 2] = PointOnCircle(1f, end);
                vertices[vertex + 3] = PointOnCircle(InnerRadius, end);
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
                name = "Runtime_UnitHoverDashedRingMesh",
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
