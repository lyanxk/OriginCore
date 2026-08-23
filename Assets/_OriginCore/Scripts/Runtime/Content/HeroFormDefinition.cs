using OriginCore.Combat;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_HeroForm_New", menuName = "OriginCore/Content/Hero Form")]
    public sealed class HeroFormDefinition : ContentDefinition
    {
        [SerializeField] private string _requiredPreviousFormId;
        [Min(0f), SerializeField] private float _energyCost;
        [SerializeField] private bool _lockEnergyToMaximum;
        [Min(0.01f), SerializeField] private float _respawnTimeMultiplier = 1f;
        [Range(0f, 1f), SerializeField] private float _isolationDamageBonus;
        [Range(0f, 1f), SerializeField] private float _isolationDamageReduction;
        [SerializeField] private bool _flightEnabled;
        [Min(0f), SerializeField] private float _directModeDamageMultiplier = 1f;
        [Min(1), SerializeField] private int _basicAttackHitCount = 1;
        [Min(0f), SerializeField] private float _basicAttackHitInterval;
        [SerializeField] private DamageType _basicAttackDamageType = DamageType.Physical;
        [SerializeField] private DamageFlags _basicAttackDamageFlags;
        [SerializeField] private AbilityDefinition[] _rtsAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _actAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _fpsAbilities = new AbilityDefinition[0];
        [SerializeField] private StatModifierDefinition[] _statModifiers =
            new StatModifierDefinition[0];

        public string RequiredPreviousFormId => ContentIdUtility.Normalize(_requiredPreviousFormId);
        public float EnergyCost => Mathf.Max(0f, _energyCost);
        public bool LockEnergyToMaximum => _lockEnergyToMaximum;
        public float RespawnTimeMultiplier => Mathf.Max(0.01f, _respawnTimeMultiplier);
        public float IsolationDamageBonus => Mathf.Clamp01(_isolationDamageBonus);
        public float IsolationDamageReduction => Mathf.Clamp01(_isolationDamageReduction);
        public bool FlightEnabled => _flightEnabled;
        public float DirectModeDamageMultiplier => Mathf.Max(0f, _directModeDamageMultiplier);
        public int BasicAttackHitCount => Mathf.Max(1, _basicAttackHitCount);
        public float BasicAttackHitInterval => Mathf.Max(0f, _basicAttackHitInterval);
        public DamageType BasicAttackDamageType => _basicAttackDamageType;
        public DamageFlags BasicAttackDamageFlags => _basicAttackDamageFlags;
        public AbilityDefinition[] RtsAbilities => _rtsAbilities;
        public AbilityDefinition[] ActAbilities => _actAbilities;
        public AbilityDefinition[] FpsAbilities => _fpsAbilities;
        public StatModifierDefinition[] StatModifiers => _statModifiers;

        public void Configure(
            string requiredPreviousFormId,
            float energyCost,
            bool lockEnergyToMaximum,
            float respawnTimeMultiplier,
            float isolationDamageBonus,
            float isolationDamageReduction,
            bool flightEnabled,
            float directModeDamageMultiplier,
            int basicAttackHitCount,
            float basicAttackHitInterval,
            DamageType basicAttackDamageType,
            DamageFlags basicAttackDamageFlags,
            AbilityDefinition[] rtsAbilities,
            AbilityDefinition[] actAbilities,
            AbilityDefinition[] fpsAbilities,
            StatModifierDefinition[] statModifiers)
        {
            _requiredPreviousFormId = ContentIdUtility.Normalize(requiredPreviousFormId);
            _energyCost = Mathf.Max(0f, energyCost);
            _lockEnergyToMaximum = lockEnergyToMaximum;
            _respawnTimeMultiplier = Mathf.Max(0.01f, respawnTimeMultiplier);
            _isolationDamageBonus = Mathf.Clamp01(isolationDamageBonus);
            _isolationDamageReduction = Mathf.Clamp01(isolationDamageReduction);
            _flightEnabled = flightEnabled;
            _directModeDamageMultiplier = Mathf.Max(0f, directModeDamageMultiplier);
            _basicAttackHitCount = Mathf.Max(1, basicAttackHitCount);
            _basicAttackHitInterval = Mathf.Max(0f, basicAttackHitInterval);
            _basicAttackDamageType = basicAttackDamageType;
            _basicAttackDamageFlags = basicAttackDamageFlags;
            _rtsAbilities = rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = fpsAbilities ?? new AbilityDefinition[0];
            _statModifiers = statModifiers ?? new StatModifierDefinition[0];
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _requiredPreviousFormId = ContentIdUtility.Normalize(_requiredPreviousFormId);
            _energyCost = Mathf.Max(0f, _energyCost);
            _respawnTimeMultiplier = Mathf.Max(0.01f, _respawnTimeMultiplier);
            _isolationDamageBonus = Mathf.Clamp01(_isolationDamageBonus);
            _isolationDamageReduction = Mathf.Clamp01(_isolationDamageReduction);
            _directModeDamageMultiplier = Mathf.Max(0f, _directModeDamageMultiplier);
            _basicAttackHitCount = Mathf.Max(1, _basicAttackHitCount);
            _basicAttackHitInterval = Mathf.Max(0f, _basicAttackHitInterval);
            _rtsAbilities = _rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = _actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = _fpsAbilities ?? new AbilityDefinition[0];
            _statModifiers = _statModifiers ?? new StatModifierDefinition[0];
        }
    }
}
