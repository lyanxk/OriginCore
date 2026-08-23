using OriginCore.Core;
using UnityEngine;

namespace OriginCore.SceneFlow
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class SceneBootProxy : MonoBehaviour
    {
        [SerializeField] private AppRoot _appRootPrefab;

        public AppRoot AppRootPrefab => _appRootPrefab;

        private void Awake()
        {
            if (AppRoot.HasInstance || GameBootstrap.HasInstance)
            {
                return;
            }

            if (_appRootPrefab == null)
            {
                Debug.LogError(
                    "[OriginCore SceneBootProxy] PF_AppRoot reference is missing in scene '" +
                    gameObject.scene.name + "'.",
                    this);
                return;
            }

            AppRoot instance = Instantiate(_appRootPrefab);
            instance.name = _appRootPrefab.name;
        }
    }
}
