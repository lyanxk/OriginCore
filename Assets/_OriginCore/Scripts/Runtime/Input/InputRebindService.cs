using System;
using OriginCore.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OriginCore.Input
{
    [DefaultExecutionOrder(-11790)]
    [DisallowMultipleComponent]
    public sealed class InputRebindService : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actionsAsset;
        [SerializeField] private SettingsData _settingsData = new SettingsData();
        [SerializeField] private bool _persistToPlayerPrefs = true;

        private InputActionRebindingExtensions.RebindingOperation _activeRebind;
        private InputAction _rebindAction;
        private int _rebindBindingIndex = -1;
        private bool _rebindActionWasEnabled;

        public event Action<InputAction, int> RebindCompleted;
        public event Action<InputAction, int> RebindCanceled;
        public event Action BindingOverridesChanged;

        public InputActionAsset ActionsAsset => _actionsAsset;
        public SettingsData SettingsData => _settingsData;
        public bool IsRebinding => _activeRebind != null;
        public int LastRebindCanceledFrame { get; private set; } = -1;

        private void Awake()
        {
            if (_settingsData == null)
            {
                _settingsData = new SettingsData();
            }

            if (_actionsAsset == null || !_persistToPlayerPrefs)
            {
                return;
            }

            string error;
            InputBindingPersistence.TryLoadSettingsFromPlayerPrefs(_settingsData, out error);
            if (!InputBindingPersistence.TryApplyFromSettings(_actionsAsset, _settingsData, out error))
            {
                Debug.LogWarning(
                    "[OriginCore Input] Ignored invalid persisted binding overrides: " + error,
                    this);
                InputBindingPersistence.ClearPersistedOverrides(_actionsAsset, _settingsData);
            }
        }

        private void OnDisable()
        {
            CancelCurrentRebind();
        }

        public bool Configure(
            InputActionAsset actionsAsset,
            SettingsData settingsData = null,
            bool persistToPlayerPrefs = true)
        {
            if (actionsAsset == null || IsRebinding)
            {
                return false;
            }

            _actionsAsset = actionsAsset;
            _settingsData = settingsData ?? new SettingsData();
            _persistToPlayerPrefs = persistToPlayerPrefs;
            return true;
        }

        public bool BeginInteractiveRebind(string mapName, string actionName, int bindingIndex)
        {
            string error;
            return TryBeginInteractiveRebind(mapName, actionName, bindingIndex, out error);
        }

        public bool TryBeginInteractiveRebind(
            string mapName,
            string actionName,
            int bindingIndex,
            out string error)
        {
            InputAction action;
            if (!TryGetBinding(mapName, actionName, bindingIndex, out action, out error))
            {
                return false;
            }

            if (action.bindings[bindingIndex].isComposite)
            {
                error = "Composite roots cannot be rebound directly; choose one of their part bindings.";
                return false;
            }

            CancelCurrentRebind();
            _rebindAction = action;
            _rebindBindingIndex = bindingIndex;
            _rebindActionWasEnabled = action.enabled;
            if (_rebindActionWasEnabled)
            {
                action.Disable();
            }

            try
            {
                _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .OnCancel(HandleRebindCanceled)
                    .OnComplete(HandleRebindCompleted);
                _activeRebind.Start();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                RestoreRebindActionState();
                DisposeActiveOperation();
                error = exception.Message;
                return false;
            }
        }

        public bool ApplyBindingOverride(
            string mapName,
            string actionName,
            int bindingIndex,
            string overridePath,
            out string error)
        {
            InputAction action;
            if (!TryGetBinding(mapName, actionName, bindingIndex, out action, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(overridePath))
            {
                error = "Override path is empty.";
                return false;
            }

            // Duplicate effective paths are intentionally allowed by design.
            action.ApplyBindingOverride(bindingIndex, overridePath);
            PersistCurrentOverrides();
            RebindCompleted?.Invoke(action, bindingIndex);
            BindingOverridesChanged?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool ResetBinding(string mapName, string actionName, int bindingIndex, out string error)
        {
            InputAction action;
            if (!TryGetBinding(mapName, actionName, bindingIndex, out action, out error))
            {
                return false;
            }

            action.RemoveBindingOverride(bindingIndex);
            PersistCurrentOverrides();
            BindingOverridesChanged?.Invoke();
            error = string.Empty;
            return true;
        }

        public void ResetAllBindings()
        {
            CancelCurrentRebind();
            if (_actionsAsset == null)
            {
                return;
            }

            _actionsAsset.RemoveAllBindingOverrides();
            if (_settingsData == null)
            {
                _settingsData = new SettingsData();
            }

            _settingsData.BindingOverridesJson = string.Empty;
            if (_persistToPlayerPrefs)
            {
                PlayerPrefs.DeleteKey(InputBindingPersistence.PlayerPrefsKey);
                PlayerPrefs.Save();
            }

            BindingOverridesChanged?.Invoke();
        }

        public void CancelCurrentRebind()
        {
            if (_activeRebind != null)
            {
                _activeRebind.Cancel();
            }
        }

        private bool TryGetBinding(
            string mapName,
            string actionName,
            int bindingIndex,
            out InputAction action,
            out string error)
        {
            action = null;
            if (_actionsAsset == null)
            {
                error = "InputActionAsset is not configured.";
                return false;
            }

            InputActionMap map = _actionsAsset.FindActionMap(mapName, false);
            if (map == null)
            {
                error = "Action Map '" + mapName + "' was not found.";
                return false;
            }

            action = map.FindAction(actionName, false);
            if (action == null)
            {
                error = "Action '" + mapName + "/" + actionName + "' was not found.";
                return false;
            }

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                error = "Binding index is outside the action binding list.";
                action = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void HandleRebindCompleted(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputAction action = _rebindAction;
            int bindingIndex = _rebindBindingIndex;
            RestoreRebindActionState();
            DisposeActiveOperation();
            PersistCurrentOverrides();
            RebindCompleted?.Invoke(action, bindingIndex);
            BindingOverridesChanged?.Invoke();
        }

        private void HandleRebindCanceled(InputActionRebindingExtensions.RebindingOperation operation)
        {
            InputAction action = _rebindAction;
            int bindingIndex = _rebindBindingIndex;
            LastRebindCanceledFrame = Time.frameCount;
            RestoreRebindActionState();
            DisposeActiveOperation();
            RebindCanceled?.Invoke(action, bindingIndex);
        }

        private void RestoreRebindActionState()
        {
            if (_rebindActionWasEnabled && _rebindAction != null)
            {
                _rebindAction.Enable();
            }
        }

        private void DisposeActiveOperation()
        {
            InputActionRebindingExtensions.RebindingOperation operation = _activeRebind;
            _activeRebind = null;
            _rebindAction = null;
            _rebindBindingIndex = -1;
            _rebindActionWasEnabled = false;
            operation?.Dispose();
        }

        private void PersistCurrentOverrides()
        {
            if (_actionsAsset == null)
            {
                return;
            }

            if (_settingsData == null)
            {
                _settingsData = new SettingsData();
            }

            InputBindingPersistence.CaptureIntoSettings(_actionsAsset, _settingsData);
            if (_persistToPlayerPrefs)
            {
                InputBindingPersistence.SaveSettingsToPlayerPrefs(_settingsData);
            }
        }
    }
}
