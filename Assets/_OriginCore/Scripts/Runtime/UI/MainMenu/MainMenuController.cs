using OriginCore.Core;
using OriginCore.Input;
using OriginCore.Matches;
using OriginCore.SceneFlow;
using OriginCore.UI.Common;
using OriginCore.UI.Settings;
using OriginCore.UI.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour, IUiEscapeHandler
    {
        public const string MainPageId = "Main";
        public const string PlayPageId = "Play";
        public const string StoryPageId = "Story";
        public const string MapPageId = "Map";
        public const string OptionsPageId = "Options";
        public const string MatchSetupPageId = "MatchSetup";

        [SerializeField] private PageStack _pageStack;
        [SerializeField] private ModalStack _modalStack;
        [SerializeField] private ConfirmDialog _confirmDialog;
        [SerializeField] private OptionsView _optionsView;
        [SerializeField] private MapSelectView _mapSelectView;
        [SerializeField] private MatchSetupView _matchSetupView;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _storyButton;
        [SerializeField] private Button _mapButton;
        [SerializeField] private Button _playBackButton;
        [SerializeField] private Button _newStoryButton;
        [SerializeField] private Button _loadStoryButton;
        [SerializeField] private Button _storyBackButton;
        [SerializeField] private Button _mapBackButton;
        [SerializeField] private LoadGamePanel _loadGamePanel;

        private PauseService _pauseService;
        private SceneFlowService _sceneFlow;
        private MatchSessionService _matchSession;
        private InputRouter _inputRouter;
        private bool _controlsBound;

        public PageStack PageStack => _pageStack;
        public ModalStack ModalStack => _modalStack;
        public LoadGamePanel LoadGamePanel => _loadGamePanel;

        private void OnEnable()
        {
            BindControls();
            TryBindServices();
            _pageStack?.ResetTo(MainPageId);
            _modalStack?.CloseAll();
            SetStatus("SELECT START TO CONTINUE");
        }

        private void Start()
        {
            TryBindServices();
        }

        private void OnDisable()
        {
            // UnityEngine.Object can be a destroyed "fake null" during scene/test teardown.
            // Do not use ?. here because it bypasses Unity's overloaded null comparison.
            if (_pauseService != null)
            {
                _pauseService.UnregisterEscapeHandler(this);
            }

            if (_inputRouter != null)
            {
                _inputRouter.SetGameplayEnabled(true);
            }

            _loadGamePanel?.Close();

            _pauseService = null;
            _sceneFlow = null;
            _matchSession = null;
            _inputRouter = null;
        }

        private void OnDestroy()
        {
            if (!_controlsBound)
            {
                return;
            }

            if (_startButton != null) _startButton.onClick.RemoveListener(OpenPlay);
            if (_optionsButton != null) _optionsButton.onClick.RemoveListener(OpenOptions);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(ShowQuitConfirmation);
            if (_storyButton != null) _storyButton.onClick.RemoveListener(OpenStory);
            if (_mapButton != null) _mapButton.onClick.RemoveListener(OpenMap);
            if (_playBackButton != null) _playBackButton.onClick.RemoveListener(GoBack);
            if (_newStoryButton != null) _newStoryButton.onClick.RemoveListener(OpenStoryMatchSetup);
            if (_loadStoryButton != null) _loadStoryButton.onClick.RemoveListener(ShowLoadDeferred);
            if (_storyBackButton != null) _storyBackButton.onClick.RemoveListener(GoBack);
            if (_mapBackButton != null) _mapBackButton.onClick.RemoveListener(GoBack);
            if (_optionsView != null)
            {
                _optionsView.BackRequested -= GoBack;
            }

            if (_mapSelectView != null)
            {
                _mapSelectView.SystemTestSelected -= OpenMapMatchSetup;
            }

            if (_matchSetupView != null)
            {
                _matchSetupView.Confirmed -= StartConfiguredMatch;
                _matchSetupView.BackRequested -= GoBack;
            }
        }

        public void Configure(
            PageStack pageStack,
            ModalStack modalStack,
            ConfirmDialog confirmDialog,
            OptionsView optionsView,
            MapSelectView mapSelectView,
            TMP_Text statusLabel,
            Button startButton,
            Button optionsButton,
            Button quitButton,
            Button storyButton,
            Button mapButton,
            Button playBackButton,
            Button newStoryButton,
            Button loadStoryButton,
            Button storyBackButton,
            Button mapBackButton)
        {
            _pageStack = pageStack;
            _modalStack = modalStack;
            _confirmDialog = confirmDialog;
            _optionsView = optionsView;
            _mapSelectView = mapSelectView;
            _statusLabel = statusLabel;
            _startButton = startButton;
            _optionsButton = optionsButton;
            _quitButton = quitButton;
            _storyButton = storyButton;
            _mapButton = mapButton;
            _playBackButton = playBackButton;
            _newStoryButton = newStoryButton;
            _loadStoryButton = loadStoryButton;
            _storyBackButton = storyBackButton;
            _mapBackButton = mapBackButton;
            if (Application.isPlaying)
            {
                BindControls();
            }
        }

        public bool TryHandleEscape()
        {
            if (_loadGamePanel != null && _loadGamePanel.TryHandleEscape())
            {
                return true;
            }

            if (_modalStack != null && _modalStack.CloseTop())
            {
                return true;
            }

            if (_pageStack != null && _pageStack.Pop())
            {
                return true;
            }

            ShowQuitConfirmation();
            return true;
        }

        public void OpenPlay()
        {
            _pageStack?.Push(PlayPageId);
            SetStatus("CHOOSE STORY OR MAP");
        }

        public void OpenOptions()
        {
            _pageStack?.Push(OptionsPageId);
            SetStatus("OPTIONS");
        }

        public void OpenStory()
        {
            _pageStack?.Push(StoryPageId);
            SetStatus("STORY FLOW");
        }

        public void OpenMap()
        {
            _pageStack?.Push(MapPageId);
            SetStatus("ONLY SYSTEM TEST 0 IS AVAILABLE IN P13");
        }

        public void GoBack()
        {
            _pageStack?.Pop();
            SetStatus("BACK");
        }

        public void StartSystemTest()
        {
            TryBindServices();
            if (_sceneFlow == null)
            {
                SetStatus("SCENE FLOW SERVICE UNAVAILABLE");
                return;
            }

            MatchConfiguration configuration = MatchConfiguration.CreateSystemTestFallback();
            string error = "MATCH SESSION UNAVAILABLE";
            if (_matchSession != null && _matchSession.StartNewMatch(configuration, out error))
            {
                SetStatus("LOADING SYSTEM TEST 0...");
                return;
            }

            SetStatus(string.IsNullOrWhiteSpace(error)
                ? "SYSTEM TEST 0 COULD NOT BE LOADED"
                : error);
        }

        public void ShowLoadDeferred()
        {
            if (_loadGamePanel != null)
            {
                _loadGamePanel.Show(SavePanelMode.Load);
                SetStatus("SELECT A SAVE SLOT");
                return;
            }

            SetStatus("LOAD PANEL UNAVAILABLE");
        }

        public void ConfigurePersistence(LoadGamePanel loadGamePanel)
        {
            _loadGamePanel = loadGamePanel;
        }

        public void ConfigureMatchSetup(MatchSetupView matchSetupView)
        {
            if (_matchSetupView != null)
            {
                _matchSetupView.Confirmed -= StartConfiguredMatch;
                _matchSetupView.BackRequested -= GoBack;
            }

            _matchSetupView = matchSetupView;
            if (_matchSetupView != null)
            {
                _matchSetupView.Confirmed += StartConfiguredMatch;
                _matchSetupView.BackRequested += GoBack;
            }
        }

        public void OpenStoryMatchSetup()
        {
            OpenMatchSetup(MatchGameModeSource.Story, "map.system-test-0");
        }

        public void OpenMapMatchSetup()
        {
            OpenMatchSetup(MatchGameModeSource.Skirmish, "map.system-test-0");
        }

        private void OpenMatchSetup(MatchGameModeSource source, string mapId)
        {
            if (_matchSetupView == null)
            {
                StartSystemTest();
                return;
            }

            _matchSetupView.Show(source, mapId);
            _pageStack?.Push(MatchSetupPageId);
            SetStatus("CONFIGURE COMMANDER, HERO AND WEAPONS");
        }

        private void StartConfiguredMatch(MatchConfiguration configuration)
        {
            TryBindServices();
            string error = "MATCH SESSION UNAVAILABLE";
            if (_matchSession != null && _matchSession.StartNewMatch(configuration, out error))
            {
                SetStatus("LOADING " + configuration.mapId + "...");
                return;
            }

            SetStatus(error);
        }

        public void ShowQuitConfirmation()
        {
            if (_confirmDialog == null ||
                !_confirmDialog.Show(
                    "QUIT",
                    "QUIT ORIGINCORE?",
                    Application.Quit))
            {
                SetStatus("QUIT CONFIRMATION UNAVAILABLE");
            }
        }

        private void TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return;
            }

            if (_pauseService != appRoot.Services.PauseService)
            {
                _pauseService?.UnregisterEscapeHandler(this);
                _pauseService = appRoot.Services.PauseService;
                _pauseService?.RegisterEscapeHandler(this);
            }

            _sceneFlow = appRoot.Services.SceneFlow;
            _matchSession = appRoot.Services.MatchSession;
            _inputRouter = appRoot.Services.InputRouter;
            _inputRouter?.SetGameplayEnabled(false);
        }

        private void BindControls()
        {
            if (_controlsBound || _startButton == null || _optionsButton == null ||
                _quitButton == null || _storyButton == null || _mapButton == null ||
                _playBackButton == null || _newStoryButton == null ||
                _loadStoryButton == null || _storyBackButton == null ||
                _mapBackButton == null)
            {
                return;
            }

            _startButton.onClick.AddListener(OpenPlay);
            _optionsButton.onClick.AddListener(OpenOptions);
            _quitButton.onClick.AddListener(ShowQuitConfirmation);
            _storyButton.onClick.AddListener(OpenStory);
            _mapButton.onClick.AddListener(OpenMap);
            _playBackButton.onClick.AddListener(GoBack);
            _newStoryButton.onClick.AddListener(OpenStoryMatchSetup);
            _loadStoryButton.onClick.AddListener(ShowLoadDeferred);
            _storyBackButton.onClick.AddListener(GoBack);
            _mapBackButton.onClick.AddListener(GoBack);
            if (_optionsView != null)
            {
                _optionsView.BackRequested -= GoBack;
                _optionsView.BackRequested += GoBack;
            }

            if (_mapSelectView != null)
            {
                _mapSelectView.SystemTestSelected -= OpenMapMatchSetup;
                _mapSelectView.SystemTestSelected += OpenMapMatchSetup;
            }

            if (_matchSetupView != null)
            {
                _matchSetupView.Confirmed -= StartConfiguredMatch;
                _matchSetupView.Confirmed += StartConfiguredMatch;
                _matchSetupView.BackRequested -= GoBack;
                _matchSetupView.BackRequested += GoBack;
            }

            _controlsBound = true;
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message ?? string.Empty;
            }
        }
    }
}
