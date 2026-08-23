using System;
using System.Collections.Generic;
using OriginCore.Core;
using OriginCore.Input;
using OriginCore.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class OptionsView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _windowModeDropdown;
        [SerializeField] private Slider _brightnessSlider;
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _crosshairRedSlider;
        [SerializeField] private Slider _crosshairGreenSlider;
        [SerializeField] private Slider _crosshairBlueSlider;
        [SerializeField] private Image _crosshairPreview;
        [SerializeField] private TMP_Text _brightnessValueLabel;
        [SerializeField] private TMP_Text _masterVolumeValueLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private InputBindingsView _inputBindingsView;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _restoreBindingsButton;
        [SerializeField] private Button _backButton;

        private readonly List<ResolutionOption> _resolutions =
            new List<ResolutionOption>();
        private SettingsService _settingsService;
        private InputRebindService _inputRebindService;
        private bool _controlsBound;

        public event Action BackRequested;

        public SettingsService SettingsService => _settingsService;
        public int ResolutionOptionCount => _resolutions.Count;

        private void OnEnable()
        {
            BindControls();
            BindServices();
            RefreshFromSettings();
        }

        private void OnDisable()
        {
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= HandleSettingsChanged;
            }
        }

        private void OnDestroy()
        {
            if (!_controlsBound)
            {
                return;
            }

            if (_applyButton != null) _applyButton.onClick.RemoveListener(Apply);
            if (_restoreBindingsButton != null)
            {
                _restoreBindingsButton.onClick.RemoveListener(RestoreBindings);
            }

            if (_backButton != null) _backButton.onClick.RemoveListener(Back);
            if (_brightnessSlider != null)
            {
                _brightnessSlider.onValueChanged.RemoveListener(HandlePreviewChanged);
            }

            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.onValueChanged.RemoveListener(HandlePreviewChanged);
            }

            if (_crosshairRedSlider != null)
            {
                _crosshairRedSlider.onValueChanged.RemoveListener(HandlePreviewChanged);
            }

            if (_crosshairGreenSlider != null)
            {
                _crosshairGreenSlider.onValueChanged.RemoveListener(HandlePreviewChanged);
            }

            if (_crosshairBlueSlider != null)
            {
                _crosshairBlueSlider.onValueChanged.RemoveListener(HandlePreviewChanged);
            }
        }

        public void Configure(
            TMP_Dropdown resolutionDropdown,
            TMP_Dropdown windowModeDropdown,
            Slider brightnessSlider,
            Slider masterVolumeSlider,
            Slider crosshairRedSlider,
            Slider crosshairGreenSlider,
            Slider crosshairBlueSlider,
            Image crosshairPreview,
            TMP_Text brightnessValueLabel,
            TMP_Text masterVolumeValueLabel,
            TMP_Text statusLabel,
            InputBindingsView inputBindingsView,
            Button applyButton,
            Button restoreBindingsButton,
            Button backButton)
        {
            _resolutionDropdown = resolutionDropdown;
            _windowModeDropdown = windowModeDropdown;
            _brightnessSlider = brightnessSlider;
            _masterVolumeSlider = masterVolumeSlider;
            _crosshairRedSlider = crosshairRedSlider;
            _crosshairGreenSlider = crosshairGreenSlider;
            _crosshairBlueSlider = crosshairBlueSlider;
            _crosshairPreview = crosshairPreview;
            _brightnessValueLabel = brightnessValueLabel;
            _masterVolumeValueLabel = masterVolumeValueLabel;
            _statusLabel = statusLabel;
            _inputBindingsView = inputBindingsView;
            _applyButton = applyButton;
            _restoreBindingsButton = restoreBindingsButton;
            _backButton = backButton;
            if (Application.isPlaying)
            {
                BindControls();
            }
        }

        public void RefreshFromSettings()
        {
            if (_settingsService == null)
            {
                SetStatus("SETTINGS SERVICE UNAVAILABLE");
                return;
            }

            SettingsData data = _settingsService.Data;
            PopulateResolutionDropdown(data);
            PopulateWindowModeDropdown(data);
            SetSlider(_brightnessSlider, 0.5f, 1.5f, data.Brightness);
            SetSlider(_masterVolumeSlider, 0f, 1f, data.MasterVolume);
            Color crosshair = data.CrosshairColor;
            SetSlider(_crosshairRedSlider, 0f, 1f, crosshair.r);
            SetSlider(_crosshairGreenSlider, 0f, 1f, crosshair.g);
            SetSlider(_crosshairBlueSlider, 0f, 1f, crosshair.b);
            _inputBindingsView?.Bind(_inputRebindService);
            RefreshPreview();
            SetStatus("SETTINGS READY");
        }

        public void Apply()
        {
            if (_settingsService == null || _resolutions.Count == 0)
            {
                SetStatus("SETTINGS COULD NOT BE APPLIED");
                return;
            }

            int resolutionIndex = _resolutionDropdown != null
                ? Mathf.Clamp(_resolutionDropdown.value, 0, _resolutions.Count - 1)
                : 0;
            ResolutionOption resolution = _resolutions[resolutionIndex];
            SettingsData values = _settingsService.Data.Clone();
            values.ScreenWidth = resolution.Width;
            values.ScreenHeight = resolution.Height;
            values.WindowMode = _windowModeDropdown != null && _windowModeDropdown.value == 1
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;
            values.Brightness = ValueOf(_brightnessSlider, 1f);
            values.MasterVolume = ValueOf(_masterVolumeSlider, 1f);
            values.CrosshairColor = new Color(
                ValueOf(_crosshairRedSlider, 1f),
                ValueOf(_crosshairGreenSlider, 1f),
                ValueOf(_crosshairBlueSlider, 1f),
                1f);

            SetStatus(_settingsService.Apply(values)
                ? "SETTINGS APPLIED AND SAVED"
                : "APPLY FAILED: " + _settingsService.LastError);
        }

        public void RestoreBindings()
        {
            if (_inputRebindService == null)
            {
                SetStatus("INPUT SERVICE UNAVAILABLE");
                return;
            }

            _inputRebindService.ResetAllBindings();
            _inputBindingsView?.Refresh();
            SetStatus("DEFAULT BINDINGS RESTORED");
        }

        public void Back()
        {
            BackRequested?.Invoke();
        }

        private void BindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return;
            }

            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= HandleSettingsChanged;
            }

            _settingsService = appRoot.Services.SettingsService;
            _inputRebindService = appRoot.Services.InputRebindService;
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += HandleSettingsChanged;
            }
        }

        private void BindControls()
        {
            if (_controlsBound || _applyButton == null ||
                _restoreBindingsButton == null || _backButton == null)
            {
                return;
            }

            _applyButton.onClick.AddListener(Apply);
            _restoreBindingsButton.onClick.AddListener(RestoreBindings);
            _backButton.onClick.AddListener(Back);
            _brightnessSlider?.onValueChanged.AddListener(HandlePreviewChanged);
            _masterVolumeSlider?.onValueChanged.AddListener(HandlePreviewChanged);
            _crosshairRedSlider?.onValueChanged.AddListener(HandlePreviewChanged);
            _crosshairGreenSlider?.onValueChanged.AddListener(HandlePreviewChanged);
            _crosshairBlueSlider?.onValueChanged.AddListener(HandlePreviewChanged);
            _controlsBound = true;
        }

        private void PopulateResolutionDropdown(SettingsData data)
        {
            _resolutions.Clear();
            IReadOnlyList<ResolutionOption> available = _settingsService.GetAvailableResolutions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            int selected = 0;
            for (int i = 0; i < available.Count; i++)
            {
                ResolutionOption option = available[i];
                _resolutions.Add(option);
                options.Add(new TMP_Dropdown.OptionData(option.Label));
                if (option.Width == data.ScreenWidth && option.Height == data.ScreenHeight)
                {
                    selected = i;
                }
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.ClearOptions();
                _resolutionDropdown.AddOptions(options);
                _resolutionDropdown.SetValueWithoutNotify(selected);
                _resolutionDropdown.RefreshShownValue();
            }
        }

        private void PopulateWindowModeDropdown(SettingsData data)
        {
            if (_windowModeDropdown == null)
            {
                return;
            }

            _windowModeDropdown.ClearOptions();
            _windowModeDropdown.AddOptions(new List<string>
            {
                "BORDERLESS FULLSCREEN",
                "WINDOWED"
            });
            _windowModeDropdown.SetValueWithoutNotify(
                data.WindowMode == FullScreenMode.Windowed ? 1 : 0);
            _windowModeDropdown.RefreshShownValue();
        }

        private void HandleSettingsChanged(SettingsData data)
        {
            RefreshFromSettings();
        }

        private void HandlePreviewChanged(float value)
        {
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            float brightness = ValueOf(_brightnessSlider, 1f);
            float volume = ValueOf(_masterVolumeSlider, 1f);
            if (_brightnessValueLabel != null)
            {
                _brightnessValueLabel.text = brightness.ToString("0.00");
            }

            if (_masterVolumeValueLabel != null)
            {
                _masterVolumeValueLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
            }

            if (_crosshairPreview != null)
            {
                _crosshairPreview.color = new Color(
                    ValueOf(_crosshairRedSlider, 1f),
                    ValueOf(_crosshairGreenSlider, 1f),
                    ValueOf(_crosshairBlueSlider, 1f),
                    1f);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message ?? string.Empty;
            }
        }

        private static void SetSlider(Slider slider, float minimum, float maximum, float value)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, minimum, maximum));
        }

        private static float ValueOf(Slider slider, float fallback)
        {
            return slider != null ? slider.value : fallback;
        }
    }
}
