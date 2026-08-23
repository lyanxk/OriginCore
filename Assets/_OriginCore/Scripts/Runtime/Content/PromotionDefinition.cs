using OriginCore.Economy;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_Promotion_New", menuName = "OriginCore/Content/Promotion")]
    public sealed class PromotionDefinition : ContentDefinition
    {
        [SerializeField] private UnitDefinition _sourceUnit;
        [SerializeField] private UnitDefinition _targetUnit;
        [SerializeField] private ResourceCost _resourceCost;
        [Min(0), SerializeField] private int _targetInfluence;
        [SerializeField] private string _requiredUnlockId;
        [SerializeField] private Sprite _icon;

        public UnitDefinition SourceUnit => _sourceUnit;
        public UnitDefinition TargetUnit => _targetUnit;
        public ResourceCost ResourceCost => _resourceCost;
        public int TargetInfluence => Mathf.Max(0, _targetInfluence);
        public string RequiredUnlockId => ContentIdUtility.Normalize(_requiredUnlockId);
        public Sprite Icon => _icon;

        public void Configure(
            UnitDefinition sourceUnit,
            UnitDefinition targetUnit,
            ResourceCost resourceCost,
            int targetInfluence,
            string requiredUnlockId,
            Sprite icon = null)
        {
            _sourceUnit = sourceUnit;
            _targetUnit = targetUnit;
            _resourceCost = new ResourceCost(
                resourceCost.CommanderResource,
                resourceCost.Crystal,
                0);
            _targetInfluence = Mathf.Max(0, targetInfluence);
            _requiredUnlockId = ContentIdUtility.Normalize(requiredUnlockId);
            _icon = icon;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _resourceCost = new ResourceCost(
                _resourceCost.CommanderResource,
                _resourceCost.Crystal,
                0);
            _targetInfluence = Mathf.Max(0, _targetInfluence);
            _requiredUnlockId = ContentIdUtility.Normalize(_requiredUnlockId);
        }
    }
}
