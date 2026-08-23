using System;
using OriginCore.Buildings;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class WorkerSummonController : MonoBehaviour
    {
        private const int RaycastCapacity = 32;

        [SerializeField] private ProductionRecipe _workerRecipe;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private LayerMask _resourceMask = ~0;
        [Min(0.1f), SerializeField] private float _maximumRayDistance = 500f;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[RaycastCapacity];
        private InputRouter _inputRouter;

        public event Action<EntityIdentity> WorkerSummoned;
        public event Action<string> SummonRejected;

        public ProductionRecipe WorkerRecipe => _workerRecipe;
        public string LastFailureReason { get; private set; } = string.Empty;

        private void OnEnable()
        {
            TryBindInput();
        }

        private void Start()
        {
            TryBindInput();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
                _inputRouter = null;
            }
        }

        private void OnValidate()
        {
            _maximumRayDistance = Mathf.Max(0.1f, _maximumRayDistance);
        }

        public void Configure(
            ProductionRecipe workerRecipe,
            Camera aimCamera = null,
            float maximumRayDistance = 500f)
        {
            _workerRecipe = workerRecipe;
            _aimCamera = aimCamera;
            _maximumRayDistance = Mathf.Max(0.1f, maximumRayDistance);
        }

        public bool TrySummon(out EntityIdentity worker, out string error)
        {
            worker = null;
            error = string.Empty;
            if (!IsPossessedPawn())
            {
                return Reject("Only the currently possessed hero can summon a worker.", out error);
            }

            if (_workerRecipe == null || !_workerRecipe.IsValid ||
                _workerRecipe.BatchCount != 1)
            {
                return Reject("The worker summon recipe is unavailable.", out error);
            }

            Camera camera = ResolveAimCamera();
            if (camera == null || !TryHitResourceNode(camera, out ResourceNode node) ||
                node.IsDepleted)
            {
                return Reject("Aim at a non-depleted resource node before summoning a worker.", out error);
            }

            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null)
            {
                return Reject("Gameplay services are unavailable.", out error);
            }

            UnitSpawner spawner = FindFriendlySpawner(root.Services, node.transform.position);
            if (spawner == null)
            {
                return Reject("No active friendly worker spawner is available.", out error);
            }

            ResourceService resources = root.Services.ResourceService;
            if (resources == null || !resources.TrySpend(_workerRecipe.Cost))
            {
                return Reject("Insufficient resources or population capacity to summon a worker.", out error);
            }

            if (!spawner.TrySpawn(_workerRecipe, resources, out worker, out string spawnError))
            {
                resources.Refund(_workerRecipe.Cost);
                return Reject(spawnError, out error);
            }

            LastFailureReason = string.Empty;
            WorkerSummoned?.Invoke(worker);
            return true;
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || !IsPossessedPawn())
            {
                return;
            }

            bool requested = snapshot.ActiveMap == GameplayInputMap.ACT
                ? snapshot.ACT.SummonWorker.WasPressedThisFrame
                : snapshot.ActiveMap == GameplayInputMap.FPS &&
                  snapshot.FPS.SummonWorker.WasPressedThisFrame;
            if (requested)
            {
                TrySummon(out _, out _);
            }
        }

        private bool TryHitResourceNode(Camera camera, out ResourceNode node)
        {
            node = null;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                _maximumRayDistance,
                _resourceMask,
                QueryTriggerInteraction.Collide);
            float nearestDistance = float.PositiveInfinity;
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _raycastHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }

            node = nearestCollider != null
                ? nearestCollider.GetComponentInParent<ResourceNode>()
                : null;
            return node != null;
        }

        private UnitSpawner FindFriendlySpawner(GameServices services, Vector3 targetPosition)
        {
            FactionMember ownerFaction = GetComponent<FactionMember>();
            FactionId faction = ownerFaction != null
                ? ownerFaction.Faction
                : FactionId.Friendly;
            UnitSpawner nearest = null;
            float nearestDistance = float.PositiveInfinity;
            var entities = services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity identity = entities[i];
                UnitSpawner candidate = identity != null
                    ? identity.GetComponent<UnitSpawner>()
                    : null;
                FactionMember candidateFaction = candidate != null
                    ? candidate.GetComponent<FactionMember>()
                    : null;
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidateFaction != null && candidateFaction.Faction != faction)
                {
                    continue;
                }

                float distance = (candidate.transform.position - targetPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private Camera ResolveAimCamera()
        {
            if (_aimCamera != null)
            {
                return _aimCamera;
            }

            if (AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                root.Services.CurrentSceneContext != null)
            {
                _aimCamera = root.Services.CurrentSceneContext.MainCamera;
            }
            return _aimCamera;
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private bool Reject(string reason, out string error)
        {
            error = string.IsNullOrWhiteSpace(reason)
                ? "Worker summon failed."
                : reason;
            LastFailureReason = error;
            SummonRejected?.Invoke(error);
            return false;
        }

        private void TryBindInput()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.InputRouter == null)
            {
                return;
            }

            InputRouter next = root.Services.InputRouter;
            if (_inputRouter == next)
            {
                return;
            }

            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
            }
            _inputRouter = next;
            _inputRouter.SnapshotReady += HandleSnapshot;
        }
    }
}
