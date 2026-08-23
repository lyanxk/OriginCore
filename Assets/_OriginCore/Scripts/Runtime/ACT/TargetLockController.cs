using System;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Visibility;
using UnityEngine;

namespace OriginCore.ACT
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(ActMovementController))]
    public sealed class TargetLockController : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private ActMovementController _movement;
        [Min(0.1f), SerializeField] private float _maximumDistance = 40f;
        [SerializeField] private Vector3 _targetOffset = Vector3.up;

        private InputRouter _inputRouter;

        public event Action<EntityIdentity, EntityIdentity> TargetChanged;

        public EntityIdentity CurrentTarget { get; private set; }

        private void OnEnable()
        {
            CacheComponents();
            TryBindInput();
        }

        private void Start()
        {
            TryBindInput();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
                _inputRouter = null;
            }
            SetTarget(null);
        }

        private void OnValidate()
        {
            CacheComponents();
            _maximumDistance = Mathf.Max(0.1f, _maximumDistance);
        }

        public void Configure(float maximumDistance, Vector3 targetOffset)
        {
            _maximumDistance = Mathf.Max(0.1f, maximumDistance);
            _targetOffset = targetOffset;
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || snapshot.ActiveMap != GameplayInputMap.ACT ||
                !IsPossessedPawn())
            {
                SetTarget(null);
                return;
            }

            InputButtonState lockButton = snapshot.ACT.TargetLock;
            if (!lockButton.IsPressed)
            {
                if (lockButton.WasReleasedThisFrame || CurrentTarget != null)
                {
                    SetTarget(null);
                }
                return;
            }

            if (!IsValidTarget(CurrentTarget))
            {
                SetTarget(FindNearestTarget());
            }

            if (CurrentTarget == null || _movement == null ||
                _movement.ActCameraTarget == null)
            {
                return;
            }

            Vector3 direction = CurrentTarget.transform.position + _targetOffset -
                                _movement.ActCameraTarget.position;
            _movement.SetCameraLookDirection(direction);
        }

        private EntityIdentity FindNearestTarget()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null)
            {
                return null;
            }

            EntityIdentity nearest = null;
            float nearestDistance = _maximumDistance * _maximumDistance;
            var entities = root.Services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity candidate = entities[i];
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private bool IsValidTarget(EntityIdentity target)
        {
            if (target == null || target == _identity || !target.isActiveAndEnabled)
            {
                return false;
            }

            VitalsComponent vitals = target.GetComponent<VitalsComponent>();
            if (vitals != null && !vitals.IsAlive)
            {
                return false;
            }

            FactionMember targetFaction = target.GetComponent<FactionMember>();
            FactionId ownFaction = _faction != null ? _faction.Faction : FactionId.Friendly;
            if (targetFaction == null || targetFaction.Faction == ownFaction ||
                targetFaction.Faction == FactionId.Neutral)
            {
                return false;
            }

            VisibilityTarget visibility = target.GetComponent<VisibilityTarget>();
            if (visibility != null && !visibility.IsVisibleTo(ownFaction))
            {
                return false;
            }

            return (target.transform.position - transform.position).sqrMagnitude <=
                   _maximumDistance * _maximumDistance;
        }

        private void SetTarget(EntityIdentity target)
        {
            if (CurrentTarget == target)
            {
                return;
            }

            EntityIdentity previous = CurrentTarget;
            CurrentTarget = target;
            TargetChanged?.Invoke(previous, CurrentTarget);
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private void TryBindInput()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.InputRouter == null)
            {
                return;
            }

            InputRouter next = root.Services.InputRouter;
            if (_inputRouter == next)
            {
                return;
            }
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
            }
            _inputRouter = next;
            _inputRouter.SnapshotReady += HandleSnapshot;
        }

        private void CacheComponents()
        {
            if (_identity == null) _identity = GetComponent<EntityIdentity>();
            if (_faction == null) _faction = GetComponent<FactionMember>();
            if (_movement == null) _movement = GetComponent<ActMovementController>();
        }
    }
}
