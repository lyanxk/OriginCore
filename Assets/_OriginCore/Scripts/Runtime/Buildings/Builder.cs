using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Units;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity))]
    public sealed class Builder : MonoBehaviour
    {
        private const int PlacementBufferSize = 64;

        [SerializeField] private BuildingDefinition[] _availableBuildings =
            new BuildingDefinition[0];
        [Min(0.1f), SerializeField] private float _navMeshSampleDistance = 1.5f;

        private readonly Collider[] _placementBuffer = new Collider[PlacementBufferSize];

        public IReadOnlyList<BuildingDefinition> AvailableBuildings => _availableBuildings;

        private void OnValidate()
        {
            _availableBuildings = _availableBuildings ?? new BuildingDefinition[0];
            _navMeshSampleDistance = Mathf.Max(0.1f, _navMeshSampleDistance);
        }

        public void Configure(
            BuildingDefinition[] availableBuildings,
            float navMeshSampleDistance = 1.5f)
        {
            _availableBuildings = availableBuildings != null
                ? (BuildingDefinition[])availableBuildings.Clone()
                : new BuildingDefinition[0];
            _navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        }

        public bool Offers(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            for (int i = 0; i < _availableBuildings.Length; i++)
            {
                if (_availableBuildings[i] == definition)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryValidatePlacement(
            BuildingDefinition definition,
            Vector3 requestedPosition,
            out Vector3 placementPosition,
            out string error)
        {
            return TryValidatePlacement(
                definition,
                requestedPosition,
                out placementPosition,
                out _,
                out error);
        }

        public bool TryValidatePlacement(
            BuildingDefinition definition,
            Vector3 requestedPosition,
            out Vector3 placementPosition,
            out Vector3 approachPosition,
            out string error)
        {
            placementPosition = default(Vector3);
            approachPosition = default(Vector3);
            if (!Offers(definition))
            {
                error = "This worker cannot construct the selected building.";
                return false;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                !appRoot.Services.TechTree.CanConstruct(definition))
            {
                error = definition.Unique
                    ? "The building is locked or its unique instance already exists."
                    : "The building is still locked.";
                return false;
            }

            VisibilitySystem visibility = appRoot.Services.CurrentSceneContext != null
                ? appRoot.Services.CurrentSceneContext.VisibilitySystem
                : null;
            if (visibility != null &&
                visibility.GetState(requestedPosition) != VisibilityCellState.Visible)
            {
                error = "Buildings can only be placed at a currently visible position.";
                return false;
            }

            bool requiresResourceNode = definition.Prefab != null &&
                                        definition.Prefab.GetComponent<AutomaticResourceExtractor>() != null;
            ResourceNode placementNode = null;
            if (requiresResourceNode && !TryResolveCrystalPlacementNode(
                    definition,
                    requestedPosition,
                    out placementNode))
            {
                error = "This building must be placed directly on a non-depleted Crystal node.";
                return false;
            }

            placementPosition = placementNode != null
                ? placementNode.transform.position
                : requestedPosition;
            Vector3 halfExtents = new Vector3(
                definition.Footprint.x * 0.5f,
                1f,
                definition.Footprint.y * 0.5f);
            Vector3 center = placementPosition + Vector3.up;
            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _placementBuffer,
                Quaternion.identity,
                definition.PlacementBlockingMask,
                QueryTriggerInteraction.Ignore);
            int groundLayer = LayerMask.NameToLayer("Ground");
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _placementBuffer[i];
                if (candidate == null || candidate.gameObject.layer == groundLayer ||
                    candidate.transform == transform || candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                ResourceNode resourceNode = candidate.GetComponentInParent<ResourceNode>();
                if (placementNode != null && resourceNode == placementNode)
                {
                    continue;
                }

                error = "The construction footprint is blocked.";
                return false;
            }

            if (!TryResolveConstructionApproach(
                    definition,
                    placementPosition,
                    out approachPosition,
                    out error))
            {
                placementPosition = default;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryResolveConstructionApproach(
            BuildingDefinition definition,
            Vector3 placementPosition,
            out Vector3 approachPosition,
            out string error)
        {
            approachPosition = default;
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (definition == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                error = "The worker is not attached to reachable navigation ground.";
                return false;
            }

            float clearance = Mathf.Max(0.25f, agent.radius + 0.25f);
            float halfWidth = definition.Footprint.x * 0.5f + clearance;
            float halfDepth = definition.Footprint.y * 0.5f + clearance;
            Vector3[] candidates =
            {
                placementPosition + new Vector3(halfWidth, 0f, 0f),
                placementPosition + new Vector3(-halfWidth, 0f, 0f),
                placementPosition + new Vector3(0f, 0f, halfDepth),
                placementPosition + new Vector3(0f, 0f, -halfDepth),
                placementPosition + new Vector3(halfWidth, 0f, halfDepth),
                placementPosition + new Vector3(-halfWidth, 0f, halfDepth),
                placementPosition + new Vector3(halfWidth, 0f, -halfDepth),
                placementPosition + new Vector3(-halfWidth, 0f, -halfDepth)
            };

            bool found = false;
            float bestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!NavMeshPositionResolver.TryResolveReachablePosition(
                        agent,
                        candidates[i],
                        Mathf.Min(0.75f, _navMeshSampleDistance),
                        Mathf.Max(2.5f, _navMeshSampleDistance),
                        out Vector3 resolved))
                {
                    continue;
                }

                Vector3 local = resolved - placementPosition;
                if (Mathf.Abs(local.x) < definition.Footprint.x * 0.5f + 0.05f &&
                    Mathf.Abs(local.z) < definition.Footprint.y * 0.5f + 0.05f)
                {
                    continue;
                }

                float distanceSquared = (resolved - agent.transform.position).sqrMagnitude;
                if (found && distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                found = true;
                bestDistanceSquared = distanceSquared;
                approachPosition = resolved;
            }

            error = found
                ? string.Empty
                : "No reachable construction approach point was found around the footprint.";
            return found;
        }

        private bool TryResolveCrystalPlacementNode(
            BuildingDefinition definition,
            Vector3 requestedPosition,
            out ResourceNode placementNode)
        {
            placementNode = null;
            float searchRadius = Mathf.Max(
                2.5f,
                Mathf.Max(definition.Footprint.x, definition.Footprint.y));
            int count = Physics.OverlapSphereNonAlloc(
                requestedPosition,
                searchRadius,
                _placementBuffer,
                definition.PlacementBlockingMask,
                QueryTriggerInteraction.Ignore);
            float bestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _placementBuffer[i];
                ResourceNode resourceNode = candidate != null
                    ? candidate.GetComponentInParent<ResourceNode>()
                    : null;
                if (resourceNode == null || resourceNode.IsDepleted ||
                    resourceNode.ResourceType != ResourceType.Crystal)
                {
                    continue;
                }

                Vector3 closest = candidate.ClosestPoint(requestedPosition);
                Vector2 horizontalGap = new Vector2(
                    closest.x - requestedPosition.x,
                    closest.z - requestedPosition.z);
                if (horizontalGap.sqrMagnitude > 0.36f)
                {
                    continue;
                }

                Vector3 nodeOffset = resourceNode.transform.position - requestedPosition;
                float distanceSquared = nodeOffset.x * nodeOffset.x +
                                        nodeOffset.z * nodeOffset.z;
                if (placementNode != null && distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                placementNode = resourceNode;
                bestDistanceSquared = distanceSquared;
            }

            return placementNode != null;
        }

        public bool TryPlace(
            BuildingDefinition definition,
            Vector3 requestedPosition,
            out BuildingRuntime building,
            out string error)
        {
            building = null;
            if (!TryValidatePlacement(
                    definition,
                    requestedPosition,
                    out Vector3 placementPosition,
                    out error) || definition.Prefab == null)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "The building prefab is unavailable.";
                }

                return false;
            }

            GameObject instance = Instantiate(
                definition.Prefab,
                placementPosition,
                Quaternion.identity);
            building = instance.GetComponent<BuildingRuntime>();
            ConstructionSite site = instance.GetComponent<ConstructionSite>();
            EntityIdentity identity = instance.GetComponent<EntityIdentity>();
            if (building == null || site == null || identity == null ||
                identity.Definition != definition.EntityDefinition)
            {
                Destroy(instance);
                building = null;
                error = "The building prefab does not satisfy the construction contract.";
                return false;
            }

            identity.MarkRuntimeSpawned();
            FactionMember workerFaction = GetComponent<FactionMember>();
            FactionMember buildingFaction = instance.GetComponent<FactionMember>();
            if (workerFaction != null && buildingFaction != null)
            {
                buildingFaction.SetFaction(workerFaction.Faction);
            }

            site.BeginConstruction(definition);
            error = string.Empty;
            return true;
        }
    }
}
