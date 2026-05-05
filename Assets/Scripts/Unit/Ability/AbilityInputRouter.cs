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
        public Transform ActLockedTarget => IsActMode() ? _actLockedTarget : null;

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
            UpdateActFpsAim(aimDirection, ResolveActTargetLockOrigin());
            Process(intent);
        }

        public void Process(InputIntent intent, Vector3 aimDirection, Vector3 aimOrigin)
        {
            UpdateActFpsAim(aimDirection, aimOrigin);
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

        void UpdateActFpsAim(Vector3 aimDirection, Vector3 aimOrigin)
        {
            if (aimDirection.sqrMagnitude > 1e-6f)
            {
                _actFpsAimDirection = aimDirection.normalized;
                _hasActFpsAimDirection = true;
            }

            _actFpsAimOrigin = aimOrigin;
            _hasActFpsAimOrigin = true;

            RefreshActTargetLock();
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
            if (!IsActMode())
            {
                ClearActTargetLock();
                return;
            }

            CacheTargetingReferences();

            LayerMask targetMask = _combat != null ? _combat.targetMask : ~0;
            Vector3 origin = ResolveActTargetLockOrigin();
            TeamAffiliation ownerTeam = ResolveOwnerTeam();
            if (ownerTeam == null)
            {
                ClearActTargetLock();
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                origin,
                actTargetLockRange,
                _actTargetLockBuffer,
                targetMask,
                QueryTriggerInteraction.Ignore);

            Health best = null;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _actTargetLockBuffer[i];
                Health health = ResolveActLockHealth(hitCollider, ownerTeam);
                if (health == null)
                    continue;

                Vector3 targetPoint = hitCollider.bounds.center;
                float sqrDistance = (targetPoint - origin).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                best = health;
            }

            _actLockedTarget = best != null ? best.transform : null;
        }

        Vector3 ResolveActTargetLockOrigin()
        {
            Transform attackOrigin = _combat != null ? _combat.attackOrigin : null;
            if (attackOrigin != null)
                return attackOrigin.position;

            float height = _combat != null ? _combat.defaultOriginHeight : 1f;
            return transform.position + Vector3.up * height;
        }

        TeamAffiliation ResolveOwnerTeam()
        {
            return _teamAffiliation != null ? _teamAffiliation : _combat?.TeamAffiliation;
        }

        Health ResolveActLockHealth(Component hitComponent, TeamAffiliation ownerTeam)
        {
            if (!Health.TryResolve(hitComponent, out Health health))
                return null;

            TeamAffiliation targetTeam = health.TeamAffiliation;
            if (health.transform.root == transform.root || targetTeam == null)
                return null;

            return ownerTeam.IsHostileTo(targetTeam) ? health : null;
        }

        void ClearActTargetLock()
        {
            _actLockedTarget = null;
        }

        void CacheTargetingReferences()
        {
            _combat = _combat ?? GetComponent<UnitCombat>() ?? GetComponentInParent<UnitCombat>() ?? GetComponentInChildren<UnitCombat>(true);
            _teamAffiliation = _teamAffiliation ?? GetComponent<TeamAffiliation>() ?? GetComponentInParent<TeamAffiliation>() ?? GetComponentInChildren<TeamAffiliation>(true);
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
            if (actFpsAbilities == null)
            {
                actFpsAbilities = CreateDefaultActFpsAbilities();
                return;
            }

            if (IsDefaultActFpsAbilitySet(actFpsAbilities))
                return;

            if (!ContainsLegacyWeaponAbility(actFpsAbilities) && !IsLegacySingleDashSet(actFpsAbilities))
                return;

            actFpsAbilities = CreateActFpsAbilitiesFromLegacy(actFpsAbilities);
        }

        static UnitAbility[] CreateDefaultActFpsAbilities()
        {
            return new UnitAbility[]
            {
                new DashAbility(),
                new FlightAbility()
            };
        }

        void RegisterAbilityGroup(IReadOnlyList<UnitAbility> abilityGroup)
        {
            if (abilityGroup == null)
                return;

            for (int i = 0; i < abilityGroup.Count; i++)
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

        static bool IsDefaultActFpsAbilitySet(UnitAbility[] abilities)
        {
            if (abilities.Length != 2)
                return false;

            return abilities[0] is DashAbility && abilities[1] is FlightAbility;
        }

        static bool ContainsLegacyWeaponAbility(UnitAbility[] abilities)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                UnitAbility ability = abilities[i];
                if (ability == null)
                    continue;

                if (ability is not DashAbility && ability is not FlightAbility)
                    return true;
            }

            return false;
        }

        static bool IsLegacySingleDashSet(UnitAbility[] abilities)
        {
            return abilities.Length == 1 && abilities[0] is DashAbility;
        }

        static UnitAbility[] CreateActFpsAbilitiesFromLegacy(UnitAbility[] abilities)
        {
            DashAbility dash = null;
            FlightAbility flight = null;

            for (int i = 0; i < abilities.Length; i++)
            {
                if (dash == null && abilities[i] is DashAbility dashAbility)
                    dash = dashAbility;
                else if (flight == null && abilities[i] is FlightAbility flightAbility)
                    flight = flightAbility;
            }

            return new UnitAbility[]
            {
                dash ?? new DashAbility(),
                flight ?? new FlightAbility()
            };
        }
    }
}
