using OriginCore.SceneFlow;
using UnityEngine;

namespace OriginCore.Core
{
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static GameBootstrap s_instance;
        private static bool s_duplicateWarningLogged;

        [SerializeField] private AppRoot _appRoot;
        [SerializeField] private bool _loadMainMenuOnStart;

        public static GameBootstrap Instance => s_instance;
        public static bool HasInstance => s_instance != null;
        public bool IsPrimaryInstance => s_instance == this;
        public bool LoadsMainMenuOnStart => _loadMainMenuOnStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            s_duplicateWarningLogged = false;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                if (!s_duplicateWarningLogged)
                {
                    s_duplicateWarningLogged = true;
                    Debug.LogWarning(
                        "[OriginCore Bootstrap] A duplicate AppRoot was rejected. " +
                        "Only the first persistent service root is kept.",
                        this);
                }

                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!IsPrimaryInstance || !_loadMainMenuOnStart)
            {
                return;
            }

            if (_appRoot == null || _appRoot.Services == null || _appRoot.Services.SceneFlow == null)
            {
                Debug.LogError(
                    "[OriginCore Bootstrap] Cannot load MainMenu because the AppRoot service references are incomplete.",
                    this);
                return;
            }

            _appRoot.Services.SceneFlow.LoadSceneAsync(SceneCatalog.MainMenuSceneKey);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }
    }
}
