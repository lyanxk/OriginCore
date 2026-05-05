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
        const float RouteLineWidth = 0.12f;
        static readonly Color RouteLineColor = new Color(0f, 1f, 0.05f, 0.9f);

        static Material s_routeLineMaterial;

        [SerializeField, HideInInspector] Outline outline;
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
            ConfigureOutline();
        }

        void OnValidate()
        {
            CacheComponents();
            ConfigureOutline();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (outline != null)
                outline.enabled = selected;

            if (!selected)
                ClearSelectionRouteVisual();
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
            UpdateSelectionRouteVisual();
        }

        void OnDisable()
        {
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
            outline = ResolveNearbyComponent(outline);
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

        void ConfigureOutline()
        {
            if (outline == null)
                return;

            outline.enabled = IsSelected;
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineColor = Color.green;
            outline.OutlineWidth = 4f;
        }

        T ResolveNearbyComponent<T>(T cached) where T : Component
        {
            if (cached != null)
                return cached;

            return GetComponent<T>() ?? GetComponentInParent<T>() ?? GetComponentInChildren<T>(true);
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
                _selectionRouteLine.SetPosition(i, _selectionRoutePoints[i] + Vector3.up * 0.15f);
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
            _selectionRouteLine.textureMode = LineTextureMode.Stretch;
            _selectionRouteLine.numCapVertices = 2;
            _selectionRouteLine.startWidth = RouteLineWidth;
            _selectionRouteLine.endWidth = RouteLineWidth;
            _selectionRouteLine.startColor = RouteLineColor;
            _selectionRouteLine.endColor = RouteLineColor;
            _selectionRouteLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRouteLine.receiveShadows = false;
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

        static Material GetRouteLineMaterial()
        {
            if (s_routeLineMaterial != null)
                return s_routeLineMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            s_routeLineMaterial = new Material(shader)
            {
                name = "SelectionRouteLine"
            };

            return s_routeLineMaterial;
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
