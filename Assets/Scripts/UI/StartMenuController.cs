using Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI
{
    public sealed class StartMenuController : MonoBehaviour
    {
        static readonly string[] PreferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "DengXian",
            "SimHei",
            "SimSun",
            "Noto Sans SC"
        };

        [Header("Navigation")]
        [SerializeField] string testSceneName = "TestScene";

        [Header("Layout")]
        [SerializeField] Color backgroundColor = Color.white;
        [SerializeField] Color buttonColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] Color buttonTextColor = Color.black;
        [SerializeField] Vector2 buttonSize = new Vector2(280f, 64f);
        [SerializeField] float buttonSpacing = 20f;
        [SerializeField] int buttonFontSize = 28;

        Button _startButton;
        Button _quitButton;
        Font _font;

        void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureCamera();
            EnsureEventSystem();
            BuildMenu();
        }

        void OnDestroy()
        {
            if (_startButton != null)
                _startButton.onClick.RemoveListener(EnterTestScene);
            if (_quitButton != null)
                _quitButton.onClick.RemoveListener(QuitGame);
        }

        public void EnterTestScene()
        {
            if (string.IsNullOrWhiteSpace(testSceneName))
            {
                Debug.LogError("Start menu cannot enter the test scene because no scene name is set.");
                return;
            }

            GameObject finalizerObject = new GameObject("Start Menu Scene Load Finalizer");
            finalizerObject.AddComponent<StartMenuSceneLoadFinalizer>().Initialize(testSceneName);
            SceneManager.LoadScene(testSceneName, LoadSceneMode.Single);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildMenu()
        {
            _font = ResolveFont();

            GameObject canvasObject = new GameObject(
                "StartMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateBackground(canvasObject.transform);
            RectTransform buttonGroup = CreateButtonGroup(canvasObject.transform);
            _startButton = CreateButton(
                buttonGroup,
                GameText.GetText("ui.start.enterTestScene", "ui.start.enterTestScene"),
                EnterTestScene);
            _quitButton = CreateButton(
                buttonGroup,
                GameText.GetText("ui.start.quit", "ui.start.quit"),
                QuitGame);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
        }

        void CreateBackground(Transform parent)
        {
            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);

            RectTransform rect = backgroundObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = backgroundObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = false;
        }

        RectTransform CreateButtonGroup(Transform parent)
        {
            GameObject groupObject = new GameObject(
                "ButtonGroup",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            groupObject.transform.SetParent(parent, false);

            RectTransform rect = groupObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(buttonSize.x, buttonSize.y * 2f + buttonSpacing);

            VerticalLayoutGroup layout = groupObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = buttonSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return rect;
        }

        Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(
                label,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = buttonSize.x;
            layout.preferredHeight = buttonSize.y;

            Image image = buttonObject.GetComponent<Image>();
            image.color = buttonColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            CreateButtonLabel(buttonObject.transform, label);
            return button;
        }

        void CreateButtonLabel(Transform parent, string label)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.font = _font;
            text.fontSize = buttonFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = buttonTextColor;
            text.raycastTarget = false;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        void EnsureCamera()
        {
            UnityEngine.Camera sceneCamera = FindMenuSceneCamera();
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Start Menu Camera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
                sceneCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            }

            sceneCamera.gameObject.tag = "Untagged";
            sceneCamera.gameObject.name = "Start Menu Camera";
            sceneCamera.enabled = true;
            sceneCamera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = backgroundColor;
            sceneCamera.cullingMask = 0;
        }

        UnityEngine.Camera FindMenuSceneCamera()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                UnityEngine.Camera[] cameras = roots[rootIndex].GetComponentsInChildren<UnityEngine.Camera>(true);
                for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                {
                    if (cameras[cameraIndex] != null)
                        return cameras[cameraIndex];
                }
            }

            return null;
        }

        static Font ResolveFont()
        {
            string[] installedFonts = Font.GetOSInstalledFontNames();
            for (int preferredIndex = 0; preferredIndex < PreferredFonts.Length; preferredIndex++)
            {
                string preferredFont = PreferredFonts[preferredIndex];
                for (int installedIndex = 0; installedIndex < installedFonts.Length; installedIndex++)
                {
                    if (!string.Equals(installedFonts[installedIndex], preferredFont, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    return Font.CreateDynamicFontFromOSFont(installedFonts[installedIndex], 48);
                }
            }

            for (int i = 0; i < PreferredFonts.Length; i++)
            {
                Font font = Font.CreateDynamicFontFromOSFont(PreferredFonts[i], 48);
                if (font != null)
                    return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    public sealed class StartMenuSceneLoadFinalizer : MonoBehaviour
    {
        string _targetSceneName;

        public void Initialize(string targetSceneName)
        {
            _targetSceneName = targetSceneName;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrWhiteSpace(_targetSceneName) && scene.name != _targetSceneName)
                return;

            SceneManager.SetActiveScene(scene);
            DynamicGI.UpdateEnvironment();
            Destroy(gameObject);
        }
    }
}
