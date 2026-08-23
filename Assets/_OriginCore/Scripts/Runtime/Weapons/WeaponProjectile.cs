using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    public sealed class WeaponProjectile : MonoBehaviour
    {
        private EntityIdentity _source;
        private FactionId _sourceFaction;
        private WeaponDefinition _weapon;
        private Vector3 _velocity;
        private float _remainingLifetime;
        private LayerMask _hitMask;
        private bool _launched;

        public void Launch(
            EntityIdentity source,
            FactionId sourceFaction,
            WeaponDefinition weapon,
            Vector3 direction,
            float speed,
            float lifetime,
            LayerMask hitMask)
        {
            _source = source;
            _sourceFaction = sourceFaction;
            _weapon = weapon;
            _velocity = direction.normalized * Mathf.Max(0.1f, speed);
            _remainingLifetime = Mathf.Max(0.1f, lifetime);
            _hitMask = hitMask;
            _launched = weapon != null;
        }

        private void Update()
        {
            if (!_launched || Time.deltaTime <= 0f)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float distance = _velocity.magnitude * deltaTime;
            Vector3 direction = _velocity.normalized;
            if (Physics.Raycast(
                    transform.position,
                    direction,
                    out RaycastHit hit,
                    distance,
                    _hitMask,
                    QueryTriggerInteraction.Collide))
            {
                ResolveHit(hit);
                Destroy(gameObject);
                return;
            }

            transform.position += _velocity * deltaTime;
            _remainingLifetime -= deltaTime;
            if (_remainingLifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void ResolveHit(RaycastHit hit)
        {
            if (_weapon == null || hit.collider == null ||
                _source != null && hit.collider.transform.IsChildOf(_source.transform))
            {
                return;
            }

            DamageReceiver receiver = hit.collider.GetComponentInParent<DamageReceiver>();
            EntityIdentity target = receiver != null
                ? receiver.GetComponent<EntityIdentity>()
                : null;
            if (receiver == null || target == null ||
                !target.TryGetComponent(out FactionMember targetFaction) ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.FactionRelations.GetRelation(
                    _sourceFaction,
                    targetFaction.Faction) != FactionRelation.Hostile)
            {
                return;
            }

            receiver.TryReceiveDamage(new DamageInfo(
                _weapon.Damage,
                _source,
                _sourceFaction,
                _weapon.DamageType,
                _weapon.DamageFlags,
                hit.point,
                true,
                string.Empty,
                _weapon.ContentId), out _);
        }
    }
}
