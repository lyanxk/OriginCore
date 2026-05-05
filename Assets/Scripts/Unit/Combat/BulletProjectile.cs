using Gameplay;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class BulletProjectile : MonoBehaviour
    {
        const string BulletLayerName = "Bullet";

        [Header("Movement")]
        [Min(0.01f)] [SerializeField] float speed = 28f;
        [Min(0.01f)] [SerializeField] float maxDistance = 30f;
        [Min(0.01f)] [SerializeField] float maxLifetime = 3f;
        [Min(0f)] [SerializeField] float collisionRadius = 0.09f;

        [Header("Hit")]
        [Min(0f)] [SerializeField] float damage = 10f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] GameObject hitEffectPrefab;
        [Min(0f)] [SerializeField] float hitEffectSurfaceOffset = 0.02f;

        [Header("Launch")]
        [SerializeField] bool launchOnHit;
        [Min(0f)] [SerializeField] float launchVerticalSpeed = 6f;
        [Min(0f)] [SerializeField] float launchPlanarSpeed = 1.5f;
        [Min(0f)] [SerializeField] float launchPlanarDuration = 0.18f;

        readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        UnitCombat _sourceCombat;
        TeamAffiliation _sourceTeam;
        Transform _sourceRoot;
        Vector3 _direction;
        Vector3 _spawnPosition;
        Vector3 _lastPosition;
        float _elapsed;
        bool _hasImpacted;

        public float Damage => damage;
        public float Speed => speed;
        public TeamAffiliation SourceTeam => _sourceTeam;

        public void ConfigureCollisionRadius(float radius)
        {
            collisionRadius = Mathf.Max(0f, radius);
        }

        public void ConfigureLaunch(float verticalSpeed, float planarSpeed, float planarDuration)
        {
            launchVerticalSpeed = Mathf.Max(0f, verticalSpeed);
            launchPlanarSpeed = Mathf.Max(0f, planarSpeed);
            launchPlanarDuration = Mathf.Max(0f, planarDuration);
            launchOnHit = launchVerticalSpeed > 0f || launchPlanarSpeed > 0f;
        }

        void Awake()
        {
            AssignBulletLayerIfAvailable(transform);
            ExcludeBulletLayerFromHitMask();

            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
                ownCollider.isTrigger = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }

        void OnEnable()
        {
            ResetFlightState();
        }

        void Update()
        {
            if (_hasImpacted)
                return;

            float dt = Time.deltaTime;
            _elapsed += dt;

            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + _direction * (speed * dt);
            float stepDistance = Vector3.Distance(currentPosition, nextPosition);

            if (stepDistance > 0f && TryFindImpact(currentPosition, stepDistance, out RaycastHit hit))
            {
                transform.position = hit.point;
                Impact(hit.collider, hit.point, hit.normal);
                return;
            }

            transform.position = nextPosition;
            _lastPosition = nextPosition;

            float travelled = Vector3.Distance(_spawnPosition, nextPosition);
            if (_elapsed >= maxLifetime || travelled >= maxDistance)
                Impact(null, nextPosition, -_direction);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted || ShouldIgnore(other))
                return;

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 normal = transform.position - point;
            if (normal.sqrMagnitude <= 1e-6f)
                normal = -_direction;

            Impact(other, point, normal.normalized);
        }

        public void Initialize(UnitCombat source, Vector3 direction, float amount)
        {
            Initialize(source, direction, amount, hitMask, hitEffectPrefab, speed, maxDistance);
        }

        public void Initialize(
            UnitCombat source,
            Vector3 direction,
            float amount,
            GameObject impactEffectPrefab)
        {
            Initialize(source, direction, amount, hitMask, impactEffectPrefab, speed, maxDistance);
        }

        public void Initialize(
            UnitCombat source,
            Vector3 direction,
            float amount,
            LayerMask projectileHitMask,
            GameObject impactEffectPrefab = null,
            float speedOverride = -1f,
            float maxDistanceOverride = -1f)
        {
            _sourceCombat = source;
            _sourceTeam = source != null ? source.TeamAffiliation : null;
            _sourceRoot = source != null ? source.transform.root : null;
            damage = Mathf.Max(0f, amount);
            hitMask = projectileHitMask;
            ExcludeBulletLayerFromHitMask();

            if (impactEffectPrefab != null)
                hitEffectPrefab = impactEffectPrefab;
            if (speedOverride > 0f)
                speed = speedOverride;
            if (maxDistanceOverride > 0f)
                maxDistance = maxDistanceOverride;

            SetDirection(direction);
            ResetFlightState();
        }

        public void Initialize(
            TeamAffiliation sourceTeam,
            Transform sourceRoot,
            Vector3 direction,
            float amount)
        {
            _sourceCombat = null;
            _sourceTeam = sourceTeam;
            _sourceRoot = sourceRoot != null ? sourceRoot.root : null;
            damage = Mathf.Max(0f, amount);

            SetDirection(direction);
            ResetFlightState();
        }

        void ResetFlightState()
        {
            _hasImpacted = false;
            _elapsed = 0f;
            _spawnPosition = transform.position;
            _lastPosition = transform.position;

            if (_direction.sqrMagnitude <= 1e-6f)
                SetDirection(transform.forward);
        }

        void SetDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 1e-6f)
                direction = transform.forward;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            _direction = direction.normalized;
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        bool TryFindImpact(Vector3 origin, float distance, out RaycastHit bestHit)
        {
            int hitCount = collisionRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    origin,
                    collisionRadius,
                    _direction,
                    _hitBuffer,
                    distance,
                    hitMask,
                    triggerInteraction)
                : Physics.RaycastNonAlloc(
                    origin,
                    _direction,
                    _hitBuffer,
                    distance,
                    hitMask,
                    triggerInteraction);

            bestHit = default;
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (ShouldIgnore(hit.collider))
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestHit = hit;
                bestDistance = hit.distance;
                found = true;
            }

            return found;
        }

        bool ShouldIgnore(Collider other)
        {
            if (other == null)
                return true;

            if (_sourceRoot != null && other.transform.root == _sourceRoot)
                return true;

            int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
            if (bulletLayer >= 0 && other.gameObject.layer == bulletLayer)
                return true;

            int otherLayerMask = 1 << other.gameObject.layer;
            return (hitMask.value & otherLayerMask) == 0;
        }

        void ExcludeBulletLayerFromHitMask()
        {
            int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
            if (bulletLayer < 0)
                return;

            hitMask = hitMask.value & ~(1 << bulletLayer);
        }

        static void AssignBulletLayerIfAvailable(Transform root)
        {
            int bulletLayer = LayerMask.NameToLayer(BulletLayerName);
            if (bulletLayer < 0 || root == null)
                return;

            root.gameObject.layer = bulletLayer;
            for (int i = 0; i < root.childCount; i++)
                AssignBulletLayerIfAvailable(root.GetChild(i));
        }

        void Impact(Collider hitCollider, Vector3 point, Vector3 normal)
        {
            if (_hasImpacted)
                return;

            _hasImpacted = true;
            TryDamage(hitCollider);
            SpawnHitEffect(point, normal);
            Destroy(gameObject);
        }

        void TryDamage(Component hitComponent)
        {
            if (damage <= 0f)
                return;

            if (!Health.TryResolve(hitComponent, out Health health))
                return;

            if (_sourceRoot != null && health.transform.root == _sourceRoot)
                return;

            TeamAffiliation targetTeam = health.TeamAffiliation;
            if (_sourceTeam == null || targetTeam == null || !_sourceTeam.IsHostileTo(targetTeam))
                return;

            bool damaged = _sourceCombat != null
                ? _sourceCombat.TryApplyDamage(health, damage)
                : true;

            if (_sourceCombat == null)
                health.TakeDamage(damage);

            if (damaged)
                TryLaunch(health);
        }

        void TryLaunch(Health health)
        {
            if (!launchOnHit || health == null)
                return;

            UnitBase targetUnit = health.GetComponent<UnitBase>() ?? health.GetComponentInParent<UnitBase>();
            if (targetUnit != null)
            {
                Vector3 planarVelocity = _direction;
                planarVelocity.y = 0f;
                if (planarVelocity.sqrMagnitude > 1e-6f)
                    planarVelocity = planarVelocity.normalized * launchPlanarSpeed;

                targetUnit.Launch(planarVelocity, launchVerticalSpeed, launchPlanarDuration);
                return;
            }

            Rigidbody rb = health.GetComponent<Rigidbody>() ?? health.GetComponentInParent<Rigidbody>();
            if (rb == null)
                return;

            Vector3 impulse = Vector3.up * launchVerticalSpeed;
            Vector3 planarImpulse = _direction;
            planarImpulse.y = 0f;
            if (planarImpulse.sqrMagnitude > 1e-6f)
                impulse += planarImpulse.normalized * launchPlanarSpeed;

            rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        void SpawnHitEffect(Vector3 point, Vector3 normal)
        {
            if (hitEffectPrefab == null)
                return;

            Vector3 safeNormal = normal.sqrMagnitude > 1e-6f ? normal.normalized : -_direction;
            Vector3 spawnPoint = point + safeNormal * hitEffectSurfaceOffset;
            Quaternion rotation = Quaternion.LookRotation(safeNormal, Vector3.up);
            GameObject effect = Instantiate(hitEffectPrefab, spawnPoint, rotation);

            if (effect.TryGetComponent(out BulletHitEffect hitEffect))
                hitEffect.Play(spawnPoint, safeNormal);
        }
    }
}
