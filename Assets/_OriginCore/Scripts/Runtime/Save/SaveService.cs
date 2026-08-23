using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using OriginCore.Core;
using OriginCore.Matches;
using OriginCore.SceneFlow;
using UnityEngine;

namespace OriginCore.Save
{
    public enum SaveOperationKind
    {
        Save = 0,
        Load = 1,
        Rename = 2,
        Delete = 3
    }

    public readonly struct SaveOperationResult
    {
        public SaveOperationResult(
            SaveOperationKind kind,
            string slotId,
            bool succeeded,
            string message)
        {
            Kind = kind;
            SlotId = slotId ?? string.Empty;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public SaveOperationKind Kind { get; }
        public string SlotId { get; }
        public bool Succeeded { get; }
        public string Message { get; }
    }

    [DefaultExecutionOrder(-11650)]
    [DisallowMultipleComponent]
    public sealed class SaveService : MonoBehaviour
    {
        private const float SceneContextTimeoutSeconds = 15f;

        private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();
        private readonly List<string> _warnings = new List<string>();

        private GameServices _services;
        private SaveRepository _repository;
        private Coroutine _loadRoutine;
        private Action _quitOverride;
        private bool _busy;
        private string _currentSlotId = string.Empty;

        public event Action SlotsChanged;
        public event Action<bool> BusyChanged;
        public event Action<SaveOperationResult> OperationFinished;

        public bool IsInitialized => _services != null && _repository != null;
        public bool IsBusy => _busy;
        public string CurrentSlotId => _currentSlotId;
        public SaveRepository Repository => _repository;

        internal bool Initialize(GameServices services)
        {
            if (services == null)
            {
                Debug.LogError("[OriginCore P14] SaveService requires GameServices.", this);
                return false;
            }

            if (_services != null && _services != services)
            {
                Debug.LogError("[OriginCore P14] SaveService is already initialized.", this);
                return false;
            }

            _services = services;
            if (_repository == null)
            {
                _repository = new SaveRepository(Path.Combine(
                    Application.persistentDataPath,
                    "OriginCore",
                    "Saves"));
            }

            RegisterDefaultParticipants();
            return true;
        }

        internal void Shutdown()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }

            _participants.Clear();
            _warnings.Clear();
            _services = null;
            _busy = false;
            _quitOverride = null;
        }

        public bool ConfigureRepositoryPath(string savesDirectory, out string error)
        {
            error = string.Empty;
            if (_busy)
            {
                error = "A save operation is already in progress.";
                return false;
            }

            try
            {
                _repository = new SaveRepository(savesDirectory);
                _currentSlotId = string.Empty;
                SlotsChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void SetQuitOverride(Action quitAction)
        {
            _quitOverride = quitAction;
        }

        public IReadOnlyList<SaveSlotMetadata> EnumerateSlots()
        {
            return _repository != null
                ? _repository.EnumerateSlots()
                : Array.Empty<SaveSlotMetadata>();
        }

        public bool TrySaveNew(
            string displayName,
            bool exitAfterSave,
            out SaveSlotMetadata metadata,
            out string error)
        {
            string slotId = SaveRepository.CreateSlotId();
            return TrySaveAs(slotId, displayName, exitAfterSave, out metadata, out error);
        }

        public bool TryOverwrite(
            string slotId,
            bool exitAfterSave,
            out SaveSlotMetadata metadata,
            out string error)
        {
            metadata = null;
            if (!EnsureReady(out error) ||
                !_repository.TryGetMetadata(slotId, out SaveSlotMetadata existing, out error))
            {
                return false;
            }

            return TrySaveAs(
                slotId,
                existing.DisplayName,
                exitAfterSave,
                out metadata,
                out error);
        }

        public bool TrySaveAs(
            string slotId,
            string displayName,
            bool exitAfterSave,
            out SaveSlotMetadata metadata,
            out string error)
        {
            metadata = null;
            if (!EnsureReady(out error) || !SaveRepository.IsValidSlotId(slotId))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Save slot id is invalid.";
                }

                return false;
            }

            if (!TryResolveCurrentSceneKey(out string sceneKey, out error))
            {
                Finish(SaveOperationKind.Save, slotId, false, error);
                return false;
            }

            bool previousGameplayEnabled = _services.InputRouter.GameplayEnabled;
            SetBusy(true);
            _services.InputRouter.SetGameplayEnabled(false);
            bool succeeded = false;
            try
            {
                SaveGameData data = SaveGameData.Create(
                    slotId,
                    displayName,
                    sceneKey,
                    DateTime.UtcNow);
                _warnings.Clear();
                SaveOperationContext context = new SaveOperationContext(
                    _services,
                    _services.CurrentSceneContext,
                    _warnings);
                for (int i = 0; i < _participants.Count; i++)
                {
                    _participants[i].Capture(context, data);
                }

                LogWarnings("capture");
                if (!_repository.TryWrite(data, out error))
                {
                    Finish(SaveOperationKind.Save, slotId, false, error);
                    return false;
                }

                _currentSlotId = slotId;
                _repository.TryGetMetadata(slotId, out metadata, out _);
                succeeded = true;
                SlotsChanged?.Invoke();
                Finish(SaveOperationKind.Save, slotId, true, "Save completed.");
                return true;
            }
            catch (Exception exception)
            {
                error = "Save capture failed: " + exception.Message;
                Finish(SaveOperationKind.Save, slotId, false, error);
                return false;
            }
            finally
            {
                _services.InputRouter.SetGameplayEnabled(previousGameplayEnabled);
                SetBusy(false);
                if (succeeded && exitAfterSave)
                {
                    RequestQuit();
                }
            }
        }

        public bool TryRename(string slotId, string displayName, out string error)
        {
            if (!EnsureReady(out error) || !_repository.TryRename(slotId, displayName, out error))
            {
                Finish(SaveOperationKind.Rename, slotId, false, error);
                return false;
            }

            SlotsChanged?.Invoke();
            Finish(SaveOperationKind.Rename, slotId, true, "Save renamed.");
            return true;
        }

        public bool TryDelete(string slotId, out string error)
        {
            if (!EnsureReady(out error) || !_repository.TryDelete(slotId, out error))
            {
                Finish(SaveOperationKind.Delete, slotId, false, error);
                return false;
            }

            if (string.Equals(_currentSlotId, slotId, StringComparison.Ordinal))
            {
                _currentSlotId = string.Empty;
            }

            SlotsChanged?.Invoke();
            Finish(SaveOperationKind.Delete, slotId, true, "Save deleted.");
            return true;
        }

        public bool TryBeginLoad(string slotId, out string error)
        {
            if (!EnsureReady(out error) || !_repository.TryRead(slotId, out SaveGameData data, out error))
            {
                Finish(SaveOperationKind.Load, slotId, false, error);
                return false;
            }

            MatchConfiguration matchConfiguration = data.matchConfiguration.ToConfiguration();
            if (!_services.MatchSession.TryPrepareLoad(matchConfiguration, out error))
            {
                Finish(SaveOperationKind.Load, slotId, false, error);
                return false;
            }

            if (_services.SceneFlow.Catalog == null ||
                !_services.SceneFlow.Catalog.TryGetScenePath(data.sceneKey, out _))
            {
                error = "Save references unknown scene key '" + data.sceneKey + "'.";
                _services.MatchSession.AbortPreparedLoad(error);
                Finish(SaveOperationKind.Load, slotId, false, error);
                return false;
            }

            SetBusy(true);
            _loadRoutine = StartCoroutine(LoadRoutine(data));
            error = string.Empty;
            return true;
        }

        private IEnumerator LoadRoutine(SaveGameData data)
        {
            string slotId = data.slotId;
            bool previousPaused = _services.PauseService.IsPaused;
            bool previousGameplayEnabled = _services.InputRouter.GameplayEnabled;

            if (previousPaused)
            {
                _services.PauseService.SetPaused(false);
            }

            _services.InputRouter.SetGameplayEnabled(false);
            if (_services.GameModeController.CurrentMode != GameMode.RTS)
            {
                _services.GameModeController.RequestMode(GameMode.RTS);
            }

            AsyncOperation operation = _services.SceneFlow.LoadSceneAsync(data.sceneKey);
            if (operation == null)
            {
                _services.MatchSession.AbortPreparedLoad("Scene load could not be started.");
                FailLoadBeforeSceneChange(
                    slotId,
                    "Scene load could not be started.",
                    previousPaused,
                    previousGameplayEnabled);
                yield break;
            }

            yield return operation;
            _services.InputRouter.SetGameplayEnabled(false);

            float deadline = Time.realtimeSinceStartup + SceneContextTimeoutSeconds;
            while (!IsExpectedSceneContextReady(data.sceneKey) &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (_services.InputRouter.GameplayEnabled)
                {
                    _services.InputRouter.SetGameplayEnabled(false);
                }

                yield return null;
            }

            if (!IsExpectedSceneContextReady(data.sceneKey))
            {
                FailLoadAfterSceneChange(
                    slotId,
                    "The loaded scene did not register its SceneContext in time.");
                yield break;
            }

            _warnings.Clear();
            SaveOperationContext context = new SaveOperationContext(
                _services,
                _services.CurrentSceneContext,
                _warnings);
            try
            {
                for (int i = 0; i < _participants.Count; i++)
                {
                    _participants[i].Restore(context, data);
                }
            }
            catch (Exception exception)
            {
                FailLoadAfterSceneChange(
                    slotId,
                    "Restore failed in participant pipeline: " + exception.Message);
                yield break;
            }

            LogWarnings("restore");
            _currentSlotId = slotId;
            _services.PauseService.SetPaused(false);
            _services.InputRouter.SetGameplayEnabled(true);
            _loadRoutine = null;
            SetBusy(false);
            Finish(SaveOperationKind.Load, slotId, true, "Load completed.");
        }

        private void FailLoadBeforeSceneChange(
            string slotId,
            string error,
            bool previousPaused,
            bool previousGameplayEnabled)
        {
            _services.MatchSession.AbortPreparedLoad(error);
            if (previousPaused)
            {
                _services.PauseService.SetPaused(true);
            }
            else
            {
                _services.InputRouter.SetGameplayEnabled(previousGameplayEnabled);
            }

            _loadRoutine = null;
            SetBusy(false);
            Finish(SaveOperationKind.Load, slotId, false, error);
        }

        private void FailLoadAfterSceneChange(string slotId, string error)
        {
            _services.MatchSession.AbortPreparedLoad(error);
            _services.PauseService.SetPaused(false);
            _services.InputRouter.SetGameplayEnabled(true);
            _loadRoutine = null;
            SetBusy(false);
            Finish(SaveOperationKind.Load, slotId, false, error);
        }

        private bool EnsureReady(out string error)
        {
            if (!IsInitialized)
            {
                error = "SaveService is not initialized.";
                return false;
            }

            if (_busy)
            {
                error = "A save operation is already in progress.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryResolveCurrentSceneKey(out string sceneKey, out string error)
        {
            sceneKey = string.Empty;
            SceneContext context = _services.CurrentSceneContext;
            if (context == null)
            {
                error = "The current scene has no registered SceneContext.";
                return false;
            }

            if (_services.SceneFlow.Catalog == null ||
                !_services.SceneFlow.Catalog.TryGetSceneKey(context.Scene.path, out sceneKey))
            {
                error = "The current scene is not registered in SceneCatalog.";
                return false;
            }

            if (string.Equals(sceneKey, SceneCatalog.MainMenuSceneKey, StringComparison.Ordinal))
            {
                error = "MainMenu is not a saveable gameplay scene.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool IsExpectedSceneContextReady(string sceneKey)
        {
            SceneContext context = _services.CurrentSceneContext;
            return context != null && _services.SceneFlow.Catalog != null &&
                   _services.SceneFlow.Catalog.TryGetSceneKey(context.Scene.path, out string currentKey) &&
                   string.Equals(currentKey, sceneKey, StringComparison.Ordinal);
        }

        private void RegisterDefaultParticipants()
        {
            if (_participants.Count > 0)
            {
                return;
            }

            _participants.Add(new MatchConfigurationSaveParticipant());
            _participants.Add(new EntitySaveParticipant());
            _participants.Add(new ConstructionSaveParticipant());
            _participants.Add(new HeroDeploymentSaveParticipant());
            _participants.Add(new RallyPointSaveParticipant());
            _participants.Add(new ResourceGatheringSaveParticipant());
            _participants.Add(new ProductionQueueSaveParticipant());
            _participants.Add(new EnemyAiSaveParticipant());
            _participants.Add(new InventoryAbilitySaveParticipant());
            _participants.Add(new ResourceSaveParticipant());
            _participants.Add(new TechnologySaveParticipant());
            _participants.Add(new VisibilitySaveParticipant());
            _participants.Add(new GameModeSaveParticipant());
            _participants.Sort((left, right) => left.RestoreOrder.CompareTo(right.RestoreOrder));
        }

        private void SetBusy(bool busy)
        {
            if (_busy == busy)
            {
                return;
            }

            _busy = busy;
            BusyChanged?.Invoke(_busy);
        }

        private void Finish(
            SaveOperationKind kind,
            string slotId,
            bool succeeded,
            string message)
        {
            SaveOperationResult result = new SaveOperationResult(
                kind,
                slotId,
                succeeded,
                message);
            OperationFinished?.Invoke(result);
            if (!succeeded && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogError("[OriginCore P14] " + message, this);
            }
        }

        private void LogWarnings(string stage)
        {
            for (int i = 0; i < _warnings.Count; i++)
            {
                Debug.LogWarning(
                    "[OriginCore P14] Save " + stage + " warning: " + _warnings[i],
                    this);
            }
        }

        private void RequestQuit()
        {
            if (_quitOverride != null)
            {
                _quitOverride.Invoke();
                return;
            }

#if UNITY_EDITOR
            Type editorApplication = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            System.Reflection.PropertyInfo isPlaying = editorApplication?.GetProperty(
                "isPlaying",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (isPlaying != null && isPlaying.CanWrite)
            {
                isPlaying.SetValue(null, false, null);
                return;
            }
#endif
            Application.Quit();
        }
    }
}
