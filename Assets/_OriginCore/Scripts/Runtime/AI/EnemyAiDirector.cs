using System;
using System.Collections.Generic;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.AI
{
    public enum EnemyAiPhase
    {
        Dormant = 0,
        BuildArmy = 1,
        Assault = 2,
        Regroup = 3,
        Defeated = 4
    }

    [Serializable]
    public struct EnemyAiRuntimeSnapshot
    {
        public EnemyAiPhase Phase;
        public int AssaultSerial;
        public float ThinkRemaining;
        public float RegroupRemaining;
        public int ProductionCursor;
        public string StrategicTargetRuntimeId;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyProductionAccount))]
    public sealed class EnemyAiDirector : MonoBehaviour
    {
        [SerializeField] private EnemyAiDefinition _definition;
        [SerializeField] private string _directorId = "enemy.main";
        [SerializeField] private EnemyProductionAccount _account;
        [SerializeField] private EnemyProductionSite[] _sites =
            Array.Empty<EnemyProductionSite>();
        [SerializeField] private StrategicTarget[] _targets =
            Array.Empty<StrategicTarget>();
        [SerializeField] private EntityRegistry _entityRegistry;

        private readonly List<AiControllableUnit> _units =
            new List<AiControllableUnit>(32);
        private readonly List<AiControllableUnit> _assault =
            new List<AiControllableUnit>(32);
        private float _thinkRemaining;
        private float _regroupRemaining;
        private int _productionCursor;
        private bool _configurationErrorReported;
        private StrategicTarget _currentTarget;

        public EnemyAiPhase Phase { get; private set; } = EnemyAiPhase.Dormant;
        public int AssaultSerial { get; private set; }
        public EnemyProductionAccount Account => _account;
        public FactionId Faction => _definition != null
            ? _definition.Faction
            : FactionId.Enemy;
        public string DirectorId => string.IsNullOrWhiteSpace(_directorId)
            ? "enemy.main"
            : _directorId.Trim().ToLowerInvariant();
        public StrategicTarget CurrentTarget => _currentTarget;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            DiscoverSceneMembers();
            BindRegistry();
            TryStart();
        }

        private void OnDisable()
        {
            UnbindRegistry();
            _units.Clear();
            _assault.Clear();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || Phase == EnemyAiPhase.Dormant ||
                Phase == EnemyAiPhase.Defeated)
            {
                return;
            }
            _thinkRemaining -= Time.deltaTime;
            if (_thinkRemaining > 0f) return;
            _thinkRemaining = _definition != null ? _definition.ThinkInterval : 0.5f;
            TickDirector(_thinkRemaining);
        }

        public bool TryStart()
        {
            if (_definition == null || _account == null || !_account.IsConfigured)
            {
                Phase = EnemyAiPhase.Dormant;
                if (!_configurationErrorReported)
                {
                    Debug.LogError(
                        "[OriginCore EnemyAI] Director is dormant: definition or funding policy is missing.",
                        this);
                    _configurationErrorReported = true;
                }
                return false;
            }
            ProductionRecipe[] allowed = CollectAllowedRecipes();
            ProductionContext context = new ProductionContext(
                _definition.Faction,
                _account,
                new RecipeAllowListPolicy(allowed));
            for (int i = 0; i < _sites.Length; i++)
            {
                if (_sites[i] != null) _sites[i].ApplyContext(context);
            }
            _configurationErrorReported = false;
            Phase = EnemyAiPhase.BuildArmy;
            _thinkRemaining = 0f;
            return true;
        }

        public void Configure(
            EnemyAiDefinition definition,
            string directorId,
            EnemyProductionAccount account,
            EnemyProductionSite[] sites,
            StrategicTarget[] targets,
            EntityRegistry entityRegistry)
        {
            UnbindRegistry();
            _definition = definition;
            _directorId = string.IsNullOrWhiteSpace(directorId)
                ? "enemy.main"
                : directorId.Trim().ToLowerInvariant();
            _account = account != null ? account : GetComponent<EnemyProductionAccount>();
            _sites = sites != null
                ? (EnemyProductionSite[])sites.Clone()
                : Array.Empty<EnemyProductionSite>();
            _targets = targets != null
                ? (StrategicTarget[])targets.Clone()
                : Array.Empty<StrategicTarget>();
            _entityRegistry = entityRegistry;
            _productionCursor = 0;
            _configurationErrorReported = false;
            RefreshUnits();
            if (isActiveAndEnabled)
            {
                BindRegistry();
            }
        }

        public void TickDirector(float deltaTime)
        {
            RefreshUnits();
            if (!HasOperationalProductionSite() && CountOperationalUnits() == 0)
            {
                Phase = EnemyAiPhase.Defeated;
                return;
            }

            TickProduction();
            switch (Phase)
            {
                case EnemyAiPhase.BuildArmy:
                    if (CountCommandablePopulation() >=
                        _definition.AssaultPopulationThreshold)
                    {
                        BeginAssault();
                    }
                    break;
                case EnemyAiPhase.Assault:
                    if (!IsTargetValid(_currentTarget) ||
                        CountLivingAssaultMembers() <= Mathf.FloorToInt(
                            _assault.Count * _definition.RegroupRemainingRatio))
                    {
                        Phase = EnemyAiPhase.Regroup;
                        _regroupRemaining = _definition.RegroupSeconds;
                    }
                    break;
                case EnemyAiPhase.Regroup:
                    _regroupRemaining = Mathf.Max(0f, _regroupRemaining - deltaTime);
                    if (_regroupRemaining <= 0f)
                    {
                        _assault.Clear();
                        Phase = EnemyAiPhase.BuildArmy;
                    }
                    break;
            }
        }

        public EnemyAiRuntimeSnapshot CaptureSnapshot()
        {
            return new EnemyAiRuntimeSnapshot
            {
                Phase = Phase,
                AssaultSerial = AssaultSerial,
                ThinkRemaining = _thinkRemaining,
                RegroupRemaining = _regroupRemaining,
                ProductionCursor = _productionCursor,
                StrategicTargetRuntimeId = _currentTarget != null &&
                    _currentTarget.Identity != null
                    ? _currentTarget.Identity.RuntimeId
                    : string.Empty
            };
        }

        public void RestoreSnapshot(EnemyAiRuntimeSnapshot snapshot)
        {
            DiscoverSceneMembers();
            AssaultSerial = Mathf.Max(0, snapshot.AssaultSerial);
            _thinkRemaining = Mathf.Max(0f, snapshot.ThinkRemaining);
            _regroupRemaining = Mathf.Max(0f, snapshot.RegroupRemaining);
            _productionCursor = Mathf.Max(0, snapshot.ProductionCursor);
            _currentTarget = FindTarget(snapshot.StrategicTargetRuntimeId);
            Phase = snapshot.Phase;
            if (Phase == EnemyAiPhase.Assault && !IsTargetValid(_currentTarget))
            {
                Phase = EnemyAiPhase.Regroup;
            }
            else if (Phase == EnemyAiPhase.Assault)
            {
                _assault.Clear();
                for (int i = 0; i < _units.Count; i++)
                {
                    if (IsOperational(_units[i]))
                    {
                        _assault.Add(_units[i]);
                    }
                }
                if (_assault.Count == 0)
                {
                    Phase = EnemyAiPhase.Regroup;
                }
                else
                {
                    IssueAssaultOrders();
                }
            }
        }

        private void BeginAssault()
        {
            _currentTarget = FindBestTarget();
            if (!IsTargetValid(_currentTarget)) return;
            _assault.Clear();
            for (int i = 0; i < _units.Count; i++)
            {
                AiControllableUnit unit = _units[i];
                if (!IsOperational(unit)) continue;
                _assault.Add(unit);
            }
            if (_assault.Count == 0) return;
            AssaultSerial++;
            IssueAssaultOrders();
            Phase = EnemyAiPhase.Assault;
        }

        private void IssueAssaultOrders()
        {
            if (!IsTargetValid(_currentTarget))
            {
                return;
            }

            Vector3 destination = _currentTarget.transform.position;
            for (int i = 0; i < _assault.Count; i++)
            {
                AiControllableUnit unit = _assault[i];
                if (!IsOperational(unit))
                {
                    continue;
                }

                unit.Commands.TryIssue(
                    new AttackMoveCommand(destination),
                    false,
                    out CommandRejectionReason _);
            }
        }

        private void TickProduction()
        {
            EnemyProductionWeight[] weights = _definition.ProductionWeights;
            if (weights == null || weights.Length == 0) return;
            int totalWeight = GetTotalProductionWeight(weights);
            if (totalWeight <= 0) return;
            for (int i = 0; i < _sites.Length; i++)
            {
                EnemyProductionSite site = _sites[i];
                ProductionQueue queue = site != null && site.IsOperational
                    ? site.Queue
                    : null;
                if (queue == null || queue.TotalCount >= queue.Capacity) continue;
                for (int attempt = 0; attempt < totalWeight; attempt++)
                {
                    EnemyProductionWeight entry = SelectWeightedEntry(
                        weights,
                        totalWeight,
                        _productionCursor++);
                    if (entry?.Recipe != null && queue.TryEnqueue(entry.Recipe)) break;
                }
            }
        }

        private static int GetTotalProductionWeight(EnemyProductionWeight[] weights)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i]?.Recipe != null)
                {
                    total = Mathf.Min(1024, total + weights[i].Weight);
                }
            }
            return total;
        }

        private static EnemyProductionWeight SelectWeightedEntry(
            EnemyProductionWeight[] weights,
            int totalWeight,
            int cursor)
        {
            int slot = (cursor % totalWeight + totalWeight) % totalWeight;
            for (int i = 0; i < weights.Length; i++)
            {
                EnemyProductionWeight entry = weights[i];
                if (entry?.Recipe == null)
                {
                    continue;
                }
                if (slot < entry.Weight)
                {
                    return entry;
                }
                slot -= entry.Weight;
            }
            return null;
        }

        private int CountCommandablePopulation()
        {
            int population = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                AiControllableUnit unit = _units[i];
                if (!IsOperational(unit)) continue;
                PopulationOwner owner = unit.GetComponent<PopulationOwner>();
                population += owner != null && owner.ReservedInfluence > 0
                    ? owner.ReservedInfluence
                    : 1;
            }
            return population;
        }

        private int CountOperationalUnits()
        {
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (IsOperational(_units[i]))
                {
                    count++;
                }
            }
            return count;
        }

        private bool HasOperationalProductionSite()
        {
            for (int i = 0; i < _sites.Length; i++)
            {
                if (_sites[i] != null && _sites[i].IsOperational)
                {
                    return true;
                }
            }
            return false;
        }

        private int CountLivingAssaultMembers()
        {
            int count = 0;
            for (int i = 0; i < _assault.Count; i++)
            {
                if (IsOperational(_assault[i])) count++;
            }
            return count;
        }

        private void DiscoverSceneMembers()
        {
            if (_sites == null || _sites.Length == 0)
                _sites = FindObjectsOfType<EnemyProductionSite>(true);
            if (_targets == null || _targets.Length == 0)
                _targets = FindObjectsOfType<StrategicTarget>(true);
            RefreshUnits();
        }

        private void RefreshUnits()
        {
            _units.Clear();
            if (_entityRegistry != null)
            {
                IReadOnlyList<EntityIdentity> entities = _entityRegistry.Entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    TryAddUnit(entities[i]);
                }
                return;
            }
            AiControllableUnit[] found = FindObjectsOfType<AiControllableUnit>(true);
            for (int i = 0; i < found.Length; i++)
            {
                TryAddUnit(found[i] != null ? found[i].Identity : null);
            }
        }

        private void TryAddUnit(EntityIdentity identity)
        {
            if (identity == null || !identity.isActiveAndEnabled ||
                identity.HasAnyRole(UnitRole.Hero) ||
                !identity.HasAnyRole(UnitRole.Combat))
            {
                return;
            }
            FactionMember faction = identity.GetComponent<FactionMember>();
            UnitCommandQueue commands = identity.GetComponent<UnitCommandQueue>();
            if (faction == null || _definition == null ||
                faction.Faction != _definition.Faction || commands == null)
            {
                return;
            }
            AiControllableUnit unit = identity.GetComponent<AiControllableUnit>();
            if (unit == null)
            {
                unit = identity.gameObject.AddComponent<AiControllableUnit>();
            }
            if (!_units.Contains(unit))
            {
                _units.Add(unit);
            }
        }

        private void HandleEntityRegistered(EntityIdentity identity)
        {
            TryAddUnit(identity);
        }

        private void HandleEntityUnregistered(EntityIdentity identity)
        {
            if (identity == null)
            {
                return;
            }
            AiControllableUnit unit = identity.GetComponent<AiControllableUnit>();
            if (unit == null)
            {
                return;
            }
            _units.Remove(unit);
            _assault.Remove(unit);
        }

        private void BindRegistry()
        {
            if (_entityRegistry == null &&
                OriginCore.Core.AppRoot.TryGetInstance(out OriginCore.Core.AppRoot root) &&
                root.Services != null)
            {
                _entityRegistry = root.Services.EntityRegistry;
            }
            if (_entityRegistry == null)
            {
                return;
            }
            _entityRegistry.EntityRegistered -= HandleEntityRegistered;
            _entityRegistry.EntityUnregistered -= HandleEntityUnregistered;
            _entityRegistry.EntityRegistered += HandleEntityRegistered;
            _entityRegistry.EntityUnregistered += HandleEntityUnregistered;
            RefreshUnits();
        }

        private void UnbindRegistry()
        {
            if (_entityRegistry == null)
            {
                return;
            }
            _entityRegistry.EntityRegistered -= HandleEntityRegistered;
            _entityRegistry.EntityUnregistered -= HandleEntityUnregistered;
        }

        private ProductionRecipe[] CollectAllowedRecipes()
        {
            EnemyProductionWeight[] weights = _definition.ProductionWeights;
            var recipes = new List<ProductionRecipe>(weights?.Length ?? 0);
            if (weights != null)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    ProductionRecipe recipe = weights[i]?.Recipe;
                    if (recipe != null && !recipes.Contains(recipe)) recipes.Add(recipe);
                }
            }
            return recipes.ToArray();
        }

        private StrategicTarget FindBestTarget()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (IsTargetValid(_targets[i])) return _targets[i];
            }
            return null;
        }

        private StrategicTarget FindTarget(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId)) return FindBestTarget();
            for (int i = 0; i < _targets.Length; i++)
            {
                StrategicTarget target = _targets[i];
                if (target != null && target.Identity != null &&
                    string.Equals(target.Identity.RuntimeId, runtimeId,
                        StringComparison.Ordinal)) return target;
            }
            return null;
        }

        private bool IsTargetValid(StrategicTarget target)
        {
            if (target == null || !target.isActiveAndEnabled || target.Identity == null)
                return false;
            VitalsComponent vitals = target.GetComponent<VitalsComponent>();
            return vitals == null || vitals.IsAlive;
        }

        private static bool IsOperational(AiControllableUnit unit)
        {
            if (unit == null || !unit.isActiveAndEnabled || unit.Commands == null) return false;
            VitalsComponent vitals = unit.GetComponent<VitalsComponent>();
            return vitals == null || vitals.IsAlive;
        }

        private void CacheComponents()
        {
            _directorId = string.IsNullOrWhiteSpace(_directorId)
                ? "enemy.main"
                : _directorId.Trim().ToLowerInvariant();
            if (_account == null) _account = GetComponent<EnemyProductionAccount>();
            if (_entityRegistry == null &&
                OriginCore.Core.AppRoot.TryGetInstance(out OriginCore.Core.AppRoot root) &&
                root.Services != null)
            {
                _entityRegistry = root.Services.EntityRegistry;
            }
            _sites = _sites ?? Array.Empty<EnemyProductionSite>();
            _targets = _targets ?? Array.Empty<StrategicTarget>();
        }
    }
}
