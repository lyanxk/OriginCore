using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.SceneFlow
{
    [DefaultExecutionOrder(-11700)]
    [DisallowMultipleComponent]
    public sealed class SceneFlowService : MonoBehaviour
    {
        [SerializeField] private SceneCatalog _sceneCatalog;

        private bool _isLoading;
        private string _pendingSceneKey;
        private string _pendingRuntimePath;

        public bool IsLoading => _isLoading;
        public SceneCatalog Catalog => _sceneCatalog;

        public event Action<bool> LoadingStateChanged;
        public event Action<string, Scene> LoadCompleted;
        public event Action<string, string> LoadFailed;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public bool LoadScene(string sceneKey)
        {
            int buildIndex;
            if (!TryBeginLoad(sceneKey, out buildIndex))
            {
                return false;
            }

            try
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                FailPendingLoad("Unity rejected the scene load: " + exception.Message);
                return false;
            }
        }

        public AsyncOperation LoadSceneAsync(string sceneKey)
        {
            int buildIndex;
            if (!TryBeginLoad(sceneKey, out buildIndex))
            {
                return null;
            }

            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
                if (operation == null)
                {
                    FailPendingLoad("Unity returned no AsyncOperation for the scene load.");
                }

                return operation;
            }
            catch (Exception exception)
            {
                FailPendingLoad("Unity rejected the asynchronous scene load: " + exception.Message);
                return null;
            }
        }

        private bool TryBeginLoad(string sceneKey, out int buildIndex)
        {
            buildIndex = -1;

            if (_isLoading)
            {
                ReportFailure(
                    sceneKey,
                    "A scene load is already in progress for key '" + _pendingSceneKey + "'.");
                return false;
            }

            if (_sceneCatalog == null)
            {
                ReportFailure(sceneKey, "SceneCatalog reference is missing.");
                return false;
            }

            string sceneAssetPath;
            if (!_sceneCatalog.TryGetScenePath(sceneKey, out sceneAssetPath))
            {
                ReportFailure(sceneKey, "Scene key was not found in the catalog.");
                return false;
            }

            string runtimePath = SceneCatalog.ToRuntimeLoadPath(sceneAssetPath);
            if (string.IsNullOrWhiteSpace(runtimePath))
            {
                ReportFailure(sceneKey, "The catalog entry has an empty runtime path.");
                return false;
            }

            buildIndex = SceneUtility.GetBuildIndexByScenePath(sceneAssetPath);
            if (buildIndex < 0)
            {
                ReportFailure(
                    sceneKey,
                    "Scene '" + sceneAssetPath + "' is not available in Build Settings.");
                return false;
            }

            _pendingSceneKey = sceneKey;
            _pendingRuntimePath = runtimePath;
            SetLoading(true);
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_isLoading || !MatchesPendingScene(scene))
            {
                return;
            }

            string completedKey = _pendingSceneKey;
            ClearPendingLoad();
            LoadCompleted?.Invoke(completedKey, scene);
        }

        private bool MatchesPendingScene(Scene scene)
        {
            string loadedRuntimePath = SceneCatalog.ToRuntimeLoadPath(scene.path);
            if (string.Equals(loadedRuntimePath, _pendingRuntimePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int separator = _pendingRuntimePath.LastIndexOf('/');
            string expectedName = separator >= 0
                ? _pendingRuntimePath.Substring(separator + 1)
                : _pendingRuntimePath;
            return string.Equals(scene.name, expectedName, StringComparison.OrdinalIgnoreCase);
        }

        private void FailPendingLoad(string reason)
        {
            string failedKey = _pendingSceneKey;
            ClearPendingLoad();
            ReportFailure(failedKey, reason);
        }

        private void ClearPendingLoad()
        {
            _pendingSceneKey = string.Empty;
            _pendingRuntimePath = string.Empty;
            SetLoading(false);
        }

        private void SetLoading(bool isLoading)
        {
            if (_isLoading == isLoading)
            {
                return;
            }

            _isLoading = isLoading;
            LoadingStateChanged?.Invoke(_isLoading);
        }

        private void ReportFailure(string sceneKey, string reason)
        {
            string safeKey = string.IsNullOrWhiteSpace(sceneKey) ? "<empty>" : sceneKey;
            Debug.LogError("[OriginCore SceneFlow] Load failed for '" + safeKey + "': " + reason, this);
            LoadFailed?.Invoke(safeKey, reason);
        }
    }
}
