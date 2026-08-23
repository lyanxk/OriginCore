using System;
using System.Collections;
using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(VitalsComponent))]
    public sealed class AbilityLoadout : MonoBehaviour
    {
        private const int OverlapBufferSize = 64;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private AbilityDefinition[] _rtsAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _actAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _fpsAbilities = new AbilityDefinition[0];
        [SerializeField] private LayerMask _effectMask = ~0;
        [Min(0.1f), SerializeField] private float _teleportNavMeshSearchDistance = 4f;

        private readonly Dictionary<string, AbilityRuntime> _runtimeById =
            new Dictionary<string, AbilityRuntime>(StringComparer.Ordinal);
        private readonly List<AbilityRuntime> _runtimes = new List<AbilityRuntime>();
        private readonly SharedAbilityChargePool _sharedPool = new SharedAbilityChargePool();
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        private readonly HashSet<EntityIdentity> _effectTargets = new HashSet<EntityIdentity>();
        private bool _initialized;

        public event Action<AbilityLoadout, AbilityCastContext, AbilityCastResult> CastFinished;
        public event Action<AbilityLoadout> LoadoutChanged;

        public EntityIdentity Identity => _identity;
        public VitalsComponent Vitals => _vitals;
        public IReadOnlyList<AbilityRuntime> Runtimes => _runtimes;
        public AbilityDefinition[] RtsAbilities => _rtsAbilities;
        public AbilityDefinition[] ActAbilities => _actAbilities;
        public AbilityDefinition[] FpsAbilities => _fpsAbilities;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            CancelPendingEffects();
        }

        private void OnValidate()
        {
            CacheComponents();
            _rtsAbilities = _rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = _actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = _fpsAbilities ?? new AbilityDefinition[0];
            _teleportNavMeshSearchDistance = Mathf.Max(
                0.1f,
                _teleportNavMeshSearchDistance);
            _initialized = false;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            EnsureInitialized();
            float cooldownRate = _stats != null
                ? _stats.GetStat(RuntimeStatId.CooldownRate)
                : 1f;
            float scaledDelta = Time.deltaTime * Mathf.Max(0f, cooldownRate);
            _sharedPool.Tick(scaledDelta, _runtimes);
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].TickLocal(scaledDelta);
            }
        }

        public void Configure(
            AbilityDefinition[] rtsAbilities,
            AbilityDefinition[] actAbilities,
            AbilityDefinition[] fpsAbilities,
            bool refreshCooldowns = false)
        {
            _rtsAbilities = CopyDefinitions(rtsAbilities);
            _actAbilities = CopyDefinitions(actAbilities);
            _fpsAbilities = CopyDefinitions(fpsAbilities);
            CacheComponents();
            RebuildRuntimes(refreshCooldowns);
            _initialized = true;
            LoadoutChanged?.Invoke(this);
        }

        public AbilityDefinition[] GetAbilities(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.RTS:
                    return _rtsAbilities;
                case GameMode.ACT:
                    return _actAbilities;
                case GameMode.FPS:
                    return _fpsAbilities;
                default:
                    return new AbilityDefinition[0];
            }
        }

        public bool TryGetRuntime(string abilityId, out AbilityRuntime runtime)
        {
            EnsureInitialized();
            return _runtimeById.TryGetValue(ContentIdUtility.Normalize(abilityId), out runtime);
        }

        public bool TryCastSlot(
            GameMode mode,
            int slotIndex,
            AbilityTarget target,
            out AbilityCastResult result)
        {
            AbilityDefinition[] abilities = GetAbilities(mode);
            if (slotIndex < 0 || slotIndex >= abilities.Length || abilities[slotIndex] == null)
            {
                result = AbilityCastResult.Fail(
                    string.Empty,
                    AbilityCastFailure.MissingDefinition,
                    "Ability slot is empty.");
                return false;
            }

            return TryCast(abilities[slotIndex], mode, target, out result);
        }

        public bool CanCastSlot(
            GameMode mode,
            int slotIndex,
            ref AbilityTarget target,
            out AbilityCastResult result)
        {
            AbilityDefinition[] abilities = GetAbilities(mode);
            if (slotIndex < 0 || slotIndex >= abilities.Length || abilities[slotIndex] == null)
            {
                result = AbilityCastResult.Fail(
                    string.Empty,
                    AbilityCastFailure.MissingDefinition,
                    "Ability slot is empty.");
                return false;
            }

            EnsureInitialized();
            AbilityDefinition definition = abilities[slotIndex];
            if (!TryValidateCast(
                    definition,
                    mode,
                    ref target,
                    out AbilityCastFailure failure,
                    out string error))
            {
                result = AbilityCastResult.Fail(definition.ContentId, failure, error);
                return false;
            }

            result = AbilityCastResult.Success(definition.ContentId);
            return true;
        }

        public bool TryCast(
            AbilityDefinition definition,
            GameMode mode,
            AbilityTarget target,
            out AbilityCastResult result)
        {
            EnsureInitialized();
            string abilityId = definition != null ? definition.ContentId : string.Empty;
            if (!TryValidateCast(definition, mode, ref target, out AbilityCastFailure failure, out string error))
            {
                result = AbilityCastResult.Fail(abilityId, failure, error);
                PublishRejected(definition, mode, target, result);
                return false;
            }

            AbilityRuntime runtime = _runtimeById[abilityId];
            ResourceService resources = ResolveResourceService();
            ResourceCost resourceCost = definition.ResourceCost;
            float previousEnergy = _vitals.Energy;
            if (!resourceCost.IsZero && (resources == null || !resources.TrySpend(resourceCost)))
            {
                result = AbilityCastResult.Fail(
                    abilityId,
                    AbilityCastFailure.InsufficientResources,
                    "Ability resources could not be committed.");
                PublishRejected(definition, mode, target, result);
                return false;
            }

            if (definition.EnergyCost > 0f && !_vitals.SetEnergy(previousEnergy - definition.EnergyCost))
            {
                resources?.Refund(resourceCost);
                result = AbilityCastResult.Fail(
                    abilityId,
                    AbilityCastFailure.InsufficientEnergy,
                    "Ability energy could not be committed.");
                PublishRejected(definition, mode, target, result);
                return false;
            }

            AbilityCastContext context = new AbilityCastContext(
                this,
                definition,
                _identity,
                _faction != null ? _faction.Faction : FactionId.Neutral,
                mode,
                target,
                transform.position,
                Time.frameCount);
            runtime.CommitCast();
            AbilityEffectDefinition[] effects = definition.Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                StartCoroutine(ExecuteEffectRoutine(context, effects[i]));
            }

            result = AbilityCastResult.Success(abilityId);
            CastFinished?.Invoke(this, context, result);
            return true;
        }

        public void RefreshAllCooldowns()
        {
            EnsureInitialized();
            ResetCooldownsUnsafe();
        }

        public void CancelPendingEffects()
        {
            StopAllCoroutines();
        }

        private void ResetCooldownsUnsafe()
        {
            _sharedPool.Reset();
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].Refresh();
            }
        }

        private bool TryValidateCast(
            AbilityDefinition definition,
            GameMode mode,
            ref AbilityTarget target,
            out AbilityCastFailure failure,
            out string error)
        {
            failure = AbilityCastFailure.None;
            error = string.Empty;
            if (definition == null || !_runtimeById.TryGetValue(definition.ContentId, out AbilityRuntime runtime))
            {
                failure = AbilityCastFailure.MissingDefinition;
                error = "Ability is not in this loadout.";
                return false;
            }

            if (_vitals == null || !_vitals.IsAlive)
            {
                failure = AbilityCastFailure.CasterDead;
                error = "Dead entities cannot cast abilities.";
                return false;
            }

            if (!SupportsMode(definition, mode))
            {
                failure = AbilityCastFailure.WrongMode;
                error = "Ability is not available in the active mode.";
                return false;
            }

            if (!runtime.IsReady)
            {
                failure = runtime.Charges <= 0
                    ? AbilityCastFailure.NoCharges
                    : AbilityCastFailure.OnCooldown;
                error = "Ability has no ready charges.";
                return false;
            }

            if (_vitals.Energy + 0.0001f < definition.EnergyCost)
            {
                failure = AbilityCastFailure.InsufficientEnergy;
                error = "Not enough Energy.";
                return false;
            }

            ResourceService resources = ResolveResourceService();
            if (!definition.ResourceCost.IsZero &&
                (resources == null || !resources.CanAfford(definition.ResourceCost)))
            {
                failure = AbilityCastFailure.InsufficientResources;
                error = "Not enough match resources.";
                return false;
            }

            if (!ValidateTarget(definition, ref target, out error))
            {
                failure = AbilityCastFailure.InvalidTarget;
                return false;
            }

            if (TryResolveTargetPoint(target, out Vector3 point) && definition.Range > 0f)
            {
                Vector3 delta = point - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > definition.Range * definition.Range)
                {
                    failure = AbilityCastFailure.OutOfRange;
                    error = "Ability target is out of range.";
                    return false;
                }
            }

            if (!ValidateEffects(definition, target, out error))
            {
                failure = AbilityCastFailure.EffectUnavailable;
                return false;
            }

            return true;
        }

        private bool ValidateTarget(
            AbilityDefinition definition,
            ref AbilityTarget target,
            out string error)
        {
            switch (definition.TargetType)
            {
                case AbilityTargetType.None:
                    error = string.Empty;
                    return true;
                case AbilityTargetType.Self:
                    target = AbilityTarget.ForEntity(_identity);
                    error = string.Empty;
                    return true;
                case AbilityTargetType.Point:
                    error = target.HasPoint ? string.Empty : "Ability requires a point target.";
                    return target.HasPoint;
                case AbilityTargetType.Direction:
                    error = target.HasDirection ? string.Empty : "Ability requires a direction.";
                    return target.HasDirection;
                case AbilityTargetType.FriendlyEntity:
                case AbilityTargetType.HostileEntity:
                case AbilityTargetType.AnyEntity:
                    return ValidateEntityTarget(definition.TargetType, target.Entity, out error);
                default:
                    error = "Ability target type is unsupported.";
                    return false;
            }
        }

        private bool ValidateEntityTarget(
            AbilityTargetType targetType,
            EntityIdentity entity,
            out string error)
        {
            if (entity == null || !entity.isActiveAndEnabled)
            {
                error = "Ability entity target is unavailable.";
                return false;
            }

            VitalsComponent targetVitals = entity.GetComponent<VitalsComponent>();
            if (targetVitals != null && !targetVitals.IsAlive)
            {
                error = "Ability entity target is dead.";
                return false;
            }

            if (targetType == AbilityTargetType.AnyEntity)
            {
                error = string.Empty;
                return true;
            }

            FactionMember targetFaction = entity.GetComponent<FactionMember>();
            FactionRelationService relations = ResolveFactionRelations();
            if (_faction == null || targetFaction == null || relations == null)
            {
                error = "Ability faction relation is unavailable.";
                return false;
            }

            bool valid = targetType == AbilityTargetType.HostileEntity
                ? relations.AreHostile(_faction.Faction, targetFaction.Faction)
                : relations.AreAllied(_faction.Faction, targetFaction.Faction);
            error = valid ? string.Empty : "Ability target has the wrong faction relation.";
            return valid;
        }

        private bool ValidateEffects(
            AbilityDefinition definition,
            AbilityTarget target,
            out string error)
        {
            AbilityEffectDefinition[] effects = definition.Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                AbilityEffectDefinition effect = effects[i];
                switch (effect.EffectType)
                {
                    case AbilityEffectType.Damage:
                        if (definition.Shape == AbilityShape.Single &&
                            target.Entity == null)
                        {
                            error = "Single-target damage has no entity target.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.Heal:
                    case AbilityEffectType.RestoreShield:
                    case AbilityEffectType.RestoreEnergy:
                        if (ResolveEffectEntity(target) == null)
                        {
                            error = "Vitals effect has no target.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.Teleport:
                        if (!target.HasPoint && !target.HasDirection)
                        {
                            error = "Teleport effect has no destination.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.SpawnContent:
                        if (string.IsNullOrEmpty(effect.ContentId) || ResolveCatalog() == null ||
                            !ResolveCatalog().TryGetUnit(effect.ContentId, out _, out _))
                        {
                            error = "Spawn effect references unavailable content.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.ChangeHeroForm:
                        if (GetComponent<HeroFormStateMachine>() == null)
                        {
                            error = "Hero form state machine is unavailable.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.RestoreEnergyFromTargetAttack:
                        if (target.Entity == null)
                        {
                            error = "Target-attack Energy restoration has no entity target.";
                            return false;
                        }
                        break;
                    case AbilityEffectType.ApplyStatus:
                        error = "Status effect executor is not configured.";
                        return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private IEnumerator ExecuteEffectRoutine(
            AbilityCastContext context,
            AbilityEffectDefinition effect)
        {
            if (effect.Delay > 0f)
            {
                yield return new WaitForSeconds(effect.Delay);
            }

            int repeatCount = effect.RepeatCount;
            for (int i = 0; i < repeatCount; i++)
            {
                ExecuteSingleEffect(context, effect);
                if (i + 1 < repeatCount && effect.RepeatInterval > 0f)
                {
                    yield return new WaitForSeconds(effect.RepeatInterval);
                }
            }
        }

        private void ExecuteSingleEffect(
            AbilityCastContext context,
            AbilityEffectDefinition effect)
        {
            switch (effect.EffectType)
            {
                case AbilityEffectType.Damage:
                    ExecuteDamage(context, effect);
                    break;
                case AbilityEffectType.Heal:
                    VitalsComponent healVitals = ResolveEffectVitals(context.Target);
                    if (healVitals != null)
                    {
                        healVitals.SetHealth(healVitals.Health + effect.Magnitude);
                    }
                    break;
                case AbilityEffectType.RestoreShield:
                    VitalsComponent shieldVitals = ResolveEffectVitals(context.Target);
                    if (shieldVitals != null)
                    {
                        shieldVitals.SetShield(shieldVitals.Shield + effect.Magnitude);
                    }
                    break;
                case AbilityEffectType.RestoreEnergy:
                    VitalsComponent energyVitals = ResolveEffectVitals(context.Target);
                    if (energyVitals != null)
                    {
                        energyVitals.SetEnergy(energyVitals.Energy + effect.Magnitude);
                    }
                    break;
                case AbilityEffectType.AddStatModifier:
                    ResolveEffectEntity(context.Target)?.GetComponent<RuntimeStatBlock>()?
                        .AddModifier(effect.StatModifier);
                    break;
                case AbilityEffectType.Teleport:
                    TeleportCaster(
                        context.Target,
                        effect.Magnitude > 0f ? effect.Magnitude : context.Definition.Range);
                    break;
                case AbilityEffectType.SpawnContent:
                    SpawnContent(effect.ContentId, ResolveEffectPoint(context));
                    break;
                case AbilityEffectType.ReturnToOrigin:
                    TeleportTo(context.Origin);
                    break;
                case AbilityEffectType.RefreshCooldown:
                    if (TryGetRuntime(effect.ContentId, out AbilityRuntime runtime))
                    {
                        runtime.Refresh();
                    }
                    break;
                case AbilityEffectType.ChangeHeroForm:
                    GetComponent<HeroFormStateMachine>()?.TryEnterForm(effect.ContentId, out _);
                    break;
                case AbilityEffectType.SetFlight:
                    GetComponent<FlightController>()?.SetFlightEnabled(effect.Magnitude > 0f);
                    break;
                case AbilityEffectType.RestoreEnergyFromTargetAttack:
                    RestoreEnergyFromTargetAttack(context.Target, effect.Magnitude);
                    break;
            }
        }

        private void ExecuteDamage(AbilityCastContext context, AbilityEffectDefinition effect)
        {
            if (context.Definition.Shape == AbilityShape.Single && effect.Radius <= 0f)
            {
                ApplyDamageTo(context, effect, context.Target.Entity);
                return;
            }

            Vector3 center = context.Definition.Shape == AbilityShape.Cone ||
                             context.Definition.Shape == AbilityShape.Line
                ? transform.position
                : ResolveEffectPoint(context);
            float radius = Mathf.Max(
                0.1f,
                effect.Radius > 0f ? effect.Radius : context.Definition.Radius);
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                _overlapBuffer,
                _effectMask,
                QueryTriggerInteraction.Collide);
            _effectTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                EntityIdentity target = _overlapBuffer[i] != null
                    ? _overlapBuffer[i].GetComponentInParent<EntityIdentity>()
                    : null;
                if (target == null || target == _identity || !_effectTargets.Add(target) ||
                    !IsHostile(target) || !MatchesShape(context, target.transform.position, center, radius))
                {
                    continue;
                }

                ApplyDamageTo(context, effect, target);
            }
        }

        private void ApplyDamageTo(
            AbilityCastContext context,
            AbilityEffectDefinition effect,
            EntityIdentity target)
        {
            DamageReceiver receiver = target != null ? target.GetComponent<DamageReceiver>() : null;
            if (receiver == null)
            {
                return;
            }

            if (receiver.TryReceiveDamage(
                    new DamageInfo(
                        effect.Magnitude,
                        _identity,
                        context.CasterFaction,
                        effect.DamageType,
                        effect.DamageFlags,
                        target.transform.position,
                        true,
                        context.Definition.ContentId),
                    out DamageResult result))
            {
                float energy = effect.EnergyOnHit;
                if (result.Killed)
                {
                    energy += effect.EnergyOnKill;
                }

                if (energy > 0f)
                {
                    _vitals.SetEnergy(_vitals.Energy + energy);
                }
            }
        }

        private bool MatchesShape(
            AbilityCastContext context,
            Vector3 target,
            Vector3 center,
            float radius)
        {
            if (context.Definition.Shape == AbilityShape.Circle ||
                context.Definition.Shape == AbilityShape.Ring)
            {
                return true;
            }

            Vector3 forward = context.Target.HasDirection
                ? context.Target.Direction
                : center - transform.position;
            forward.y = 0f;
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f || toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            if (context.Definition.Shape == AbilityShape.Cone)
            {
                return Vector3.Angle(forward, toTarget) <= context.Definition.Angle * 0.5f;
            }

            if (context.Definition.Shape == AbilityShape.Line)
            {
                Vector3 end = transform.position + forward.normalized * context.Definition.Range;
                return DistanceToSegment(target, transform.position, end) <= radius;
            }

            return true;
        }

        private void TeleportCaster(AbilityTarget target, float distance)
        {
            if (target.Entity != null)
            {
                Vector3 away = transform.position - target.Entity.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude <= 0.0001f)
                {
                    away = -target.Entity.transform.forward;
                    away.y = 0f;
                }

                float separation = Mathf.Max(0.5f, distance);
                Vector3 entityDestination = target.Entity.transform.position +
                                            away.normalized * separation;
                TeleportTo(entityDestination);
                Vector3 face = target.Entity.transform.position - transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
                }

                return;
            }

            Vector3 destination = target.HasPoint
                ? target.Point
                : transform.position + target.Direction * Mathf.Max(0f, distance);
            TeleportTo(destination);
        }

        private void RestoreEnergyFromTargetAttack(AbilityTarget target, float fraction)
        {
            if (_vitals == null || target.Entity == null)
            {
                return;
            }

            RuntimeStatBlock targetStats = target.Entity.GetComponent<RuntimeStatBlock>();
            float attackDamage = targetStats != null
                ? targetStats.GetStat(RuntimeStatId.AttackDamage)
                : target.Entity.Definition != null
                    ? target.Entity.Definition.AttackDamage
                    : 0f;
            float restored = Mathf.Max(0f, attackDamage) * Mathf.Max(0f, fraction);
            if (restored > 0f)
            {
                _vitals.SetEnergy(_vitals.Energy + restored);
            }
        }

        private bool TeleportTo(Vector3 destination)
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            float searchDistance = Mathf.Max(0.1f, _teleportNavMeshSearchDistance);
            Vector3 resolvedDestination;
            bool resolved = agent != null && agent.enabled && agent.isOnNavMesh
                ? NavMeshPositionResolver.TryResolveReachablePosition(
                    agent,
                    destination,
                    Mathf.Min(1.5f, searchDistance),
                    searchDistance,
                    out resolvedDestination)
                : NavMeshPositionResolver.TryResolvePosition(
                    destination,
                    areaMask,
                    Mathf.Min(1.5f, searchDistance),
                    searchDistance,
                    out resolvedDestination);
            if (!resolved)
            {
                return false;
            }

            CharacterController controller = GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            Vector3 originalPosition = transform.position;
            if (controller != null)
            {
                controller.enabled = false;
            }

            if (agent != null && agent.enabled)
            {
                bool warped = agent.Warp(resolvedDestination) && agent.isOnNavMesh;
                if (!warped)
                {
                    if (!NavMeshPositionResolver.TryResolvePosition(
                            originalPosition,
                            areaMask,
                            0.5f,
                            searchDistance,
                            out Vector3 recoveryPosition) ||
                        !agent.Warp(recoveryPosition))
                    {
                        agent.enabled = false;
                        transform.position = originalPosition;
                        agent.enabled = true;
                    }

                    if (controller != null)
                    {
                        controller.enabled = wasEnabled;
                    }

                    return false;
                }
            }
            else
            {
                transform.position = resolvedDestination;
            }

            if (controller != null)
            {
                controller.enabled = wasEnabled;
            }

            return true;
        }

        private void SpawnContent(string contentId, Vector3 position)
        {
            ContentCatalog catalog = ResolveCatalog();
            if (catalog == null || !catalog.TryGetUnit(
                    contentId,
                    out UnitDefinition definition,
                    out GameObject prefab))
            {
                return;
            }

            GameObject instance = Instantiate(prefab, position, transform.rotation);
            EntityIdentity identity = instance.GetComponent<EntityIdentity>();
            if (identity != null)
            {
                identity.ConfigureDefinition(definition);
                identity.MarkRuntimeSpawned();
            }

            FactionMember member = instance.GetComponent<FactionMember>();
            if (member != null && _faction != null)
            {
                member.SetFaction(_faction.Faction);
            }
        }

        private void RebuildRuntimes(bool refreshCooldowns)
        {
            Dictionary<string, AbilityRuntime> previous =
                new Dictionary<string, AbilityRuntime>(_runtimeById, StringComparer.Ordinal);
            _runtimeById.Clear();
            _runtimes.Clear();
            AddDefinitions(_rtsAbilities, previous);
            AddDefinitions(_actAbilities, previous);
            AddDefinitions(_fpsAbilities, previous);
            if (refreshCooldowns)
            {
                ResetCooldownsUnsafe();
            }
        }

        private void AddDefinitions(
            AbilityDefinition[] definitions,
            Dictionary<string, AbilityRuntime> previous)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                AbilityDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.ContentId) ||
                    _runtimeById.ContainsKey(definition.ContentId))
                {
                    continue;
                }

                AbilityRuntime runtime;
                if (!previous.TryGetValue(definition.ContentId, out runtime) ||
                    runtime.Definition != definition)
                {
                    runtime = new AbilityRuntime(definition, _sharedPool);
                }

                _runtimeById.Add(definition.ContentId, runtime);
                _runtimes.Add(runtime);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            CacheComponents();
            _rtsAbilities = _rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = _actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = _fpsAbilities ?? new AbilityDefinition[0];
            RebuildRuntimes(false);
            _initialized = true;
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
            if (_vitals == null) _vitals = GetComponent<VitalsComponent>();
            if (_stats == null) _stats = GetComponent<RuntimeStatBlock>();
        }

        private void PublishRejected(
            AbilityDefinition definition,
            GameMode mode,
            AbilityTarget target,
            AbilityCastResult result)
        {
            AbilityCastContext context = new AbilityCastContext(
                this,
                definition,
                _identity,
                _faction != null ? _faction.Faction : FactionId.Neutral,
                mode,
                target,
                transform.position,
                Time.frameCount);
            CastFinished?.Invoke(this, context, result);
        }

        private EntityIdentity ResolveEffectEntity(AbilityTarget target)
        {
            return target.Entity != null ? target.Entity : _identity;
        }

        private VitalsComponent ResolveEffectVitals(AbilityTarget target)
        {
            EntityIdentity entity = ResolveEffectEntity(target);
            return entity != null ? entity.GetComponent<VitalsComponent>() : null;
        }

        private Vector3 ResolveEffectPoint(AbilityCastContext context)
        {
            if (context.Target.HasPoint) return context.Target.Point;
            if (context.Target.Entity != null) return context.Target.Entity.transform.position;
            if (context.Target.HasDirection)
            {
                return transform.position + context.Target.Direction * context.Definition.Range;
            }

            return transform.position;
        }

        private bool IsHostile(EntityIdentity target)
        {
            FactionRelationService relations = ResolveFactionRelations();
            FactionMember targetFaction = target != null ? target.GetComponent<FactionMember>() : null;
            return relations != null && _faction != null && targetFaction != null &&
                   relations.AreHostile(_faction.Faction, targetFaction.Faction);
        }

        private ResourceService ResolveResourceService()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null
                ? root.Services.ResourceService
                : null;
        }

        private FactionRelationService ResolveFactionRelations()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null
                ? root.Services.FactionRelations
                : null;
        }

        private ContentCatalog ResolveCatalog()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.ContentCatalog != null
                ? root.Services.ContentCatalog.Catalog
                : null;
        }

        private static bool SupportsMode(AbilityDefinition definition, GameMode mode)
        {
            AbilityModeMask mask = mode == GameMode.RTS
                ? AbilityModeMask.RTS
                : mode == GameMode.ACT ? AbilityModeMask.ACT : AbilityModeMask.FPS;
            return (definition.Modes & mask) != 0;
        }

        private static bool TryResolveTargetPoint(AbilityTarget target, out Vector3 point)
        {
            if (target.Entity != null)
            {
                point = target.Entity.transform.position;
                return true;
            }

            if (target.HasPoint)
            {
                point = target.Point;
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return Vector3.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return Vector3.Distance(point, start + segment * t);
        }

        private static AbilityDefinition[] CopyDefinitions(AbilityDefinition[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new AbilityDefinition[0];
            }

            AbilityDefinition[] copy = new AbilityDefinition[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
