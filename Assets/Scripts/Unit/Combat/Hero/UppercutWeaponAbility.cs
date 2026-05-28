using System;
using Content;
using Input;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class UppercutWeaponAbility : WeaponAbility
    {
        const string UppercutAbilityId = "weapon.sword.uppercut";
        const float Damage = 40f;
        const float Speed = 18f;
        const float Range = 12f;
        const float SpawnForwardOffset = 0.85f;
        const float SpawnHeightOffset = 0.25f;
        const float HitboxWidth = 2f;
        const float HitboxHeight = 2f;
        const float HitboxDepth = 1f;
        const float LaunchVerticalSpeed = 5f;
        const float LaunchPlanarSpeed = 2f;
        const float LaunchPlanarDuration = 0.18f;
        const float FallReductionDuration = 0.9f;
        const float FallReductionMaxFallSpeed = 1.5f;
        const float FallReductionGravityMultiplier = 0.1f;
        const float VisualYawOffset = -90f;

        public override string AbilityId => UppercutAbilityId;
        public override string DisplayName => GameText.GetName("weaponAbility.uppercut", AbilityId);

        public override void ProcessInput(InputIntent intent)
        {
            if (!intent.CommandR)
                return;

            if (Hero == null || Combat == null || Weapon is not SwordWeapon sword || sword.UppercutPrefab == null)
                return;

            if (!Combat.TryConsumePrimaryCooldown())
                return;

            Hero.GetCurrentWeaponShotPose(out Vector3 origin, out Vector3 direction);
            direction = ResolveForwardDirection(direction);

            Vector3 spawnPosition = origin + direction * SpawnForwardOffset + Vector3.up * SpawnHeightOffset;
            GameObject projectileObject = UnityEngine.Object.Instantiate(
                sword.UppercutPrefab,
                spawnPosition,
                ResolveVisualRotation(direction));

            LaunchCaster();

            if (!projectileObject.TryGetComponent(out UppercutProjectile projectile))
                projectile = projectileObject.AddComponent<UppercutProjectile>();

            projectile.Initialize(
                Combat,
                direction,
                Damage,
                Combat.targetMask,
                Speed,
                Range,
                new Vector3(HitboxWidth, HitboxHeight, HitboxDepth),
                LaunchVerticalSpeed,
                LaunchPlanarSpeed,
                LaunchPlanarDuration,
                FallReductionDuration,
                FallReductionMaxFallSpeed,
                FallReductionGravityMultiplier,
                VisualYawOffset);
        }

        void LaunchCaster()
        {
            UnitBase casterUnit = Hero != null ? Hero.GetComponent<UnitBase>() : null;
            if (casterUnit == null)
                return;

            casterUnit.Launch(Vector3.zero, LaunchVerticalSpeed, 0f);
            casterUnit.ApplyFallSpeedReduction(
                FallReductionDuration,
                FallReductionMaxFallSpeed,
                FallReductionGravityMultiplier);
        }

        Vector3 ResolveForwardDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f && HeroTransform != null)
                direction = HeroTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            return direction.normalized;
        }

        static Quaternion ResolveVisualRotation(Vector3 direction)
        {
            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, VisualYawOffset, 0f);
        }
    }
}
