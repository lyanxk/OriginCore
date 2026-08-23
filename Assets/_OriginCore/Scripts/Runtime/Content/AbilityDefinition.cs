using System;
using OriginCore.Combat;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Content
{
    [Flags]
    public enum AbilityModeMask
    {
        None = 0,
        RTS = 1 << 0,
        ACT = 1 << 1,
        FPS = 1 << 2,
        All = RTS | ACT | FPS
    }

    public enum AbilityTargetType
    {
        None = 0,
        Self = 1,
        Point = 2,
        Direction = 3,
        FriendlyEntity = 4,
        HostileEntity = 5,
        AnyEntity = 6
    }

    public enum AbilityShape
    {
        Single = 0,
        Circle = 1,
        Cone = 2,
        Line = 3,
        Ring = 4
    }

    public enum AbilityEffectType
    {
        Damage = 0,
        Heal = 1,
        RestoreShield = 2,
        RestoreEnergy = 3,
        AddStatModifier = 4,
        Teleport = 5,
        SpawnContent = 6,
        ApplyStatus = 7,
        ReturnToOrigin = 8,
        RefreshCooldown = 9,
        ChangeHeroForm = 10,
        SetFlight = 11,
        RestoreEnergyFromTargetAttack = 12
    }

    [Serializable]
    public struct AbilityEffectDefinition
    {
        [SerializeField] private AbilityEffectType _effectType;
        [SerializeField] private float _magnitude;
        [Min(0f), SerializeField] private float _duration;
        [Min(0f), SerializeField] private float _delay;
        [Min(1), SerializeField] private int _repeatCount;
        [Min(0f), SerializeField] private float _repeatInterval;
        [Min(0f), SerializeField] private float _energyOnKill;
        [Min(0f), SerializeField] private float _energyOnHit;
        [Min(0f), SerializeField] private float _radius;
        [SerializeField] private string _contentId;
        [SerializeField] private DamageType _damageType;
        [SerializeField] private DamageFlags _damageFlags;
        [SerializeField] private StatModifierDefinition _statModifier;

        public AbilityEffectDefinition(
            AbilityEffectType effectType,
            float magnitude = 0f,
            float duration = 0f,
            float delay = 0f,
            int repeatCount = 1,
            float repeatInterval = 0f,
            float energyOnKill = 0f,
            float energyOnHit = 0f,
            float radius = 0f,
            string contentId = "",
            DamageType damageType = DamageType.Physical,
            DamageFlags damageFlags = DamageFlags.None,
            StatModifierDefinition statModifier = default(StatModifierDefinition))
        {
            _effectType = effectType;
            _magnitude = magnitude;
            _duration = Mathf.Max(0f, duration);
            _delay = Mathf.Max(0f, delay);
            _repeatCount = Mathf.Max(1, repeatCount);
            _repeatInterval = Mathf.Max(0f, repeatInterval);
            _energyOnKill = Mathf.Max(0f, energyOnKill);
            _energyOnHit = Mathf.Max(0f, energyOnHit);
            _radius = Mathf.Max(0f, radius);
            _contentId = ContentIdUtility.Normalize(contentId);
            _damageType = damageType;
            _damageFlags = damageFlags;
            _statModifier = statModifier;
        }

        public AbilityEffectType EffectType => _effectType;
        public float Magnitude => _magnitude;
        public float Duration => Mathf.Max(0f, _duration);
        public float Delay => Mathf.Max(0f, _delay);
        public int RepeatCount => Mathf.Max(1, _repeatCount);
        public float RepeatInterval => Mathf.Max(0f, _repeatInterval);
        public float EnergyOnKill => Mathf.Max(0f, _energyOnKill);
        public float EnergyOnHit => Mathf.Max(0f, _energyOnHit);
        public float Radius => Mathf.Max(0f, _radius);
        public string ContentId => _contentId ?? string.Empty;
        public DamageType DamageType => _damageType;
        public DamageFlags DamageFlags => _damageFlags;
        public StatModifierDefinition StatModifier => _statModifier;
    }

    [CreateAssetMenu(fileName = "SO_Ability_New", menuName = "OriginCore/Content/Ability")]
    public sealed class AbilityDefinition : ContentDefinition
    {
        [SerializeField] private AbilityModeMask _modes = AbilityModeMask.RTS;
        [SerializeField] private AbilityTargetType _targetType = AbilityTargetType.Point;
        [SerializeField] private AbilityShape _shape = AbilityShape.Single;
        [Min(0f), SerializeField] private float _range = 8f;
        [Min(0f), SerializeField] private float _radius;
        [Range(0f, 360f), SerializeField] private float _angle = 90f;
        [Min(0f), SerializeField] private float _cooldown = 1f;
        [Min(1), SerializeField] private int _maximumCharges = 1;
        [SerializeField] private string _sharedCooldownGroup;
        [SerializeField] private ResourceCost _resourceCost;
        [Min(0f), SerializeField] private float _energyCost;
        [SerializeField] private AbilityEffectDefinition[] _effects =
            new AbilityEffectDefinition[0];
        [SerializeField] private Sprite _icon;

        public AbilityModeMask Modes => _modes;
        public AbilityTargetType TargetType => _targetType;
        public AbilityShape Shape => _shape;
        public float Range => Mathf.Max(0f, _range);
        public float Radius => Mathf.Max(0f, _radius);
        public float Angle => Mathf.Clamp(_angle, 0f, 360f);
        public float Cooldown => Mathf.Max(0f, _cooldown);
        public int MaximumCharges => Mathf.Max(1, _maximumCharges);
        public string SharedCooldownGroup => _sharedCooldownGroup ?? string.Empty;
        public ResourceCost ResourceCost => _resourceCost;
        public float EnergyCost => Mathf.Max(0f, _energyCost);
        public AbilityEffectDefinition[] Effects => _effects;
        public Sprite Icon => _icon;
        public bool IsPlaceholder => _effects == null || _effects.Length == 0;

        public void Configure(
            AbilityModeMask modes,
            AbilityTargetType targetType,
            AbilityShape shape,
            float range,
            float radius,
            float angle,
            float cooldown,
            int maximumCharges,
            string sharedCooldownGroup,
            ResourceCost resourceCost,
            float energyCost,
            AbilityEffectDefinition[] effects,
            Sprite icon = null)
        {
            _modes = modes;
            _targetType = targetType;
            _shape = shape;
            _range = Mathf.Max(0f, range);
            _radius = Mathf.Max(0f, radius);
            _angle = Mathf.Clamp(angle, 0f, 360f);
            _cooldown = Mathf.Max(0f, cooldown);
            _maximumCharges = Mathf.Max(1, maximumCharges);
            _sharedCooldownGroup = ContentIdUtility.Normalize(sharedCooldownGroup);
            _resourceCost = resourceCost;
            _energyCost = Mathf.Max(0f, energyCost);
            _effects = effects ?? new AbilityEffectDefinition[0];
            _icon = icon;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _range = Mathf.Max(0f, _range);
            _radius = Mathf.Max(0f, _radius);
            _angle = Mathf.Clamp(_angle, 0f, 360f);
            _cooldown = Mathf.Max(0f, _cooldown);
            _maximumCharges = Mathf.Max(1, _maximumCharges);
            _sharedCooldownGroup = ContentIdUtility.Normalize(_sharedCooldownGroup);
            _energyCost = Mathf.Max(0f, _energyCost);
            _effects = _effects ?? new AbilityEffectDefinition[0];
        }
    }
}
