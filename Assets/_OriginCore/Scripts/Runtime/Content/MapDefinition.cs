using OriginCore.AI;
using OriginCore.UI.RTS;
using OriginCore.Visibility;
using UnityEngine;

namespace OriginCore.Content
{
    [CreateAssetMenu(fileName = "SO_Map_New", menuName = "OriginCore/Content/Map")]
    public sealed class MapDefinition : ContentDefinition
    {
        [SerializeField] private string _sceneKey;
        [SerializeField] private Bounds _worldBounds = new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
        [SerializeField] private MinimapMapDefinition _minimapDefinition;
        [SerializeField] private VisibilityElevationDefinition _visibilityElevation;
        [SerializeField] private EnemyAiMapSetup _enemyAiSetup;
        [SerializeField] private MissionDefinition[] _missions = new MissionDefinition[0];
        [SerializeField] private CommanderDefinition[] _allowedCommanders = new CommanderDefinition[0];
        [SerializeField] private HeroDefinition[] _allowedHeroes = new HeroDefinition[0];
        [SerializeField] private bool _allowSkirmish = true;
        [Tooltip("Positive values override initial hero deployment time for this map only.")]
        [SerializeField] private float _initialHeroDeploymentSecondsOverride = -1f;
        [Tooltip("Positive values override every unit production recipe for this map only.")]
        [SerializeField] private float _unitProductionSecondsOverride = -1f;

        public string SceneKey => _sceneKey ?? string.Empty;
        public Bounds WorldBounds => _worldBounds;
        public MinimapMapDefinition MinimapDefinition => _minimapDefinition;
        public VisibilityElevationDefinition VisibilityElevation => _visibilityElevation;
        public EnemyAiMapSetup EnemyAiSetup => _enemyAiSetup;
        public MissionDefinition[] Missions => _missions;
        public CommanderDefinition[] AllowedCommanders => _allowedCommanders;
        public HeroDefinition[] AllowedHeroes => _allowedHeroes;
        public bool AllowSkirmish => _allowSkirmish;
        public bool OverridesInitialHeroDeployment =>
            _initialHeroDeploymentSecondsOverride > 0f;
        public bool OverridesUnitProduction => _unitProductionSecondsOverride > 0f;

        public float ResolveInitialHeroDeploymentSeconds(float definitionSeconds)
        {
            return OverridesInitialHeroDeployment
                ? Mathf.Max(0.05f, _initialHeroDeploymentSecondsOverride)
                : Mathf.Max(0f, definitionSeconds);
        }

        public float ResolveUnitProductionSeconds(float definitionSeconds)
        {
            return OverridesUnitProduction
                ? Mathf.Max(0.05f, _unitProductionSecondsOverride)
                : Mathf.Max(0.05f, definitionSeconds);
        }

        public void Configure(
            string sceneKey,
            Bounds worldBounds,
            MinimapMapDefinition minimapDefinition,
            MissionDefinition[] missions,
            CommanderDefinition[] allowedCommanders,
            HeroDefinition[] allowedHeroes,
            bool allowSkirmish,
            float initialHeroDeploymentSecondsOverride = -1f,
            float unitProductionSecondsOverride = -1f)
        {
            _sceneKey = string.IsNullOrWhiteSpace(sceneKey)
                ? string.Empty
                : sceneKey.Trim();
            Vector3 size = worldBounds.size;
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            size.z = Mathf.Max(0.1f, size.z);
            worldBounds.size = size;
            _worldBounds = worldBounds;
            _minimapDefinition = minimapDefinition;
            _missions = missions != null
                ? (MissionDefinition[])missions.Clone()
                : new MissionDefinition[0];
            _allowedCommanders = allowedCommanders != null
                ? (CommanderDefinition[])allowedCommanders.Clone()
                : new CommanderDefinition[0];
            _allowedHeroes = allowedHeroes != null
                ? (HeroDefinition[])allowedHeroes.Clone()
                : new HeroDefinition[0];
            _allowSkirmish = allowSkirmish;
            _initialHeroDeploymentSecondsOverride =
                initialHeroDeploymentSecondsOverride;
            _unitProductionSecondsOverride = unitProductionSecondsOverride;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _sceneKey = string.IsNullOrWhiteSpace(_sceneKey) ? string.Empty : _sceneKey.Trim();
            Vector3 size = _worldBounds.size;
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            size.z = Mathf.Max(0.1f, size.z);
            _worldBounds.size = size;
            _missions = _missions ?? new MissionDefinition[0];
            _allowedCommanders = _allowedCommanders ?? new CommanderDefinition[0];
            _allowedHeroes = _allowedHeroes ?? new HeroDefinition[0];
        }
    }
}
