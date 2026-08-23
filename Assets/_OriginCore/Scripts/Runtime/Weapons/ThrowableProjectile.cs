using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    public sealed class ThrowableProjectile : MonoBehaviour
    {
        private const int RaycastCapacity = 16;
        private const int BlastCapacity = 64;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[RaycastCapacity];
        private readonly Collider[] _blastHits = new Collider[BlastCapacity];
        private readonly HashSet<EntityIdentity> _damaged = new HashSet<EntityIdentity>();
        private EntityIdentity _source;
        private FactionId _sourceFaction;
        private ThrowableDefinition _definition;
        private Vector3 _velocity;
        private LayerMask _hitMask;
        private float _fuseRemaining;
        private float _lifetimeRemaining;
        private bool _launched;

        public void Launch(
            EntityIdentity source,
            FactionId sourceFaction,
            ThrowableDefinition definition,
            Vector3 direction,
            LayerMask hitMask)
        {
            _source = source;
            _sourceFaction = sourceFaction;
            _definition = definition;
            _velocity = direction.normalized *
                        (definition != null ? definition.ThrowSpeed : 0f);
            _hitMask = hitMask;
            _fuseRemaining = definition != null ? definition.FuseSeconds : 0f;
            _lifetimeRemaining = definition != null ? definition.MaximumLifetime : 0f;
            _launched = definition != null;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }
        }

        private void Update()
        {
            if (!_launched || Time.deltaTime <= 0f)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _fuseRemaining -= deltaTime;
            _lifetimeRemaining -= deltaTime;
            if (_fuseRemaining <= 0f || _lifetimeRemaining <= 0f)
            {
                Detonate();
                return;
            }

            Vector3 displacement = _velocity * deltaTime +
                                   Physics.gravity * (0.5f * deltaTime * deltaTime);
            _velocity += Physics.gravity * deltaTime;
            float distance = displacement.magnitude;
            if (distance > 0.0001f && TryFindImpact(
                    displacement / distance,
                    distance,
                    out RaycastHit impact))
            {
                transform.position = impact.point;
                if (_definition.DetonateOnImpact)
                {
                    Detonate();
                    return;
                }

                Vector3 normal = impact.normal.sqrMagnitude > 0.0001f
                    ? impact.normal.normalized
                    : Vector3.up;
                _velocity = Vector3.Reflect(_velocity, normal) * 0.35f;
                transform.position += normal * 0.03f;
            }
            else
            {
                transform.position += displacement;
            }

            if (_velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity.normalized);
            }
        }

        private bool TryFindImpact(
            Vector3 direction,
            float distance,
            out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(
                transform.position,
                direction,
                _raycastHits,
                distance,
                _hitMask,
                QueryTriggerInteraction.Collide);
            nearest = default(RaycastHit);
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _raycastHits[i];
                if (candidate.collider == null ||
                    _source != null &&
                    candidate.collider.transform.IsChildOf(_source.transform) ||
                    candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }
            return found;
        }

        private void Detonate()
        {
            if (!_launched)
            {
                return;
            }

            _launched = false;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _definition.BlastRadius,
                _blastHits,
                _hitMask,
                QueryTriggerInteraction.Collide);
            _damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _blastHits[i];
                DamageReceiver receiver = candidate != null
                    ? candidate.GetComponentInParent<DamageReceiver>()
                    : null;
                EntityIdentity target = receiver != null
                    ? receiver.GetComponent<EntityIdentity>()
                    : null;
                if (receiver == null || target == null || target == _source ||
                    !_damaged.Add(target) || !IsHostile(target))
                {
                    continue;
                }

                receiver.TryReceiveDamage(new DamageInfo(
                    _definition.Damage,
                    _source,
                    _sourceFaction,
                    _definition.DamageType,
                    _definition.DamageFlags,
                    transform.position,
                    true,
                    string.Empty,
                    _definition.ContentId), out _);
            }

            Destroy(gameObject);
        }

        private bool IsHostile(EntityIdentity target)
        {
            return target != null &&
                   target.TryGetComponent(out FactionMember targetFaction) &&
                   AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.FactionRelations.GetRelation(
                       _sourceFaction,
                       targetFaction.Faction) == FactionRelation.Hostile;
        }
    }
}
