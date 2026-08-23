using System;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.Progression
{
    public enum PromotionFailure
    {
        None = 0,
        InvalidDefinition = 1,
        Locked = 2,
        InsufficientResources = 3,
        TargetUnavailable = 4,
        PopulationTransferFailed = 5
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(PopulationOwner))]
    public sealed class PromotionController : MonoBehaviour
    {
        [SerializeField] private PromotionDefinition _definition;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private PopulationOwner _populationOwner;

        private bool _promoting;

        public event Action<PromotionController, EntityIdentity> Promoted;

        public PromotionDefinition Definition => _definition;
        public bool IsPromoting => _promoting;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(PromotionDefinition definition)
        {
            _definition = definition;
            CacheComponents();
        }

        public bool CanPromote(out PromotionFailure failure, out string error)
        {
            if (_promoting || _definition == null || _identity == null ||
                _definition.SourceUnit != _identity.Definition || _definition.TargetUnit == null)
            {
                failure = PromotionFailure.InvalidDefinition;
                error = "Promotion definition does not match this unit.";
                return false;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                failure = PromotionFailure.TargetUnavailable;
                error = "Core services are unavailable.";
                return false;
            }

            if (!appRoot.Services.TechTree.IsUnlocked(_definition.RequiredUnlockId))
            {
                failure = PromotionFailure.Locked;
                error = "Promotion research is not completed.";
                return false;
            }

            int currentInfluence = _populationOwner != null
                ? _populationOwner.ReservedInfluence
                : 0;
            int additionalInfluence = Mathf.Max(
                0,
                _definition.TargetInfluence - currentInfluence);
            ResourceCost cost = new ResourceCost(
                _definition.ResourceCost.CommanderResource,
                _definition.ResourceCost.Crystal,
                additionalInfluence);
            if (!appRoot.Services.ResourceService.CanAfford(cost))
            {
                failure = PromotionFailure.InsufficientResources;
                error = "Insufficient resources or Influence for promotion.";
                return false;
            }

            failure = PromotionFailure.None;
            error = string.Empty;
            return true;
        }

        public bool TryPromote(out EntityIdentity promotedIdentity, out PromotionFailure failure, out string error)
        {
            promotedIdentity = null;
            if (!CanPromote(out failure, out error) ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            ContentCatalog catalog = appRoot.Services.ContentCatalog.Catalog;
            if (!catalog.TryGetUnit(
                    _definition.TargetUnit.ArchetypeId,
                    out UnitDefinition targetDefinition,
                    out GameObject targetPrefab) ||
                targetDefinition != _definition.TargetUnit || targetPrefab == null)
            {
                failure = PromotionFailure.TargetUnavailable;
                error = "Promotion target is not registered in the content catalog.";
                return false;
            }

            int currentInfluence = _populationOwner.ReservedInfluence;
            int additionalInfluence = Mathf.Max(
                0,
                _definition.TargetInfluence - currentInfluence);
            ResourceCost committedCost = new ResourceCost(
                _definition.ResourceCost.CommanderResource,
                _definition.ResourceCost.Crystal,
                additionalInfluence);
            ResourceService resources = appRoot.Services.ResourceService;
            if (!resources.TrySpend(committedCost))
            {
                failure = PromotionFailure.InsufficientResources;
                error = "Promotion resources could not be committed.";
                return false;
            }

            _promoting = true;
            string runtimeId = _identity.EnsureRuntimeId();
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            FactionMember sourceFaction = GetComponent<FactionMember>();
            bool wasSelected = false;
            SelectionService selection = appRoot.Services.CurrentSceneContext != null
                ? appRoot.Services.CurrentSceneContext.SelectionService
                : null;
            OriginCore.Gameplay.Selectable sourceSelectable = GetComponent<OriginCore.Gameplay.Selectable>();
            if (selection != null && sourceSelectable != null)
            {
                wasSelected = selection.IsSelected(sourceSelectable);
            }

            _identity.UnregisterFromRegistry();
            GameObject instance = Instantiate(targetPrefab, position, rotation);
            promotedIdentity = instance.GetComponent<EntityIdentity>();
            PopulationOwner targetPopulation = instance.GetComponent<PopulationOwner>();
            if (promotedIdentity == null || promotedIdentity.Definition != targetDefinition ||
                targetPopulation == null || !_populationOwner.TryTransferTo(
                    targetPopulation,
                    _definition.TargetInfluence))
            {
                if (instance != null)
                {
                    Destroy(instance);
                }

                _identity.RegisterWith(appRoot.Services.EntityRegistry);
                resources.Refund(committedCost);
                promotedIdentity = null;
                _promoting = false;
                failure = PromotionFailure.PopulationTransferFailed;
                error = "Promotion target could not assume the population reservation.";
                return false;
            }

            promotedIdentity.MarkRuntimeSpawned();
            promotedIdentity.UnregisterFromRegistry();
            if (!promotedIdentity.AssignRuntimeId(runtimeId, true) ||
                !promotedIdentity.RegisterWith(appRoot.Services.EntityRegistry))
            {
                targetPopulation.TryTransferTo(
                    _populationOwner,
                    currentInfluence);
                Destroy(instance);
                _identity.RegisterWith(appRoot.Services.EntityRegistry);
                resources.Refund(new ResourceCost(
                    committedCost.CommanderResource,
                    committedCost.Crystal,
                    0));
                promotedIdentity = null;
                _promoting = false;
                failure = PromotionFailure.TargetUnavailable;
                error = "Promotion target could not register with the source runtime id.";
                return false;
            }

            FactionMember targetFaction = instance.GetComponent<FactionMember>();
            if (targetFaction != null && sourceFaction != null)
            {
                targetFaction.SetFaction(sourceFaction.Faction);
            }

            LifetimeComponent lifetime = instance.GetComponent<LifetimeComponent>();
            lifetime?.MakePermanent();
            if (wasSelected && selection != null)
            {
                OriginCore.Gameplay.Selectable promotedSelectable =
                    instance.GetComponent<OriginCore.Gameplay.Selectable>();
                if (promotedSelectable != null)
                {
                    selection.SelectOnly(promotedSelectable);
                }
            }

            Promoted?.Invoke(this, promotedIdentity);
            Destroy(gameObject);
            _promoting = false;
            failure = PromotionFailure.None;
            error = string.Empty;
            return true;
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_populationOwner == null)
            {
                _populationOwner = GetComponent<PopulationOwner>();
            }
        }
    }
}
