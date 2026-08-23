using System;
using System.Collections;
using OriginCore.AI;
using OriginCore.Buildings;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Save;
using OriginCore.SceneFlow;
using OriginCore.Weapons;
using UnityEngine;

namespace OriginCore.Matches
{
    public enum MatchSessionState
    {
        Idle = 0,
        Validating = 1,
        LoadingScene = 2,
        BindingScene = 3,
        Running = 4,
        LoadingSave = 5,
        Failed = 6,
        ReturningToMenu = 7
    }

    public readonly struct MatchSessionChange
    {
        public MatchSessionChange(
            MatchSessionState previous,
            MatchSessionState current,
            string message)
        {
            Previous = previous;
            Current = current;
            Message = message ?? string.Empty;
        }

        public MatchSessionState Previous { get; }
        public MatchSessionState Current { get; }
        public string Message { get; }
    }

    [DefaultExecutionOrder(-11640)]
    [DisallowMultipleComponent]
    public sealed class MatchSessionService : MonoBehaviour
    {
        private const string StableRiftBuildingId = "z.building.stable_rift";

        [Min(1f), SerializeField] private float _sceneContextTimeoutSeconds = 15f;

        private GameServices _services;
        private Coroutine _transitionRoutine;
        private MatchConfiguration _currentConfiguration;
        private MatchConfiguration _pendingConfiguration;
        private EntityIdentity _activeHero;
        private HeroDeploymentController _initialHeroDeployment;
        private ResourceSnapshot _resourcesBeforeTransition;
        private bool _hasResourceRollback;
        private MatchSessionState _state;
        private string _lastError = string.Empty;

        public event Action<MatchSessionChange> StateChanged;
        public event Action<MatchConfiguration> MatchStarted;
        public event Action MatchEnded;

        public bool IsInitialized => _services != null;
        public bool HasActiveMatch => _state == MatchSessionState.Running && _currentConfiguration != null;
        public MatchSessionState State => _state;
        public MatchConfiguration CurrentConfiguration => _currentConfiguration?.Clone();
        public MatchConfiguration PendingConfiguration => _pendingConfiguration?.Clone();
        public EntityIdentity ActiveHero => _activeHero;
        public HeroDeploymentController InitialHeroDeployment =>
            _initialHeroDeployment;
        public bool HasPendingInitialHeroDeployment =>
            _initialHeroDeployment != null && _initialHeroDeployment.IsDeploying;
        public string LastError => _lastError;

        public float ResolveUnitProductionSeconds(float definitionSeconds)
        {
            MatchConfiguration configuration =
                _currentConfiguration ?? _pendingConfiguration;
            if (configuration != null && _services != null &&
                _services.ContentCatalog.Catalog.TryGetMap(
                    configuration.mapId,
                    out MapDefinition mapDefinition) &&
                mapDefinition != null)
            {
                return mapDefinition.ResolveUnitProductionSeconds(definitionSeconds);
            }

            return Mathf.Max(0.05f, definitionSeconds);
        }

        internal bool Initialize(GameServices services)
        {
            if (services == null || services.ContentCatalog == null ||
                services.SceneFlow == null || services.ResourceService == null ||
                services.EntityRegistry == null)
            {
                Debug.LogError("[OriginCore Match] Required services are unavailable.", this);
                return false;
            }

            if (_services != null && _services != services)
            {
                Debug.LogError("[OriginCore Match] MatchSessionService is already initialized.", this);
                return false;
            }

            _services = services;
            _state = MatchSessionState.Idle;
            _lastError = string.Empty;
            return true;
        }

        internal void Shutdown()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _services = null;
            _currentConfiguration = null;
            _pendingConfiguration = null;
            _activeHero = null;
            _initialHeroDeployment = null;
            _hasResourceRollback = false;
            _state = MatchSessionState.Idle;
            _lastError = string.Empty;
        }

        public bool StartNewMatch(MatchConfiguration configuration, out string error)
        {
            if (!EnsureCanStartTransition(out error))
            {
                return false;
            }

            SetState(MatchSessionState.Validating, "Validating match configuration.");
            MatchConfiguration candidate = configuration?.Clone();
            if (!TryValidate(candidate, out error))
            {
                FailWithoutSceneChange(error);
                return false;
            }

            _resourcesBeforeTransition = _services.ResourceService.Snapshot;
            _hasResourceRollback = true;
            _pendingConfiguration = candidate;
            _lastError = string.Empty;
            _transitionRoutine = StartCoroutine(StartNewMatchRoutine(candidate));
            return true;
        }

        public bool TryPrepareLoad(MatchConfiguration configuration, out string error)
        {
            if (!EnsureCanStartTransition(out error))
            {
                return false;
            }

            MatchConfiguration candidate = configuration?.Clone();
            if (!TryValidate(candidate, out error))
            {
                FailWithoutSceneChange(error);
                return false;
            }

            _pendingConfiguration = candidate;
            _lastError = string.Empty;
            if (_services.ContentCatalog.Catalog.TryGetCommander(
                    candidate.commanderId,
                    out CommanderDefinition commander))
            {
                _services.TechTree.ResetForCommander(commander);
            }

            SetState(MatchSessionState.LoadingSave, "Loading saved match.");
            return true;
        }

        public bool CommitLoadedMatch(MatchConfiguration configuration, out string error)
        {
            if (_services == null || _state != MatchSessionState.LoadingSave)
            {
                error = "No saved match is awaiting commit.";
                return false;
            }

            MatchConfiguration candidate = configuration?.Clone();
            if (!TryValidate(candidate, out error) ||
                !IsCurrentScene(candidate.sceneKey))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Loaded scene does not match the saved match configuration.";
                }

                return false;
            }

            if (!TryBindSceneContent(candidate, out error))
            {
                return false;
            }

            Commit(candidate, "Saved match restored.");
            return true;
        }

        public void AbortPreparedLoad(string reason)
        {
            if (_state != MatchSessionState.LoadingSave)
            {
                return;
            }

            _pendingConfiguration = null;
            _lastError = string.IsNullOrWhiteSpace(reason) ? "Load aborted." : reason.Trim();
            SetState(MatchSessionState.Failed, _lastError);
        }

        public bool ReturnToMainMenu(out string error)
        {
            if (_services == null)
            {
                error = "MatchSessionService is not initialized.";
                return false;
            }

            if (_transitionRoutine != null || _services.SceneFlow.IsLoading)
            {
                error = "A scene transition is already in progress.";
                return false;
            }

            _services.PauseService.SetPaused(false);
            _services.InputRouter.SetGameplayEnabled(false);
            _currentConfiguration = null;
            _pendingConfiguration = null;
            _activeHero = null;
            _initialHeroDeployment = null;
            _lastError = string.Empty;
            SetState(MatchSessionState.ReturningToMenu, "Returning to main menu.");
            if (_services.SceneFlow.LoadSceneAsync(SceneCatalog.MainMenuSceneKey) == null)
            {
                error = "Main menu scene could not be loaded.";
                SetState(MatchSessionState.Failed, error);
                _services.InputRouter.SetGameplayEnabled(true);
                return false;
            }

            MatchEnded?.Invoke();
            error = string.Empty;
            return true;
        }

        private IEnumerator StartNewMatchRoutine(MatchConfiguration configuration)
        {
            if (!_services.ContentCatalog.Catalog.TryGetCommander(
                    configuration.commanderId,
                    out CommanderDefinition commander))
            {
                FailAfterTransition("Configured commander is unavailable.", false);
                yield break;
            }

            _services.PauseService.SetPaused(false);
            _services.InputRouter.SetGameplayEnabled(false);
            _services.GameModeController.RequestMode(GameMode.RTS);
            _services.ResourceService.Restore(new ResourceSnapshot(
                commander.InitialCommanderResource,
                commander.InitialCrystal,
                0,
                commander.InitialPopulationCap));
            _services.TechTree.ResetForCommander(commander);

            SetState(MatchSessionState.LoadingScene, "Loading match scene.");
            AsyncOperation operation = _services.SceneFlow.LoadSceneAsync(configuration.sceneKey);
            if (operation == null)
            {
                FailAfterTransition("Match scene load could not be started.", false);
                yield break;
            }

            yield return operation;
            _services.InputRouter.SetGameplayEnabled(false);
            SetState(MatchSessionState.BindingScene, "Binding match scene.");
            float deadline = Time.realtimeSinceStartup + _sceneContextTimeoutSeconds;
            while (!IsCurrentScene(configuration.sceneKey) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!IsCurrentScene(configuration.sceneKey))
            {
                FailAfterTransition("Loaded match scene did not register its SceneContext in time.", true);
                yield break;
            }

            yield return null;
            if (!TryBindSceneContent(configuration, out string bindingError))
            {
                FailAfterTransition(bindingError, true);
                yield break;
            }

            SceneContext context = _services.CurrentSceneContext;
            if (context != null && context.VisibilitySystem != null)
            {
                context.VisibilitySystem.RestoreExploredState(new VisibilitySaveData());
            }

            Commit(configuration, "Match started.");
            _services.InputRouter.SetGameplayEnabled(true);
            _transitionRoutine = null;
        }

        private bool TryBindSceneContent(MatchConfiguration configuration, out string error)
        {
            SceneContext sceneContext = _services.CurrentSceneContext;
            if (_services.ContentCatalog.Catalog.TryGetMap(
                    configuration.mapId,
                    out MapDefinition mapDefinition) &&
                sceneContext != null)
            {
                if (sceneContext.VisibilitySystem != null)
                {
                    sceneContext.VisibilitySystem.SetElevationDefinition(
                        mapDefinition.VisibilityElevation);
                }

                sceneContext.ConfigureWorldBounds(mapDefinition.WorldBounds);
            }
            if (_services.ContentCatalog.Catalog.TryGetCommander(
                    configuration.commanderId,
                    out CommanderDefinition commander) &&
                sceneContext != null && sceneContext.VisibilitySystem != null)
            {
                sceneContext.VisibilitySystem.SetVisibilityPolicy(commander.VisibilityPolicy);
            }

            if (!TryResolveCommanderBaseSites(
                    sceneContext,
                    commander,
                    out error))
            {
                return false;
            }

            if (!_services.ContentCatalog.Catalog.TryGetHero(
                    configuration.heroId,
                    out HeroDefinition heroDefinition) ||
                heroDefinition.UnitDefinition == null || heroDefinition.Prefab == null)
            {
                error = "Configured hero content is incomplete.";
                return false;
            }

            EntityIdentity hero = FindRegisteredHero(heroDefinition.UnitDefinition.ArchetypeId);
            bool loadingSave = _state == MatchSessionState.LoadingSave;
            if (loadingSave)
            {
                if (!TrySpawnAndBindHero(
                        heroDefinition,
                        configuration,
                        hero,
                        out error))
                {
                    return false;
                }
            }
            else if (FindHeroDeploymentHost(null) == null &&
                     string.Equals(
                         heroDefinition.ContentId,
                         "hero.placeholder",
                         StringComparison.Ordinal))
            {
                // The isolated system-test map intentionally has no faction base.
                // Preserve that fixture while production matches deploy from a beacon.
                if (!TrySpawnAndBindHero(
                        heroDefinition,
                        configuration,
                        hero,
                        out error))
                {
                    return false;
                }
            }
            else
            {
                float deploymentSeconds = mapDefinition != null
                    ? mapDefinition.ResolveInitialHeroDeploymentSeconds(
                        heroDefinition.RespawnSeconds)
                    : heroDefinition.RespawnSeconds;
                if (!TryStartInitialHeroDeployment(
                         heroDefinition,
                         deploymentSeconds,
                         deploymentSeconds,
                         null,
                         hero,
                         out error))
                {
                    return false;
                }
            }

            if (mapDefinition != null &&
                !EnemyAiSceneInstaller.TryInstall(
                    sceneContext,
                    mapDefinition,
                    _services,
                    out EnemyAiDirector _,
                    out string aiError))
            {
                error = aiError;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryResolveCommanderBaseSites(
            SceneContext sceneContext,
            CommanderDefinition commander,
            out string error)
        {
            if (sceneContext == null)
            {
                error = "SceneContext is unavailable while resolving commander base sites.";
                return false;
            }

            CommanderBaseSite[] sites =
                Resources.FindObjectsOfTypeAll<CommanderBaseSite>();
            for (int i = 0; i < sites.Length; i++)
            {
                CommanderBaseSite site = sites[i];
                if (site == null || site.gameObject.scene != sceneContext.gameObject.scene)
                {
                    continue;
                }

                if (!site.TryReplace(commander, out BuildingRuntime _, out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryCompleteInitialHeroDeployment(
            HeroDeploymentController deployment)
        {
            if (deployment == null || deployment != _initialHeroDeployment ||
                !deployment.IsDeploying || deployment.HeroDefinition == null)
            {
                return false;
            }

            MatchConfiguration configuration =
                _currentConfiguration ?? _pendingConfiguration;
            if (configuration == null)
            {
                return false;
            }

            if (!TrySpawnAndBindHero(
                    deployment.HeroDefinition,
                    configuration,
                    null,
                    out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning(
                        "[OriginCore Match] Hero deployment is waiting: " + error,
                        deployment);
                }

                return false;
            }

            _initialHeroDeployment = null;
            Destroy(deployment);
            return true;
        }

        public bool RestoreInitialHeroDeployment(
            float remainingSeconds,
            string hostRuntimeId,
            out string error)
        {
            MatchConfiguration configuration =
                _currentConfiguration ?? _pendingConfiguration;
            if (configuration == null ||
                !_services.ContentCatalog.Catalog.TryGetHero(
                    configuration.heroId,
                    out HeroDefinition heroDefinition) ||
                heroDefinition == null)
            {
                error = "Saved hero deployment has no valid HeroDefinition.";
                return false;
            }

            float deploymentSeconds = ResolveInitialHeroDeploymentSeconds(
                configuration,
                heroDefinition.RespawnSeconds);
            return TryStartInitialHeroDeployment(
                heroDefinition,
                deploymentSeconds,
                remainingSeconds,
                hostRuntimeId,
                _activeHero,
                out error);
        }

        private float ResolveInitialHeroDeploymentSeconds(
            MatchConfiguration configuration,
            float definitionSeconds)
        {
            if (configuration != null && _services != null &&
                _services.ContentCatalog.Catalog.TryGetMap(
                    configuration.mapId,
                    out MapDefinition mapDefinition) &&
                mapDefinition != null)
            {
                return mapDefinition.ResolveInitialHeroDeploymentSeconds(definitionSeconds);
            }

            return Mathf.Max(0f, definitionSeconds);
        }

        private bool TrySpawnAndBindHero(
            HeroDefinition heroDefinition,
            MatchConfiguration configuration,
            EntityIdentity hero,
            out string error)
        {
            SceneContext sceneContext = _services.CurrentSceneContext;
            if (hero == null)
            {
                Transform spawnPoint = FindHeroSpawnAnchor(true);
                if (spawnPoint == null && sceneContext != null &&
                    sceneContext.SpawnPoints.Count > 0)
                {
                    spawnPoint = sceneContext.SpawnPoints[0];
                }

                Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
                Quaternion rotation = spawnPoint != null
                    ? spawnPoint.rotation
                    : Quaternion.identity;
                GameObject instance = Instantiate(heroDefinition.Prefab, position, rotation);
                instance.name = heroDefinition.Prefab.name + "_MatchHero";
                hero = instance.GetComponent<EntityIdentity>();
                if (hero == null || hero.Definition != heroDefinition.UnitDefinition)
                {
                    Destroy(instance);
                    error = "Spawned hero prefab does not match its catalog definition.";
                    return false;
                }

                hero.MarkRuntimeSpawned();
                FactionMember faction = instance.GetComponent<FactionMember>();
                faction?.SetFaction(FactionId.Friendly);
            }

            _activeHero = hero;
            WeaponInventory weaponInventory = hero.GetComponent<WeaponInventory>();
            weaponInventory?.ConfigureFromMatch(
                configuration,
                _services.ContentCatalog.Catalog);
            HybridControlDriver driver = hero.GetComponent<HybridControlDriver>();
            if (driver == null)
            {
                error = "Configured hero has no HybridControlDriver.";
                return false;
            }

            _services.PossessionService.BindPawn(driver);
            error = string.Empty;
            return true;
        }

        private bool TryStartInitialHeroDeployment(
            HeroDefinition heroDefinition,
            float totalSeconds,
            float remainingSeconds,
            string preferredHostRuntimeId,
            EntityIdentity existingHero,
            out string error)
        {
            EntityIdentity host = FindHeroDeploymentHost(preferredHostRuntimeId);
            if (host == null)
            {
                error = "No friendly stable rift can host hero deployment.";
                return false;
            }

            _services.PossessionService.BindPawn(null);
            if (existingHero != null)
            {
                existingHero.gameObject.SetActive(false);
                Destroy(existingHero.gameObject);
            }

            _activeHero = null;
            if (_initialHeroDeployment != null &&
                _initialHeroDeployment.gameObject != host.gameObject)
            {
                Destroy(_initialHeroDeployment);
                _initialHeroDeployment = null;
            }

            _initialHeroDeployment =
                host.GetComponent<HeroDeploymentController>() ??
                host.gameObject.AddComponent<HeroDeploymentController>();
            _initialHeroDeployment.Configure(
                this,
                heroDefinition,
                totalSeconds,
                remainingSeconds);
            error = string.Empty;
            return true;
        }

        private EntityIdentity FindHeroDeploymentHost(string preferredRuntimeId)
        {
            EntityIdentity best = null;
            int bestPriority = int.MaxValue;
            var entities = _services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity identity = entities[i];
                BuildingRuntime building = identity != null
                    ? identity.GetComponent<BuildingRuntime>()
                    : null;
                FactionMember faction = identity != null
                    ? identity.GetComponent<FactionMember>()
                    : null;
                if (identity == null || building == null || !building.IsOperational ||
                    faction == null || faction.Faction != FactionId.Friendly ||
                    building.Definition == null ||
                    !string.Equals(
                        building.Definition.ContentId,
                        StableRiftBuildingId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string runtimeId = identity.EnsureRuntimeId();
                if (!string.IsNullOrWhiteSpace(preferredRuntimeId) &&
                    string.Equals(
                        runtimeId,
                        preferredRuntimeId,
                        StringComparison.Ordinal))
                {
                    return identity;
                }

                int priority = identity.GetComponent<HeroSpawnAnchor>() != null
                    ? 0
                    : 1;
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    best = identity;
                }
            }

            return best;
        }

        private EntityIdentity FindRegisteredHero(string archetypeId)
        {
            var entities = _services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity identity = entities[i];
                if (identity != null && identity.isActiveAndEnabled &&
                    string.Equals(identity.ArchetypeId, archetypeId, StringComparison.Ordinal))
                {
                    return identity;
                }
            }

            return null;
        }

        private Transform FindHeroSpawnAnchor(bool requireInitialSpawn)
        {
            var entities = _services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity identity = entities[i];
                HeroSpawnAnchor anchor = identity != null
                    ? identity.GetComponent<HeroSpawnAnchor>()
                    : null;
                if (anchor != null &&
                    (!requireInitialSpawn || anchor.SupportsInitialSpawn) &&
                    anchor.IsAvailableFor(FactionId.Friendly))
                {
                    return anchor.AnchorPoint;
                }
            }

            return null;
        }

        private bool TryValidate(MatchConfiguration configuration, out string error)
        {
            if (configuration == null)
            {
                error = "Match configuration is null.";
                return false;
            }

            ContentCatalog catalog = _services.ContentCatalog.Catalog;
            return configuration.TryValidate(catalog, out error);
        }

        private bool EnsureCanStartTransition(out string error)
        {
            if (_services == null)
            {
                error = "MatchSessionService is not initialized.";
                return false;
            }

            if (_transitionRoutine != null || _services.SceneFlow.IsLoading ||
                _state == MatchSessionState.LoadingSave)
            {
                error = "A match transition is already in progress.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool IsCurrentScene(string sceneKey)
        {
            SceneContext context = _services.CurrentSceneContext;
            return context != null && _services.SceneFlow.Catalog != null &&
                   _services.SceneFlow.Catalog.TryGetSceneKey(context.Scene.path, out string currentKey) &&
                   string.Equals(currentKey, sceneKey, StringComparison.Ordinal);
        }

        private void Commit(MatchConfiguration configuration, string message)
        {
            _currentConfiguration = configuration.Clone();
            _pendingConfiguration = null;
            _hasResourceRollback = false;
            _lastError = string.Empty;
            SetState(MatchSessionState.Running, message);
            MatchStarted?.Invoke(_currentConfiguration.Clone());
        }

        private void FailWithoutSceneChange(string error)
        {
            _pendingConfiguration = null;
            _lastError = NormalizeError(error);
            SetState(MatchSessionState.Failed, _lastError);
        }

        private void FailAfterTransition(string error, bool returnToMenu)
        {
            _transitionRoutine = null;
            _pendingConfiguration = null;
            _activeHero = null;
            _initialHeroDeployment = null;
            _lastError = NormalizeError(error);
            if (_hasResourceRollback)
            {
                _services.ResourceService.Restore(_resourcesBeforeTransition);
                _hasResourceRollback = false;
            }

            SetState(MatchSessionState.Failed, _lastError);
            Debug.LogError("[OriginCore Match] " + _lastError, this);
            if (returnToMenu && !_services.SceneFlow.IsLoading)
            {
                _services.SceneFlow.LoadSceneAsync(SceneCatalog.MainMenuSceneKey);
            }
            else
            {
                _services.InputRouter.SetGameplayEnabled(true);
            }
        }

        private void SetState(MatchSessionState next, string message)
        {
            if (_state == next && string.IsNullOrEmpty(message))
            {
                return;
            }

            MatchSessionState previous = _state;
            _state = next;
            StateChanged?.Invoke(new MatchSessionChange(previous, next, message));
        }

        private static string NormalizeError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? "Match transition failed." : error.Trim();
        }
    }
}
