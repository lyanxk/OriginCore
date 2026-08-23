using System.Collections.Generic;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Units
{
    [DisallowMultipleComponent]
    public sealed class UnitSpawner : MonoBehaviour
    {
        private const int OccupancyBufferSize = 32;
        private const int CandidateCount = 25;

        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private ContentCatalogService _contentCatalogService;
        [SerializeField] private FactionMember _populationFaction;
        [SerializeField] private UnitDefinition _workerDefinition;
        [SerializeField] private GameObject _workerPrefab;
        [SerializeField] private UnitDefinition _combatDefinition;
        [SerializeField] private GameObject _combatPrefab;
        [Min(0.1f), SerializeField] private float _spawnRadius = 0.6f;
        [Min(0.1f), SerializeField] private float _navMeshSampleDistance = 1.5f;
        [Min(0.25f), SerializeField] private float _candidateSpacing = 1.25f;

        private readonly Collider[] _occupancyBuffer = new Collider[OccupancyBufferSize];

        public Transform SpawnPoint => _spawnPoint;
        public ContentCatalogService ContentCatalogService => _contentCatalogService;
        public FactionMember PopulationFaction => _populationFaction;
        public UnitDefinition WorkerDefinition => _workerDefinition;
        public GameObject WorkerPrefab => _workerPrefab;
        public UnitDefinition CombatDefinition => _combatDefinition;
        public GameObject CombatPrefab => _combatPrefab;
        public float SpawnRadius => _spawnRadius;
        public float NavMeshSampleDistance => _navMeshSampleDistance;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
            _spawnRadius = Mathf.Max(0.1f, _spawnRadius);
            _navMeshSampleDistance = Mathf.Max(0.1f, _navMeshSampleDistance);
            _candidateSpacing = Mathf.Max(0.25f, _candidateSpacing);
        }

        public void Configure(
            Transform spawnPoint,
            FactionMember populationFaction,
            UnitDefinition workerDefinition,
            GameObject workerPrefab,
            UnitDefinition combatDefinition,
            GameObject combatPrefab,
            float spawnRadius = 0.6f,
            float navMeshSampleDistance = 1.5f,
            float candidateSpacing = 1.25f)
        {
            _spawnPoint = spawnPoint;
            _populationFaction = populationFaction != null
                ? populationFaction
                : GetComponent<FactionMember>();
            _workerDefinition = workerDefinition;
            _workerPrefab = workerPrefab;
            _combatDefinition = combatDefinition;
            _combatPrefab = combatPrefab;
            _spawnRadius = Mathf.Max(0.1f, spawnRadius);
            _navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
            _candidateSpacing = Mathf.Max(0.25f, candidateSpacing);
        }

        public void ConfigureCatalog(ContentCatalogService contentCatalogService)
        {
            _contentCatalogService = contentCatalogService;
        }

        public bool TrySpawn(
            ProductionRecipe recipe,
            IProductionResourceAccount resourceService,
            out EntityIdentity spawnedIdentity,
            out string error)
        {
            spawnedIdentity = null;
            if (recipe != null && recipe.BatchCount != 1)
            {
                error = "Batch recipes must use TrySpawnBatch.";
                return false;
            }

            return TrySpawnInstance(
                recipe,
                resourceService,
                recipe != null ? recipe.InfluenceCost : 0,
                null,
                out spawnedIdentity,
                out error);
        }

        public bool TrySpawnBatch(
            ProductionRecipe recipe,
            IProductionResourceAccount resourceService,
            out List<EntityIdentity> spawnedIdentities,
            out string error)
        {
            spawnedIdentities = new List<EntityIdentity>(
                recipe != null ? recipe.BatchCount : 1);
            error = string.Empty;
            if (recipe == null || !recipe.IsValid)
            {
                error = "Production recipe is invalid.";
                return false;
            }

            int batchCount = recipe.BatchCount;
            int influenceBase = recipe.InfluenceCost / batchCount;
            int influenceRemainder = recipe.InfluenceCost % batchCount;
            for (int i = 0; i < batchCount; i++)
            {
                int assignedInfluence = influenceBase + (i < influenceRemainder ? 1 : 0);
                if (TrySpawnInstance(
                        recipe,
                        resourceService,
                        assignedInfluence,
                        null,
                        out EntityIdentity spawned,
                        out error))
                {
                    spawnedIdentities.Add(spawned);
                    continue;
                }

                RollBackBatch(spawnedIdentities);
                spawnedIdentities.Clear();
                return false;
            }

            return true;
        }

        public bool TrySpawnBatchAt(
            ProductionRecipe recipe,
            IProductionResourceAccount resourceService,
            Vector3 worldPosition,
            out List<EntityIdentity> spawnedIdentities,
            out string error)
        {
            spawnedIdentities = new List<EntityIdentity>(
                recipe != null ? recipe.BatchCount : 1);
            error = string.Empty;
            if (recipe == null || !recipe.IsValid)
            {
                error = "Production recipe is invalid.";
                return false;
            }

            int batchCount = recipe.BatchCount;
            int influenceBase = recipe.InfluenceCost / batchCount;
            int influenceRemainder = recipe.InfluenceCost % batchCount;
            for (int i = 0; i < batchCount; i++)
            {
                int assignedInfluence = influenceBase + (i < influenceRemainder ? 1 : 0);
                Vector3 requested = worldPosition + GetCandidateOffset(i) * 0.75f;
                if (TrySpawnInstance(
                        recipe,
                        resourceService,
                        assignedInfluence,
                        requested,
                        out EntityIdentity spawned,
                        out error))
                {
                    spawnedIdentities.Add(spawned);
                    continue;
                }

                RollBackBatch(spawnedIdentities);
                spawnedIdentities.Clear();
                return false;
            }

            return true;
        }

        private bool TrySpawnInstance(
            ProductionRecipe recipe,
            IProductionResourceAccount resourceService,
            int assignedInfluence,
            Vector3? requestedPosition,
            out EntityIdentity spawnedIdentity,
            out string error)
        {
            spawnedIdentity = null;
            error = string.Empty;
            if (recipe == null || !recipe.IsValid)
            {
                error = "Production recipe is invalid.";
                return false;
            }

            if (resourceService == null)
            {
                error = "ResourceService is unavailable for population ownership.";
                return false;
            }

            if (!TryResolvePrefab(recipe.Archetype, out GameObject prefab))
            {
                error = "The production archetype is not registered in the content catalog or legacy slots.";
                return false;
            }

            Vector3 spawnPosition;
            if (requestedPosition.HasValue)
            {
                if (!NavMesh.SamplePosition(
                        requestedPosition.Value,
                        out NavMeshHit requestedHit,
                        _navMeshSampleDistance,
                        NavMesh.AllAreas) || IsOccupied(requestedHit.position))
                {
                    error = "The requested deployment point is unavailable.";
                    return false;
                }

                spawnPosition = requestedHit.position;
            }
            else if (!TryFindSpawnPosition(out spawnPosition))
            {
                error = "No free NavMesh spawn point is currently available.";
                return false;
            }

            Quaternion rotation = _spawnPoint != null
                ? _spawnPoint.rotation
                : transform.rotation;
            GameObject instance = Instantiate(prefab, spawnPosition, rotation);
            instance.name = prefab.name + "_Produced";
            spawnedIdentity = instance.GetComponent<EntityIdentity>();
            NavMeshAgent spawnedAgent = instance.GetComponent<NavMeshAgent>();
            if (spawnedIdentity == null || spawnedIdentity.Definition != recipe.Archetype ||
                instance.GetComponent<UnitCommandQueue>() == null || spawnedAgent == null)
            {
                error = "Spawned prefab does not match the recipe commandable-unit contract.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            if (spawnedAgent.enabled && !spawnedAgent.isOnNavMesh &&
                !spawnedAgent.Warp(spawnPosition))
            {
                error = "Spawned unit could not be placed on the NavMesh.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            spawnedIdentity.MarkRuntimeSpawned();

            FactionMember spawnedFaction = instance.GetComponent<FactionMember>();
            if (spawnedFaction != null && _populationFaction != null)
            {
                spawnedFaction.SetFaction(_populationFaction.Faction);
            }

            PopulationOwner populationOwner = instance.GetComponent<PopulationOwner>();
            if (populationOwner == null)
            {
                populationOwner = instance.AddComponent<PopulationOwner>();
            }

            populationOwner.Configure(instance.GetComponent<VitalsComponent>());
            if (!populationOwner.Assign(resourceService, assignedInfluence))
            {
                error = "Spawned unit could not take ownership of its Influence reservation.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            return true;
        }

        private static void RollBackBatch(List<EntityIdentity> spawnedIdentities)
        {
            for (int i = 0; i < spawnedIdentities.Count; i++)
            {
                EntityIdentity identity = spawnedIdentities[i];
                if (identity == null)
                {
                    continue;
                }

                PopulationOwner owner = identity.GetComponent<PopulationOwner>();
                owner?.AbandonReservationWithoutRelease();
                DestroySpawnedInstance(identity.gameObject);
            }
        }

        public bool CanRestoreArchetype(string archetypeId)
        {
            return TryResolvePrefab(archetypeId, out _);
        }

        public bool TryRestoreSpawn(
            string archetypeId,
            string runtimeId,
            Vector3 position,
            Quaternion rotation,
            FactionId faction,
            int reservedInfluence,
            IProductionResourceAccount resourceService,
            out EntityIdentity spawnedIdentity,
            out string error)
        {
            spawnedIdentity = null;
            error = string.Empty;
            if (!TryResolvePrefab(archetypeId, out GameObject prefab))
            {
                error = "Unknown restorable archetype '" + archetypeId + "'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                error = "Restored runtime id is empty.";
                return false;
            }

            bool requiresCommandPipeline =
                prefab.GetComponent<NavMeshAgent>() != null ||
                prefab.GetComponent<UnitCommandQueue>() != null;
            Vector3 restoredPosition = position;
            NavMeshHit hit = default(NavMeshHit);
            if (requiresCommandPipeline && !NavMesh.SamplePosition(
                    position,
                    out hit,
                    _navMeshSampleDistance,
                    NavMesh.AllAreas))
            {
                error = "Saved position is not near the current NavMesh.";
                return false;
            }

            if (requiresCommandPipeline)
            {
                restoredPosition = hit.position;
            }

            GameObject instance = Instantiate(prefab, restoredPosition, rotation);
            instance.name = prefab.name + "_Restored";
            spawnedIdentity = instance.GetComponent<EntityIdentity>();
            NavMeshAgent spawnedAgent = instance.GetComponent<NavMeshAgent>();
            if (spawnedIdentity == null || requiresCommandPipeline &&
                (spawnedAgent == null || instance.GetComponent<UnitCommandQueue>() == null))
            {
                error = spawnedIdentity == null
                    ? "Restored prefab has no EntityIdentity."
                    : "Restored prefab does not satisfy the commandable-unit contract.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            spawnedIdentity.MarkRuntimeSpawned();
            spawnedIdentity.UnregisterFromRegistry();
            if (!spawnedIdentity.AssignRuntimeId(runtimeId, true) ||
                !spawnedIdentity.RegisterWith(
                    AppRoot.TryGetInstance(out AppRoot appRoot) && appRoot.Services != null
                        ? appRoot.Services.EntityRegistry
                        : null))
            {
                error = "Restored unit could not register its saved runtime id.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            if (spawnedAgent != null && spawnedAgent.enabled &&
                !spawnedAgent.isOnNavMesh && !spawnedAgent.Warp(restoredPosition))
            {
                error = "Restored unit could not be placed on the NavMesh.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            FactionMember spawnedFaction = instance.GetComponent<FactionMember>();
            spawnedFaction?.SetFaction(faction);
            PopulationOwner populationOwner = instance.GetComponent<PopulationOwner>();
            if (populationOwner == null && reservedInfluence > 0)
            {
                populationOwner = instance.AddComponent<PopulationOwner>();
            }

            if (populationOwner != null)
            {
                populationOwner.Configure(instance.GetComponent<VitalsComponent>());
            }

            if (reservedInfluence > 0 && populationOwner != null &&
                !populationOwner.Assign(resourceService, reservedInfluence))
            {
                error = "Restored unit could not reclaim its Influence ownership.";
                DestroySpawnedInstance(instance);
                spawnedIdentity = null;
                return false;
            }

            return true;
        }

        public bool TryFindSpawnPosition(out Vector3 position)
        {
            Vector3 origin = _spawnPoint != null
                ? _spawnPoint.position
                : transform.position;
            for (int i = 0; i < CandidateCount; i++)
            {
                Vector3 offset = GetCandidateOffset(i);
                Vector3 requested = origin + offset;
                if (!NavMesh.SamplePosition(
                        requested,
                        out NavMeshHit hit,
                        _navMeshSampleDistance,
                        NavMesh.AllAreas) || IsOccupied(hit.position))
                {
                    continue;
                }

                position = hit.position;
                return true;
            }

            position = default(Vector3);
            return false;
        }

        private bool TryResolvePrefab(UnitDefinition definition, out GameObject prefab)
        {
            prefab = null;
            if (definition == null || definition.Roles.HasAny(UnitRole.Hero | UnitRole.Building))
            {
                return false;
            }

            // SceneBootProxy may create AppRoot after this component's Awake when a
            // gameplay scene is entered directly from the Editor. Resolve the shared
            // catalog at the point of use as well so production does not depend on
            // script execution order.
            if (_contentCatalogService == null)
            {
                CacheComponents();
            }

            if (_contentCatalogService != null && _contentCatalogService.Catalog != null &&
                _contentCatalogService.Catalog.TryGetUnit(definition, out prefab))
            {
                return prefab != null;
            }

            if (MatchesDefinition(definition, _workerDefinition) &&
                definition.Roles.HasAny(UnitRole.Worker))
            {
                prefab = _workerPrefab;
            }
            else if (MatchesDefinition(definition, _combatDefinition) &&
                     definition.Roles.HasAny(UnitRole.Combat))
            {
                prefab = _combatPrefab;
            }

            return prefab != null;
        }

        private bool TryResolvePrefab(string archetypeId, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                return false;
            }

            if (_contentCatalogService == null)
            {
                CacheComponents();
            }

            if (_contentCatalogService != null && _contentCatalogService.Catalog != null &&
                _contentCatalogService.Catalog.TryGetUnit(archetypeId, out UnitDefinition catalogDefinition, out prefab) &&
                catalogDefinition != null &&
                !catalogDefinition.Roles.HasAny(UnitRole.Hero | UnitRole.Building))
            {
                return prefab != null;
            }

            if (_workerDefinition != null &&
                string.Equals(
                    _workerDefinition.ArchetypeId,
                    archetypeId,
                    System.StringComparison.Ordinal))
            {
                prefab = _workerPrefab;
            }
            else if (_combatDefinition != null &&
                     string.Equals(
                         _combatDefinition.ArchetypeId,
                         archetypeId,
                         System.StringComparison.Ordinal))
            {
                prefab = _combatPrefab;
            }

            return prefab != null;
        }

        private bool IsOccupied(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(
                position + Vector3.up * _spawnRadius,
                _spawnRadius,
                _occupancyBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);
            int groundLayer = LayerMask.NameToLayer("Ground");
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _occupancyBuffer[i];
                if (candidate == null || candidate.gameObject.layer == groundLayer ||
                    candidate.transform == transform ||
                    candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private Vector3 GetCandidateOffset(int index)
        {
            if (index == 0)
            {
                return Vector3.zero;
            }

            int ring = 1 + (index - 1) / 8;
            int slot = (index - 1) % 8;
            float angle = slot * Mathf.PI * 0.25f;
            float radius = ring * _candidateSpacing;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static bool MatchesDefinition(
            UnitDefinition candidate,
            UnitDefinition configured)
        {
            return candidate == configured ||
                   (candidate != null && configured != null &&
                    !string.IsNullOrWhiteSpace(candidate.ArchetypeId) &&
                    candidate.ArchetypeId == configured.ArchetypeId);
        }

        private static void DestroySpawnedInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private void CacheComponents()
        {
            if (_populationFaction == null)
            {
                _populationFaction = GetComponent<FactionMember>();
            }

            if (_contentCatalogService == null &&
                OriginCore.Core.AppRoot.TryGetInstance(out OriginCore.Core.AppRoot appRoot) &&
                appRoot.Services != null)
            {
                _contentCatalogService = appRoot.Services.ContentCatalog;
            }
        }
    }
}
