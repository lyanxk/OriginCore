using System;
using OriginCore.Combat;
using UnityEngine;

namespace OriginCore.Gameplay
{
    public enum SelectionVisualState
    {
        Normal = 0,
        Preview = 1,
        Selected = 2,
        Inspected = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(FactionMember))]
    public sealed class Selectable : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private SelectionIndicatorController _indicator;
        [SerializeField] private Collider _selectionCollider;
        [SerializeField] private SelectionVisualState _state;

        public event Action<Selectable, SelectionVisualState, SelectionVisualState> StateChanged;

        public EntityIdentity Identity => _identity;
        public FactionMember Faction => _faction;
        public VitalsComponent Vitals => _vitals;
        public SelectionIndicatorController Indicator => _indicator;
        public Collider SelectionCollider => _selectionCollider;
        public SelectionVisualState State => _state;
        public bool IsAlive => _vitals == null || _vitals.IsAlive;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            _indicator?.SetState(_state);
        }

        private void OnDisable()
        {
            SetState(SelectionVisualState.Normal);
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void Configure(
            EntityIdentity identity,
            FactionMember faction,
            VitalsComponent vitals,
            SelectionIndicatorController indicator)
        {
            _identity = identity;
            _faction = faction;
            _vitals = vitals;
            _indicator = indicator;
            CacheSelectionCollider();
            _state = SelectionVisualState.Normal;
            _indicator?.SetState(_state);
        }

        public bool ConfigureSelectionCollider(Collider selectionCollider)
        {
            if (selectionCollider == null ||
                (selectionCollider.transform != transform &&
                 !selectionCollider.transform.IsChildOf(transform)))
            {
                return false;
            }

            _selectionCollider = selectionCollider;
            return true;
        }

        public Vector3 GetSelectionWorldPoint()
        {
            if (_selectionCollider != null && _selectionCollider.enabled &&
                _selectionCollider.gameObject.activeInHierarchy)
            {
                return _selectionCollider.bounds.center;
            }

            return transform.position;
        }

        public bool SetState(SelectionVisualState state)
        {
            if (_state == state)
            {
                _indicator?.SetState(state);
                return false;
            }

            SelectionVisualState previous = _state;
            _state = state;
            _indicator?.SetState(_state);
            StateChanged?.Invoke(this, previous, _state);
            return true;
        }

        public bool CanBeCommandedBy(
            FactionId observerFaction,
            FactionRelationService relationService)
        {
            return relationService != null && _faction != null && IsAlive &&
                   relationService.AreAllied(observerFaction, _faction.Faction);
        }

        public static bool TryResolve(Component hitComponent, out Selectable selectable)
        {
            selectable = null;
            if (hitComponent == null)
            {
                return false;
            }

            if (hitComponent.TryGetComponent(out selectable))
            {
                return selectable.isActiveAndEnabled;
            }

            selectable = hitComponent.GetComponentInParent<Selectable>();
            return selectable != null && selectable.isActiveAndEnabled;
        }

        private void CacheComponents()
        {
            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_faction == null)
            {
                _faction = GetComponent<FactionMember>();
            }

            if (_vitals == null)
            {
                _vitals = GetComponent<VitalsComponent>();
            }

            if (_indicator == null)
            {
                _indicator = GetComponent<SelectionIndicatorController>();
            }

            CacheSelectionCollider();
        }

        private void CacheSelectionCollider()
        {
            if (_selectionCollider != null &&
                (_selectionCollider.transform == transform ||
                 _selectionCollider.transform.IsChildOf(transform)))
            {
                return;
            }

            _selectionCollider = null;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    _selectionCollider = candidate;
                    return;
                }
            }

            if (colliders.Length > 0)
            {
                _selectionCollider = colliders[0];
            }
        }
    }
}
