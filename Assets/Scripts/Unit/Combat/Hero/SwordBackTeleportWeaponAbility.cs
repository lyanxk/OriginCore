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
    public sealed class SwordBackTeleportWeaponAbility : WeaponAbility
    {
        const string BackTeleportAbilityId = "weapon.sword.backTeleport";
        const float BehindDistance = 1.35f;
        const float VerticalOffset = 0.05f;
        const float FallReductionDuration = 0.45f;
        const float FallReductionMaxFallSpeed = 2f;
        const float FallReductionGravityMultiplier = 0.15f;

        public override string AbilityId => BackTeleportAbilityId;
        public override string DisplayName => GameText.GetName("weaponAbility.swordBackTeleport", AbilityId);

        public override bool ProcessPriorityInput(
            InputIntent intent,
            out bool blocksModeAbilities,
            out bool blocksMovement,
            out bool blocksPrimaryAttack)
        {
            blocksModeAbilities = false;
            blocksMovement = false;
            blocksPrimaryAttack = false;

            if (!intent.CommandV || !IsActMode() || Hero == null)
                return false;

            if (!TryResolveTarget(out Transform target))
                return false;

            UnitBase casterUnit = Hero.GetComponent<UnitBase>();
            if (casterUnit == null)
                return false;

            Vector3 targetForward = target.forward;
            targetForward.y = 0f;
            if (targetForward.sqrMagnitude <= 1e-6f)
                targetForward = target.position - HeroTransform.position;
            targetForward.y = 0f;
            if (targetForward.sqrMagnitude <= 1e-6f)
                targetForward = Vector3.forward;
            targetForward.Normalize();

            Vector3 destination = target.position - targetForward * BehindDistance + Vector3.up * VerticalOffset;
            casterUnit.TeleportTo(destination);
            casterUnit.ApplyFallSpeedReduction(
                FallReductionDuration,
                FallReductionMaxFallSpeed,
                FallReductionGravityMultiplier);

            Vector3 toTarget = target.position - HeroTransform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 1e-6f)
                casterUnit.SetYaw(Quaternion.LookRotation(toTarget, Vector3.up).eulerAngles.y);

            blocksModeAbilities = true;
            blocksMovement = true;
            blocksPrimaryAttack = true;
            return true;
        }

        bool TryResolveTarget(out Transform target)
        {
            target = null;

            UnitBase casterUnit = Hero != null ? Hero.GetComponent<UnitBase>() : null;
            target = casterUnit != null && casterUnit.AbilityRouter != null
                ? casterUnit.AbilityRouter.ActLockedTarget
                : null;

            if (target != null)
                return true;

            if (Combat == null)
                return false;

            target = Combat.FindNearestTargetInDetectionRange();
            return target != null && Health.TryResolve(target, out _);
        }

        static bool IsActMode()
        {
            ControlModeManager manager = ControlModeManager.Instance;
            return manager != null && string.Equals(manager.CurrentModeName, "ACT", StringComparison.OrdinalIgnoreCase);
        }
    }
}
