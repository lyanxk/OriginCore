using OriginCore.Content;
using UnityEngine;

namespace OriginCore.Gameplay
{
    [CreateAssetMenu(fileName = "SO_Unit_New", menuName = "OriginCore/Gameplay/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject, IContentDefinition
    {
        [Header("Identity")]
        [SerializeField] private string _archetypeId = "unit.new";
        [SerializeField] private string _displayName = "Unit";
        [SerializeField] private UnitRole _roles = UnitRole.Combat;
        [SerializeField] private FactionId _defaultFaction = FactionId.Friendly;

        [Header("Movement Placeholder")]
        [Min(0f), SerializeField] private float _moveSpeed = 4f;
        [Min(0f), SerializeField] private float _acceleration = 16f;
        [Min(0f), SerializeField] private float _angularSpeed = 720f;
        [Min(0f), SerializeField] private float _stoppingDistance = 0.2f;

        [Header("Senses Placeholder")]
        [Min(0f), SerializeField] private float _visionRange = 12f;

        [Header("Vitals")]
        [Min(1f), SerializeField] private float _maxHealth = 100f;
        [Min(0f), SerializeField] private float _maxShield;
        [Min(0f), SerializeField] private float _maxEnergy;
        [Min(0f), SerializeField] private float _armor;
        [Min(0f), SerializeField] private float _healthRegen;
        [Min(0f), SerializeField] private float _shieldRegen = 1f;
        [Min(0f), SerializeField] private float _energyRegen;

        [Header("Attack Placeholder")]
        [Min(0f), SerializeField] private float _attackRange = 2f;
        [Min(0f), SerializeField] private float _attackDamage = 10f;
        [Min(0f), SerializeField] private float _attackCooldown = 1f;

        [Header("Runtime Modifiers")]
        [Min(0f), SerializeField] private float _cooldownRate = 1f;
        [Min(0f), SerializeField] private float _damageDealtMultiplier = 1f;
        [Min(0f), SerializeField] private float _damageTakenMultiplier = 1f;
        [Min(0f), SerializeField] private float _lifetimeSeconds;

        public string ArchetypeId => _archetypeId;
        public string ContentId => _archetypeId;
        public string DisplayName => _displayName;
        public UnitRole Roles => _roles;
        public FactionId DefaultFaction => _defaultFaction;
        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float AngularSpeed => _angularSpeed;
        public float StoppingDistance => _stoppingDistance;
        public float VisionRange => _visionRange;
        public float MaxHealth => _maxHealth;
        public float MaxShield => _maxShield;
        public float MaxEnergy => _maxEnergy;
        public float Armor => _armor;
        public float HealthRegen => _healthRegen;
        public float ShieldRegen => _shieldRegen;
        public float EnergyRegen => _energyRegen;
        public float AttackRange => _attackRange;
        public float AttackDamage => _attackDamage;
        public float AttackCooldown => _attackCooldown;
        public float CooldownRate => _cooldownRate;
        public float DamageDealtMultiplier => _damageDealtMultiplier;
        public float DamageTakenMultiplier => _damageTakenMultiplier;
        public float LifetimeSeconds => _lifetimeSeconds;

        public void Configure(
            string archetypeId,
            string displayName,
            UnitRole roles,
            FactionId defaultFaction,
            float moveSpeed,
            float acceleration,
            float angularSpeed,
            float stoppingDistance,
            float visionRange,
            float maxHealth,
            float maxShield,
            float maxEnergy,
            float attackRange,
            float attackDamage,
            float attackCooldown)
        {
            _archetypeId = string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "Unit" : displayName.Trim();
            _roles = roles;
            _defaultFaction = defaultFaction;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _acceleration = Mathf.Max(0f, acceleration);
            _angularSpeed = Mathf.Max(0f, angularSpeed);
            _stoppingDistance = Mathf.Max(0f, stoppingDistance);
            _visionRange = Mathf.Max(0f, visionRange);
            _maxHealth = Mathf.Max(1f, maxHealth);
            _maxShield = Mathf.Max(0f, maxShield);
            _maxEnergy = Mathf.Max(0f, maxEnergy);
            _attackRange = Mathf.Max(0f, attackRange);
            _attackDamage = Mathf.Max(0f, attackDamage);
            _attackCooldown = Mathf.Max(0f, attackCooldown);
        }

        public void ConfigureExtendedStats(
            float armor,
            float healthRegen,
            float shieldRegen,
            float energyRegen,
            float cooldownRate = 1f,
            float damageDealtMultiplier = 1f,
            float damageTakenMultiplier = 1f,
            float lifetimeSeconds = 0f)
        {
            _armor = Mathf.Max(0f, armor);
            _healthRegen = Mathf.Max(0f, healthRegen);
            _shieldRegen = Mathf.Max(0f, shieldRegen);
            _energyRegen = Mathf.Max(0f, energyRegen);
            _cooldownRate = Mathf.Max(0f, cooldownRate);
            _damageDealtMultiplier = Mathf.Max(0f, damageDealtMultiplier);
            _damageTakenMultiplier = Mathf.Max(0f, damageTakenMultiplier);
            _lifetimeSeconds = Mathf.Max(0f, lifetimeSeconds);
        }

        private void OnValidate()
        {
            _archetypeId = string.IsNullOrWhiteSpace(_archetypeId) ? string.Empty : _archetypeId.Trim();
            _displayName = string.IsNullOrWhiteSpace(_displayName) ? "Unit" : _displayName.Trim();
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _acceleration = Mathf.Max(0f, _acceleration);
            _angularSpeed = Mathf.Max(0f, _angularSpeed);
            _stoppingDistance = Mathf.Max(0f, _stoppingDistance);
            _visionRange = Mathf.Max(0f, _visionRange);
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _maxShield = Mathf.Max(0f, _maxShield);
            _maxEnergy = Mathf.Max(0f, _maxEnergy);
            _armor = Mathf.Max(0f, _armor);
            _healthRegen = Mathf.Max(0f, _healthRegen);
            _shieldRegen = Mathf.Max(0f, _shieldRegen);
            _energyRegen = Mathf.Max(0f, _energyRegen);
            _attackRange = Mathf.Max(0f, _attackRange);
            _attackDamage = Mathf.Max(0f, _attackDamage);
            _attackCooldown = Mathf.Max(0f, _attackCooldown);
            _cooldownRate = Mathf.Max(0f, _cooldownRate);
            _damageDealtMultiplier = Mathf.Max(0f, _damageDealtMultiplier);
            _damageTakenMultiplier = Mathf.Max(0f, _damageTakenMultiplier);
            _lifetimeSeconds = Mathf.Max(0f, _lifetimeSeconds);
        }
    }
}
