using System;
using UnityEngine;

namespace OriginCore.Settings
{
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [Min(640), SerializeField] private int _screenWidth = 1920;
        [Min(360), SerializeField] private int _screenHeight = 1080;
        [SerializeField] private FullScreenMode _windowMode = FullScreenMode.FullScreenWindow;
        [Range(0.5f, 1.5f), SerializeField] private float _brightness = 1f;
        [Range(0f, 1f), SerializeField] private float _masterVolume = 1f;
        [SerializeField] private string _bindingOverridesJson = string.Empty;
        [SerializeField] private Color _crosshairColor = Color.white;

        public int SchemaVersion
        {
            get => Mathf.Max(1, _schemaVersion);
            set => _schemaVersion = Mathf.Max(1, value);
        }

        public int ScreenWidth
        {
            get => Mathf.Max(640, _screenWidth);
            set => _screenWidth = Mathf.Max(640, value);
        }

        public int ScreenHeight
        {
            get => Mathf.Max(360, _screenHeight);
            set => _screenHeight = Mathf.Max(360, value);
        }

        public FullScreenMode WindowMode
        {
            get => NormalizeWindowMode(_windowMode);
            set => _windowMode = NormalizeWindowMode(value);
        }

        public float Brightness
        {
            get => Mathf.Clamp(_brightness, 0.5f, 1.5f);
            set => _brightness = Mathf.Clamp(value, 0.5f, 1.5f);
        }

        public float MasterVolume
        {
            get => Mathf.Clamp01(_masterVolume);
            set => _masterVolume = Mathf.Clamp01(value);
        }

        public string BindingOverridesJson
        {
            get => _bindingOverridesJson ?? string.Empty;
            set => _bindingOverridesJson = value ?? string.Empty;
        }

        public Color CrosshairColor
        {
            get
            {
                if (_crosshairColor.a <= 0f)
                {
                    return Color.white;
                }

                return new Color(
                    Mathf.Clamp01(_crosshairColor.r),
                    Mathf.Clamp01(_crosshairColor.g),
                    Mathf.Clamp01(_crosshairColor.b),
                    1f);
            }
            set => _crosshairColor = new Color(
                Mathf.Clamp01(value.r),
                Mathf.Clamp01(value.g),
                Mathf.Clamp01(value.b),
                1f);
        }

        public void Normalize()
        {
            _schemaVersion = CurrentSchemaVersion;
            _screenWidth = ScreenWidth;
            _screenHeight = ScreenHeight;
            _windowMode = WindowMode;
            _brightness = Brightness;
            _masterVolume = MasterVolume;
            _bindingOverridesJson = BindingOverridesJson;
            _crosshairColor = CrosshairColor;
        }

        public SettingsData Clone()
        {
            SettingsData clone = new SettingsData();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(SettingsData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _schemaVersion = source.SchemaVersion;
            _screenWidth = source.ScreenWidth;
            _screenHeight = source.ScreenHeight;
            _windowMode = source.WindowMode;
            _brightness = source.Brightness;
            _masterVolume = source.MasterVolume;
            _bindingOverridesJson = source.BindingOverridesJson;
            _crosshairColor = source.CrosshairColor;
            Normalize();
        }

        public static SettingsData CreateDefaults(int screenWidth, int screenHeight)
        {
            SettingsData defaults = new SettingsData
            {
                ScreenWidth = screenWidth,
                ScreenHeight = screenHeight,
                WindowMode = FullScreenMode.FullScreenWindow,
                Brightness = 1f,
                MasterVolume = 1f,
                CrosshairColor = Color.white,
                BindingOverridesJson = string.Empty
            };
            defaults.Normalize();
            return defaults;
        }

        private static FullScreenMode NormalizeWindowMode(FullScreenMode mode)
        {
            return mode == FullScreenMode.Windowed
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow;
        }
    }
}
