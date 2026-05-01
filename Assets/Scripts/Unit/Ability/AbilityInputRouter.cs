using System;
using System.Collections.Generic;
using Input;
using Modes;
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

        readonly List<UnitAbility> _activeAbilities = new List<UnitAbility>(RtsAbilitySlotCount + 8);
        readonly Dictionary<string, UnitAbility> _abilityMap =
            new Dictionary<string, UnitAbility>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<UnitAbility> Abilities => _activeAbilities;
        public IReadOnlyList<UnitAbility> RtsAbilitySlots => rtsAbilities;
        public IReadOnlyList<UnitAbility> ActFpsAbilitySlots => actFpsAbilities;

        void Awake()
        {
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        void OnDisable()
        {
            ShutdownAbilities();
            RtsAbilityTargetingState.Clear(this);
        }

        void OnValidate()
        {
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
