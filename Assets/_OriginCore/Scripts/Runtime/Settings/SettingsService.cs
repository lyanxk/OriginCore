using System;
using System.Collections.Generic;
using System.IO;
using OriginCore.Input;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace OriginCore.Settings
{
    public readonly struct ResolutionOption
    {
        public ResolutionOption(int width, int height)
        {
            Width = Mathf.Max(640, width);
            Height = Mathf.Max(360, height);
        }

        public int Width { get; }
        public int Height { get; }
        public string Label => Width + " x " + Height;
    }

    [DefaultExecutionOrder(-11795)]
    [DisallowMultipleComponent]
    public sealed class SettingsService : MonoBehaviour
    {
        public const string MasterVolumeParameter = "MasterVolume";
        public const string RelativeSettingsPath = "OriginCore/settings.json";

        [SerializeField] private SettingsData _data = new SettingsData();
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private BrightnessImageEffect _brightnessEffect;
        [SerializeField] private bool _applyToSystem = true;

        private SettingsRepository _repository;
        private InputRebindService _inputRebindService;
        private bool _initialized;

        public event Action<SettingsData> SettingsChanged;

        public SettingsData Data => _data;
        public AudioMixer AudioMixer => _audioMixer;
        public BrightnessImageEffect BrightnessEffect => _brightnessEffect;
        public bool IsInitialized => _initialized;
        public string SettingsPath => _repository != null ? _repository.FilePath : string.Empty;
        public string LastError { get; private set; } = string.Empty;

        public void Configure(
            AudioMixer audioMixer,
            BrightnessImageEffect brightnessEffect,
            bool applyToSystem = true)
        {
            _audioMixer = audioMixer;
            _brightnessEffect = brightnessEffect;
            _applyToSystem = applyToSystem;
        }

        public bool Initialize(InputRebindService inputRebindService)
        {
            string path = Path.Combine(Application.persistentDataPath, RelativeSettingsPath);
            return Initialize(inputRebindService, path);
        }

        public bool Initialize(InputRebindService inputRebindService, string absolutePath)
        {
            if (_initialized)
            {
                return true;
            }

            if (inputRebindService == null || inputRebindService.ActionsAsset == null)
            {
                LastError = "InputRebindService or IA_OriginCore is missing.";
                Debug.LogError("[OriginCore Settings] " + LastError, this);
                return false;
            }

            _repository = new SettingsRepository(absolutePath);
            if (!_repository.TryLoad(out SettingsData loaded, out string loadError))
            {
                Debug.LogWarning(
                    "[OriginCore Settings] Invalid settings were replaced with defaults: " +
                    loadError,
                    this);
            }

            _data = loaded ?? SettingsData.CreateDefaults(Screen.width, Screen.height);
            _data.Normalize();
            _inputRebindService = inputRebindService;
            if (!_inputRebindService.Configure(
                    _inputRebindService.ActionsAsset,
                    _data,
                    false))
            {
                LastError = "InputRebindService rejected the shared SettingsData instance.";
                Debug.LogError("[OriginCore Settings] " + LastError, this);
                return false;
            }

            if (!InputBindingPersistence.TryApplyFromSettings(
                    _inputRebindService.ActionsAsset,
                    _data,
                    out string bindingError))
            {
                Debug.LogWarning(
                    "[OriginCore Settings] Invalid binding overrides were cleared: " + bindingError,
                    this);
                _data.BindingOverridesJson = string.Empty;
                _inputRebindService.ActionsAsset.RemoveAllBindingOverrides();
            }

            _inputRebindService.BindingOverridesChanged += HandleBindingOverridesChanged;
            _initialized = true;
            ApplyRuntimeValues(false);
            Save();
            LastError = string.Empty;
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            if (_inputRebindService != null)
            {
                _inputRebindService.BindingOverridesChanged -= HandleBindingOverridesChanged;
            }

            _inputRebindService = null;
            _initialized = false;
        }

        public bool Apply(SettingsData values)
        {
            if (!_initialized || values == null)
            {
                LastError = !_initialized
                    ? "SettingsService is not initialized."
                    : "SettingsData is null.";
                return false;
            }

            _data.CopyFrom(values);
            if (!InputBindingPersistence.TryApplyFromSettings(
                    _inputRebindService.ActionsAsset,
                    _data,
                    out string bindingError))
            {
                LastError = "Binding overrides could not be applied: " + bindingError;
                return false;
            }

            ApplyRuntimeValues(true);
            return Save();
        }

        public bool Save()
        {
            if (_repository == null)
            {
                LastError = "Settings repository is not initialized.";
                return false;
            }

            if (!_repository.TrySave(_data, out string error))
            {
                LastError = error;
                Debug.LogError("[OriginCore Settings] Save failed: " + error, this);
                return false;
            }

            LastError = string.Empty;
            return true;
        }

        public IReadOnlyList<ResolutionOption> GetAvailableResolutions()
        {
            List<ResolutionOption> options = new List<ResolutionOption>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                AddResolution(options, seen, resolutions[i].width, resolutions[i].height);
            }

            AddResolution(options, seen, _data.ScreenWidth, _data.ScreenHeight);
            if (options.Count == 0)
            {
                AddResolution(options, seen, Screen.width, Screen.height);
            }

            options.Sort((left, right) =>
            {
                int width = left.Width.CompareTo(right.Width);
                return width != 0 ? width : left.Height.CompareTo(right.Height);
            });
            return options;
        }

        public static float MasterVolumeToDecibels(float normalizedVolume)
        {
            float normalized = Mathf.Clamp01(normalizedVolume);
            return normalized <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(normalized));
        }

        private void ApplyRuntimeValues(bool raiseEvent)
        {
            _data.Normalize();
            if (_applyToSystem)
            {
                if (Screen.width != _data.ScreenWidth ||
                    Screen.height != _data.ScreenHeight ||
                    Screen.fullScreenMode != _data.WindowMode)
                {
                    Screen.SetResolution(
                        _data.ScreenWidth,
                        _data.ScreenHeight,
                        _data.WindowMode);
                }

                if (_audioMixer != null)
                {
                    _audioMixer.SetFloat(
                        MasterVolumeParameter,
                        MasterVolumeToDecibels(_data.MasterVolume));
                }

                _brightnessEffect?.Apply(_data.Brightness);
            }

            if (raiseEvent)
            {
                SettingsChanged?.Invoke(_data);
            }
        }

        private void HandleBindingOverridesChanged()
        {
            Save();
            SettingsChanged?.Invoke(_data);
        }

        private static void AddResolution(
            ICollection<ResolutionOption> options,
            ISet<string> seen,
            int width,
            int height)
        {
            ResolutionOption option = new ResolutionOption(width, height);
            string key = option.Width + "x" + option.Height;
            if (seen.Add(key))
            {
                options.Add(option);
            }
        }
    }
}
