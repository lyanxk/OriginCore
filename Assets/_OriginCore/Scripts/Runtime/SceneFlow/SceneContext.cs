using System;
using System.Collections.Generic;
using OriginCore.Cameras;
using OriginCore.Core;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using OriginCore.UI;
using OriginCore.UI.RTS;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.SceneFlow
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class SceneContext : MonoBehaviour
    {
        [Serializable]
        public struct ModeCameraTarget
        {
            [SerializeField] private string _modeKey;
            [SerializeField] private Transform _target;

            public string ModeKey => _modeKey;
            public Transform Target => _target;
        }

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Transform _hudRoot;
        [SerializeField] private ModeCameraTarget[] _modeCameraTargets = Array.Empty<ModeCameraTarget>();
        [SerializeField] private GameObject _defaultPawn;
        [SerializeField] private Collider _minimapBounds;
        [SerializeField] private Collider _visibilityBounds;
        [SerializeField] private Transform[] _spawnPoints = Array.Empty<Transform>();
        [SerializeField] private CameraModeCoordinator _cameraModeCoordinator;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private RtsCameraController _rtsCameraController;
        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private SelectionBoxView _selectionBoxView;
        [SerializeField] private SelectionSummaryView _selectionSummaryView;
        [SerializeField] private RtsCommandIssuer _rtsCommandIssuer;
        [SerializeField] private RtsCommandPanelView _rtsCommandPanelView;
        [SerializeField] private ResourceHudView _resourceHudView;
        [SerializeField] private RtsRosterView _rtsRosterView;
        [SerializeField] private ProductionPanelView _productionPanelView;
        [SerializeField] private MinimapBounds _minimapCoordinateBounds;
        [SerializeField] private MinimapView _minimapView;
        [SerializeField] private MinimapMapDefinition _minimapMapDefinition;
        [SerializeField] private VisibilitySystem _visibilitySystem;

        private GameServices _registeredServices;
        private WorldBoundaryWalls _worldBoundaryWalls;

        public Scene Scene => gameObject.scene;
        public Camera MainCamera => _mainCamera;
        public Transform HudRoot => _hudRoot;
        public GameObject DefaultPawn => _defaultPawn;
        public Collider MinimapBounds => _minimapBounds;
        public Collider VisibilityBounds => _visibilityBounds;
        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;
        public CameraModeCoordinator CameraModeCoordinator => _cameraModeCoordinator;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public RtsCameraController RtsCameraController => _rtsCameraController;
        public SelectionService SelectionService => _selectionService;
        public SelectionBoxView SelectionBoxView => _selectionBoxView;
        public SelectionSummaryView SelectionSummaryView => _selectionSummaryView;
        public RtsCommandIssuer RtsCommandIssuer => _rtsCommandIssuer;
        public RtsCommandPanelView RtsCommandPanelView => _rtsCommandPanelView;
        public ResourceHudView ResourceHudView => _resourceHudView;
        public RtsRosterView RtsRosterView => _rtsRosterView;
        public ProductionPanelView ProductionPanelView => _productionPanelView;
        public MinimapBounds MinimapCoordinateBounds => _minimapCoordinateBounds;
        public MinimapView MinimapView => _minimapView;
        public MinimapMapDefinition MinimapMapDefinition => _minimapMapDefinition;
        public VisibilitySystem VisibilitySystem => _visibilitySystem;
        public WorldBoundaryWalls WorldBoundaryWalls => _worldBoundaryWalls;

        private void OnEnable()
        {
            EnsureFallbackWorldBoundaries();

            AppRoot appRoot;
            if (!AppRoot.TryGetInstance(out appRoot) || appRoot.Services == null)
            {
                Debug.LogError(
                    "[OriginCore SceneContext] No initialized AppRoot is available for scene '" +
                    gameObject.scene.name + "'.",
                    this);
                return;
            }

            if (appRoot.Services.RegisterSceneContext(this))
            {
                _registeredServices = appRoot.Services;
            }
        }

        public void ConfigureWorldBounds(Bounds worldBounds)
        {
            _visibilitySystem?.SetWorldBounds(worldBounds);
            _worldBoundaryWalls =
                WorldBoundaryWalls.EnsureForScene(gameObject.scene);
            _worldBoundaryWalls?.Configure(worldBounds);
        }

        private void EnsureFallbackWorldBoundaries()
        {
            Collider boundsSource = _visibilitySystem != null
                ? _visibilitySystem.BoundsSource
                : _visibilityBounds != null
                    ? _visibilityBounds
                    : _minimapBounds;
            if (boundsSource == null)
            {
                return;
            }

            _worldBoundaryWalls =
                WorldBoundaryWalls.EnsureForScene(gameObject.scene);
            _worldBoundaryWalls?.Configure(boundsSource.bounds);
        }

        private void OnDisable()
        {
            if (_registeredServices != null)
            {
                _registeredServices.UnregisterSceneContext(this);
                _registeredServices = null;
            }
        }

        public bool TryGetModeCameraTarget(string modeKey, out Transform target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(modeKey))
            {
                return false;
            }

            for (int i = 0; i < _modeCameraTargets.Length; i++)
            {
                ModeCameraTarget entry = _modeCameraTargets[i];
                if (string.Equals(entry.ModeKey, modeKey, StringComparison.OrdinalIgnoreCase) &&
                    entry.Target != null)
                {
                    target = entry.Target;
                    return true;
                }
            }

            return false;
        }
    }
}
