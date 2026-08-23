using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Buildings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProductionQueue))]
    public sealed class ProductionResourceConverter : MonoBehaviour
    {
        [SerializeField] private string _requiredUnlockId;
        [Min(0f), SerializeField] private float _commanderResourcePerCrystal = 1f;

        public void Configure(string requiredUnlockId, float commanderResourcePerCrystal)
        {
            _requiredUnlockId = ContentIdUtility.Normalize(requiredUnlockId);
            _commanderResourcePerCrystal = Mathf.Max(0f, commanderResourcePerCrystal);
        }

        public int GrantForCompletedProduction(ProductionRecipe recipe)
        {
            if (recipe == null || recipe.Cost.Crystal <= 0 ||
                _commanderResourcePerCrystal <= 0f ||
                !AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                !appRoot.Services.TechTree.IsUnlocked(_requiredUnlockId))
            {
                return 0;
            }

            int amount = Mathf.FloorToInt(
                recipe.Cost.Crystal * _commanderResourcePerCrystal);
            return amount > 0 && appRoot.Services.ResourceService.Add(
                ResourceType.CommanderResource,
                amount)
                ? amount
                : 0;
        }

        private void OnValidate()
        {
            _requiredUnlockId = ContentIdUtility.Normalize(_requiredUnlockId);
            _commanderResourcePerCrystal = Mathf.Max(0f, _commanderResourcePerCrystal);
        }
    }
}
