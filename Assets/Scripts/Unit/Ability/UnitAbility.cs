using System;
using UnityEngine;

namespace Unit.Ability
{
    public enum RtsAbilityActivationType
    {
        Passive = 0,
        Active = 1
    }

    public enum RtsAbilityTargetingMode
    {
        None = 0,
        Self = 1,
        Unit = 2,
        Point = 3
    }

    [Serializable]
    public abstract class UnitAbility : IAbilityInput, IActivatableAbility
    {
        [NonSerialized] AbilityInputRouter _router;
        [NonSerialized] UnitBase _motor;
        [NonSerialized] UnitCombat _combat;
        [NonSerialized] TeamAffiliation _teamAffiliation;
        [NonSerialized] Selectable _selectable;
        [NonSerialized] Transform _transform;
        [NonSerialized] int _slotIndex = -1;

        public abstract string AbilityId { get; }
        public abstract string DisplayName { get; }
        public abstract Sprite Icon { get; }
        public virtual string HotkeyText => string.Empty;
        public virtual string Tooltip => string.Empty;
        public virtual bool IsEnabled => true;
        public virtual float Cooldown01 => 0f;
        public virtual CommandEntryType EntryType => CommandEntryType.Ability;
        public virtual bool ShowInCommandCard => true;

        public bool IsAvailableInCurrentMode => IsAvailableInMode(CurrentModeName);
        public int SlotIndex => _slotIndex;

        protected AbilityInputRouter Router => _router;
        protected UnitBase Motor => _motor;
        protected UnitCombat Combat => _combat;
        protected TeamAffiliation TeamAffiliation => _teamAffiliation;
        protected Selectable Selectable => _selectable;
        protected Transform CachedTransform => _transform;
        protected string CurrentModeName => ControlModeManager.Instance != null
            ? ControlModeManager.Instance.CurrentModeName
            : string.Empty;

        internal void Bind(AbilityInputRouter router, int slotIndex)
        {
            _router = router;
            _slotIndex = slotIndex;

            if (router != null)
            {
                _transform = router.transform;
                _motor = ResolveNearbyComponent<UnitBase>(router.transform);
                _combat = ResolveNearbyComponent<UnitCombat>(router.transform);
                _teamAffiliation = ResolveNearbyComponent<TeamAffiliation>(router.transform);
                _selectable = ResolveNearbyComponent<Selectable>(router.transform);
            }
            else
            {
                _transform = null;
                _motor = null;
                _combat = null;
                _teamAffiliation = null;
                _selectable = null;
            }

            OnBound();
        }

        internal void Unbind()
        {
            OnUnbound();

            _router = null;
            _slotIndex = -1;
            _transform = null;
            _motor = null;
            _combat = null;
            _teamAffiliation = null;
            _selectable = null;
        }

        public virtual void ProcessInput(InputIntent intent)
        {
        }

        public virtual bool TryActivate()
        {
            return false;
        }

        public virtual void Tick(float deltaTime)
        {
        }

        protected virtual void OnBound()
        {
        }

        protected virtual void OnUnbound()
        {
        }

        protected abstract bool IsAvailableInMode(string modeName);

        static T ResolveNearbyComponent<T>(Transform origin) where T : Component
        {
            if (origin == null)
                return null;

            T resolved = origin.GetComponent<T>();
            if (resolved != null)
                return resolved;

            resolved = origin.GetComponentInParent<T>();
            if (resolved != null)
                return resolved;

            return origin.GetComponentInChildren<T>(true);
        }
    }

    [Serializable]
    public abstract class RtsUnitAbility : UnitAbility
    {
        public virtual RtsAbilityActivationType ActivationType => RtsAbilityActivationType.Active;
        public virtual RtsAbilityTargetingMode TargetingMode => ActivationType == RtsAbilityActivationType.Passive
            ? RtsAbilityTargetingMode.None
            : RtsAbilityTargetingMode.Self;
        public virtual float TargetingPreviewRadius => 0f;
        public sealed override CommandEntryType EntryType => ActivationType == RtsAbilityActivationType.Passive
            ? CommandEntryType.Passive
            : CommandEntryType.Ability;

        public sealed override bool TryActivate()
        {
            if (!IsAvailableInCurrentMode || !IsEnabled)
                return false;

            if (ActivationType == RtsAbilityActivationType.Passive)
                return false;

            switch (TargetingMode)
            {
                case RtsAbilityTargetingMode.Self:
                    RtsQueuedOrderState.Clear();
                    RtsAbilityTargetingState.Clear();
                    return TryActivateSelf();

                case RtsAbilityTargetingMode.Unit:
                case RtsAbilityTargetingMode.Point:
                    if (Router == null)
                        return false;

                    if (RtsAbilityTargetingState.IsPending(Router, this))
                    {
                        RtsAbilityTargetingState.Clear();
                        return true;
                    }

                    RtsQueuedOrderState.Clear();
                    RtsAbilityTargetingState.SetPending(Router, this);
                    return true;

                default:
                    return false;
            }
        }

        protected sealed override bool IsAvailableInMode(string modeName)
        {
            return string.Equals(modeName, "RTS", StringComparison.OrdinalIgnoreCase);
        }

        internal bool TryActivatePendingUnit(Selectable target, Vector3 worldPoint)
        {
            if (TargetingMode != RtsAbilityTargetingMode.Unit)
                return false;

            return TryActivateOnUnit(target, worldPoint);
        }

        internal bool TryActivatePendingPoint(Vector3 worldPoint)
        {
            if (TargetingMode != RtsAbilityTargetingMode.Point)
                return false;

            return TryActivateOnPoint(worldPoint);
        }

        protected virtual bool TryActivateSelf()
        {
            return false;
        }

        protected virtual bool TryActivateOnUnit(Selectable target, Vector3 worldPoint)
        {
            return false;
        }

        protected virtual bool TryActivateOnPoint(Vector3 worldPoint)
        {
            return false;
        }
    }

    [Serializable]
    public abstract class ActFpsUnitAbility : UnitAbility
    {
        protected override bool IsAvailableInMode(string modeName)
        {
            return string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase);
        }
    }
}
