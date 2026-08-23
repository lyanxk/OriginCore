using OriginCore.Debugging;
using OriginCore.Cameras;
using OriginCore.Content;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Matches;
using OriginCore.Progression;
using OriginCore.SceneFlow;
using OriginCore.Save;
using OriginCore.Settings;
using UnityEngine;

namespace OriginCore.Core
{
    [DefaultExecutionOrder(-11800)]
    [DisallowMultipleComponent]
    public sealed class GameServices : MonoBehaviour
    {
        [SerializeField] private SceneFlowService _sceneFlow;
        [SerializeField] private ContentCatalogService _contentCatalog;
        [SerializeField] private DebugOverlayService _debugOverlay;
        [SerializeField] private InputRouter _inputRouter;
        [SerializeField] private InputRebindService _inputRebindService;
        [SerializeField] private EntityRegistry _entityRegistry;
        [SerializeField] private FactionRelationService _factionRelations;
        [SerializeField] private GameModeController _gameModeController;
        [SerializeField] private PossessionService _possessionService;
        [SerializeField] private CursorPolicy _cursorPolicy;
        [SerializeField] private ResourceService _resourceService;
        [SerializeField] private MatchSessionService _matchSession;
        [SerializeField] private TechTreeService _techTree;
        [SerializeField] private SettingsService _settingsService;
        [SerializeField] private PauseService _pauseService;
        [SerializeField] private SaveService _saveService;

        private AppRoot _owner;
        private SceneContext _currentSceneContext;

        public bool IsInitialized => _owner != null;
        public SceneFlowService SceneFlow => _sceneFlow;
        public ContentCatalogService ContentCatalog => _contentCatalog;
        public IDebugOverlayService DebugOverlay => _debugOverlay;
        public InputRouter InputRouter => _inputRouter;
        public InputRebindService InputRebindService => _inputRebindService;
        public EntityRegistry EntityRegistry => _entityRegistry;
        public FactionRelationService FactionRelations => _factionRelations;
        public GameModeController GameModeController => _gameModeController;
        public PossessionService PossessionService => _possessionService;
        public CursorPolicy CursorPolicy => _cursorPolicy;
        public ResourceService ResourceService => _resourceService;
        public MatchSessionService MatchSession => _matchSession;
        public TechTreeService TechTree => _techTree;
        public SettingsService SettingsService => _settingsService;
        public PauseService PauseService => _pauseService;
        public SaveService SaveService => _saveService;

        public SceneContext CurrentSceneContext
        {
            get
            {
                if (_currentSceneContext == null)
                {
                    _currentSceneContext = null;
                }

                return _currentSceneContext;
            }
        }

        internal bool Initialize(AppRoot owner)
        {
            if (owner == null)
            {
                Debug.LogError("[OriginCore Services] Cannot initialize without an AppRoot owner.", this);
                return false;
            }

            if (_owner != null && _owner != owner)
            {
                Debug.LogError("[OriginCore Services] This service registry already belongs to another AppRoot.", this);
                return false;
            }

            if (_sceneFlow == null || _contentCatalog == null || _debugOverlay == null ||
                _inputRouter == null || _inputRebindService == null ||
                _entityRegistry == null || _factionRelations == null ||
                _gameModeController == null || _possessionService == null ||
                _cursorPolicy == null || _resourceService == null ||
                _matchSession == null || _techTree == null ||
                _settingsService == null || _pauseService == null ||
                _saveService == null)
            {
                Debug.LogError("[OriginCore Services] One or more core service references are missing.", this);
                return false;
            }

            _owner = owner;
            if (!_contentCatalog.Initialize(out string catalogError))
            {
                Debug.LogError("[OriginCore Services] " + catalogError, _contentCatalog);
                _owner = null;
                return false;
            }

            if (!_settingsService.Initialize(_inputRebindService))
            {
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            _resourceService.Initialize(_debugOverlay);
            if (!_techTree.Initialize(_contentCatalog, _resourceService, _entityRegistry))
            {
                _resourceService.Shutdown();
                _settingsService.Shutdown();
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            if (!_matchSession.Initialize(this))
            {
                _techTree.Shutdown();
                _resourceService.Shutdown();
                _settingsService.Shutdown();
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            if (!_gameModeController.Initialize())
            {
                _matchSession.Shutdown();
                _techTree.Shutdown();
                _resourceService.Shutdown();
                _settingsService.Shutdown();
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            if (!_pauseService.Initialize(
                    _inputRouter,
                    _inputRebindService,
                    _cursorPolicy,
                    this))
            {
                _gameModeController.Shutdown();
                _matchSession.Shutdown();
                _techTree.Shutdown();
                _resourceService.Shutdown();
                _settingsService.Shutdown();
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            if (!_saveService.Initialize(this))
            {
                _pauseService.Shutdown();
                _gameModeController.Shutdown();
                _matchSession.Shutdown();
                _techTree.Shutdown();
                _resourceService.Shutdown();
                _settingsService.Shutdown();
                _contentCatalog.Shutdown();
                _owner = null;
                return false;
            }

            return true;
        }

        internal void Shutdown(AppRoot owner)
        {
            if (_owner != owner)
            {
                return;
            }

            _currentSceneContext = null;
            _saveService.Shutdown();
            _pauseService.Shutdown();
            _gameModeController.Shutdown();
            _matchSession.Shutdown();
            _techTree.Shutdown();
            _resourceService.Shutdown();
            _settingsService.Shutdown();
            _contentCatalog.Shutdown();
            _owner = null;
        }

        public bool RegisterSceneContext(SceneContext context)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[OriginCore Services] SceneContext registered before AppRoot initialization.", context);
                return false;
            }

            if (context == null)
            {
                Debug.LogError("[OriginCore Services] Cannot register a null SceneContext.", this);
                return false;
            }

            if (_currentSceneContext != null && _currentSceneContext != context)
            {
                Debug.LogWarning(
                    "[OriginCore Services] Replacing an active SceneContext. " +
                    "This is expected only during an intentional scene transition.",
                    context);
                _gameModeController.UnbindScene(_currentSceneContext);
            }

            _currentSceneContext = context;
            _gameModeController.BindScene(context);
            return true;
        }

        public void UnregisterSceneContext(SceneContext context)
        {
            if (_currentSceneContext == context)
            {
                _gameModeController.UnbindScene(context);
                _currentSceneContext = null;
            }
        }
    }
}
