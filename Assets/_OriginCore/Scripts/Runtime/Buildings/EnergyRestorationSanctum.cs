using System.Collections.Generic;
using OriginCore.Combat;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Buildings
{
    /// <summary>
    /// An indestructible neutral sanctuary that immediately refills the Energy of
    /// living units standing inside its ground effect.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnergyRestorationSanctum : MonoBehaviour, IIncomingDamageModifier
    {
        private const int InitialHitCapacity = 64;

        [Header("Restoration")]
        [Min(0.1f), SerializeField] private float _restorationRadius = 3.25f;
        [Min(0.02f), SerializeField] private float _scanInterval = 0.1f;
        [SerializeField] private LayerMask _unitMask = ~0;

        [Header("Visual")]
        [SerializeField] private Transform _rotatingVisual;
        [SerializeField] private float _rotationDegreesPerSecond = 8f;

        private readonly HashSet<VitalsComponent> _processedVitals =
            new HashSet<VitalsComponent>();
        private Collider[] _hits = new Collider[InitialHitCapacity];
        private float _nextScanTime;

        public float RestorationRadius => _restorationRadius;

        private void OnValidate()
        {
            _restorationRadius = Mathf.Max(0.1f, _restorationRadius);
            _scanInterval = Mathf.Max(0.02f, _scanInterval);
        }

        private void Update()
        {
            if (_rotatingVisual != null && !Mathf.Approximately(_rotationDegreesPerSecond, 0f))
            {
                _rotatingVisual.Rotate(
                    Vector3.forward,
                    _rotationDegreesPerSecond * Time.deltaTime,
                    Space.Self);
            }

            if (Time.time + 0.0001f < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.time + _scanInterval;
            RestoreUnitsInRange();
        }

        public int RestoreUnitsInRange()
        {
            _processedVitals.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _restorationRadius,
                _hits,
                _unitMask,
                QueryTriggerInteraction.Collide);

            if (hitCount >= _hits.Length)
            {
                _hits = new Collider[_hits.Length * 2];
                hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    _restorationRadius,
                    _hits,
                    _unitMask,
                    QueryTriggerInteraction.Collide);
            }

            int restoredCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                if (hit == null)
                {
                    continue;
                }

                EntityIdentity identity = hit.GetComponentInParent<EntityIdentity>();
                if (identity == null || identity.HasAnyRole(UnitRole.Building))
                {
                    continue;
                }

                VitalsComponent vitals = hit.GetComponentInParent<VitalsComponent>();
                if (vitals == null || !_processedVitals.Add(vitals) ||
                    !vitals.IsAlive || vitals.MaxEnergy <= 0f)
                {
                    continue;
                }

                if (vitals.SetEnergy(vitals.MaxEnergy))
                {
                    restoredCount++;
                }
            }

            return restoredCount;
        }

        public float ModifyIncomingDamage(DamageInfo damageInfo, float currentAmount)
        {
            return 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, _restorationRadius);
        }
    }
}
