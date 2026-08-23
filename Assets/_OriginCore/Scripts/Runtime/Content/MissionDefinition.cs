using System;
using UnityEngine;

namespace OriginCore.Content
{
    public enum MissionObjectiveType
    {
        DestroyEntity = 0,
        ProtectEntity = 1,
        ReachArea = 2,
        GatherResource = 3,
        ResearchTechnology = 4,
        SurviveDuration = 5,
        CustomSignal = 6
    }

    [Serializable]
    public struct MissionObjectiveDefinition
    {
        [SerializeField] private string _objectiveId;
        [SerializeField] private string _displayText;
        [SerializeField] private MissionObjectiveType _objectiveType;
        [SerializeField] private string _targetContentId;
        [Min(0f), SerializeField] private float _requiredAmount;
        [SerializeField] private bool _optional;

        public string ObjectiveId => ContentIdUtility.Normalize(_objectiveId);
        public string DisplayText => _displayText ?? string.Empty;
        public MissionObjectiveType ObjectiveType => _objectiveType;
        public string TargetContentId => ContentIdUtility.Normalize(_targetContentId);
        public float RequiredAmount => Mathf.Max(0f, _requiredAmount);
        public bool Optional => _optional;
    }

    [CreateAssetMenu(fileName = "SO_Mission_New", menuName = "OriginCore/Content/Mission")]
    public sealed class MissionDefinition : ContentDefinition
    {
        [TextArea, SerializeField] private string _briefing;
        [SerializeField] private MissionObjectiveDefinition[] _objectives =
            new MissionObjectiveDefinition[0];
        [SerializeField] private string[] _enemyContentIds = new string[0];

        public string Briefing => _briefing ?? string.Empty;
        public MissionObjectiveDefinition[] Objectives => _objectives;
        public string[] EnemyContentIds => _enemyContentIds;

        protected override void OnValidate()
        {
            base.OnValidate();
            _briefing = _briefing ?? string.Empty;
            _objectives = _objectives ?? new MissionObjectiveDefinition[0];
            _enemyContentIds = _enemyContentIds ?? new string[0];
            for (int i = 0; i < _enemyContentIds.Length; i++)
            {
                _enemyContentIds[i] = ContentIdUtility.Normalize(_enemyContentIds[i]);
            }
        }
    }
}
