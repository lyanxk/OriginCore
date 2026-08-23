using OriginCore.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color _validColor =
            new Color(0.2f, 0.9f, 0.72f, 0.48f);
        [SerializeField] private Color _invalidColor =
            new Color(1f, 0.22f, 0.28f, 0.5f);

        private MaterialPropertyBlock _propertyBlock;
        private BuildingDefinition _definition;
        private Material _previewMaterial;
        private Renderer[] _renderers = new Renderer[0];

        public bool IsVisible => gameObject.activeSelf;
        public bool IsValidPlacement { get; private set; }
        public BuildingDefinition Definition => _definition;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_previewMaterial != null)
            {
                Destroy(_previewMaterial);
            }
        }

        public void Show(
            BuildingDefinition definition,
            Vector3 worldPosition,
            bool validPlacement)
        {
            if (definition == null || definition.Prefab == null)
            {
                Hide();
                return;
            }

            if (_definition != definition)
            {
                Rebuild(definition);
            }

            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            IsValidPlacement = validPlacement;
            ApplyColor(validPlacement ? _validColor : _invalidColor);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            IsValidPlacement = false;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Rebuild(BuildingDefinition definition)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            _definition = definition;
            EnsureMaterial();
            Transform prefabRoot = definition.Prefab.transform;
            MeshFilter[] filters = definition.Prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter sourceFilter = filters[i];
                MeshRenderer sourceRenderer = sourceFilter != null
                    ? sourceFilter.GetComponent<MeshRenderer>()
                    : null;
                if (sourceFilter == null || sourceFilter.sharedMesh == null ||
                    sourceRenderer == null || IsExcluded(sourceFilter.transform, prefabRoot))
                {
                    continue;
                }

                GameObject piece = new GameObject(
                    "Preview_" + sourceFilter.name,
                    typeof(MeshFilter),
                    typeof(MeshRenderer));
                Transform pieceTransform = piece.transform;
                pieceTransform.SetParent(transform, false);
                CopyRelativeTransform(prefabRoot, sourceFilter.transform, pieceTransform);
                piece.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
                int materialCount = Mathf.Max(1, sourceRenderer.sharedMaterials.Length);
                Material[] materials = new Material[materialCount];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = _previewMaterial;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            CreateFootprint(definition.Footprint);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void CreateFootprint(Vector2 footprint)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.name = "PlacementFootprint";
            plane.transform.SetParent(transform, false);
            plane.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            plane.transform.localRotation = Quaternion.identity;
            plane.transform.localScale = new Vector3(
                Mathf.Max(0.1f, footprint.x),
                0.04f,
                Mathf.Max(0.1f, footprint.y));
            Collider collider = plane.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _previewMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void EnsureMaterial()
        {
            if (_previewMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _previewMaterial = new Material(shader)
            {
                name = "Runtime_BuildingPlacementPreview",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000
            };
            if (_previewMaterial.HasProperty("_Surface"))
            {
                _previewMaterial.SetFloat("_Surface", 1f);
                _previewMaterial.SetFloat("_SrcBlend", 5f);
                _previewMaterial.SetFloat("_DstBlend", 10f);
                _previewMaterial.SetFloat("_ZWrite", 0f);
                _previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _previewMaterial.SetOverrideTag("RenderType", "Transparent");
            }
        }

        private void ApplyColor(Color color)
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i]?.SetPropertyBlock(_propertyBlock);
            }
        }

        private static bool IsExcluded(Transform candidate, Transform prefabRoot)
        {
            Transform current = candidate;
            while (current != null && current != prefabRoot)
            {
                if (current.name.IndexOf("SelectionIndicator", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.IndexOf("WorldHealthBar", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.IndexOf("Marker", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void CopyRelativeTransform(
            Transform root,
            Transform source,
            Transform target)
        {
            target.localPosition = root.InverseTransformPoint(source.position);
            target.localRotation = Quaternion.Inverse(root.rotation) * source.rotation;
            Vector3 rootScale = root.lossyScale;
            Vector3 sourceScale = source.lossyScale;
            target.localScale = new Vector3(
                SafeDivide(sourceScale.x, rootScale.x),
                SafeDivide(sourceScale.y, rootScale.y),
                SafeDivide(sourceScale.z, rootScale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }
    }
}
