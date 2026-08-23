using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Buildings
{
    public enum ProductionSpawnPolicy
    {
        RallyPoint = 0,
        ProducerExit = 1,
        WorldTarget = 2
    }

    [CreateAssetMenu(
        fileName = "SO_Recipe_New",
        menuName = "OriginCore/Buildings/Production Recipe")]
    public sealed class ProductionRecipe : ScriptableObject
    {
        [SerializeField] private string _recipeId = "production.new";
        [SerializeField] private string _displayName = "Unit";
        [SerializeField] private UnitDefinition _archetype;
        [SerializeField] private string _resultContentId;
        [Min(1), SerializeField] private int _batchCount = 1;
        [SerializeField] private ResourceCost _cost;
        [Min(0.05f), SerializeField] private float _productionTime = 2f;
        [SerializeField] private string _requiredTechnologyId;
        [SerializeField] private ProductionSpawnPolicy _spawnPolicy = ProductionSpawnPolicy.RallyPoint;

        public string RecipeId => _recipeId;
        public string DisplayName => _displayName;
        public UnitDefinition Archetype => _archetype;
        public string ResultContentId => !string.IsNullOrWhiteSpace(_resultContentId)
            ? _resultContentId
            : _archetype != null ? _archetype.ArchetypeId : string.Empty;
        public int BatchCount => Mathf.Max(1, _batchCount);
        public ResourceCost Cost => _cost;
        public int InfluenceCost => _cost.Influence;
        public float ProductionTime => _productionTime;
        public string RequiredTechnologyId => _requiredTechnologyId ?? string.Empty;
        public ProductionSpawnPolicy SpawnPolicy => _spawnPolicy;
        public bool IsValid => !string.IsNullOrWhiteSpace(_recipeId) &&
                               _archetype != null && _productionTime > 0f;

        public void Configure(
            string recipeId,
            string displayName,
            UnitDefinition archetype,
            ResourceCost cost,
            float productionTime)
        {
            _recipeId = string.IsNullOrWhiteSpace(recipeId)
                ? string.Empty
                : recipeId.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName)
                ? "Unit"
                : displayName.Trim();
            _archetype = archetype;
            _cost = cost;
            _productionTime = Mathf.Max(0.05f, productionTime);
        }

        public void ConfigureExtended(
            string resultContentId,
            int batchCount,
            string requiredTechnologyId,
            ProductionSpawnPolicy spawnPolicy)
        {
            _resultContentId = string.IsNullOrWhiteSpace(resultContentId)
                ? string.Empty
                : resultContentId.Trim().ToLowerInvariant();
            _batchCount = Mathf.Max(1, batchCount);
            _requiredTechnologyId = string.IsNullOrWhiteSpace(requiredTechnologyId)
                ? string.Empty
                : requiredTechnologyId.Trim().ToLowerInvariant();
            _spawnPolicy = spawnPolicy;
        }

        private void OnValidate()
        {
            _recipeId = string.IsNullOrWhiteSpace(_recipeId)
                ? string.Empty
                : _recipeId.Trim();
            _displayName = string.IsNullOrWhiteSpace(_displayName)
                ? "Unit"
                : _displayName.Trim();
            _productionTime = Mathf.Max(0.05f, _productionTime);
            _resultContentId = string.IsNullOrWhiteSpace(_resultContentId)
                ? string.Empty
                : _resultContentId.Trim().ToLowerInvariant();
            _batchCount = Mathf.Max(1, _batchCount);
            _requiredTechnologyId = string.IsNullOrWhiteSpace(_requiredTechnologyId)
                ? string.Empty
                : _requiredTechnologyId.Trim().ToLowerInvariant();
        }
    }
}
