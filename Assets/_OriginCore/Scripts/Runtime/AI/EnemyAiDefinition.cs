using System;
using OriginCore.Buildings;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.AI
{
    [Serializable]
    public sealed class EnemyProductionWeight
    {
        [SerializeField] private ProductionRecipe _recipe;
        [Min(1), SerializeField] private int _weight = 1;

        public ProductionRecipe Recipe => _recipe;
        public int Weight => Mathf.Max(1, _weight);
    }

    [CreateAssetMenu(fileName = "SO_EnemyAI_New", menuName = "OriginCore/AI/Enemy AI")]
    public sealed class EnemyAiDefinition : ScriptableObject
    {
        [SerializeField] private FactionId _faction = FactionId.Enemy;
        [Min(0.05f), SerializeField] private float _thinkInterval = 0.5f;
        [Min(1), SerializeField] private int _assaultPopulationThreshold = 10;
        [Range(0f, 1f), SerializeField] private float _regroupRemainingRatio = 0.25f;
        [Min(0f), SerializeField] private float _regroupSeconds = 5f;
        [SerializeField] private EnemyProductionWeight[] _productionWeights =
            Array.Empty<EnemyProductionWeight>();

        public FactionId Faction => _faction;
        public float ThinkInterval => Mathf.Max(0.05f, _thinkInterval);
        public int AssaultPopulationThreshold => Mathf.Max(1, _assaultPopulationThreshold);
        public float RegroupRemainingRatio => Mathf.Clamp01(_regroupRemainingRatio);
        public float RegroupSeconds => Mathf.Max(0f, _regroupSeconds);
        public EnemyProductionWeight[] ProductionWeights => _productionWeights;

        public bool TryValidate(out string error)
        {
            if (_faction != FactionId.Enemy)
            {
                error = "Enemy AI definition must control the Enemy faction.";
                return false;
            }
            if (_productionWeights == null || _productionWeights.Length == 0)
            {
                error = "Enemy AI definition has no production entries.";
                return false;
            }
            for (int i = 0; i < _productionWeights.Length; i++)
            {
                EnemyProductionWeight entry = _productionWeights[i];
                if (entry == null || entry.Recipe == null || !entry.Recipe.IsValid)
                {
                    error = "Enemy AI definition contains an invalid production entry.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            _thinkInterval = Mathf.Max(0.05f, _thinkInterval);
            _assaultPopulationThreshold = Mathf.Max(1, _assaultPopulationThreshold);
            _regroupRemainingRatio = Mathf.Clamp01(_regroupRemainingRatio);
            _regroupSeconds = Mathf.Max(0f, _regroupSeconds);
            _productionWeights = _productionWeights ??
                Array.Empty<EnemyProductionWeight>();
        }
    }
}
