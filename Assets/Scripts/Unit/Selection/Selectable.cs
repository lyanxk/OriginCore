using Gameplay;
using System.Collections.Generic;
using Unit.Ability;
using Unit.Combat;
using Unit.Command;
using Unit.Movement;
using Unit.UI;
using UnityEngine;
namespace Unit.Selection
{
    [DisallowMultipleComponent]
    public class Selectable : MonoBehaviour
    {
        const int SelectionRingSegments = 64;
        const float SelectionRingWidth = 0.06f;
        const float SelectionRingMinRadius = 0.65f;
        const float SelectionRingPadding = 0.18f;
        const float SelectionRingYOffset = 0.04f;
        const float RouteLineWidth = 0.055f;
        const int RouteDashTextureWidth = 32;
        const int RouteDashSolidPixels = 18;
        const float RouteLineYOffset = 0.12f;
        const int RouteLineRenderQueue = 5000;
        const int RouteLineSortingOrder = 64;
        const string OverlayLineShaderName = "OriginCore/Overlay Line";

        static readonly Color SelectionRingColor = new Color(0.1f, 1f, 0.15f, 0.95f);
        static readonly Color RouteLineColor = new Color(0.78f, 0.82f, 0.82f, 0.85f);

        static Material s_selectionRingMaterial;
        static Material s_routeLineMaterial;
        static Texture2D s_routeDashTexture;

        [SerializeField, HideInInspector] TeamAffiliation teamAffiliation;
        [SerializeField, HideInInspector] CommandExecutor commandExecutor;
        [SerializeField, HideInInspector] UnitCombat combat;
        [SerializeField, HideInInspector] UnitBase motor;
        [SerializeField, HideInInspector] AbilityInputRouter abilityRouter;
        [SerializeField, HideInInspector] MonoBehaviour commandCardDataSourceComponent;
        [SerializeField, HideInInspector] MonoBehaviour groundCommandReceiverComponent;
        [SerializeField, HideInInspector] MonoBehaviour selectionRouteProviderComponent;

        readonly List<Vector3> _selectionRoutePoints = new List<Vector3>(8);
        bool _registered;
        LineRenderer _selectionRingLine;
        LineRenderer _selectionRouteLine;

        public bool IsSelected { get; private set; }
        public TeamAffiliation TeamAffiliation => teamAffiliation;
        public CommandExecutor CommandExecutor => commandExecutor;
        public UnitCombat Combat => combat;
        public UnitBase Motor => motor;
        public AbilityInputRouter AbilityRouter => abilityRouter;
        public ICommandCardDataSource CommandCardDataSource => commandCardDataSourceComponent as ICommandCardDataSource;
        public IGroundCommandReceiver GroundCommandReceiver => groundCommandReceiverComponent as IGroundCommandReceiver;
        public ISelectionRouteProvider SelectionRouteProvider => selectionRouteProviderComponent as ISelectionRouteProvider;

        void Awake()
        {
            CacheComponents();
        }

        void OnValidate()
        {
            CacheComponents();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (!selected)
            {
                ClearSelectionRingVisual();
                ClearSelectionRouteVisual();
            }
            else
            {
                UpdateSelectionRingVisual();
            }
        }

        void OnEnable()
        {
            TryRegister();
        }

        void Start()
        {
            // SelectionManager may initialize after this component's OnEnable.
            TryRegister();
        }

        void Update()
        {
            if (!_registered)
                TryRegister();
        }

        void LateUpdate()
        {
            UpdateSelectionRingVisual();
            UpdateSelectionRouteVisual();
        }

        void OnDisable()
        {
            ClearSelectionRingVisual();
            ClearSelectionRouteVisual();

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.Unregister(this);

            _registered = false;
        }

        void TryRegister()
        {
            if (_registered)
                return;

            SelectionManager manager = SelectionManager.Instance;
            if (manager == null)
                return;

            manager.Register(this);
            _registered = true;
        }

        void CacheComponents()
        {
            teamAffiliation = ResolveNearbyComponent(teamAffiliation);
            commandExecutor = ResolveNearbyComponent(commandExecutor);
            combat = ResolveNearbyComponent(combat);
            motor = ResolveNearbyComponent(motor);
            abilityRouter = ResolveNearbyComponent(abilityRouter);

            if (commandCardDataSourceComponent == null || commandCardDataSourceComponent is not ICommandCardDataSource)
                commandCardDataSourceComponent = ResolveInterfaceComponent<ICommandCardDataSource>();

            if (groundCommandReceiverComponent == null || groundCommandReceiverComponent is not IGroundCommandReceiver)
                groundCommandReceiverComponent = ResolveInterfaceComponent<IGroundCommandReceiver>();

            if (selectionRouteProviderComponent == null || selectionRouteProviderComponent is not ISelectionRouteProvider)
                selectionRouteProviderComponent = ResolveInterfaceComponent<ISelectionRouteProvider>();
        }

        T ResolveNearbyComponent<T>(T cached) where T : Component
        {
            if (cached != null)
                return cached;

            return GetComponent<T>() ?? GetComponentInParent<T>() ?? GetComponentInChildren<T>(true);
        }

        void UpdateSelectionRingVisual()
        {
            if (!IsSelected)
            {
                ClearSelectionRingVisual();
                return;
            }

            EnsureSelectionRingLine();
            if (_selectionRingLine == null)
                return;

            ResolveSelectionRing(out Vector3 center, out float radius);

            _selectionRingLine.enabled = true;
            _selectionRingLine.positionCount = SelectionRingSegments;
            for (int i = 0; i < SelectionRingSegments; i++)
            {
                float angle = i / (float)SelectionRingSegments * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                _selectionRingLine.SetPosition(i, center + offset);
            }
        }

        void EnsureSelectionRingLine()
        {
            if (_selectionRingLine != null)
                return;

            GameObject ringObject = new GameObject("SelectionRing");
            ringObject.transform.SetParent(transform, false);

            _selectionRingLine = ringObject.AddComponent<LineRenderer>();
            _selectionRingLine.useWorldSpace = true;
            _selectionRingLine.alignment = LineAlignment.View;
            _selectionRingLine.textureMode = LineTextureMode.Stretch;
            _selectionRingLine.loop = true;
            _selectionRingLine.positionCount = SelectionRingSegments;
            _selectionRingLine.startWidth = SelectionRingWidth;
            _selectionRingLine.endWidth = SelectionRingWidth;
            _selectionRingLine.startColor = SelectionRingColor;
            _selectionRingLine.endColor = SelectionRingColor;
            _selectionRingLine.numCapVertices = 2;
            _selectionRingLine.numCornerVertices = 2;
            _selectionRingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRingLine.receiveShadows = false;
            _selectionRingLine.enabled = false;

            Material ringMaterial = GetSelectionRingMaterial();
            if (ringMaterial != null)
                _selectionRingLine.sharedMaterial = ringMaterial;
        }

        void ClearSelectionRingVisual()
        {
            if (_selectionRingLine == null)
                return;

            _selectionRingLine.enabled = false;
        }

        void ResolveSelectionRing(out Vector3 center, out float radius)
        {
            if (TryGetSelectableBounds(out Bounds bounds))
            {
                center = new Vector3(bounds.center.x, bounds.min.y + SelectionRingYOffset, bounds.center.z);
                radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + SelectionRingPadding;
                radius = Mathf.Max(radius, SelectionRingMinRadius);
                return;
            }

            center = transform.position + Vector3.up * SelectionRingYOffset;
            radius = SelectionRingMinRadius;
        }

        bool TryGetSelectableBounds(out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider selectableCollider = colliders[i];
                if (selectableCollider == null || !selectableCollider.enabled)
                    continue;

                if (!hasBounds)
                    bounds = selectableCollider.bounds;
                else
                    bounds.Encapsulate(selectableCollider.bounds);

                hasBounds = true;
            }

            if (hasBounds)
                return true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer selectableRenderer = renderers[i];
                if (selectableRenderer == null || !selectableRenderer.enabled || selectableRenderer is LineRenderer)
                    continue;

                if (!hasBounds)
                    bounds = selectableRenderer.bounds;
                else
                    bounds.Encapsulate(selectableRenderer.bounds);

                hasBounds = true;
            }

            return hasBounds;
        }

        void UpdateSelectionRouteVisual()
        {
            if (!IsSelected || SelectionRouteProvider == null)
            {
                ClearSelectionRouteVisual();
                return;
            }

            EnsureSelectionRouteLine();
            if (_selectionRouteLine == null)
                return;

            _selectionRoutePoints.Clear();
            if (!SelectionRouteProvider.TryBuildSelectionRoute(_selectionRoutePoints) || _selectionRoutePoints.Count < 2)
            {
                ClearSelectionRouteVisual();
                return;
            }

            _selectionRouteLine.enabled = true;
            _selectionRouteLine.positionCount = _selectionRoutePoints.Count;
            for (int i = 0; i < _selectionRoutePoints.Count; i++)
                _selectionRouteLine.SetPosition(i, _selectionRoutePoints[i] + Vector3.up * RouteLineYOffset);
        }

        void EnsureSelectionRouteLine()
        {
            if (_selectionRouteLine != null)
                return;

            // The runtime-only route line keeps building and unit path feedback consistent without extra scene wiring.
            GameObject routeObject = new GameObject("SelectionRoute");
            routeObject.transform.SetParent(transform, false);

            _selectionRouteLine = routeObject.AddComponent<LineRenderer>();
            _selectionRouteLine.useWorldSpace = true;
            _selectionRouteLine.alignment = LineAlignment.View;
            _selectionRouteLine.textureMode = LineTextureMode.Tile;
            _selectionRouteLine.numCapVertices = 0;
            _selectionRouteLine.startWidth = RouteLineWidth;
            _selectionRouteLine.endWidth = RouteLineWidth;
            _selectionRouteLine.startColor = RouteLineColor;
            _selectionRouteLine.endColor = RouteLineColor;
            _selectionRouteLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRouteLine.receiveShadows = false;
            _selectionRouteLine.sortingOrder = RouteLineSortingOrder;
            _selectionRouteLine.enabled = false;

            Material routeMaterial = GetRouteLineMaterial();
            if (routeMaterial != null)
                _selectionRouteLine.sharedMaterial = routeMaterial;
        }

        void ClearSelectionRouteVisual()
        {
            if (_selectionRouteLine == null)
                return;

            _selectionRouteLine.positionCount = 0;
            _selectionRouteLine.enabled = false;
        }

        static MonoBehaviour FindInterfaceComponent<T>(MonoBehaviour[] behaviours) where T : class
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour is T)
                    return behaviour;
            }

            return null;
        }

        MonoBehaviour ResolveInterfaceComponent<T>() where T : class
        {
            return FindInterfaceComponent<T>(GetComponents<MonoBehaviour>())
                   ?? FindInterfaceComponent<T>(GetComponentsInParent<MonoBehaviour>(true))
                   ?? FindInterfaceComponent<T>(GetComponentsInChildren<MonoBehaviour>(true));
        }

        static Material GetSelectionRingMaterial()
        {
            if (s_selectionRingMaterial != null)
                return s_selectionRingMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            s_selectionRingMaterial = new Material(shader)
            {
                name = "SelectionRing",
                hideFlags = HideFlags.HideAndDontSave
            };

            return s_selectionRingMaterial;
        }

        static Material GetRouteLineMaterial()
        {
            if (s_routeLineMaterial != null)
                return s_routeLineMaterial;

            Shader shader = Shader.Find(OverlayLineShaderName);
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            s_routeLineMaterial = new Material(shader)
            {
                name = "SelectionRouteDashedLine",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetRouteDashTexture()
            };
            ConfigureOverlayLineMaterial(s_routeLineMaterial);

            return s_routeLineMaterial;
        }

        static void ConfigureOverlayLineMaterial(Material material)
        {
            material.renderQueue = RouteLineRenderQueue;

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);

            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);

            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        static Texture2D GetRouteDashTexture()
        {
            if (s_routeDashTexture != null)
                return s_routeDashTexture;

            Color32[] pixels = new Color32[RouteDashTextureWidth];
            Color32 solid = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = i < RouteDashSolidPixels ? solid : clear;

            s_routeDashTexture = new Texture2D(RouteDashTextureWidth, 1, TextureFormat.RGBA32, false)
            {
                name = "SelectionRouteDashPattern",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            s_routeDashTexture.SetPixels32(pixels);
            s_routeDashTexture.Apply();

            return s_routeDashTexture;
        }

        public static bool TryResolve(Component hitComponent, out Selectable selectable)
        {
            selectable = null;
            if (hitComponent == null)
                return false;

            if (hitComponent.TryGetComponent(out selectable))
                return true;

            selectable = hitComponent.GetComponentInParent<Selectable>();
            if (selectable != null)
                return true;

            Transform current = hitComponent.transform;
            while (current != null)
            {
                selectable = FindClosestChildSelectable(current, hitComponent.transform);
                if (selectable != null)
                    return true;

                current = current.parent;
            }

            return false;
        }

        static Selectable FindClosestChildSelectable(Transform root, Transform source)
        {
            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
            Selectable best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable candidate = selectables[i];
                int distance = GetHierarchyDistance(source, candidate.transform);
                if (distance >= bestDistance)
                    continue;

                best = candidate;
                bestDistance = distance;
            }

            return best;
        }

        static int GetHierarchyDistance(Transform a, Transform b)
        {
            if (a == null || b == null)
                return int.MaxValue;

            int depthA = GetDepth(a);
            int depthB = GetDepth(b);
            int distance = 0;

            while (depthA > depthB)
            {
                a = a.parent;
                depthA--;
                distance++;
            }

            while (depthB > depthA)
            {
                b = b.parent;
                depthB--;
                distance++;
            }

            while (a != b)
            {
                if (a == null || b == null)
                    return int.MaxValue;

                a = a.parent;
                b = b.parent;
                distance += 2;
            }

            return distance;
        }

        static int GetDepth(Transform transform)
        {
            int depth = 0;
            while (transform != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}
