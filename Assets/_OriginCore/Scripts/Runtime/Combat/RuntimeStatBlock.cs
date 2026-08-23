using System;
using System.Collections.Generic;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Combat
{
    public enum RuntimeStatId
    {
        MaxHealth = 0,
        MaxShield = 1,
        Armor = 2,
        MaxEnergy = 3,
        MoveSpeed = 4,
        Acceleration = 5,
        VisionRange = 6,
        AttackDamage = 7,
        AttackInterval = 8,
        AttackRange = 9,
        CooldownRate = 10,
        DamageDealt = 11,
        DamageTaken = 12,
        HealthRegen = 13,
        ShieldRegen = 14,
        EnergyRegen = 15,
        Count = 16
    }

    public enum StatModifierOperation
    {
        Add = 0,
        Multiply = 1,
        Override = 2
    }

    public enum StatStackingRule
    {
        Stack = 0,
        ReplaceExisting = 1,
        RefreshDuration = 2,
        KeepStrongest = 3
    }

    [Serializable]
    public struct StatModifierDefinition
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private RuntimeStatId _statId;
        [SerializeField] private StatModifierOperation _operation;
        [SerializeField] private float _value;
        [SerializeField] private int _priority;
        [Min(0f), SerializeField] private float _duration;
        [SerializeField] private StatStackingRule _stackingRule;

        public StatModifierDefinition(
            string sourceId,
            RuntimeStatId statId,
            StatModifierOperation operation,
            float value,
            int priority = 0,
            float duration = 0f,
            StatStackingRule stackingRule = StatStackingRule.Stack)
        {
            _sourceId = NormalizeSourceId(sourceId);
            _statId = statId;
            _operation = operation;
            _value = IsFinite(value) ? value : 0f;
            _priority = priority;
            _duration = Mathf.Max(0f, duration);
            _stackingRule = stackingRule;
        }

        public string SourceId => NormalizeSourceId(_sourceId);
        public RuntimeStatId StatId => IsRuntimeStat(_statId) ? _statId : RuntimeStatId.MaxHealth;
        public StatModifierOperation Operation => _operation;
        public float Value => IsFinite(_value) ? _value : 0f;
        public int Priority => _priority;
        public float Duration => Mathf.Max(0f, _duration);
        public bool IsPermanent => Duration <= 0f;
        public StatStackingRule StackingRule => _stackingRule;

        internal static bool IsRuntimeStat(RuntimeStatId statId)
        {
            return statId >= RuntimeStatId.MaxHealth && statId < RuntimeStatId.Count;
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static string NormalizeSourceId(string sourceId)
        {
            string normalized = ContentIdUtility.Normalize(sourceId);
            return string.IsNullOrEmpty(normalized) ? "runtime.modifier" : normalized;
        }
    }

    [Serializable]
    public sealed class RuntimeStatModifier
    {
        [SerializeField] private string _sourceId = "runtime.modifier";
        [SerializeField] private RuntimeStatId _statId;
        [SerializeField] private StatModifierOperation _operation;
        [SerializeField] private float _value;
        [SerializeField] private int _priority;
        [Min(0f), SerializeField] private float _durationRemaining;
        [SerializeField] private bool _permanent = true;
        [SerializeField] private StatStackingRule _stackingRule;
        [SerializeField, HideInInspector] private int _sequence;

        public RuntimeStatModifier(StatModifierDefinition definition, int sequence)
        {
            _sourceId = definition.SourceId;
            _statId = definition.StatId;
            _operation = definition.Operation;
            _value = definition.Value;
            _priority = definition.Priority;
            _durationRemaining = definition.Duration;
            _permanent = definition.IsPermanent;
            _stackingRule = definition.StackingRule;
            _sequence = sequence;
        }

        public string SourceId => StatModifierDefinition.NormalizeSourceId(_sourceId);
        public RuntimeStatId StatId => _statId;
        public StatModifierOperation Operation => _operation;
        public float Value => StatModifierDefinition.IsFinite(_value) ? _value : 0f;
        public int Priority => _priority;
        public float DurationRemaining => Mathf.Max(0f, _durationRemaining);
        public bool IsPermanent => _permanent;
        public StatStackingRule StackingRule => _stackingRule;
        internal int Sequence => _sequence;
        internal bool IsExpired => !_permanent && _durationRemaining <= 0f;

        internal void Refresh(StatModifierDefinition definition, int sequence)
        {
            _sourceId = definition.SourceId;
            _statId = definition.StatId;
            _operation = definition.Operation;
            _value = definition.Value;
            _priority = definition.Priority;
            _durationRemaining = definition.Duration;
            _permanent = definition.IsPermanent;
            _stackingRule = definition.StackingRule;
            _sequence = sequence;
        }

        internal void Advance(float deltaTime)
        {
            if (!_permanent)
            {
                _durationRemaining = Mathf.Max(0f, _durationRemaining - Mathf.Max(0f, deltaTime));
            }
        }
    }

    [Serializable]
    public struct RuntimeStatBaseOverride
    {
        [SerializeField] private RuntimeStatId _statId;
        [SerializeField] private float _value;

        public RuntimeStatId StatId => _statId;
        public float Value => StatModifierDefinition.IsFinite(_value) ? _value : 0f;
    }

    [DisallowMultipleComponent]
    public sealed class RuntimeStatBlock : MonoBehaviour
    {
        private const int StatCount = (int)RuntimeStatId.Count;

        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private UnitDefinition _definition;
        [SerializeField] private RuntimeStatBaseOverride[] _baseOverrides = new RuntimeStatBaseOverride[0];
        [SerializeField] private List<RuntimeStatModifier> _modifiers = new List<RuntimeStatModifier>();

        private readonly float[] _baseValues = new float[StatCount];
        private readonly float[] _resolvedValues = new float[StatCount];
        private readonly float[] _previousValues = new float[StatCount];
        private bool _initialized;
        private bool _dirty = true;
        private int _nextSequence = 1;

        public event Action<RuntimeStatBlock, RuntimeStatId, float, float> StatChanged;

        public UnitDefinition Definition => _definition;
        public IReadOnlyList<RuntimeStatModifier> Modifiers => _modifiers;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            CacheDefinition();
            _baseOverrides = _baseOverrides ?? new RuntimeStatBaseOverride[0];
            _modifiers = _modifiers ?? new List<RuntimeStatModifier>();
            _initialized = false;
            _dirty = true;
        }

        private void Update()
        {
            AdvanceDurations(Time.deltaTime);
        }

        public void Configure(UnitDefinition definition, bool clearModifiers = false)
        {
            _definition = definition;
            if (clearModifiers)
            {
                _modifiers.Clear();
            }

            _initialized = false;
            EnsureInitialized();
        }

        public float GetStat(RuntimeStatId statId)
        {
            if (!StatModifierDefinition.IsRuntimeStat(statId))
            {
                return 0f;
            }

            EnsureInitialized();
            ResolveIfDirty();
            return _resolvedValues[(int)statId];
        }

        public float GetBaseStat(RuntimeStatId statId)
        {
            if (!StatModifierDefinition.IsRuntimeStat(statId))
            {
                return 0f;
            }

            EnsureInitialized();
            return _baseValues[(int)statId];
        }

        public void SetBaseStat(RuntimeStatId statId, float value)
        {
            if (!StatModifierDefinition.IsRuntimeStat(statId) || !StatModifierDefinition.IsFinite(value))
            {
                return;
            }

            EnsureInitialized();
            _baseValues[(int)statId] = value;
            _dirty = true;
        }

        public bool AddModifier(StatModifierDefinition definition)
        {
            if (!StatModifierDefinition.IsRuntimeStat(definition.StatId) ||
                !StatModifierDefinition.IsFinite(definition.Value))
            {
                return false;
            }

            EnsureInitialized();
            int existingIndex = FindMatchingModifier(definition.SourceId, definition.StatId);
            if (existingIndex >= 0)
            {
                RuntimeStatModifier existing = _modifiers[existingIndex];
                switch (definition.StackingRule)
                {
                    case StatStackingRule.ReplaceExisting:
                    case StatStackingRule.RefreshDuration:
                        existing.Refresh(definition, _nextSequence++);
                        SortModifiers();
                        _dirty = true;
                        return true;
                    case StatStackingRule.KeepStrongest:
                        if (Mathf.Abs(existing.Value) >= Mathf.Abs(definition.Value))
                        {
                            if (!existing.IsPermanent && definition.Duration > existing.DurationRemaining)
                            {
                                existing.Refresh(definition, _nextSequence++);
                                SortModifiers();
                                _dirty = true;
                            }

                            return true;
                        }

                        existing.Refresh(definition, _nextSequence++);
                        SortModifiers();
                        _dirty = true;
                        return true;
                }
            }

            _modifiers.Add(new RuntimeStatModifier(definition, _nextSequence++));
            SortModifiers();
            _dirty = true;
            return true;
        }

        public int RemoveModifiersBySource(string sourceId)
        {
            EnsureInitialized();
            string normalized = StatModifierDefinition.NormalizeSourceId(sourceId);
            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                RuntimeStatModifier modifier = _modifiers[i];
                if (modifier != null && string.Equals(modifier.SourceId, normalized, StringComparison.Ordinal))
                {
                    _modifiers.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                _dirty = true;
            }

            return removed;
        }

        public void ClearModifiers()
        {
            EnsureInitialized();
            if (_modifiers.Count == 0)
            {
                return;
            }

            _modifiers.Clear();
            _dirty = true;
        }

        public void AdvanceDurations(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            EnsureInitialized();
            bool changed = false;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                RuntimeStatModifier modifier = _modifiers[i];
                if (modifier == null)
                {
                    _modifiers.RemoveAt(i);
                    changed = true;
                    continue;
                }

                modifier.Advance(deltaTime);
                if (modifier.IsExpired)
                {
                    _modifiers.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                _dirty = true;
                ResolveIfDirty();
            }
        }

        public void RebuildFromDefinition()
        {
            _initialized = false;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            CacheDefinition();
            _modifiers = _modifiers ?? new List<RuntimeStatModifier>();
            Array.Clear(_baseValues, 0, _baseValues.Length);
            ApplyDefaultBaseValues();
            ApplyDefinitionBaseValues();
            ApplyBaseOverrides();
            _nextSequence = 1;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                RuntimeStatModifier modifier = _modifiers[i];
                if (modifier == null || modifier.IsExpired ||
                    !StatModifierDefinition.IsRuntimeStat(modifier.StatId))
                {
                    _modifiers.RemoveAt(i);
                    continue;
                }

                _nextSequence = Mathf.Max(_nextSequence, modifier.Sequence + 1);
            }

            SortModifiers();
            Array.Copy(_baseValues, _resolvedValues, StatCount);
            Array.Copy(_baseValues, _previousValues, StatCount);
            _initialized = true;
            _dirty = true;
            ResolveIfDirty(false);
        }

        private void ApplyDefaultBaseValues()
        {
            _baseValues[(int)RuntimeStatId.MaxHealth] = 100f;
            _baseValues[(int)RuntimeStatId.AttackInterval] = 1f;
            _baseValues[(int)RuntimeStatId.CooldownRate] = 1f;
            _baseValues[(int)RuntimeStatId.DamageDealt] = 1f;
            _baseValues[(int)RuntimeStatId.DamageTaken] = 1f;
            _baseValues[(int)RuntimeStatId.ShieldRegen] = 1f;
        }

        private void ApplyDefinitionBaseValues()
        {
            if (_definition == null)
            {
                return;
            }

            _baseValues[(int)RuntimeStatId.MaxHealth] = _definition.MaxHealth;
            _baseValues[(int)RuntimeStatId.MaxShield] = _definition.MaxShield;
            _baseValues[(int)RuntimeStatId.Armor] = _definition.Armor;
            _baseValues[(int)RuntimeStatId.MaxEnergy] = _definition.MaxEnergy;
            _baseValues[(int)RuntimeStatId.MoveSpeed] = _definition.MoveSpeed;
            _baseValues[(int)RuntimeStatId.Acceleration] = _definition.Acceleration;
            _baseValues[(int)RuntimeStatId.VisionRange] = _definition.VisionRange;
            _baseValues[(int)RuntimeStatId.AttackDamage] = _definition.AttackDamage;
            _baseValues[(int)RuntimeStatId.AttackInterval] = _definition.AttackCooldown;
            _baseValues[(int)RuntimeStatId.AttackRange] = _definition.AttackRange;
            _baseValues[(int)RuntimeStatId.CooldownRate] = _definition.CooldownRate;
            _baseValues[(int)RuntimeStatId.DamageDealt] = _definition.DamageDealtMultiplier;
            _baseValues[(int)RuntimeStatId.DamageTaken] = _definition.DamageTakenMultiplier;
            _baseValues[(int)RuntimeStatId.HealthRegen] = _definition.HealthRegen;
            _baseValues[(int)RuntimeStatId.ShieldRegen] = _definition.ShieldRegen;
            _baseValues[(int)RuntimeStatId.EnergyRegen] = _definition.EnergyRegen;
        }

        private void ApplyBaseOverrides()
        {
            if (_baseOverrides == null)
            {
                return;
            }

            for (int i = 0; i < _baseOverrides.Length; i++)
            {
                RuntimeStatBaseOverride value = _baseOverrides[i];
                if (StatModifierDefinition.IsRuntimeStat(value.StatId))
                {
                    _baseValues[(int)value.StatId] = value.Value;
                }
            }
        }

        private void ResolveIfDirty(bool publish = true)
        {
            if (!_dirty)
            {
                return;
            }

            Array.Copy(_resolvedValues, _previousValues, StatCount);
            Array.Copy(_baseValues, _resolvedValues, StatCount);
            for (int i = 0; i < _modifiers.Count; i++)
            {
                RuntimeStatModifier modifier = _modifiers[i];
                if (modifier == null || modifier.IsExpired ||
                    !StatModifierDefinition.IsRuntimeStat(modifier.StatId))
                {
                    continue;
                }

                int index = (int)modifier.StatId;
                switch (modifier.Operation)
                {
                    case StatModifierOperation.Add:
                        _resolvedValues[index] += modifier.Value;
                        break;
                    case StatModifierOperation.Multiply:
                        _resolvedValues[index] *= modifier.Value;
                        break;
                    case StatModifierOperation.Override:
                        _resolvedValues[index] = modifier.Value;
                        break;
                }
            }

            for (int i = 0; i < StatCount; i++)
            {
                _resolvedValues[i] = ClampResolvedValue((RuntimeStatId)i, _resolvedValues[i]);
            }

            _dirty = false;
            if (!publish)
            {
                Array.Copy(_resolvedValues, _previousValues, StatCount);
                return;
            }

            for (int i = 0; i < StatCount; i++)
            {
                if (!Mathf.Approximately(_previousValues[i], _resolvedValues[i]))
                {
                    StatChanged?.Invoke(this, (RuntimeStatId)i, _previousValues[i], _resolvedValues[i]);
                }
            }
        }

        private int FindMatchingModifier(string sourceId, RuntimeStatId statId)
        {
            string normalized = StatModifierDefinition.NormalizeSourceId(sourceId);
            for (int i = 0; i < _modifiers.Count; i++)
            {
                RuntimeStatModifier candidate = _modifiers[i];
                if (candidate != null && candidate.StatId == statId &&
                    string.Equals(candidate.SourceId, normalized, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void SortModifiers()
        {
            _modifiers.Sort(CompareModifiers);
        }

        private static int CompareModifiers(RuntimeStatModifier left, RuntimeStatModifier right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0 ? priority : left.Sequence.CompareTo(right.Sequence);
        }

        private static float ClampResolvedValue(RuntimeStatId statId, float value)
        {
            if (!StatModifierDefinition.IsFinite(value))
            {
                return 0f;
            }

            switch (statId)
            {
                case RuntimeStatId.MaxHealth:
                    return Mathf.Max(1f, value);
                case RuntimeStatId.AttackInterval:
                    return Mathf.Max(0.01f, value);
                default:
                    return Mathf.Max(0f, value);
            }
        }

        private void CacheDefinition()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_definition == null && _identity != null)
            {
                _definition = _identity.Definition;
            }
        }
    }
}
