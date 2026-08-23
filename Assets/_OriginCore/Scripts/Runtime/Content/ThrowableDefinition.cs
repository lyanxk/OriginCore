using OriginCore.Combat;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(
        fileName = "SO_Throwable_New",
        menuName = "OriginCore/Content/Throwable")]
    public sealed class ThrowableDefinition : ContentDefinition
    {
        [SerializeField] private ResourceCost _useCost;
        [Min(0f), SerializeField] private float _damage = 25f;
        [Min(0.1f), SerializeField] private float _blastRadius = 3f;
        [Min(0.1f), SerializeField] private float _throwSpeed = 18f;
        [Min(0f), SerializeField] private float _fuseSeconds = 2.5f;
        [Min(0.1f), SerializeField] private float _maximumLifetime = 8f;
        [SerializeField] private bool _detonateOnImpact;
        [SerializeField] private DamageType _damageType = DamageType.Explosive;
        [SerializeField] private DamageFlags _damageFlags = DamageFlags.Area;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Sprite _icon;

        public ResourceCost UseCost => _useCost;
        public float Damage => Mathf.Max(0f, _damage);
        public float BlastRadius => Mathf.Max(0.1f, _blastRadius);
        public float ThrowSpeed => Mathf.Max(0.1f, _throwSpeed);
        public float FuseSeconds => Mathf.Max(0f, _fuseSeconds);
        public float MaximumLifetime => Mathf.Max(0.1f, _maximumLifetime);
        public bool DetonateOnImpact => _detonateOnImpact;
        public DamageType DamageType => _damageType;
        public DamageFlags DamageFlags => _damageFlags | DamageFlags.Area;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public Sprite Icon => _icon;

        public void Configure(
            ResourceCost useCost,
            float damage,
            float blastRadius,
            float throwSpeed,
            float fuseSeconds,
            float maximumLifetime,
            bool detonateOnImpact,
            DamageType damageType,
            DamageFlags damageFlags,
            GameObject projectilePrefab,
            Sprite icon = null)
        {
            _useCost = useCost;
            _damage = Mathf.Max(0f, damage);
            _blastRadius = Mathf.Max(0.1f, blastRadius);
            _throwSpeed = Mathf.Max(0.1f, throwSpeed);
            _fuseSeconds = Mathf.Max(0f, fuseSeconds);
            _maximumLifetime = Mathf.Max(0.1f, maximumLifetime);
            _detonateOnImpact = detonateOnImpact;
            _damageType = damageType;
            _damageFlags = damageFlags | DamageFlags.Area;
            _projectilePrefab = projectilePrefab;
            _icon = icon;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _damage = Mathf.Max(0f, _damage);
            _blastRadius = Mathf.Max(0.1f, _blastRadius);
            _throwSpeed = Mathf.Max(0.1f, _throwSpeed);
            _fuseSeconds = Mathf.Max(0f, _fuseSeconds);
            _maximumLifetime = Mathf.Max(0.1f, _maximumLifetime);
            _damageFlags |= DamageFlags.Area;
        }
    }
}
