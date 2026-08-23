using OriginCore.Core;
using OriginCore.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.FPS
{
    [DisallowMultipleComponent]
    public sealed class CrosshairView : MonoBehaviour
    {
        [SerializeField] private Graphic _dotGraphic;
        [SerializeField] private GameObject _dotRoot;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private Color _fallbackColor = Color.white;

        private bool _subscribed;
        private SettingsService _settingsService;

        public Graphic DotGraphic => _dotGraphic;
        public GameObject DotRoot => _dotRoot;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public Color Color => _dotGraphic != null ? _dotGraphic.color : _fallbackColor;
        public bool IsVisible => _dotRoot != null && _dotRoot.activeSelf;

        private void OnEnable()
        {
            Subscribe();
            RefreshFromRuntimeSettings();
            ApplyMode(_modeHudPresenter != null
                ? _modeHudPresenter.Mode
                : GameMode.RTS);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            Graphic dotGraphic,
            GameObject dotRoot,
            ModeHudPresenter modeHudPresenter,
            Color fallbackColor)
        {
            Unsubscribe();
            _dotGraphic = dotGraphic;
            _dotRoot = dotRoot != null
                ? dotRoot
                : dotGraphic != null ? dotGraphic.gameObject : null;
            _modeHudPresenter = modeHudPresenter;
            _fallbackColor = NormalizeColor(fallbackColor);
            SetColor(_fallbackColor);
            Subscribe();
            ApplyMode(_modeHudPresenter != null
                ? _modeHudPresenter.Mode
                : GameMode.RTS);
        }

        public void ApplyMode(GameMode mode)
        {
            if (_dotRoot != null)
            {
                _dotRoot.SetActive(mode == GameMode.FPS);
            }
        }

        public void RefreshColor(SettingsData settingsData)
        {
            SetColor(settingsData != null
                ? settingsData.CrosshairColor
                : _fallbackColor);
        }

        public void SetColor(Color color)
        {
            Color normalized = NormalizeColor(color);
            _fallbackColor = normalized;
            if (_dotGraphic != null)
            {
                _dotGraphic.color = normalized;
            }
        }

        private void RefreshFromRuntimeSettings()
        {
            if (AppRoot.TryGetInstance(out AppRoot appRoot) &&
                appRoot.Services != null)
            {
                _settingsService = appRoot.Services.SettingsService;
                if (_settingsService != null)
                {
                    _settingsService.SettingsChanged -= RefreshColor;
                    _settingsService.SettingsChanged += RefreshColor;
                    RefreshColor(_settingsService.Data);
                    return;
                }

                if (appRoot.Services.InputRebindService != null)
                {
                    RefreshColor(appRoot.Services.InputRebindService.SettingsData);
                    return;
                }
            }

            SetColor(_fallbackColor);
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled || _modeHudPresenter == null)
            {
                return;
            }

            _modeHudPresenter.ModePresented += ApplyMode;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && _modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= ApplyMode;
            }

            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= RefreshColor;
                _settingsService = null;
            }

            _subscribed = false;
        }

        private static Color NormalizeColor(Color color)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                1f);
        }
    }
}
