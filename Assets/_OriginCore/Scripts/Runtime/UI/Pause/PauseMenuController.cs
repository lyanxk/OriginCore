using System;
using OriginCore.Core;
using OriginCore.Matches;
using OriginCore.UI.Common;
using OriginCore.UI.Settings;
using OriginCore.UI.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Pause
{
    public interface IPersistenceMenuActions
    {
        bool TryLoad(out string message);
        bool TrySave(out string message);
        bool TrySaveAndExit(out string message);
    }

    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour, IUiEscapeHandler
    {
        public const string PausePageId = "Pause";
        public const string OptionsPageId = "Options";

        [SerializeField] private GameObject _visibleRoot;
        [SerializeField] private PageStack _pageStack;
        [SerializeField] private ModalStack _modalStack;
        [SerializeField] private ConfirmDialog _confirmDialog;
        [SerializeField] private OptionsView _optionsView;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _saveExitButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private LoadGamePanel _loadGamePanel;

        private PauseService _pauseService;
        private MatchSessionService _matchSession;
        private IPersistenceMenuActions _persistenceActions;
        private bool _controlsBound;

        public event Action LoadRequested;
        public event Action SaveRequested;
        public event Action SaveAndExitRequested;

        public GameObject VisibleRoot => _visibleRoot;
        public PageStack PageStack => _pageStack;
        public LoadGamePanel LoadGamePanel => _loadGamePanel;

        private void OnEnable()
        {
            BindControls();
            TryBindService();
            ApplyPauseVisibility(_pauseService != null && _pauseService.IsPaused);
        }

        private void Start()
        {
            TryBindService();
        }

        private void OnDisable()
        {
            if (_pauseService != null)
            {
                _pauseService.PauseStateChanged -= HandlePauseStateChanged;
                _pauseService.UnregisterEscapeHandler(this);
            }
        }

        private void OnDestroy()
        {
            if (!_controlsBound)
            {
                return;
            }

            if (_continueButton != null) _continueButton.onClick.RemoveListener(Continue);
            if (_optionsButton != null) _optionsButton.onClick.RemoveListener(OpenOptions);
            if (_loadButton != null) _loadButton.onClick.RemoveListener(Load);
            if (_saveButton != null) _saveButton.onClick.RemoveListener(Save);
            if (_saveExitButton != null)
            {
                _saveExitButton.onClick.RemoveListener(ShowSaveExitConfirmation);
            }
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(ShowMainMenuConfirmation);
            }
            if (_optionsView != null)
            {
                _optionsView.BackRequested -= GoBack;
            }
        }

        public void Configure(
            GameObject visibleRoot,
            PageStack pageStack,
            ModalStack modalStack,
            ConfirmDialog confirmDialog,
            OptionsView optionsView,
            TMP_Text statusLabel,
            Button continueButton,
            Button optionsButton,
            Button loadButton,
            Button saveButton,
            Button saveExitButton)
        {
            _visibleRoot = visibleRoot;
            _pageStack = pageStack;
            _modalStack = modalStack;
            _confirmDialog = confirmDialog;
            _optionsView = optionsView;
            _statusLabel = statusLabel;
            _continueButton = continueButton;
            _optionsButton = optionsButton;
            _loadButton = loadButton;
            _saveButton = saveButton;
            _saveExitButton = saveExitButton;
            if (Application.isPlaying)
            {
                BindControls();
            }
        }

        public void SetPersistenceActions(IPersistenceMenuActions actions)
        {
            _persistenceActions = actions;
        }

        public void ConfigurePersistence(LoadGamePanel loadGamePanel)
        {
            _loadGamePanel = loadGamePanel;
        }

        public void ConfigureMainMenuButton(Button mainMenuButton)
        {
            if (_controlsBound && _mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(ShowMainMenuConfirmation);
            }

            _mainMenuButton = mainMenuButton;
            if (_controlsBound && _mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(ShowMainMenuConfirmation);
            }
        }

        public bool TryHandleEscape()
        {
            if (_pauseService == null || !_pauseService.IsPaused)
            {
                return false;
            }

            if (_loadGamePanel != null && _loadGamePanel.TryHandleEscape())
            {
                return true;
            }

            if (_modalStack != null && _modalStack.CloseTop())
            {
                return true;
            }

            return _pageStack != null && _pageStack.Pop();
        }

        public void Continue()
        {
            _pauseService?.SetPaused(false);
        }

        public void OpenOptions()
        {
            _pageStack?.Push(OptionsPageId);
            SetStatus("OPTIONS REMAIN INTERACTIVE WHILE GAMEPLAY IS FROZEN");
        }

        public void GoBack()
        {
            _pageStack?.Pop();
        }

        public void Load()
        {
            LoadRequested?.Invoke();
            if (_loadGamePanel != null)
            {
                _loadGamePanel.Show(SavePanelMode.Load);
                SetStatus("SELECT A SAVE SLOT");
                return;
            }

            if (_persistenceActions != null && _persistenceActions.TryLoad(out string message))
            {
                SetStatus(message);
                return;
            }

            SetStatus("LOAD PANEL UNAVAILABLE");
        }

        public void Save()
        {
            SaveRequested?.Invoke();
            if (_loadGamePanel != null)
            {
                _loadGamePanel.Show(SavePanelMode.Save);
                SetStatus("SELECT A SLOT OR CREATE A NEW SAVE");
                return;
            }

            if (_persistenceActions != null && _persistenceActions.TrySave(out string message))
            {
                SetStatus(message);
                return;
            }

            SetStatus("SAVE PANEL UNAVAILABLE");
        }

        public void ShowSaveExitConfirmation()
        {
            if (_confirmDialog == null ||
                !_confirmDialog.Show(
                    "SAVE AND EXIT",
                    "SAVE CURRENT PROGRESS AND EXIT THE APPLICATION?",
                    SaveAndExit))
            {
                SetStatus("SAVE AND EXIT CONFIRMATION UNAVAILABLE");
            }
        }

        public void ShowMainMenuConfirmation()
        {
            if (_confirmDialog == null ||
                !_confirmDialog.Show(
                    "RETURN TO MAIN MENU",
                    "RETURN WITHOUT AUTOMATICALLY SAVING?",
                    ReturnToMainMenu))
            {
                SetStatus("MAIN MENU CONFIRMATION UNAVAILABLE");
            }
        }

        private void ReturnToMainMenu()
        {
            _loadGamePanel?.Close();
            _modalStack?.CloseAll();
            _pauseService?.SetPaused(false);
            string error = "MATCH SESSION UNAVAILABLE";
            if (_matchSession != null && _matchSession.ReturnToMainMenu(out error))
            {
                SetStatus("RETURNING TO MAIN MENU");
                return;
            }

            SetStatus(string.IsNullOrWhiteSpace(error)
                ? "MATCH SESSION UNAVAILABLE"
                : error);
        }

        private void SaveAndExit()
        {
            SaveAndExitRequested?.Invoke();
            if (_loadGamePanel != null)
            {
                _loadGamePanel.Show(SavePanelMode.Save, true);
                SetStatus("SAVE MUST SUCCEED BEFORE EXIT");
                return;
            }

            if (_persistenceActions != null &&
                _persistenceActions.TrySaveAndExit(out string message))
            {
                SetStatus(message);
                return;
            }

            SetStatus("SAVE PANEL UNAVAILABLE; APPLICATION REMAINS OPEN");
        }

        private void TryBindService()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return;
            }

            PauseService next = appRoot.Services.PauseService;
            if (_pauseService == next)
            {
                return;
            }

            if (_pauseService != null)
            {
                _pauseService.PauseStateChanged -= HandlePauseStateChanged;
                _pauseService.UnregisterEscapeHandler(this);
            }

            _pauseService = next;
            _matchSession = appRoot.Services.MatchSession;
            if (_pauseService != null)
            {
                _pauseService.PauseStateChanged += HandlePauseStateChanged;
                _pauseService.RegisterEscapeHandler(this);
                ApplyPauseVisibility(_pauseService.IsPaused);
            }
        }

        private void HandlePauseStateChanged(PauseStateChange change)
        {
            ApplyPauseVisibility(change.IsPaused);
        }

        private void ApplyPauseVisibility(bool visible)
        {
            if (_visibleRoot != null)
            {
                _visibleRoot.SetActive(visible);
            }

            if (visible)
            {
                _pageStack?.ResetTo(PausePageId);
                _modalStack?.CloseAll();
                SetStatus("GAMEPLAY PAUSED");
            }
            else
            {
                _loadGamePanel?.Close();
            }
        }

        private void BindControls()
        {
            if (_controlsBound || _continueButton == null || _optionsButton == null ||
                _loadButton == null || _saveButton == null || _saveExitButton == null)
            {
                return;
            }

            _continueButton.onClick.AddListener(Continue);
            _optionsButton.onClick.AddListener(OpenOptions);
            _loadButton.onClick.AddListener(Load);
            _saveButton.onClick.AddListener(Save);
            _saveExitButton.onClick.AddListener(ShowSaveExitConfirmation);
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(ShowMainMenuConfirmation);
            }
            if (_optionsView != null)
            {
                _optionsView.BackRequested -= GoBack;
                _optionsView.BackRequested += GoBack;
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
