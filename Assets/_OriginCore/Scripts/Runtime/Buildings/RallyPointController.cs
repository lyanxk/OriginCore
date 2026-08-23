using System;
using System.Collections.Generic;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(Selectable))]
    public sealed class RallyPointController : MonoBehaviour, IRtsRouteSource
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private Selectable _selectable;
        [SerializeField] private Transform _defaultRallyAnchor;
        [SerializeField] private GameObject _markerPrefab;
        [Min(0.05f), SerializeField] private float _navMeshSampleDistance = 1.5f;

        private GameObject _markerInstance;
        private Vector3 _rallyPoint;
        private bool _hasValidRallyPoint;
        private bool _invalidDispatchWarningIssued;

        public event Action<RallyPointController, Vector3> RallyPointChanged;

        public EntityIdentity Identity => _identity;
        public Selectable Selectable => _selectable;
        public Transform DefaultRallyAnchor => _defaultRallyAnchor;
        public GameObject MarkerPrefab => _markerPrefab;
        public float NavMeshSampleDistance => _navMeshSampleDistance;
        public Vector3 RallyPoint => _rallyPoint;
        public bool HasValidRallyPoint => _hasValidRallyPoint;

        private void Awake()
        {
            CacheComponents();
            InitializeDefaultRallyPoint();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindSelection();
            if (!_hasValidRallyPoint)
            {
                InitializeDefaultRallyPoint();
            }

            RefreshMarkerVisibility();
        }

        private void Start()
        {
            if (!_hasValidRallyPoint)
            {
                InitializeDefaultRallyPoint();
            }
        }

        private void OnDisable()
        {
            UnbindSelection();
            if (_markerInstance != null)
            {
                _markerInstance.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_markerInstance != null)
            {
                Destroy(_markerInstance);
            }
        }

        private void OnValidate()
        {
            CacheComponents();
            _navMeshSampleDistance = Mathf.Max(0.05f, _navMeshSampleDistance);
        }

        public void Configure(
            EntityIdentity identity,
            Selectable selectable,
            Transform defaultRallyAnchor,
            GameObject markerPrefab,
            float navMeshSampleDistance = 1.5f)
        {
            UnbindSelection();
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _selectable = selectable != null ? selectable : GetComponent<Selectable>();
            _defaultRallyAnchor = defaultRallyAnchor;
            _markerPrefab = markerPrefab;
            _navMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
            _hasValidRallyPoint = false;
            if (isActiveAndEnabled)
            {
                BindSelection();
                InitializeDefaultRallyPoint();
            }
        }

        public bool TrySetRallyPoint(Vector3 requestedPoint, out string error)
        {
            if (!NavMesh.SamplePosition(
                    requestedPoint,
                    out NavMeshHit hit,
                    _navMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                error = "No reachable NavMesh point exists near the requested rally point.";
                return false;
            }

            SetResolvedRallyPoint(hit.position);
            error = string.Empty;
            return true;
        }

        public bool TryDispatchSpawnedUnit(EntityIdentity spawnedIdentity)
        {
            if (spawnedIdentity == null)
            {
                return false;
            }

            if (!_hasValidRallyPoint)
            {
                WarnInvalidDispatchOnce(
                    "The producer has no valid rally point; the new unit remains at SpawnPoint.");
                return false;
            }

            UnitCommandQueue queue = spawnedIdentity.GetComponent<UnitCommandQueue>();
            if (queue == null)
            {
                WarnInvalidDispatchOnce(
                    "The spawned unit has no UnitCommandQueue; it remains at SpawnPoint.");
                return false;
            }

            MoveCommand command = new MoveCommand(_rallyPoint);
            if (!queue.TryIssue(
                    command,
                    false,
                    out CommandRejectionReason rejection) ||
                command.Status == CommandStatus.Failed)
            {
                string reason = command.Status == CommandStatus.Failed
                    ? command.FailureReason
                    : rejection.ToString();
                WarnInvalidDispatchOnce(
                    "The new unit could not start its rally Move command (" + reason +
                    "); it remains at SpawnPoint.");
                return false;
            }

            _invalidDispatchWarningIssued = false;
            return true;
        }

        public bool TryBuildRoute(List<Vector3> points)
        {
            if (points == null)
            {
                return false;
            }

            points.Clear();
            if (!_hasValidRallyPoint)
            {
                return false;
            }

            points.Add(transform.position);
            if ((transform.position - _rallyPoint).sqrMagnitude <= 0.01f)
            {
                return false;
            }

            points.Add(_rallyPoint);
            return true;
        }

        private bool InitializeDefaultRallyPoint()
        {
            Vector3 requested = _defaultRallyAnchor != null
                ? _defaultRallyAnchor.position
                : transform.position + transform.forward * 5f;
            if (!NavMesh.SamplePosition(
                    requested,
                    out NavMeshHit hit,
                    _navMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                _hasValidRallyPoint = false;
                RefreshMarkerVisibility();
                return false;
            }

            SetResolvedRallyPoint(hit.position);
            return true;
        }

        private void SetResolvedRallyPoint(Vector3 point)
        {
            _rallyPoint = point;
            _hasValidRallyPoint = true;
            _invalidDispatchWarningIssued = false;
            EnsureMarker();
            if (_markerInstance != null)
            {
                _markerInstance.transform.position = point;
                _markerInstance.transform.rotation = Quaternion.identity;
            }

            RefreshMarkerVisibility();
            RallyPointChanged?.Invoke(this, point);
        }

        private void EnsureMarker()
        {
            if (!Application.isPlaying || _markerInstance != null || _markerPrefab == null)
            {
                return;
            }

            _markerInstance = Instantiate(
                _markerPrefab,
                _rallyPoint,
                Quaternion.identity);
            _markerInstance.name = gameObject.name + "_RallyMarker";
        }

        private void RefreshMarkerVisibility()
        {
            if (_markerInstance == null)
            {
                return;
            }

            bool visible = isActiveAndEnabled && _hasValidRallyPoint &&
                           _selectable != null &&
                           _selectable.State == SelectionVisualState.Selected;
            if (_markerInstance.activeSelf != visible)
            {
                _markerInstance.SetActive(visible);
            }
        }

        private void HandleSelectionStateChanged(
            Selectable selectable,
            SelectionVisualState previous,
            SelectionVisualState current)
        {
            RefreshMarkerVisibility();
        }

        private void WarnInvalidDispatchOnce(string message)
        {
            if (_invalidDispatchWarningIssued)
            {
                return;
            }

            _invalidDispatchWarningIssued = true;
            Debug.LogWarning("[OriginCore P10] " + message, this);
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }
        }

        private void BindSelection()
        {
            if (_selectable != null)
            {
                _selectable.StateChanged -= HandleSelectionStateChanged;
                _selectable.StateChanged += HandleSelectionStateChanged;
            }
        }

        private void UnbindSelection()
        {
            if (_selectable != null)
            {
                _selectable.StateChanged -= HandleSelectionStateChanged;
            }
        }
    }
}
