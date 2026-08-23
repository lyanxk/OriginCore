using UnityEngine;

namespace OriginCore.Core
{
    [DefaultExecutionOrder(-11900)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameBootstrap), typeof(GameServices))]
    public sealed class AppRoot : MonoBehaviour
    {
        private static AppRoot s_instance;

        [SerializeField] private GameBootstrap _gameBootstrap;
        [SerializeField] private GameServices _gameServices;

        public static AppRoot Instance => s_instance;
        public static bool HasInstance => s_instance != null;
        public GameServices Services => _gameServices;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
        }

        public static bool TryGetInstance(out AppRoot appRoot)
        {
            appRoot = s_instance;
            return appRoot != null;
        }

        private void Awake()
        {
            if (_gameBootstrap == null)
            {
                Debug.LogError("[OriginCore AppRoot] GameBootstrap reference is missing.", this);
                enabled = false;
                return;
            }

            if (!_gameBootstrap.IsPrimaryInstance)
            {
                return;
            }

            if (s_instance != null && s_instance != this)
            {
                Debug.LogError("[OriginCore AppRoot] A second primary AppRoot was rejected.", this);
                Destroy(gameObject);
                return;
            }

            if (_gameServices == null)
            {
                Debug.LogError("[OriginCore AppRoot] GameServices reference is missing.", this);
                enabled = false;
                return;
            }

            s_instance = this;
            if (!_gameServices.Initialize(this))
            {
                Debug.LogError("[OriginCore AppRoot] Core services failed to initialize.", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (s_instance != this)
            {
                return;
            }

            if (_gameServices != null)
            {
                _gameServices.Shutdown(this);
            }

            s_instance = null;
        }
    }
}
