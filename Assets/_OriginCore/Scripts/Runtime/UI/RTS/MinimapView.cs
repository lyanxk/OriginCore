using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinimapView : MonoBehaviour, IPointerClickHandler
    {
        private sealed class MarkerVisual
        {
            public RectTransform RectTransform;
            public RawImage Image;
        }

        private const string FogOverlayName = "DataFogOverlay";
        private const string MarkerRootName = "EntityMarkers";

        [SerializeField] private RectTransform _hitArea;
        [SerializeField] private RawImage _mapImage;
        [SerializeField] private RawImage _fogImage;
        [SerializeField] private RectTransform _markerRoot;
        [SerializeField] private MinimapBounds _worldBounds;
        [SerializeField] private RtsCameraController _rtsCameraController;
        [SerializeField] private VisibilitySystem _visibilitySystem;
        [SerializeField] private MinimapMapDefinition _mapDefinition;

        [Header("Marker Style")]
        [SerializeField] private Color _friendlyColor =
            new Color(0.22f, 0.95f, 0.82f, 1f);
        [SerializeField] private Color _enemyColor =
            new Color(1f, 0.24f, 0.20f, 1f);
        [SerializeField] private Color _neutralColor =
            new Color(0.95f, 0.78f, 0.30f, 1f);
        [SerializeField] private Color _fogTint =
            new Color(0.035f, 0.045f, 0.04f, 1f);
        [Min(2f), SerializeField] private float _unitMarkerSize = 8f;
        [Min(2f), SerializeField] private float _heroMarkerSize = 10f;
        [Min(2f), SerializeField] private float _buildingMarkerSize = 12f;

        private readonly Dictionary<EntityIdentity, MarkerVisual> _entityMarkers =
            new Dictionary<EntityIdentity, MarkerVisual>(32);
        private readonly Dictionary<BuildingMemoryGhost, MarkerVisual> _memoryMarkers =
            new Dictionary<BuildingMemoryGhost, MarkerVisual>(8);
        private readonly List<EntityIdentity> _staleEntities =
            new List<EntityIdentity>(8);
        private readonly List<BuildingMemoryGhost> _staleMemories =
            new List<BuildingMemoryGhost>(4);

        private GameModeController _modeController;
        private EntityRegistry _entityRegistry;
        private Texture2D _generatedMapTexture;
        private MinimapMapDefinition _renderedMapDefinition;

        public RectTransform HitArea => _hitArea;
        public RawImage MapImage => _mapImage;
        public RawImage FogImage => _fogImage;
        public RectTransform MarkerRoot => _markerRoot;
        public MinimapBounds WorldBounds => _worldBounds;
        public RtsCameraController RtsCameraController => _rtsCameraController;
        public VisibilitySystem VisibilitySystem => _visibilitySystem;
        public MinimapMapDefinition MapDefinition => _mapDefinition;
        public Texture BaseMapTexture => _mapImage != null ? _mapImage.texture : null;
        public Texture SharedFogTexture => _visibilitySystem != null
            ? _visibilitySystem.FogTexture
            : null;
        public int EntityMarkerCount => _entityMarkers.Count;
        public int MemoryMarkerCount => _memoryMarkers.Count;
        public Vector3 LastRequestedWorldPosition { get; private set; }
        public bool IsConfigured => _hitArea != null && _mapImage != null &&
                                    _worldBounds != null &&
                                    _rtsCameraController != null &&
                                    _mapDefinition != null;

        private void Awake()
        {
            if (_hitArea == null)
            {
                _hitArea = transform as RectTransform;
            }

            if (_mapImage == null)
            {
                _mapImage = GetComponent<RawImage>();
            }

            EnsureOverlayHierarchy();
        }

        private void OnEnable()
        {
            EnsureOverlayHierarchy();
            TryBindServices();
            RefreshBaseMap();
            SyncFogTexture();
            RebuildEntityMarkers();
        }

        private void Start()
        {
            TryBindServices();
            RefreshBaseMap();
            SyncFogTexture();
            RebuildEntityMarkers();
        }

        private void LateUpdate()
        {
            if (!TryBindServices())
            {
                return;
            }

            RefreshBaseMap();
            SyncFogTexture();
            UpdateEntityMarkers();
            UpdateMemoryMarkers();
        }

        private void OnDisable()
        {
            BindEntityRegistry(null);
            ClearMarkerVisuals();
        }

        private void OnDestroy()
        {
            BindEntityRegistry(null);
            ClearMarkerVisuals();
            ReleaseGeneratedMapTexture();
        }

        public void Configure(
            RectTransform hitArea,
            RawImage mapImage,
            RawImage fogImage,
            RectTransform markerRoot,
            MinimapBounds worldBounds,
            RtsCameraController rtsCameraController,
            VisibilitySystem visibilitySystem,
            MinimapMapDefinition mapDefinition)
        {
            _hitArea = hitArea != null ? hitArea : transform as RectTransform;
            _mapImage = mapImage != null ? mapImage : GetComponent<RawImage>();
            _fogImage = fogImage;
            _markerRoot = markerRoot;
            _worldBounds = worldBounds;
            _rtsCameraController = rtsCameraController;
            _visibilitySystem = visibilitySystem;
            _mapDefinition = mapDefinition;
            EnsureOverlayHierarchy();
            RefreshBaseMap();
            SyncFogTexture();
        }

        public void Configure(
            RectTransform hitArea,
            RawImage mapImage,
            MinimapBounds worldBounds,
            RtsCameraController rtsCameraController,
            VisibilitySystem visibilitySystem)
        {
            Configure(
                hitArea,
                mapImage,
                _fogImage,
                _markerRoot,
                worldBounds,
                rtsCameraController,
                visibilitySystem,
                _mapDefinition);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                _hitArea == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _hitArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            TryCenterAtLocalPoint(localPoint);
        }

        public bool TryCenterAtLocalPoint(Vector2 localPoint)
        {
            if (_hitArea == null)
            {
                return false;
            }

            Rect rect = _hitArea.rect;
            if (!rect.Contains(localPoint) || rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            Vector2 normalized = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y));
            return TryCenterAtNormalized(normalized);
        }

        public bool TryCenterAtNormalized(Vector2 normalized)
        {
            TryBindServices();
            if (!IsConfigured || _modeController == null ||
                _modeController.CurrentMode != GameMode.RTS)
            {
                return false;
            }

            LastRequestedWorldPosition = _worldBounds.NormalizedToWorld(normalized);
            return _rtsCameraController.CenterOnWorldPosition(LastRequestedWorldPosition);
        }

        private bool TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _modeController = appRoot.Services.GameModeController;
            BindEntityRegistry(appRoot.Services.EntityRegistry);
            if (appRoot.Services.CurrentSceneContext != null)
            {
                if (_rtsCameraController == null)
                {
                    _rtsCameraController =
                        appRoot.Services.CurrentSceneContext.RtsCameraController;
                }

                if (_visibilitySystem == null)
                {
                    _visibilitySystem =
                        appRoot.Services.CurrentSceneContext.VisibilitySystem;
                }

                if (_worldBounds == null)
                {
                    _worldBounds =
                        appRoot.Services.CurrentSceneContext.MinimapCoordinateBounds;
                }

                if (_mapDefinition == null)
                {
                    _mapDefinition =
                        appRoot.Services.CurrentSceneContext.MinimapMapDefinition;
                }
            }

            return _modeController != null && _entityRegistry != null;
        }

        private void BindEntityRegistry(EntityRegistry registry)
        {
            if (_entityRegistry == registry)
            {
                return;
            }

            if (_entityRegistry != null)
            {
                _entityRegistry.EntityRegistered -= HandleEntityRegistered;
                _entityRegistry.EntityUnregistered -= HandleEntityUnregistered;
            }

            _entityRegistry = registry;
            if (_entityRegistry != null && isActiveAndEnabled)
            {
                _entityRegistry.EntityRegistered += HandleEntityRegistered;
                _entityRegistry.EntityUnregistered += HandleEntityUnregistered;
            }
        }

        private void HandleEntityRegistered(EntityIdentity entity)
        {
            EnsureEntityMarker(entity);
        }

        private void HandleEntityUnregistered(EntityIdentity entity)
        {
            RemoveEntityMarker(entity);
        }

        private void RebuildEntityMarkers()
        {
            if (_entityRegistry == null || _markerRoot == null)
            {
                return;
            }

            IReadOnlyList<EntityIdentity> entities = _entityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EnsureEntityMarker(entities[i]);
            }
        }

        private void UpdateEntityMarkers()
        {
            _staleEntities.Clear();
            foreach (KeyValuePair<EntityIdentity, MarkerVisual> pair in _entityMarkers)
            {
                EntityIdentity entity = pair.Key;
                MarkerVisual visual = pair.Value;
                if (entity == null || visual == null || visual.RectTransform == null)
                {
                    _staleEntities.Add(entity);
                    continue;
                }

                bool visible = TryGetLiveMarkerPosition(entity, out Vector3 worldPosition);
                SetMarkerVisible(visual, visible);
                if (!visible)
                {
                    continue;
                }

                PositionMarker(visual, worldPosition);
                visual.Image.color = ResolveFactionColor(entity);
                ApplyMarkerShape(visual, entity.Roles);
            }

            for (int i = 0; i < _staleEntities.Count; i++)
            {
                RemoveEntityMarker(_staleEntities[i]);
            }
        }

        private void UpdateMemoryMarkers()
        {
            if (_visibilitySystem == null || _markerRoot == null)
            {
                return;
            }

            IReadOnlyList<BuildingMemoryGhost> memories =
                _visibilitySystem.BuildingMemories;
            for (int i = 0; i < memories.Count; i++)
            {
                BuildingMemoryGhost memory = memories[i];
                if (memory != null && !_memoryMarkers.ContainsKey(memory))
                {
                    _memoryMarkers.Add(
                        memory,
                        CreateMarkerVisual("MemoryMarker_" + i));
                }
            }

            _staleMemories.Clear();
            foreach (KeyValuePair<BuildingMemoryGhost, MarkerVisual> pair in _memoryMarkers)
            {
                BuildingMemoryGhost memory = pair.Key;
                MarkerVisual visual = pair.Value;
                if (memory == null || visual == null || visual.RectTransform == null)
                {
                    _staleMemories.Add(memory);
                    continue;
                }

                SetMarkerVisible(visual, memory.IsGhostVisible);
                if (!memory.IsGhostVisible)
                {
                    continue;
                }

                PositionMarker(visual, memory.LastKnownPosition);
                Color color = ResolveFactionColor(memory.Faction);
                color.a *= 0.55f;
                visual.Image.color = color;
                ApplyMarkerShape(visual, memory.Roles | UnitRole.Building);
            }

            for (int i = 0; i < _staleMemories.Count; i++)
            {
                RemoveMemoryMarker(_staleMemories[i]);
            }
        }

        private void EnsureEntityMarker(EntityIdentity entity)
        {
            if (!ShouldTrack(entity) || _markerRoot == null ||
                _entityMarkers.ContainsKey(entity))
            {
                return;
            }

            _entityMarkers.Add(
                entity,
                CreateMarkerVisual("EntityMarker_" + entity.name));
        }

        private static bool ShouldTrack(EntityIdentity entity)
        {
            return entity != null && entity.isActiveAndEnabled &&
                   entity.Roles != UnitRole.None &&
                   entity.TryGetComponent(out FactionMember _);
        }

        private bool TryGetLiveMarkerPosition(
            EntityIdentity entity,
            out Vector3 worldPosition)
        {
            worldPosition = entity != null ? entity.transform.position : Vector3.zero;
            if (!ShouldTrack(entity))
            {
                return false;
            }

            if (entity.TryGetComponent(out VisibilityTarget target))
            {
                return target.IsAlive && target.IsVisible;
            }

            if (entity.TryGetComponent(out VitalsComponent vitals))
            {
                return vitals.IsAlive;
            }

            return true;
        }

        private void PositionMarker(MarkerVisual visual, Vector3 worldPosition)
        {
            if (_worldBounds == null || visual == null || visual.RectTransform == null)
            {
                return;
            }

            Vector2 normalized = _worldBounds.WorldToNormalized(worldPosition);
            visual.RectTransform.anchorMin = normalized;
            visual.RectTransform.anchorMax = normalized;
            visual.RectTransform.anchoredPosition = Vector2.zero;
        }

        private void ApplyMarkerShape(MarkerVisual visual, UnitRole roles)
        {
            bool building = roles.HasAny(UnitRole.Building);
            bool hero = roles.HasAny(UnitRole.Hero);
            float size = building
                ? _buildingMarkerSize
                : hero
                    ? _heroMarkerSize
                    : _unitMarkerSize;
            visual.RectTransform.sizeDelta = new Vector2(size, size);
            visual.RectTransform.localRotation = building
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 45f);
        }

        private Color ResolveFactionColor(EntityIdentity entity)
        {
            return entity != null && entity.TryGetComponent(out FactionMember member)
                ? ResolveFactionColor(member.Faction)
                : _neutralColor;
        }

        private Color ResolveFactionColor(FactionId faction)
        {
            switch (faction)
            {
                case FactionId.Friendly:
                    return _friendlyColor;
                case FactionId.Enemy:
                    return _enemyColor;
                default:
                    return _neutralColor;
            }
        }

        private MarkerVisual CreateMarkerVisual(string markerName)
        {
            GameObject markerObject = new GameObject(
                markerName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.SetParent(_markerRoot, false);
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(_unitMarkerSize, _unitMarkerSize);
            RawImage image = markerObject.GetComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.raycastTarget = false;
            return new MarkerVisual
            {
                RectTransform = markerRect,
                Image = image
            };
        }

        private static void SetMarkerVisible(MarkerVisual visual, bool visible)
        {
            if (visual != null && visual.RectTransform != null &&
                visual.RectTransform.gameObject.activeSelf != visible)
            {
                visual.RectTransform.gameObject.SetActive(visible);
            }
        }

        private void RemoveEntityMarker(EntityIdentity entity)
        {
            if (!_entityMarkers.TryGetValue(entity, out MarkerVisual visual))
            {
                return;
            }

            DestroyMarkerVisual(visual);
            _entityMarkers.Remove(entity);
        }

        private void RemoveMemoryMarker(BuildingMemoryGhost memory)
        {
            if (!_memoryMarkers.TryGetValue(memory, out MarkerVisual visual))
            {
                return;
            }

            DestroyMarkerVisual(visual);
            _memoryMarkers.Remove(memory);
        }

        private void ClearMarkerVisuals()
        {
            foreach (MarkerVisual visual in _entityMarkers.Values)
            {
                DestroyMarkerVisual(visual);
            }

            foreach (MarkerVisual visual in _memoryMarkers.Values)
            {
                DestroyMarkerVisual(visual);
            }

            _entityMarkers.Clear();
            _memoryMarkers.Clear();
        }

        private static void DestroyMarkerVisual(MarkerVisual visual)
        {
            if (visual == null || visual.RectTransform == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(visual.RectTransform.gameObject);
            }
            else
            {
                DestroyImmediate(visual.RectTransform.gameObject);
            }
        }

        private void EnsureOverlayHierarchy()
        {
            if (_fogImage == null)
            {
                Transform existing = transform.Find(FogOverlayName);
                if (existing != null)
                {
                    _fogImage = existing.GetComponent<RawImage>();
                }
            }

            if (_fogImage == null)
            {
                GameObject fogObject = new GameObject(
                    FogOverlayName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                RectTransform fogRect = fogObject.GetComponent<RectTransform>();
                StretchToParent(fogRect, transform as RectTransform);
                _fogImage = fogObject.GetComponent<RawImage>();
            }

            _fogImage.color = _fogTint;
            _fogImage.raycastTarget = false;

            if (_markerRoot == null)
            {
                Transform existing = transform.Find(MarkerRootName);
                if (existing != null)
                {
                    _markerRoot = existing as RectTransform;
                }
            }

            if (_markerRoot == null)
            {
                GameObject markerRootObject = new GameObject(
                    MarkerRootName,
                    typeof(RectTransform));
                _markerRoot = markerRootObject.GetComponent<RectTransform>();
                StretchToParent(_markerRoot, transform as RectTransform);
            }

            _fogImage.transform.SetAsFirstSibling();
            _markerRoot.SetAsLastSibling();
        }

        private static void StretchToParent(
            RectTransform child,
            RectTransform parent)
        {
            if (child == null || parent == null)
            {
                return;
            }

            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            child.pivot = new Vector2(0.5f, 0.5f);
        }

        private void RefreshBaseMap()
        {
            if (_mapImage == null || _mapDefinition == null ||
                _renderedMapDefinition == _mapDefinition && _mapImage.texture != null)
            {
                return;
            }

            ReleaseGeneratedMapTexture();
            _renderedMapDefinition = _mapDefinition;
            if (_mapDefinition.AuthoredTexture != null)
            {
                _mapImage.texture = _mapDefinition.AuthoredTexture;
            }
            else
            {
                _generatedMapTexture = _mapDefinition.CreateGeneratedTexture();
                _mapImage.texture = _generatedMapTexture;
            }

            _mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            _mapImage.raycastTarget = true;
        }

        private void SyncFogTexture()
        {
            if (_fogImage == null)
            {
                return;
            }

            Texture fogTexture = SharedFogTexture;
            if (_fogImage.texture != fogTexture)
            {
                _fogImage.texture = fogTexture;
            }

            _fogImage.color = _fogTint;
        }

        private void ReleaseGeneratedMapTexture()
        {
            if (_generatedMapTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_generatedMapTexture);
            }
            else
            {
                DestroyImmediate(_generatedMapTexture);
            }

            _generatedMapTexture = null;
            _renderedMapDefinition = null;
        }
    }
}
