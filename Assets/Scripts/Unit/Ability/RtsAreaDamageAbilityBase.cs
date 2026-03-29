using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unit.Ability
{
    [Serializable]
    public abstract class RtsAreaDamageAbilityBase : RtsUnitAbility
    {
        Collider[] _overlapBuffer;
        List<Health> _targetBuffer;

        protected int DamageHostilesInRadius(Vector3 center, float radius, float damage)
        {
            if (damage <= 0f)
                return 0;

            EnsureDamageBuffers();
            _targetBuffer.Clear();

            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                Mathf.Max(0.05f, radius),
                _overlapBuffer,
                Combat != null ? (int)Combat.targetMask : ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Health targetHealth = ResolveHostileHealth(_overlapBuffer[i]);
                if (targetHealth == null || _targetBuffer.Contains(targetHealth))
                    continue;

                _targetBuffer.Add(targetHealth);
            }

            int damagedCount = 0;
            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                Health targetHealth = _targetBuffer[i];
                if (targetHealth == null)
                    continue;

                if (Combat != null)
                {
                    if (Combat.TryApplyDamage(targetHealth, damage))
                        damagedCount++;

                    continue;
                }

                targetHealth.TakeDamage(damage);
                damagedCount++;
            }

            _targetBuffer.Clear();
            return damagedCount;
        }

        Health ResolveHostileHealth(Component hitComponent)
        {
            if (!Health.TryResolve(hitComponent, out Health targetHealth))
                return null;

            if (targetHealth == null)
                return null;

            if (CachedTransform != null && targetHealth.transform.root == CachedTransform.root)
                return null;

            return IsHostileTarget(targetHealth) ? targetHealth : null;
        }

        bool IsHostileTarget(Health targetHealth)
        {
            if (targetHealth == null)
                return false;

            TeamAffiliation ownerTeam = TeamAffiliation;
            TeamAffiliation targetTeam = targetHealth.TeamAffiliation;
            if (ownerTeam != null && targetTeam != null)
                return ownerTeam.IsHostileTo(targetTeam);

            if (ownerTeam != null || targetTeam != null)
                return false;

            Selectable ownerSelectable = Selectable;
            Selectable targetSelectable = ResolveSelectable(targetHealth);
            if (ownerSelectable != null && targetSelectable != null)
                return RtsOrderDispatcher.CanControlSelectable(ownerSelectable)
                       != RtsOrderDispatcher.CanControlSelectable(targetSelectable);

            return false;
        }

        void EnsureDamageBuffers()
        {
            _overlapBuffer ??= new Collider[64];
            _targetBuffer ??= new List<Health>(16);
        }

        static Selectable ResolveSelectable(Component source)
        {
            if (source == null)
                return null;

            Selectable selectable = source.GetComponent<Selectable>();
            if (selectable != null)
                return selectable;

            return source.GetComponentInParent<Selectable>();
        }
    }
}
