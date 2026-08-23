using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_Hero_New", menuName = "OriginCore/Content/Hero")]
    public sealed class HeroDefinition : ContentDefinition
    {
        [SerializeField] private UnitDefinition _unitDefinition;
        [SerializeField] private GameObject _prefab;
        [Min(0f), SerializeField] private float _respawnSeconds = 30f;
        [Min(0), SerializeField] private int _immediateRespawnCrystalCost;
        [SerializeField] private AbilityDefinition[] _rtsAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _actAbilities = new AbilityDefinition[0];
        [SerializeField] private AbilityDefinition[] _fpsAbilities = new AbilityDefinition[0];
        [SerializeField] private WeaponDefinition[] _allowedWeapons = new WeaponDefinition[0];
        [Tooltip("Weapons that must occupy the leading ACT loadout slots for every new or restored match.")]
        [SerializeField] private WeaponDefinition[] _requiredActWeapons =
            new WeaponDefinition[0];
        [SerializeField] private HeroFormDefinition[] _forms = new HeroFormDefinition[0];

        public UnitDefinition UnitDefinition => _unitDefinition;
        public GameObject Prefab => _prefab;
        public float RespawnSeconds => Mathf.Max(0f, _respawnSeconds);
        public int ImmediateRespawnCrystalCost => Mathf.Max(0, _immediateRespawnCrystalCost);
        public AbilityDefinition[] RtsAbilities => _rtsAbilities;
        public AbilityDefinition[] ActAbilities => _actAbilities;
        public AbilityDefinition[] FpsAbilities => _fpsAbilities;
        public WeaponDefinition[] AllowedWeapons => _allowedWeapons;
        public WeaponDefinition[] RequiredActWeapons => _requiredActWeapons;
        public HeroFormDefinition[] Forms => _forms;

        public void Configure(
            UnitDefinition unitDefinition,
            GameObject prefab,
            float respawnSeconds,
            int immediateRespawnCrystalCost,
            AbilityDefinition[] rtsAbilities,
            AbilityDefinition[] actAbilities,
            AbilityDefinition[] fpsAbilities,
            WeaponDefinition[] allowedWeapons,
            HeroFormDefinition[] forms,
            WeaponDefinition[] requiredActWeapons = null)
        {
            _unitDefinition = unitDefinition;
            _prefab = prefab;
            _respawnSeconds = Mathf.Max(0f, respawnSeconds);
            _immediateRespawnCrystalCost = Mathf.Max(0, immediateRespawnCrystalCost);
            _rtsAbilities = rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = fpsAbilities ?? new AbilityDefinition[0];
            _allowedWeapons = allowedWeapons ?? new WeaponDefinition[0];
            _forms = forms ?? new HeroFormDefinition[0];
            _requiredActWeapons = requiredActWeapons ?? new WeaponDefinition[0];
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _respawnSeconds = Mathf.Max(0f, _respawnSeconds);
            _immediateRespawnCrystalCost = Mathf.Max(0, _immediateRespawnCrystalCost);
            _rtsAbilities = _rtsAbilities ?? new AbilityDefinition[0];
            _actAbilities = _actAbilities ?? new AbilityDefinition[0];
            _fpsAbilities = _fpsAbilities ?? new AbilityDefinition[0];
            _allowedWeapons = _allowedWeapons ?? new WeaponDefinition[0];
            _requiredActWeapons = _requiredActWeapons ?? new WeaponDefinition[0];
            _forms = _forms ?? new HeroFormDefinition[0];
        }
    }
}
