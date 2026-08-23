using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI
{
    [DisallowMultipleComponent]
    public sealed class DirectControlHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _actVitalsLabel;
        [SerializeField] private TMP_Text _actStatusLabel;
        [SerializeField] private TMP_Text _fpsVitalsLabel;
        [SerializeField] private TMP_Text _fpsStatusLabel;
        [SerializeField] private Image _actHealthFill;
        [SerializeField] private Image _actShieldFill;
        [SerializeField] private Image _actEnergyFill;
        [SerializeField] private Image _fpsHealthFill;
        [SerializeField] private Image _fpsShieldFill;
        [SerializeField] private Image _fpsEnergyFill;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private GameObject _rtsRoot;
        [SerializeField] private GameObject _actRoot;
        [SerializeField] private GameObject _fpsRoot;

        private PossessionService _possessionService;
        private VitalsComponent _vitals;

        public Image ActHealthFill => _actHealthFill;
        public Image ActShieldFill => _actShieldFill;
        public Image ActEnergyFill => _actEnergyFill;
        public Image FpsHealthFill => _fpsHealthFill;
        public Image FpsShieldFill => _fpsShieldFill;
        public Image FpsEnergyFill => _fpsEnergyFill;

        private void OnEnable()
        {
            SubscribeModePresenter();
            TryBindServices();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
            Refresh();
        }

        private void Start()
        {
            TryBindServices();
        }

        private void OnDisable()
        {
            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= ApplyMode;
            }

            UnbindServices();
        }

        public void Configure(
            ModeHudPresenter modeHudPresenter,
            GameObject rtsRoot,
            GameObject actRoot,
            GameObject fpsRoot,
            TMP_Text actVitalsLabel,
            TMP_Text actStatusLabel,
            TMP_Text fpsVitalsLabel,
            TMP_Text fpsStatusLabel,
            Image actHealthFill = null,
            Image actShieldFill = null,
            Image actEnergyFill = null,
            Image fpsHealthFill = null,
            Image fpsShieldFill = null,
            Image fpsEnergyFill = null)
        {
            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= ApplyMode;
            }

            _modeHudPresenter = modeHudPresenter;
            _rtsRoot = rtsRoot;
            _actRoot = actRoot;
            _fpsRoot = fpsRoot;
            _actVitalsLabel = actVitalsLabel;
            _actStatusLabel = actStatusLabel;
            _fpsVitalsLabel = fpsVitalsLabel;
            _fpsStatusLabel = fpsStatusLabel;
            _actHealthFill = actHealthFill;
            _actShieldFill = actShieldFill;
            _actEnergyFill = actEnergyFill;
            _fpsHealthFill = fpsHealthFill;
            _fpsShieldFill = fpsShieldFill;
            _fpsEnergyFill = fpsEnergyFill;
            SubscribeModePresenter();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
            Refresh();
        }

        public void ApplyMode(GameMode mode)
        {
            SetActive(_rtsRoot, mode == GameMode.RTS);
            SetActive(_actRoot, mode == GameMode.ACT);
            SetActive(_fpsRoot, mode == GameMode.FPS);
        }

        public void Refresh()
        {
            VitalsSnapshot snapshot = _vitals != null
                ? _vitals.Snapshot
                : default(VitalsSnapshot);
            SetFill(_actHealthFill, snapshot.Health, snapshot.MaxHealth, true);
            SetFill(_actShieldFill, snapshot.Shield, snapshot.MaxShield, false);
            SetFill(_actEnergyFill, snapshot.Energy, snapshot.MaxEnergy, false);
            SetFill(_fpsHealthFill, snapshot.Health, snapshot.MaxHealth, true);
            SetFill(_fpsShieldFill, snapshot.Shield, snapshot.MaxShield, false);
            SetFill(_fpsEnergyFill, snapshot.Energy, snapshot.MaxEnergy, false);

            HideLegacyLabel(_actVitalsLabel);
            HideLegacyLabel(_actStatusLabel);
            HideLegacyLabel(_fpsVitalsLabel);
            HideLegacyLabel(_fpsStatusLabel);
        }

        private void TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return;
            }

            PossessionService next = appRoot.Services.PossessionService;
            if (_possessionService == next)
            {
                BindVitals(next != null ? next.CurrentPawn : null);
                return;
            }

            UnbindServices();
            _possessionService = next;
            if (_possessionService != null)
            {
                _possessionService.PawnChanged += HandlePawnChanged;
                BindVitals(_possessionService.CurrentPawn);
            }
        }

        private void UnbindServices()
        {
            if (_possessionService != null)
            {
                _possessionService.PawnChanged -= HandlePawnChanged;
            }

            BindVitals(null);
            _possessionService = null;
        }

        private void HandlePawnChanged(HybridControlDriver previous, HybridControlDriver current)
        {
            BindVitals(current);
        }

        private void BindVitals(HybridControlDriver pawn)
        {
            VitalsComponent next = pawn != null ? pawn.GetComponent<VitalsComponent>() : null;
            if (_vitals == next)
            {
                Refresh();
                return;
            }

            if (_vitals != null)
            {
                _vitals.VitalsChanged -= HandleVitalsChanged;
            }

            _vitals = next;
            if (_vitals != null)
            {
                _vitals.VitalsChanged += HandleVitalsChanged;
            }

            Refresh();
        }

        private void HandleVitalsChanged(
            VitalsComponent source,
            VitalsSnapshot previous,
            VitalsSnapshot current)
        {
            Refresh();
        }

        private static void SetFill(
            Image image,
            float value,
            float maximum,
            bool keepVisibleWithoutCapacity)
        {
            if (image == null)
            {
                return;
            }

            float normalized = maximum > 0f
                ? Mathf.Clamp01(value / maximum)
                : 0f;
            image.type = Image.Type.Simple;
            RectTransform rect = image.rectTransform;
            Vector2 anchorMin = rect.anchorMin;
            Vector2 anchorMax = rect.anchorMax;
            Vector2 offsetMin = rect.offsetMin;
            Vector2 offsetMax = rect.offsetMax;
            anchorMin.x = 0f;
            anchorMax.x = normalized;
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            image.enabled = keepVisibleWithoutCapacity || maximum > 0f;
        }

        private static void HideLegacyLabel(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.text = string.Empty;
            if (label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(false);
            }
        }

        private void SubscribeModePresenter()
        {
            if (!isActiveAndEnabled || _modeHudPresenter == null)
            {
                return;
            }

            _modeHudPresenter.ModePresented -= ApplyMode;
            _modeHudPresenter.ModePresented += ApplyMode;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
