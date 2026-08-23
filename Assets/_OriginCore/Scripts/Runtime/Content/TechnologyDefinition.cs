using OriginCore.Combat;
using OriginCore.Economy;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_Technology_New", menuName = "OriginCore/Content/Technology")]
    public sealed class TechnologyDefinition : ContentDefinition
    {
        [SerializeField] private ResourceCost _cost;
        [Min(0.05f), SerializeField] private float _researchSeconds = 5f;
        [SerializeField] private string[] _prerequisiteTechnologyIds = new string[0];
        [SerializeField] private string _requiredUnlockId;
        [SerializeField] private string[] _unlockedContentIds = new string[0];
        [SerializeField] private StatModifierDefinition[] _modifiers =
            new StatModifierDefinition[0];
        [SerializeField] private string[] _targetArchetypeIds = new string[0];
        [SerializeField] private Sprite _icon;

        public ResourceCost Cost => _cost;
        public float ResearchSeconds => Mathf.Max(0.05f, _researchSeconds);
        public string[] PrerequisiteTechnologyIds => _prerequisiteTechnologyIds;
        public string RequiredUnlockId => ContentIdUtility.Normalize(_requiredUnlockId);
        public string[] UnlockedContentIds => _unlockedContentIds;
        public StatModifierDefinition[] Modifiers => _modifiers;
        public string[] TargetArchetypeIds => _targetArchetypeIds;
        public Sprite Icon => _icon;

        public void Configure(
            ResourceCost cost,
            float researchSeconds,
            string[] prerequisiteTechnologyIds,
            string requiredUnlockId,
            string[] unlockedContentIds,
            StatModifierDefinition[] modifiers,
            string[] targetArchetypeIds,
            Sprite icon = null)
        {
            _cost = cost;
            _researchSeconds = Mathf.Max(0.05f, researchSeconds);
            _prerequisiteTechnologyIds = NormalizeIds(prerequisiteTechnologyIds);
            _requiredUnlockId = ContentIdUtility.Normalize(requiredUnlockId);
            _unlockedContentIds = NormalizeIds(unlockedContentIds);
            _modifiers = modifiers != null
                ? (StatModifierDefinition[])modifiers.Clone()
                : new StatModifierDefinition[0];
            _targetArchetypeIds = NormalizeIds(targetArchetypeIds);
            _icon = icon;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _researchSeconds = Mathf.Max(0.05f, _researchSeconds);
            _prerequisiteTechnologyIds = NormalizeIds(_prerequisiteTechnologyIds);
            _requiredUnlockId = ContentIdUtility.Normalize(_requiredUnlockId);
            _unlockedContentIds = NormalizeIds(_unlockedContentIds);
            _modifiers = _modifiers ?? new StatModifierDefinition[0];
            _targetArchetypeIds = NormalizeIds(_targetArchetypeIds);
        }

        private static string[] NormalizeIds(string[] values)
        {
            if (values == null)
            {
                return new string[0];
            }

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = ContentIdUtility.Normalize(values[i]);
            }

            return values;
        }
    }
}
