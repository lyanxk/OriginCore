using Gameplay;
using System;
using Input;
using Unit.Combat;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public class ChargedShotAbility : ActUnitAbility
    {
        [Header("Ability")]
        [SerializeField] string abilityId = "ability.act.chargedShot";
        [SerializeField] string displayName = "Charged Shot";
        [SerializeField] Sprite icon;
        [SerializeField] string hotkeyText = "Hold RMB";
        [TextArea]
        [SerializeField] string tooltip =
            "Hold right mouse to charge. Release to fire projectiles. Locks the nearest target once when charging starts.";

        [Header("Charge")]
        [Min(1)]
        [SerializeField] int maxChargeLevel = 3;
        [Min(0.1f)]
        [SerializeField] float secondsPerChargeLevel = 2f;

        [Header("Shot")]
        [Min(0.1f)]
        [SerializeField] float range = 18f;
        [Min(0f)]
        [SerializeField] float baseDamage = 20f;

        [Header("Projectile")]
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] LayerMask projectileHitMask = ~0;
        [Min(0.01f)]
        [SerializeField] float projectileSpeed = 28f;
        [Min(0f)]
        [SerializeField] float spawnForwardOffset = 0.25f;
        [Min(0f)]
        [SerializeField] float projectileSpreadAngle = 6f;
        [SerializeField] GameObject hitEffectPrefab;

        bool _isCharging;
        float _chargeStartTime;
        Transform _lockedTarget;

        public override string AbilityId => abilityId;
        public override string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Charged Shot" : displayName;
        public override Sprite Icon => icon;
        public override string HotkeyText => hotkeyText;
        public override string Tooltip => tooltip;

        public override void ProcessInput(InputIntent intent)
        {
            if (Combat == null || CachedTransform == null)
            {
                ResetCharging();
                return;
            }

            if (!IsAvailableInCurrentMode)
            {
                ResetCharging();
                return;
            }

            if (intent.RightHeld && !_isCharging)
                BeginCharge();

            if (_isCharging && !intent.RightHeld)
                FireChargedShot();
        }

        public override void Tick(float deltaTime)
        {
            if (!IsAvailableInCurrentMode)
            {
                ResetCharging();
                return;
            }

            if (_isCharging && IsActMode && ResolveHostileHealth(_lockedTarget) == null)
                AcquireLockedTarget();
        }

        protected override void OnUnbound()
        {
            ResetCharging();
        }

        void BeginCharge()
        {
            _isCharging = true;
            _chargeStartTime = Time.time;
            AcquireLockedTarget();
        }

        void FireChargedShot()
        {
            Vector3 origin = GetShotOrigin();
            Vector3 direction = GetShotDirection(origin);
            int projectileCount = GetCurrentChargeLevel();

            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 projectileDirection = GetSpreadDirection(direction, i, projectileCount);
                SpawnProjectile(origin + projectileDirection * spawnForwardOffset, projectileDirection, baseDamage);
            }

            ResetCharging();
        }

        Vector3 GetSpreadDirection(Vector3 direction, int projectileIndex, int projectileCount)
        {
            if (projectileCount <= 1 || projectileSpreadAngle <= 0f)
                return direction;

            float centerOffset = (projectileCount - 1) * 0.5f;
            float angle = (projectileIndex - centerOffset) * projectileSpreadAngle;
            return Quaternion.AngleAxis(angle, Vector3.up) * direction;
        }

        Vector3 GetShotDirection(Vector3 origin)
        {
            if (IsActMode && TryGetLockedTargetDirection(origin, out Vector3 lockedDirection))
                return lockedDirection;

            if (Router != null && Router.TryGetActFpsAimDirection(out Vector3 aimDirection))
                return aimDirection;

            if (TryGetLockedTargetDirection(origin, out lockedDirection))
                return lockedDirection;

            Vector3 direction = CachedTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            return direction.normalized;
        }

        void AcquireLockedTarget()
        {
            _lockedTarget = Combat != null ? Combat.FindNearestTargetInDetectionRange() : null;
        }

        bool TryGetLockedTargetDirection(Vector3 origin, out Vector3 direction)
        {
            direction = Vector3.zero;

            Health lockedHealth = ResolveHostileHealth(_lockedTarget);
            if (lockedHealth == null)
                return false;

            Vector3 toTarget = lockedHealth.transform.position - origin;
            if (toTarget.sqrMagnitude > range * range || toTarget.sqrMagnitude <= 1e-6f)
                return false;

            direction = toTarget.normalized;
            return true;
        }

        void SpawnProjectile(Vector3 origin, Vector3 direction, float damage)
        {
            GameObject projectileObject = projectilePrefab != null
                ? UnityEngine.Object.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up))
                : CreateFallbackProjectile(origin, direction);

            if (!projectileObject.TryGetComponent(out BulletProjectile projectile))
                projectile = projectileObject.AddComponent<BulletProjectile>();

            projectile.Initialize(
                Combat,
                direction,
                damage,
                projectileHitMask,
                hitEffectPrefab,
                projectileSpeed,
                range);
        }

        GameObject CreateFallbackProjectile(Vector3 origin, Vector3 direction)
        {
            GameObject projectileObject = new GameObject("ChargedShotProjectile");
            projectileObject.transform.SetPositionAndRotation(
                origin,
                Quaternion.LookRotation(direction, Vector3.up));

            SphereCollider collider = projectileObject.AddComponent<SphereCollider>();
            collider.radius = 0.09f;
            collider.isTrigger = true;

            Rigidbody rb = projectileObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            projectileObject.AddComponent<BulletProjectile>();
            return projectileObject;
        }

        int GetCurrentChargeLevel()
        {
            if (!_isCharging)
                return 1;

            int level = 1;
            if (secondsPerChargeLevel > 0f)
                level += Mathf.FloorToInt((Time.time - _chargeStartTime) / secondsPerChargeLevel);

            return Mathf.Clamp(level, 1, Mathf.Max(1, maxChargeLevel));
        }

        Health ResolveHostileHealth(Component targetComponent)
        {
            if (targetComponent == null)
                return null;

            if (!Health.TryResolve(targetComponent, out Health health) || health == null)
                return null;

            if (CachedTransform != null && health.transform.root == CachedTransform.root)
                return null;

            if (TeamAffiliation == null || health.TeamAffiliation == null)
                return null;

            return TeamAffiliation.IsHostileTo(health.TeamAffiliation) ? health : null;
        }

        Vector3 GetShotOrigin()
        {
            if (Router != null && Router.TryGetActFpsAimOrigin(out Vector3 aimOrigin))
                return aimOrigin;

            if (Combat.attackOrigin != null)
                return Combat.attackOrigin.position;

            return CachedTransform.position + Vector3.up * Combat.defaultOriginHeight;
        }

        void ResetCharging()
        {
            _isCharging = false;
            _chargeStartTime = 0f;
            _lockedTarget = null;
        }

        bool IsActMode => string.Equals(CurrentModeName, "ACT", StringComparison.OrdinalIgnoreCase);

    }
}
