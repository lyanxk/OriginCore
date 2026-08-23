using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(VitalsComponent))]
    public sealed class DirectHitEnergyReward : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private VitalsComponent _vitals;
        [Min(0f), SerializeField] private float _energyPerHit = 1f;

        private void OnEnable()
        {
            CacheComponents();
            DamageReceiver.AnyDamageApplied -= HandleAnyDamageApplied;
            DamageReceiver.AnyDamageApplied += HandleAnyDamageApplied;
        }

        private void OnDisable()
        {
            DamageReceiver.AnyDamageApplied -= HandleAnyDamageApplied;
        }

        private void OnValidate()
        {
            CacheComponents();
            _energyPerHit = Mathf.Max(0f, _energyPerHit);
        }

        public void Configure(float energyPerHit)
        {
            _energyPerHit = Mathf.Max(0f, energyPerHit);
        }

        private void HandleAnyDamageApplied(
            DamageReceiver receiver,
            DamageInfo damage,
            DamageResult result)
        {
            if (_energyPerHit <= 0f || !result.Changed || damage.Source != _identity ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                !root.Services.GameModeController.CurrentMode.IsDirectControl() ||
                root.Services.PossessionService.CurrentPawn == null ||
                root.Services.PossessionService.CurrentPawn.gameObject != gameObject)
            {
                return;
            }

            _vitals.SetEnergy(_vitals.Energy + _energyPerHit);
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_vitals == null) _vitals = GetComponent<VitalsComponent>();
        }
    }
}
