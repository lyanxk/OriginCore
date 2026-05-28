using Content;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] GameObject menuRoot;
        [SerializeField] CanvasGroup menuCanvasGroup;
        [SerializeField] Button continueButton;
        [SerializeField] Button resetSceneButton;
        [SerializeField] Button quitButton;

        [Header("Behavior")]
        [SerializeField] bool pauseTimeScale = true;
        [SerializeField] bool unlockCursorWhileOpen = true;
        [SerializeField] string sceneToReset;

        bool _isOpen;
        float _timeScaleBeforePause = 1f;
        CursorLockMode _cursorLockBeforeOpen;
        bool _cursorVisibleBeforeOpen;

        void Awake()
        {
            ApplyTexts();
            WireButtons();
            SetMenuVisible(false, false);
        }

        void OnDestroy()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(ContinueGame);
            if (resetSceneButton != null)
                resetSceneButton.onClick.RemoveListener(ResetScene);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(QuitGame);
        }

        void Update()
        {
            if (WasEscapePressedThisFrame())
                SetMenuVisible(!_isOpen);
        }

        public void ContinueGame()
        {
            SetMenuVisible(false);
        }

        public void ResetScene()
        {
            if (pauseTimeScale)
                Time.timeScale = 1f;

            Scene activeScene = SceneManager.GetActiveScene();
            string targetScene = string.IsNullOrWhiteSpace(sceneToReset)
                ? ResolveActiveSceneIdentifier(activeScene)
                : sceneToReset;

#if UNITY_EDITOR
            if (targetScene.StartsWith("Assets/"))
            {
                EditorSceneManager.LoadSceneInPlayMode(
                    targetScene,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(targetScene);
        }

        public void QuitGame()
        {
            if (pauseTimeScale)
                Time.timeScale = 1f;

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void WireButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(ContinueGame);
                continueButton.onClick.AddListener(ContinueGame);
            }

            if (resetSceneButton != null)
            {
                resetSceneButton.onClick.RemoveListener(ResetScene);
                resetSceneButton.onClick.AddListener(ResetScene);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(QuitGame);
                quitButton.onClick.AddListener(QuitGame);
            }
        }

        void ApplyTexts()
        {
            SetButtonLabel(continueButton, GameText.GetText("ui.pause.continue", "ui.pause.continue"));
            SetButtonLabel(resetSceneButton, GameText.GetText("ui.pause.resetScene", "ui.pause.resetScene"));
            SetButtonLabel(quitButton, GameText.GetText("ui.pause.quit", "ui.pause.quit"));
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
                return;

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
                tmpText.text = label;

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
                legacyText.text = label;
        }

        void SetMenuVisible(bool visible, bool applyPause = true)
        {
            if (_isOpen == visible && applyPause)
                return;

            _isOpen = visible;

            if (menuRoot != null)
                menuRoot.SetActive(visible);

            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = visible ? 1f : 0f;
                menuCanvasGroup.interactable = visible;
                menuCanvasGroup.blocksRaycasts = visible;
            }

            if (!applyPause)
                return;

            ApplyTimeScale(visible);
            ApplyCursorState(visible);
            SelectDefaultButton(visible);
        }

        void ApplyTimeScale(bool paused)
        {
            if (!pauseTimeScale)
                return;

            if (paused)
            {
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
            }
        }

        void ApplyCursorState(bool menuOpen)
        {
            if (!unlockCursorWhileOpen)
                return;

            if (menuOpen)
            {
                _cursorLockBeforeOpen = Cursor.lockState;
                _cursorVisibleBeforeOpen = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _cursorLockBeforeOpen;
                Cursor.visible = _cursorVisibleBeforeOpen;
            }
        }

        void SelectDefaultButton(bool menuOpen)
        {
            if (!menuOpen || continueButton == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        static bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        static string ResolveActiveSceneIdentifier(Scene activeScene)
        {
            return string.IsNullOrEmpty(activeScene.path) ? activeScene.name : activeScene.path;
        }
    }
}
