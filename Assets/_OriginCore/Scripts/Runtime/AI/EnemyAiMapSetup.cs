using OriginCore.Buildings;
using UnityEngine;

namespace OriginCore.AI
{
    [CreateAssetMenu(
        fileName = "SO_EnemyAIMap_New",
        menuName = "OriginCore/AI/Enemy AI Map Setup")]
    public sealed class EnemyAiMapSetup : ScriptableObject
    {
        [SerializeField] private EnemyAiDefinition _definition;
        [SerializeField] private GameObject _producerPrefab;
        [SerializeField] private string _producerRuntimeId = "enemy.producer.main";
        [SerializeField] private Vector2 _producerNormalizedPosition =
            new Vector2(0.8f, 0.8f);
        [Min(0.1f), SerializeField] private float _navMeshSearchDistance = 30f;
        [SerializeField] private EnemyFundingPolicy _fundingPolicy =
            EnemyFundingPolicy.Unconfigured;
        [Min(0), SerializeField] private int _initialCommanderResource;
        [Min(0), SerializeField] private int _initialCrystal;
        [Min(1), SerializeField] private int _populationCap = 20;
        [Min(0f), SerializeField] private float _commanderResourcePerSecond;
        [Min(0f), SerializeField] private float _crystalPerSecond;
        [SerializeField] private bool _createProducerWhenMissing = true;
        [SerializeField] private bool _markExistingEnemyUnits = true;

        public EnemyAiDefinition Definition => _definition;
        public GameObject ProducerPrefab => _producerPrefab;
        public string ProducerRuntimeId => string.IsNullOrWhiteSpace(_producerRuntimeId)
            ? "enemy.producer.main"
            : _producerRuntimeId.Trim().ToLowerInvariant();
        public Vector2 ProducerNormalizedPosition => new Vector2(
            Mathf.Clamp01(_producerNormalizedPosition.x),
            Mathf.Clamp01(_producerNormalizedPosition.y));
        public float NavMeshSearchDistance => Mathf.Max(0.1f, _navMeshSearchDistance);
        public EnemyFundingPolicy FundingPolicy => _fundingPolicy;
        public int InitialCommanderResource => Mathf.Max(0, _initialCommanderResource);
        public int InitialCrystal => Mathf.Max(0, _initialCrystal);
        public int PopulationCap => Mathf.Max(1, _populationCap);
        public float CommanderResourcePerSecond =>
            Mathf.Max(0f, _commanderResourcePerSecond);
        public float CrystalPerSecond => Mathf.Max(0f, _crystalPerSecond);
        public bool CreateProducerWhenMissing => _createProducerWhenMissing;
        public bool MarkExistingEnemyUnits => _markExistingEnemyUnits;

        public Vector3 ResolvePreferredProducerPosition(Bounds worldBounds)
        {
            Vector2 normalized = ProducerNormalizedPosition;
            return new Vector3(
                Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, normalized.x),
                worldBounds.center.y,
                Mathf.Lerp(worldBounds.min.z, worldBounds.max.z, normalized.y));
        }

        public bool TryValidate(out string error)
        {
            if (_definition == null)
            {
                error = "Enemy AI map setup has no director definition.";
                return false;
            }
            if (!_definition.TryValidate(out error))
            {
                return false;
            }
            if (_fundingPolicy == EnemyFundingPolicy.Unconfigured)
            {
                error = "Enemy AI map setup requires an explicit funding policy.";
                return false;
            }
            if (_createProducerWhenMissing && _producerPrefab == null)
            {
                error = "Enemy AI map setup cannot create its missing producer.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(ProducerRuntimeId))
            {
                error = "Enemy AI producer requires a stable runtime id.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            _producerRuntimeId = ProducerRuntimeId;
            _producerNormalizedPosition = ProducerNormalizedPosition;
            _navMeshSearchDistance = Mathf.Max(0.1f, _navMeshSearchDistance);
            _initialCommanderResource = Mathf.Max(0, _initialCommanderResource);
            _initialCrystal = Mathf.Max(0, _initialCrystal);
            _populationCap = Mathf.Max(1, _populationCap);
            _commanderResourcePerSecond = Mathf.Max(0f, _commanderResourcePerSecond);
            _crystalPerSecond = Mathf.Max(0f, _crystalPerSecond);
        }
    }
}
