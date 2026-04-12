using System;
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
            "Hold right mouse to charge. Release to fire. Locks the nearest target once when charging starts.";

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
        [Min(0f)]
        [SerializeField] float bonusDamagePerExtraCharge = 20f;

        readonly RaycastHit[] _raycastBuffer = new RaycastHit[16];

        bool _isCharging;
        bool _wasRightHeld;
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
                _wasRightHeld = intent.RightHeld;
                return;
            }

            if (!IsAvailableInCurrentMode)
            {
                ResetCharging();
                _wasRightHeld = false;
                return;
            }

            if (intent.RightClick)
                BeginCharge();

            if (_isCharging && _wasRightHeld && !intent.RightHeld)
                FireChargedShot();

            _wasRightHeld = intent.RightHeld;
        }

        public override void Tick(float deltaTime)
        {
            if (!IsAvailableInCurrentMode)
            {
                ResetCharging();
                _wasRightHeld = false;
            }
        }

        protected override void OnUnbound()
        {
            ResetCharging();
            _wasRightHeld = false;
        }

        void BeginCharge()
        {
            _isCharging = true;
            _chargeStartTime = Time.time;
            _lockedTarget = Combat.FindNearestTargetInDetectionRange();
        }

        void FireChargedShot()
        {
            float damage = baseDamage + bonusDamagePerExtraCharge * (GetCurrentChargeLevel() - 1);
            Vector3 origin = GetShotOrigin();

            Health lockedHealth = ResolveHostileHealth(_lockedTarget);
            if (lockedHealth != null)
            {
                Vector3 toTarget = lockedHealth.transform.position - origin;
                if (toTarget.sqrMagnitude <= range * range)
                {
                    Combat.TryApplyDamage(lockedHealth, damage);
                    ResetCharging();
                    return;
                }
            }

            Vector3 direction = CachedTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            direction.Normalize();
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                _raycastBuffer,
                range,
                Combat.targetMask,
                QueryTriggerInteraction.Ignore);

            Health hit = FindBestHealthFromRaycast(hitCount);
            if (hit != null)
                Combat.TryApplyDamage(hit, damage);

            ResetCharging();
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

        Health FindBestHealthFromRaycast(int hitCount)
        {
            Health best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastBuffer[i];
                Health health = ResolveHostileHealth(hit.collider);
                if (health == null)
                    continue;

                if (hit.distance < bestDistance)
                {
                    best = health;
                    bestDistance = hit.distance;
                }
            }

            return best;
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
    }
}
