using OriginCore.Combat;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Content
{
    public enum CommanderVisibilityPolicy
    {
        StandardFogOfWar = 0,
        ExploredRemainsObserved = 1
    }

    [CreateAssetMenu(fileName = "SO_Commander_New", menuName = "OriginCore/Content/Commander")]
    public sealed class CommanderDefinition : ContentDefinition
    {
        [Header("Starting Economy")]
        [Min(0), SerializeField] private int _initialCommanderResource;
        [Min(0), SerializeField] private int _initialCrystal;
        [Min(0), SerializeField] private int _initialPopulationCap = 10;
        [SerializeField] private string _commanderResourceLabel = "Command";
        [SerializeField] private string _populationLabel = "Population";

        [Header("Available Content")]
        [SerializeField] private UnitDefinition[] _roster = new UnitDefinition[0];
        [SerializeField] private BuildingDefinition[] _buildings = new BuildingDefinition[0];
        [SerializeField] private BuildingDefinition _baseBuilding;
        [SerializeField] private TechnologyDefinition[] _technologies = new TechnologyDefinition[0];
        [SerializeField] private StatModifierDefinition[] _globalModifiers =
            new StatModifierDefinition[0];
        [SerializeField] private UnitRole _globalModifierTargetRoles = UnitRole.Hero;
        [SerializeField] private CommanderVisibilityPolicy _visibilityPolicy;

        public int InitialCommanderResource => Mathf.Max(0, _initialCommanderResource);
        public int InitialCrystal => Mathf.Max(0, _initialCrystal);
        public int InitialPopulationCap => Mathf.Max(0, _initialPopulationCap);
        public string CommanderResourceLabel => _commanderResourceLabel;
        public string PopulationLabel => _populationLabel;
        public UnitDefinition[] Roster => _roster;
        public BuildingDefinition[] Buildings => _buildings;
        public BuildingDefinition BaseBuilding => _baseBuilding;
        public TechnologyDefinition[] Technologies => _technologies;
        public StatModifierDefinition[] GlobalModifiers => _globalModifiers;
        public UnitRole GlobalModifierTargetRoles => _globalModifierTargetRoles;
        public CommanderVisibilityPolicy VisibilityPolicy => _visibilityPolicy;

        public void ConfigureEconomy(
            int initialCommanderResource,
            int initialCrystal,
            int initialPopulationCap,
            string commanderResourceLabel,
            string populationLabel,
            CommanderVisibilityPolicy visibilityPolicy)
        {
            _initialCommanderResource = Mathf.Max(0, initialCommanderResource);
            _initialCrystal = Mathf.Max(0, initialCrystal);
            _initialPopulationCap = Mathf.Max(0, initialPopulationCap);
            _commanderResourceLabel = string.IsNullOrWhiteSpace(commanderResourceLabel)
                ? "Command"
                : commanderResourceLabel.Trim();
            _populationLabel = string.IsNullOrWhiteSpace(populationLabel)
                ? "Population"
                : populationLabel.Trim();
            _visibilityPolicy = visibilityPolicy;
        }

        public void ConfigureContent(
            UnitDefinition[] roster,
            BuildingDefinition[] buildings,
            TechnologyDefinition[] technologies,
            StatModifierDefinition[] globalModifiers,
            UnitRole globalModifierTargetRoles = UnitRole.Hero,
            BuildingDefinition baseBuilding = null)
        {
            _roster = roster != null ? (UnitDefinition[])roster.Clone() : new UnitDefinition[0];
            _buildings = buildings != null
                ? (BuildingDefinition[])buildings.Clone()
                : new BuildingDefinition[0];
            _technologies = technologies != null
                ? (TechnologyDefinition[])technologies.Clone()
                : new TechnologyDefinition[0];
            _globalModifiers = globalModifiers != null
                ? (StatModifierDefinition[])globalModifiers.Clone()
                : new StatModifierDefinition[0];
            _globalModifierTargetRoles = globalModifierTargetRoles;
            _baseBuilding = baseBuilding;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _initialCommanderResource = Mathf.Max(0, _initialCommanderResource);
            _initialCrystal = Mathf.Max(0, _initialCrystal);
            _initialPopulationCap = Mathf.Max(0, _initialPopulationCap);
            _commanderResourceLabel = string.IsNullOrWhiteSpace(_commanderResourceLabel)
                ? "Command"
                : _commanderResourceLabel.Trim();
            _populationLabel = string.IsNullOrWhiteSpace(_populationLabel)
                ? "Population"
                : _populationLabel.Trim();
            _roster = _roster ?? new UnitDefinition[0];
            _buildings = _buildings ?? new BuildingDefinition[0];
            _technologies = _technologies ?? new TechnologyDefinition[0];
            _globalModifiers = _globalModifiers ?? new StatModifierDefinition[0];
        }
    }
}
