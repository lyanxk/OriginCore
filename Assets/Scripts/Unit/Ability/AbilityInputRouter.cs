using System;
using System.Collections.Generic;
using Core;
using Gameplay;
using Input;
using Modes;
using Unit.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unit.Ability
{
    [DisallowMultipleComponent]
    public class AbilityInputRouter : MonoBehaviour
    {
        const int RtsAbilitySlotCount = 8;

        [FormerlySerializedAs("commonAbilities")]
        [FormerlySerializedAs("abilities")]
        [SerializeReference]
        UnitAbility[] rtsAbilities = new UnitAbility[RtsAbilitySlotCount];

        [SerializeReference]
        UnitAbility[] actFpsAbilities = Array.Empty<UnitAbility>();

        [SerializeField, HideInInspector]
        bool actFpsAbilitiesInitialized;

        [Header("ACT Target Lock")]
        [SerializeField, Min(0.1f)] float actTargetLockRange = 28f;
        [SerializeField, Range(1f, 180f)] float actTargetLockAngle = 70f;

        readonly List<UnitAbility> _activeAbilities = new List<UnitAbility>(RtsAbilitySlotCount + 8);
        readonly Dictionary<string, UnitAbility> _abilityMap =
            new Dictionary<string, UnitAbility>(StringComparer.OrdinalIgnoreCase);
        readonly Collider[] _actTargetLockBuffer = new Collider[48];

        Vector3 _actFpsAimDirection = Vector3.forward;
        Vector3 _actFpsAimOrigin;
        bool _hasActFpsAimDirection;
        bool _hasActFpsAimOrigin;
        Transform _actLockedTarget;
        UnitCombat _combat;
        TeamAffiliation _teamAffiliation;

        public IReadOnlyList<UnitAbility> Abilities => _activeAbilities;
        public IReadOnlyList<UnitAbility> RtsAbilitySlots => rtsAbilities;
        public IReadOnlyList<UnitAbility> ActFpsAbilitySlots => actFpsAbilities;
        public float ActTargetLockRange => actTargetLockRange;
        public float ActTargetLockAngle => actTargetLockAngle;

        void Awake()
        {
            CacheTargetingReferences();
            Refresh();
        }

        void OnEnable()
        {
            CacheTargetingReferences();
            Refresh();
        }

        void OnDisable()
        {
            ShutdownAbilities();
            RtsAbilityTargetingState.Clear(this);
            ClearActTargetLock();
        }

        void OnValidate()
        {
            actTargetLockRange = Mathf.Max(0.1f, actTargetLockRange);
            actTargetLockAngle = Mathf.Clamp(actTargetLockAngle, 1f, 180f);
            CacheTargetingReferences();
            EnsureAbilitySlots();
            InitializeActFpsAbilitiesIfNeeded();
            Refresh();
        }

        void Update()
        {
            for (int i = 0; i < _activeAbilities.Count; i++)
                _activeAbilities[i].Tick(Time.deltaTime);
        }

        public void Refresh()
        {
            EnsureAbilitySlots();
            InitializeActFpsAbilitiesIfNeeded();
            ShutdownAbilities();

            _activeAbilities.Clear();
            _abilityMap.Clear();

            RegisterAbilityGroup(rtsAbilities);
            RegisterAbilityGroup(actFpsAbilities);
        }

        public void Process(InputIntent intent)
        {
            for (int i = 0; i < actFpsAbilities.Length; i++)
            {
                UnitAbility ability = actFpsAbilities[i];
                if (ability == null)
                    continue;

                ability.ProcessInput(intent);
            }
        }

        public void Process(InputIntent intent, Vector3 aimDirection)
        {
            if (aimDirection.sqrMagnitude > 1e-6f)
            {
                _actFpsAimDirection = aimDirection.normalized;
                _hasActFpsAimDirection = true;
            }

            RefreshActTargetLock();
            Process(intent);
        }

        public void Process(InputIntent intent, Vector3 aimDirection, Vector3 aimOrigin)
        {
            if (aimDirection.sqrMagnitude > 1e-6f)
            {
                _actFpsAimDirection = aimDirection.normalized;
                _hasActFpsAimDirection = true;
            }

            _actFpsAimOrigin = aimOrigin;
            _hasActFpsAimOrigin = true;

            RefreshActTargetLock();
            Process(intent);
        }

        public bool TryGetActFpsAimDirection(out Vector3 aimDirection)
        {
            aimDirection = _actFpsAimDirection;
            return _hasActFpsAimDirection;
        }

        public bool TryGetActFpsAimOrigin(out Vector3 aimOrigin)
        {
            aimOrigin = _actFpsAimOrigin;
            return _hasActFpsAimOrigin;
        }

        public bool TryGetActLockedTarget(out Transform target)
        {
            if (!IsActMode() || !IsValidActLockedTarget(_actLockedTarget))
                ClearActTargetLock();

            target = _actLockedTarget;
            return target != null;
        }

        public bool TryActivate(string abilityId)
        {
            return TryActivate(abilityId, append: false);
        }

        public bool TryActivate(string abilityId, bool append)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
                return false;

            if (!_abilityMap.TryGetValue(abilityId, out UnitAbility ability))
                return false;

            if (!ability.IsAvailableInCurrentMode)
                return false;

            return ability.TryActivate(append);
        }

        public bool HasAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
                return false;

            return _abilityMap.ContainsKey(abilityId);
        }

        void EnsureAbilitySlots()
        {
            rtsAbilities = EnsureRtsSlotArraySize(rtsAbilities);
            if (actFpsAbilities == null)
                actFpsAbilities = Array.Empty<UnitAbility>();
        }

        void RefreshActTargetLock()
        {
            if (!IsActMode() || !_hasActFpsAimDirection)
            {
                ClearActTargetLock();
                return;
            }

            CacheTargetingReferences();

            LayerMask targetMask = _combat != null ? _combat.targetMask : ~0;
            Vector3 origin = ResolveActTargetLockOrigin();
            Vector3 aimDirection = _actFpsAimDirection;
            if (aimDirection.sqrMagnitude <= 1e-6f)
            {
                ClearActTargetLock();
                return;
            }

            aimDirection.Normalize();

            int count = Physics.OverlapSphereNonAlloc(
                origin,
                actTargetLockRange,
                _actTargetLockBuffer,
                targetMask,
                QueryTriggerInteraction.Ignore);

            Health best = null;
            float bestScore = float.MaxValue;
            float halfAngle = Mathf.Max(0.5f, actTargetLockAngle * 0.5f);

            for (int i = 0; i < count; i++)
            {
                Health health = ResolveActLockHealth(_actTargetLockBuffer[i]);
                if (health == null)
                    continue;

                Vector3 toTarget = health.transform.position - origin;
                float distance = toTarget.magnitude;
                if (distance <= 1e-6f)
                    continue;

                float angle = Vector3.Angle(aimDirection, toTarget / distance);
                if (angle > halfAngle)
                    continue;

                float score = angle / halfAngle + distance / actTargetLockRange * 0.25f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = health;
            }

            _actLockedTarget = best != null ? best.transform : null;
        }

        Vector3 ResolveActTargetLockOrigin()
        {
            if (_combat != null && _combat.attackOrigin != null)
                return _combat.attackOrigin.position;

            float height = _combat != null ? _combat.defaultOriginHeight : 1f;
            return transform.position + Vector3.up * height;
        }

        Health ResolveActLockHealth(Component hitComponent)
        {
            if (hitComponent == null)
                return null;

            if (!Health.TryResolve(hitComponent, out Health health) || health == null)
                return null;

            if (health.transform.root == transform.root)
                return null;

            TeamAffiliation ownerTeam = _teamAffiliation != null
                ? _teamAffiliation
                : (_combat != null ? _combat.TeamAffiliation : null);
            TeamAffiliation targetTeam = health.TeamAffiliation;
            if (ownerTeam == null || targetTeam == null)
                return null;

            return ownerTeam.IsHostileTo(targetTeam) ? health : null;
        }

        bool IsValidActLockedTarget(Transform target)
        {
            if (target == null)
                return false;

            return ResolveActLockHealth(target) != null;
        }

        void ClearActTargetLock()
        {
            _actLockedTarget = null;
        }

        void CacheTargetingReferences()
        {
            if (_combat == null)
                _combat = GetComponent<UnitCombat>();
            if (_combat == null)
                _combat = GetComponentInParent<UnitCombat>();
            if (_combat == null)
                _combat = GetComponentInChildren<UnitCombat>(true);

            if (_teamAffiliation == null)
                _teamAffiliation = GetComponent<TeamAffiliation>();
            if (_teamAffiliation == null)
                _teamAffiliation = GetComponentInParent<TeamAffiliation>();
            if (_teamAffiliation == null)
                _teamAffiliation = GetComponentInChildren<TeamAffiliation>(true);
        }

        static bool IsActMode()
        {
            ControlModeManager manager = ControlModeManager.Instance;
            return manager != null && string.Equals(manager.CurrentModeName, "ACT", StringComparison.OrdinalIgnoreCase);
        }

        void ShutdownAbilities()
        {
            for (int i = 0; i < _activeAbilities.Count; i++)
                _activeAbilities[i].Unbind();
        }

        void InitializeActFpsAbilitiesIfNeeded()
        {
            if (actFpsAbilitiesInitialized)
            {
                UpgradeLegacyDefaultActFpsAbilities();
                return;
            }

            bool hasConfiguredAbility = false;
            for (int i = 0; i < actFpsAbilities.Length; i++)
            {
                if (actFpsAbilities[i] == null)
                    continue;

                hasConfiguredAbility = true;
                break;
            }

            actFpsAbilitiesInitialized = true;
            if (hasConfiguredAbility)
            {
                UpgradeLegacyDefaultActFpsAbilities();
                return;
            }

            actFpsAbilities = CreateDefaultActFpsAbilities();
        }

        void UpgradeLegacyDefaultActFpsAbilities()
        {
            if (actFpsAbilities == null || actFpsAbilities.Length != 1)
                return;

            if (actFpsAbilities[0] is not DashAbility)
                return;

            actFpsAbilities = CreateDefaultActFpsAbilities();
        }

        static UnitAbility[] CreateDefaultActFpsAbilities()
        {
            return new UnitAbility[]
            {
                new DashAbility(),
                new FlightAbility(),
                new ChargedShotAbility()
            };
        }

        void RegisterAbilityGroup(UnitAbility[] abilityGroup)
        {
            if (abilityGroup == null)
                return;

            for (int i = 0; i < abilityGroup.Length; i++)
            {
                UnitAbility ability = abilityGroup[i];
                if (ability == null)
                    continue;

                ability.Bind(this, i);
                _activeAbilities.Add(ability);

                if (string.IsNullOrWhiteSpace(ability.AbilityId))
                    continue;

                if (_abilityMap.ContainsKey(ability.AbilityId))
                {
                    Debug.LogWarning(
                        $"Duplicate ability id '{ability.AbilityId}' on '{name}'. Keeping first registration.",
                        this);
                    continue;
                }

                _abilityMap.Add(ability.AbilityId, ability);
            }
        }

        static UnitAbility[] EnsureRtsSlotArraySize(UnitAbility[] source)
        {
            if (source != null && source.Length == RtsAbilitySlotCount)
                return source;

            UnitAbility[] resized = new UnitAbility[RtsAbilitySlotCount];
            if (source == null)
                return resized;

            int copyCount = Mathf.Min(source.Length, resized.Length);
            for (int i = 0; i < copyCount; i++)
                resized[i] = source[i];

            return resized;
        }
    }
}
