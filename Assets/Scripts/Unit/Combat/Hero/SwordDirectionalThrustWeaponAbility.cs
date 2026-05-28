using System;
using Content;
using Core;
using Gameplay;
using Input;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [Serializable]
    public sealed class SwordDirectionalThrustWeaponAbility : WeaponAbility
    {
        const string ThrustAbilityId = "weapon.sword.directionalThrust";
        const float Duration = 0.22f;
        const float Speed = 17f;
        const float Damage = 40f;
        const float HitRadius = 0.85f;
        const float FallReductionDuration = 0.45f;
        const float FallReductionMaxFallSpeed = 2f;
        const float FallReductionGravityMultiplier = 0.15f;

        readonly Collider[] _hitBuffer = new Collider[16];

        Vector3 _direction;
        float _remainingTime;
        bool _hasDamaged;

        public override string AbilityId => ThrustAbilityId;
        public override string DisplayName => GameText.GetName("weaponAbility.swordDirectionalThrust", AbilityId);

        public override bool ProcessPriorityInput(
            InputIntent intent,
            out bool blocksModeAbilities,
            out bool blocksMovement,
            out bool blocksPrimaryAttack)
        {
            blocksModeAbilities = false;
            blocksMovement = false;
            blocksPrimaryAttack = false;

            if (!IsActMode() || Hero == null || Combat == null)
                return false;

            UnitBase casterUnit = Hero.GetComponent<UnitBase>();
            Health casterHealth = Hero.GetComponent<Health>() ?? Hero.GetComponentInParent<Health>();
            if (casterUnit == null || casterHealth == null)
                return false;

            if (_remainingTime <= 0f && intent.LeftClick && intent.Move.sqrMagnitude > 0.01f)
                StartThrust(intent.Move, casterUnit, casterHealth);

            if (_remainingTime <= 0f)
                return false;

            TickThrust(casterUnit, casterHealth);
            blocksMovement = true;
            blocksPrimaryAttack = true;
            return true;
        }

        public override void ResetState()
        {
            _remainingTime = 0f;
            _direction = Vector3.zero;
            _hasDamaged = false;
        }

        void StartThrust(Vector2 moveInput, UnitBase casterUnit, Health casterHealth)
        {
            if (!Hero.TryGetRawWeaponAimDirection(out Vector3 aimDirection))
                aimDirection = HeroTransform != null ? HeroTransform.forward : Vector3.forward;

            Vector3 forward = aimDirection;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 1e-6f)
                forward = HeroTransform != null ? HeroTransform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 1e-6f)
                forward = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            right.y = 0f;
            if (right.sqrMagnitude <= 1e-6f)
                right = Vector3.right;

            _direction = right.normalized * moveInput.x + forward.normalized * moveInput.y;
            _direction.y = 0f;
            if (_direction.sqrMagnitude <= 1e-6f)
                _direction = forward;
            _direction.Normalize();

            _remainingTime = Duration;
            _hasDamaged = false;
            casterUnit.CancelPathing();
            casterUnit.DisableCharacterCollision(Duration + 0.03f);
            casterUnit.ApplyFallSpeedReduction(
                FallReductionDuration,
                FallReductionMaxFallSpeed,
                FallReductionGravityMultiplier);
            casterHealth.AddInvulnerability(Duration + 0.03f);
            casterUnit.SetYaw(Quaternion.LookRotation(_direction, Vector3.up).eulerAngles.y);
        }

        void TickThrust(UnitBase casterUnit, Health casterHealth)
        {
            float dt = Time.deltaTime;
            _remainingTime = Mathf.Max(0f, _remainingTime - dt);
            casterUnit.DisableCharacterCollision(dt + 0.03f);
            casterHealth.AddInvulnerability(dt + 0.03f);
            casterUnit.MoveIgnoringCollision(_direction * (Speed * dt));
            TryDamage(casterUnit);
        }

        void TryDamage(UnitBase casterUnit)
        {
            if (_hasDamaged || Combat == null)
                return;

            int count = Physics.OverlapSphereNonAlloc(
                casterUnit.transform.position + _direction * HitRadius,
                HitRadius,
                _hitBuffer,
                Combat.targetMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hitBuffer[i];
                if (hit == null)
                    continue;

                if (!Health.TryResolve(hit, out Health targetHealth))
                    continue;

                if (targetHealth.transform.root == casterUnit.transform.root)
                    continue;

                if (!Combat.TryApplyDamage(targetHealth, Damage))
                    continue;

                _hasDamaged = true;
                return;
            }
        }

        static bool IsActMode()
        {
            ControlModeManager manager = ControlModeManager.Instance;
            return manager != null && string.Equals(manager.CurrentModeName, "ACT", StringComparison.OrdinalIgnoreCase);
        }
    }
}
