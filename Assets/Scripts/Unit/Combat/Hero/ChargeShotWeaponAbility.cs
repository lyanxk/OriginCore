using System;
using Input;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class ChargeShotWeaponAbility : WeaponAbility
    {
        const string ChargeShotAbilityId = "weapon.revolver.chargeShot";
        const int MaxChargeLevel = 3;
        const float SecondsPerChargeLevel = 2f;
        const float ChargeShotRange = 28f;
        const float ChargeShotSpeed = 28f;
        const float SpawnForwardOffset = 0.25f;
        const float ProjectileSpreadAngle = 6f;

        bool _isCharging;
        float _chargeStartTime;

        public override string AbilityId => ChargeShotAbilityId;
        public override string DisplayName => "Charge Shot";

        public override void ProcessInput(InputIntent intent)
        {
            if (Hero == null || Combat == null || Weapon is not RevolverWeapon gun || gun.ProjectilePrefab == null)
            {
                ResetState();
                return;
            }

            if (intent.RightHeld && !_isCharging)
            {
                _isCharging = true;
                _chargeStartTime = Time.time;
            }

            if (_isCharging && !intent.RightHeld)
            {
                if (!Combat.TryConsumePrimaryCooldown())
                {
                    ResetState();
                    return;
                }

                Fire(gun);
            }
        }

        public override void ResetState()
        {
            _isCharging = false;
            _chargeStartTime = 0f;
        }

        void Fire(RevolverWeapon gun)
        {
            Hero.GetCurrentWeaponShotPose(out Vector3 origin, out Vector3 direction);

            direction = ResolveDirection(direction);
            bool hasLockedTarget = Hero.TryGetActLockedTargetAimPoint(out Vector3 targetPoint);
            int projectileCount = GetCurrentChargeLevel();
            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 projectileDirection = GetSpreadDirection(direction, i, projectileCount);
                Vector3 projectileOrigin = origin + projectileDirection * SpawnForwardOffset;
                if (hasLockedTarget)
                    projectileDirection = ResolveDirection(targetPoint - projectileOrigin);

                SpawnProjectile(gun, projectileOrigin, projectileDirection);
            }

            ResetState();
        }

        void SpawnProjectile(RevolverWeapon gun, Vector3 origin, Vector3 direction)
        {
            GameObject projectileObject = UnityEngine.Object.Instantiate(
                gun.ProjectilePrefab,
                origin,
                Quaternion.LookRotation(direction, Vector3.up));

            if (!projectileObject.TryGetComponent(out BulletProjectile projectile))
                projectile = projectileObject.AddComponent<BulletProjectile>();

            projectile.Initialize(
                Combat,
                direction,
                gun.BaseDamage,
                ~0,
                gun.HitEffectPrefab,
                ChargeShotSpeed,
                ChargeShotRange);
        }

        int GetCurrentChargeLevel()
        {
            if (!_isCharging)
                return 1;

            int level = 1 + Mathf.FloorToInt((Time.time - _chargeStartTime) / SecondsPerChargeLevel);
            return Mathf.Clamp(level, 1, MaxChargeLevel);
        }

        static Vector3 GetSpreadDirection(Vector3 direction, int projectileIndex, int projectileCount)
        {
            if (projectileCount <= 1 || ProjectileSpreadAngle <= 0f)
                return direction;

            float centerOffset = (projectileCount - 1) * 0.5f;
            float angle = (projectileIndex - centerOffset) * ProjectileSpreadAngle;
            return Quaternion.AngleAxis(angle, Vector3.up) * direction;
        }

        static Vector3 ResolveDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            return direction.normalized;
        }
    }
}
