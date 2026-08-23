using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.SceneFlow
{
    [CreateAssetMenu(fileName = "SO_SceneCatalog", menuName = "OriginCore/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        public const string MainMenuSceneKey = "MainMenu";
        public const string SystemTestSceneKey = "system_test_0";

        [Serializable]
        public sealed class SceneEntry
        {
            [SerializeField] private string _sceneKey;
            [SerializeField] private string _scenePath;

            public string SceneKey => _sceneKey;
            public string ScenePath => _scenePath;

            public SceneEntry(string sceneKey, string scenePath)
            {
                _sceneKey = string.IsNullOrWhiteSpace(sceneKey)
                    ? string.Empty
                    : sceneKey.Trim();
                _scenePath = string.IsNullOrWhiteSpace(scenePath)
                    ? string.Empty
                    : scenePath.Trim();
            }
        }

        [SerializeField] private SceneEntry[] _entries = Array.Empty<SceneEntry>();

        private Dictionary<string, string> _pathsByKey;

        public IReadOnlyList<SceneEntry> Entries => _entries;

        public void Configure(SceneEntry[] entries)
        {
            _entries = entries != null
                ? (SceneEntry[])entries.Clone()
                : Array.Empty<SceneEntry>();
            _pathsByKey = null;
        }

        public bool TryGetScenePath(string sceneKey, out string scenePath)
        {
            scenePath = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                return false;
            }

            EnsureLookup();
            return _pathsByKey.TryGetValue(sceneKey, out scenePath);
        }

        public bool TryGetSceneKey(string scenePath, out string sceneKey)
        {
            sceneKey = string.Empty;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            string requestedRuntimePath = ToRuntimeLoadPath(scenePath);
            for (int i = 0; i < _entries.Length; i++)
            {
                SceneEntry entry = _entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SceneKey) ||
                    string.IsNullOrWhiteSpace(entry.ScenePath))
                {
                    continue;
                }

                string entryRuntimePath = ToRuntimeLoadPath(entry.ScenePath);
                if (string.Equals(
                        entryRuntimePath,
                        requestedRuntimePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    sceneKey = entry.SceneKey;
                    return true;
                }
            }

            return false;
        }

        public static string ToRuntimeLoadPath(string sceneAssetPath)
        {
            if (string.IsNullOrWhiteSpace(sceneAssetPath))
            {
                return string.Empty;
            }

            const string extension = ".unity";
            if (sceneAssetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return sceneAssetPath.Substring(0, sceneAssetPath.Length - extension.Length);
            }

            return sceneAssetPath;
        }

        public void CollectValidationIssues(ICollection<string> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                SceneEntry entry = _entries[i];
                if (entry == null)
                {
                    issues.Add("SceneCatalog entry " + i + " is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.SceneKey))
                {
                    issues.Add("SceneCatalog entry " + i + " has no scene key.");
                }
                else if (!keys.Add(entry.SceneKey))
                {
                    issues.Add("SceneCatalog contains duplicate key '" + entry.SceneKey + "'.");
                }

                if (string.IsNullOrWhiteSpace(entry.ScenePath))
                {
                    issues.Add("SceneCatalog entry '" + entry.SceneKey + "' has no scene path.");
                }
            }
        }

        private void OnEnable()
        {
            _pathsByKey = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _pathsByKey = null;
        }
#endif

        private void EnsureLookup()
        {
            if (_pathsByKey != null)
            {
                return;
            }

            _pathsByKey = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                SceneEntry entry = _entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SceneKey) ||
                    string.IsNullOrWhiteSpace(entry.ScenePath) || _pathsByKey.ContainsKey(entry.SceneKey))
                {
                    continue;
                }

                _pathsByKey.Add(entry.SceneKey, entry.ScenePath);
            }
        }
    }
}
