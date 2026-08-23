using OriginCore.Buildings;
using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_Building_New", menuName = "OriginCore/Content/Building")]
    public sealed class BuildingDefinition : ContentDefinition
    {
        [SerializeField] private UnitDefinition _entityDefinition;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private ResourceCost _constructionCost;
        [Min(0.05f), SerializeField] private float _constructionSeconds = 5f;
        [Min(0.1f), SerializeField] private Vector2 _footprint = new Vector2(2f, 2f);
        [SerializeField] private LayerMask _placementBlockingMask = ~0;
        [SerializeField] private ProductionRecipe[] _productionRecipes = new ProductionRecipe[0];
        [SerializeField] private TechnologyDefinition[] _technologies = new TechnologyDefinition[0];
        [SerializeField] private bool _supportsRallyPoint = true;
        [SerializeField] private bool _supportsAttack;
        [Min(0f), SerializeField] private float _commanderResourcePerSecond;
        [Min(0f), SerializeField] private float _crystalPerSecond;
        [SerializeField] private string _requiredUnlockId;
        [SerializeField] private string[] _unlockedContentIds = new string[0];
        [SerializeField] private bool _unique;
        [SerializeField] private bool _untargetable;
        [SerializeField] private string _automaticSpawnBuildingId;

        public UnitDefinition EntityDefinition => _entityDefinition;
        public GameObject Prefab => _prefab;
        public ResourceCost ConstructionCost => _constructionCost;
        public float ConstructionSeconds => Mathf.Max(0.05f, _constructionSeconds);
        public Vector2 Footprint => new Vector2(
            Mathf.Max(0.1f, _footprint.x),
            Mathf.Max(0.1f, _footprint.y));
        public LayerMask PlacementBlockingMask => _placementBlockingMask;
        public ProductionRecipe[] ProductionRecipes => _productionRecipes;
        public TechnologyDefinition[] Technologies => _technologies;
        public bool SupportsRallyPoint => _supportsRallyPoint;
        public bool SupportsAttack => _supportsAttack;
        public float CommanderResourcePerSecond => Mathf.Max(0f, _commanderResourcePerSecond);
        public float CrystalPerSecond => Mathf.Max(0f, _crystalPerSecond);
        public string RequiredUnlockId => ContentIdUtility.Normalize(_requiredUnlockId);
        public string[] UnlockedContentIds => _unlockedContentIds;
        public bool Unique => _unique;
        public bool Untargetable => _untargetable;
        public string AutomaticSpawnBuildingId =>
            ContentIdUtility.Normalize(_automaticSpawnBuildingId);

        public void Configure(
            UnitDefinition entityDefinition,
            GameObject prefab,
            ResourceCost constructionCost,
            float constructionSeconds,
            Vector2 footprint,
            LayerMask placementBlockingMask,
            ProductionRecipe[] productionRecipes,
            TechnologyDefinition[] technologies,
            bool supportsRallyPoint,
            bool supportsAttack,
            float commanderResourcePerSecond = 0f,
            float crystalPerSecond = 0f)
        {
            _entityDefinition = entityDefinition;
            _prefab = prefab;
            _constructionCost = constructionCost;
            _constructionSeconds = Mathf.Max(0.05f, constructionSeconds);
            _footprint = new Vector2(
                Mathf.Max(0.1f, footprint.x),
                Mathf.Max(0.1f, footprint.y));
            _placementBlockingMask = placementBlockingMask;
            _productionRecipes = productionRecipes != null
                ? (ProductionRecipe[])productionRecipes.Clone()
                : new ProductionRecipe[0];
            _technologies = technologies != null
                ? (TechnologyDefinition[])technologies.Clone()
                : new TechnologyDefinition[0];
            _supportsRallyPoint = supportsRallyPoint;
            _supportsAttack = supportsAttack;
            _commanderResourcePerSecond = Mathf.Max(0f, commanderResourcePerSecond);
            _crystalPerSecond = Mathf.Max(0f, crystalPerSecond);
        }

        public void ConfigureProgression(
            string requiredUnlockId,
            string[] unlockedContentIds,
            bool unique,
            bool untargetable,
            string automaticSpawnBuildingId = "")
        {
            _requiredUnlockId = ContentIdUtility.Normalize(requiredUnlockId);
            _unlockedContentIds = NormalizeIds(unlockedContentIds);
            _unique = unique;
            _untargetable = untargetable;
            _automaticSpawnBuildingId = ContentIdUtility.Normalize(automaticSpawnBuildingId);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _constructionSeconds = Mathf.Max(0.05f, _constructionSeconds);
            _footprint.x = Mathf.Max(0.1f, _footprint.x);
            _footprint.y = Mathf.Max(0.1f, _footprint.y);
            _productionRecipes = _productionRecipes ?? new ProductionRecipe[0];
            _technologies = _technologies ?? new TechnologyDefinition[0];
            _commanderResourcePerSecond = Mathf.Max(0f, _commanderResourcePerSecond);
            _crystalPerSecond = Mathf.Max(0f, _crystalPerSecond);
            _requiredUnlockId = ContentIdUtility.Normalize(_requiredUnlockId);
            _unlockedContentIds = NormalizeIds(_unlockedContentIds);
            _automaticSpawnBuildingId = ContentIdUtility.Normalize(_automaticSpawnBuildingId);
        }

        private static string[] NormalizeIds(string[] values)
        {
            if (values == null)
            {
                return new string[0];
            }

            string[] normalized = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                normalized[i] = ContentIdUtility.Normalize(values[i]);
            }

            return normalized;
        }
    }
}
