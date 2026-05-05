using System.Collections.Generic;
using Gameplay;
using Unit.Movement;
using UnityEngine;

namespace Unit.Combat.Hero
{
    [DisallowMultipleComponent]
    public sealed class UppercutProjectile : MonoBehaviour
    {
        readonly Collider[] _hitBuffer = new Collider[32];
        readonly HashSet<Health> _damagedTargets = new HashSet<Health>();

        UnitCombat _sourceCombat;
        TeamAffiliation _sourceTeam;
        Transform _sourceRoot;
        LayerMask _hitMask = ~0;
        Vector3 _direction = Vector3.forward;
        Vector3 _hitboxSize = new Vector3(2f, 2f, 1f);
        float _damage = 40f;
        float _speed = 18f;
        float _range = 12f;
        float _travelled;
        float _launchVerticalSpeed = 7.5f;
        float _launchPlanarSpeed = 2f;
        float _launchPlanarDuration = 0.18f;
        float _fallReductionDuration = 0.9f;
        float _fallReductionMaxFallSpeed = 1.5f;
        float _fallReductionGravityMultiplier = 0.1f;
        float _visualYawOffset = -90f;

        public void Initialize(
            UnitCombat sourceCombat,
            Vector3 direction,
            float damage,
            LayerMask hitMask,
            float speed,
            float range,
            Vector3 hitboxSize,
            float launchVerticalSpeed,
            float launchPlanarSpeed,
            float launchPlanarDuration,
            float fallReductionDuration,
            float fallReductionMaxFallSpeed,
            float fallReductionGravityMultiplier,
            float visualYawOffset)
        {
            _sourceCombat = sourceCombat;
            _sourceTeam = sourceCombat != null ? sourceCombat.TeamAffiliation : null;
            _sourceRoot = sourceCombat != null ? sourceCombat.transform.root : null;
            _direction = ResolveDirection(direction);
            _damage = Mathf.Max(0f, damage);
            _hitMask = hitMask;
            _speed = Mathf.Max(0.01f, speed);
            _range = Mathf.Max(0.01f, range);
            _hitboxSize = new Vector3(
                Mathf.Max(0.01f, hitboxSize.x),
                Mathf.Max(0.01f, hitboxSize.y),
                Mathf.Max(0.01f, hitboxSize.z));
            _launchVerticalSpeed = Mathf.Max(0f, launchVerticalSpeed);
            _launchPlanarSpeed = Mathf.Max(0f, launchPlanarSpeed);
            _launchPlanarDuration = Mathf.Max(0f, launchPlanarDuration);
            _fallReductionDuration = Mathf.Max(0f, fallReductionDuration);
            _fallReductionMaxFallSpeed = Mathf.Max(0.01f, fallReductionMaxFallSpeed);
            _fallReductionGravityMultiplier = Mathf.Clamp01(fallReductionGravityMultiplier);
            _visualYawOffset = visualYawOffset;
            _travelled = 0f;
            _damagedTargets.Clear();

            transform.rotation = ResolveVisualRotation();
            CheckHits();
        }

        void Update()
        {
            float step = _speed * Time.deltaTime;
            transform.position += _direction * step;
            _travelled += step;

            transform.rotation = ResolveVisualRotation();
            CheckHits();

            if (_travelled >= _range)
                Destroy(gameObject);
        }

        void CheckHits()
        {
            int count = Physics.OverlapBoxNonAlloc(
                transform.position,
                _hitboxSize * 0.5f,
                _hitBuffer,
                Quaternion.LookRotation(_direction, Vector3.up),
                _hitMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
                TryDamage(_hitBuffer[i]);
        }

        void TryDamage(Component hitComponent)
        {
            if (_damage <= 0f || !Health.TryResolve(hitComponent, out Health health))
                return;

            if (_damagedTargets.Contains(health))
                return;

            if (_sourceRoot != null && health.transform.root == _sourceRoot)
                return;

            TeamAffiliation targetTeam = health.TeamAffiliation;
            if (_sourceTeam == null || targetTeam == null || !_sourceTeam.IsHostileTo(targetTeam))
                return;

            _damagedTargets.Add(health);
            if (_sourceCombat != null)
                _sourceCombat.TryApplyDamage(health, _damage);
            else
                health.TakeDamage(_damage);

            LaunchTarget(health);
        }

        void LaunchTarget(Health health)
        {
            UnitBase targetUnit = health.GetComponent<UnitBase>() ?? health.GetComponentInParent<UnitBase>();
            if (targetUnit != null)
            {
                Vector3 planarVelocity = _direction;
                planarVelocity.y = 0f;
                if (planarVelocity.sqrMagnitude > 1e-6f)
                    planarVelocity = planarVelocity.normalized * _launchPlanarSpeed;

                targetUnit.Launch(planarVelocity, _launchVerticalSpeed, _launchPlanarDuration);
                targetUnit.ApplyFallSpeedReduction(
                    _fallReductionDuration,
                    _fallReductionMaxFallSpeed,
                    _fallReductionGravityMultiplier);
                return;
            }

            Rigidbody rb = health.GetComponent<Rigidbody>() ?? health.GetComponentInParent<Rigidbody>();
            if (rb == null)
                return;

            Vector3 impulse = Vector3.up * _launchVerticalSpeed;
            Vector3 planarImpulse = _direction;
            planarImpulse.y = 0f;
            if (planarImpulse.sqrMagnitude > 1e-6f)
                impulse += planarImpulse.normalized * _launchPlanarSpeed;

            rb.AddForce(impulse, ForceMode.VelocityChange);
        }

        Quaternion ResolveVisualRotation()
        {
            return Quaternion.LookRotation(_direction, Vector3.up) * Quaternion.Euler(0f, _visualYawOffset, 0f);
        }

        static Vector3 ResolveDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 1e-6f)
                direction = Vector3.forward;

            return direction.normalized;
        }
    }
}
