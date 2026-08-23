using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Visibility
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityIdentity), typeof(FactionMember))]
    public sealed class VisionEmitter : MonoBehaviour
    {
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private FactionMember _faction;
        [SerializeField] private VitalsComponent _vitals;
        [Min(-1f), SerializeField] private float _rangeOverride = -1f;
        [Min(0f), SerializeField] private float _eyeHeight = 1.7f;
        [Min(0f), SerializeField] private float _heightAdvantage = 0.5f;
        [SerializeField] private bool _ignoresCliffs;

        private VisibilitySystem _system;

        public EntityIdentity Identity => _identity;
        public FactionMember Faction => _faction;
        public VitalsComponent Vitals => _vitals;
        public VisibilitySystem System => _system;
        public float RangeOverride => _rangeOverride;
        public float VisionRange => _rangeOverride >= 0f
            ? _rangeOverride
            : _identity != null && _identity.Definition != null
                ? _identity.Definition.VisionRange
                : 0f;
        public bool IsAlive => _vitals == null || _vitals.IsAlive;
        public float EyeHeight => Mathf.Max(0f, _eyeHeight);
        public float HeightAdvantage => Mathf.Max(0f, _heightAdvantage);
        public bool IgnoresCliffs => _ignoresCliffs;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            TryBindSystem();
        }

        private void Start()
        {
            TryBindSystem();
        }

        private void OnDisable()
        {
            BindSystem(null);
        }

        private void OnValidate()
        {
            CacheComponents();
            _rangeOverride = Mathf.Max(-1f, _rangeOverride);
            _eyeHeight = Mathf.Max(0f, _eyeHeight);
            _heightAdvantage = Mathf.Max(0f, _heightAdvantage);
        }

        public void Configure(
            EntityIdentity identity,
            FactionMember faction,
            VitalsComponent vitals,
            float rangeOverride = -1f)
        {
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _faction = faction != null ? faction : GetComponent<FactionMember>();
            _vitals = vitals != null ? vitals : GetComponent<VitalsComponent>();
            _rangeOverride = Mathf.Max(-1f, rangeOverride);
        }

        public bool BindSystem(VisibilitySystem system)
        {
            if (_system == system)
            {
                return _system != null;
            }

            if (_system != null)
            {
                _system.UnregisterEmitter(this);
            }

            _system = system;
            if (_system != null && isActiveAndEnabled)
            {
                _system.RegisterEmitter(this);
            }

            return _system != null;
        }

        public bool ContributesTo(FactionId observerFaction)
        {
            return isActiveAndEnabled && _faction != null &&
                   _faction.Faction == observerFaction && IsAlive && VisionRange > 0f;
        }

        private bool TryBindSystem()
        {
            if (_system != null)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                appRoot.Services.CurrentSceneContext == null)
            {
                return false;
            }

            return BindSystem(appRoot.Services.CurrentSceneContext.VisibilitySystem);
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
        }
    }
}
