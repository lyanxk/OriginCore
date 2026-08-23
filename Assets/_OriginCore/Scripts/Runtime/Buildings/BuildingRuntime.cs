using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Progression;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(VitalsComponent))]
    public sealed class BuildingRuntime : MonoBehaviour
    {
        [SerializeField] private BuildingDefinition _definition;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private bool _operational;

        private TechTreeService _techTree;
        private ResourceService _resources;
        private bool _registered;
        private bool _unlocksActive;
        private float _commanderAccumulator;
        private float _crystalAccumulator;

        public BuildingDefinition Definition => _definition;
        public bool IsOperational => _operational;

        private void Awake()
        {
            CacheComponents();
            EnsureSelectable();
        }

        private void OnEnable()
        {
            CacheComponents();
            TryBindServices();
            RegisterBuilding();
            ApplyOperationalState();
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
                _vitals.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.Died -= HandleDied;
            }

            DeactivateUnlocks();
            if (_registered && _techTree != null)
            {
                _techTree.UnregisterBuilding(_definition);
            }

            _registered = false;
        }

        private void Update()
        {
            if (!_operational || _resources == null || _definition == null ||
                Time.deltaTime <= 0f)
            {
                return;
            }

            AccumulateResource(
                ResourceType.CommanderResource,
                _definition.CommanderResourcePerSecond,
                ref _commanderAccumulator);
            AccumulateResource(
                ResourceType.Crystal,
                _definition.CrystalPerSecond,
                ref _crystalAccumulator);
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(BuildingDefinition definition, bool operational)
        {
            bool wasRegistered = _registered;
            if (wasRegistered && _techTree != null)
            {
                DeactivateUnlocks();
                _techTree.UnregisterBuilding(_definition);
                _registered = false;
            }

            _definition = definition;
            _operational = operational;
            CacheComponents();
            if (isActiveAndEnabled)
            {
                TryBindServices();
                RegisterBuilding();
                ApplyOperationalState();
            }
        }

        public void SetOperational(bool operational)
        {
            if (_operational == operational)
            {
                ApplyOperationalState();
                return;
            }

            _operational = operational;
            ApplyOperationalState();
            if (_operational)
            {
                TrySpawnAutomaticBuilding();
            }
        }

        private void ApplyOperationalState()
        {
            ProductionQueue production = GetComponent<ProductionQueue>();
            if (production != null)
            {
                production.enabled = _operational;
            }

            UnitSpawner spawner = GetComponent<UnitSpawner>();
            if (spawner != null)
            {
                spawner.enabled = _operational;
            }

            RallyPointController rally = GetComponent<RallyPointController>();
            if (rally != null)
            {
                rally.enabled = _operational;
            }

            ResourceDropoff dropoff = GetComponent<ResourceDropoff>();
            if (dropoff != null)
            {
                dropoff.enabled = _operational;
            }

            AutomaticResourceExtractor extractor =
                GetComponent<AutomaticResourceExtractor>();
            if (extractor != null)
            {
                extractor.enabled = _operational;
            }

            AttackCapability attack = GetComponent<AttackCapability>();
            if (attack != null)
            {
                attack.enabled = _operational && _definition != null &&
                                 _definition.SupportsAttack;
            }

            if (_operational)
            {
                ActivateUnlocks();
            }
            else
            {
                DeactivateUnlocks();
            }
        }

        private void RegisterBuilding()
        {
            if (_registered || _definition == null || _techTree == null)
            {
                return;
            }

            _techTree.RegisterBuilding(_definition);
            _registered = true;
        }

        private void ActivateUnlocks()
        {
            if (_unlocksActive || _definition == null || _techTree == null)
            {
                return;
            }

            _techTree.ActivateBuilding(_definition);
            _unlocksActive = true;
        }

        private void DeactivateUnlocks()
        {
            if (!_unlocksActive || _definition == null || _techTree == null)
            {
                return;
            }

            _techTree.DeactivateBuilding(_definition);
            _unlocksActive = false;
        }

        private void TrySpawnAutomaticBuilding()
        {
            if (_definition == null || _techTree == null ||
                string.IsNullOrEmpty(_definition.AutomaticSpawnBuildingId) ||
                _techTree.GetBuildingCount(_definition.ContentId) != 1 ||
                _techTree.GetBuildingCount(_definition.AutomaticSpawnBuildingId) > 0 ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                !appRoot.Services.ContentCatalog.Catalog.TryGetBuilding(
                    _definition.AutomaticSpawnBuildingId,
                    out BuildingDefinition automaticDefinition) ||
                automaticDefinition == null || automaticDefinition.Prefab == null)
            {
                return;
            }

            Vector3 offset = transform.right *
                             (_definition.Footprint.x + automaticDefinition.Footprint.x);
            GameObject instance = Instantiate(
                automaticDefinition.Prefab,
                transform.position + offset,
                transform.rotation);
            EntityIdentity identity = instance.GetComponent<EntityIdentity>();
            identity?.MarkRuntimeSpawned();
            FactionMember sourceFaction = GetComponent<FactionMember>();
            FactionMember targetFaction = instance.GetComponent<FactionMember>();
            if (sourceFaction != null && targetFaction != null)
            {
                targetFaction.SetFaction(sourceFaction.Faction);
            }
        }

        private void AccumulateResource(
            ResourceType type,
            float rate,
            ref float accumulator)
        {
            if (rate <= 0f)
            {
                return;
            }

            accumulator += rate * Time.deltaTime;
            int whole = Mathf.FloorToInt(accumulator);
            if (whole <= 0)
            {
                return;
            }

            accumulator -= whole;
            _resources.Add(type, whole);
        }

        private void HandleDied(VitalsComponent vitals)
        {
            SetOperational(false);
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }
        }

        private void EnsureSelectable()
        {
            if (!Application.isPlaying || _identity == null || _vitals == null)
            {
                return;
            }

            Selectable selectable = GetComponent<Selectable>();
            if (selectable == null)
            {
                selectable = gameObject.AddComponent<Selectable>();
            }

            selectable.Configure(
                _identity,
                GetComponent<FactionMember>(),
                _vitals,
                GetComponent<SelectionIndicatorController>());
            Collider selectionCollider = GetComponent<Collider>();
            if (selectionCollider != null)
            {
                selectable.ConfigureSelectionCollider(selectionCollider);
            }
        }

        private void TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return;
            }

            _techTree = appRoot.Services.TechTree;
            _resources = appRoot.Services.ResourceService;
        }
    }
}
