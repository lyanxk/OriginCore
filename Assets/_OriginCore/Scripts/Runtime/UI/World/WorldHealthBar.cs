using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.UI;
using GameplaySelectable = OriginCore.Gameplay.Selectable;

namespace OriginCore.UI.World
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private VitalsComponent _vitals;
        [SerializeField] private GameplaySelectable _selectable;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Image _shieldFill;

        private Camera _worldCamera;
        private WorldHealthBarData _lastData;
        private bool _hasLastData;

        public VitalsComponent Vitals => _vitals;
        public GameplaySelectable Selectable => _selectable;
        public Canvas WorldCanvas => _canvas;
        public Image HealthFill => _healthFill;
        public Image ShieldFill => _shieldFill;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
            Refresh(_vitals != null
                ? _vitals.HealthBarData
                : default(WorldHealthBarData));
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void LateUpdate()
        {
            RefreshFromVitalsIfChanged();
            if (_canvas == null || !_canvas.enabled)
            {
                return;
            }

            if (_worldCamera == null)
            {
                TryBindWorldCamera();
            }

            if (_worldCamera != null)
            {
                Transform cameraTransform = _worldCamera.transform;
                transform.rotation = Quaternion.LookRotation(
                    cameraTransform.forward,
                    cameraTransform.up);
            }
        }

        public void Configure(
            VitalsComponent vitals,
            GameplaySelectable selectable,
            Canvas worldCanvas,
            Image healthFill,
            Image shieldFill)
        {
            UnbindEvents();
            _vitals = vitals;
            _selectable = selectable;
            _canvas = worldCanvas != null ? worldCanvas : GetComponent<Canvas>();
            _healthFill = healthFill;
            _shieldFill = shieldFill;
            _worldCamera = null;
            if (isActiveAndEnabled)
            {
                BindEvents();
                Refresh(_vitals != null
                    ? _vitals.HealthBarData
                    : default(WorldHealthBarData));
            }
        }

        private void HandleHealthBarDataChanged(
            VitalsComponent vitals,
            WorldHealthBarData data)
        {
            Refresh(data);
        }

        private void HandleSelectionStateChanged(
            GameplaySelectable selectable,
            SelectionVisualState previous,
            SelectionVisualState current)
        {
            RefreshVisibility(_vitals != null
                ? _vitals.HealthBarData
                : default(WorldHealthBarData));
        }

        private void Refresh(WorldHealthBarData data)
        {
            _lastData = data;
            _hasLastData = true;
            if (_healthFill != null)
            {
                SetFill(_healthFill, data.HealthNormalized, true);
            }

            if (_shieldFill != null)
            {
                SetFill(
                    _shieldFill,
                    data.ShieldNormalized,
                    data.MaxShield > 0f);
            }

            RefreshVisibility(data);
        }

        private void RefreshFromVitalsIfChanged()
        {
            if (_vitals == null)
            {
                CacheComponents();
                if (_vitals == null)
                {
                    return;
                }

                BindEvents();
            }

            WorldHealthBarData current = _vitals.HealthBarData;
            if (!_hasLastData ||
                !Mathf.Approximately(_lastData.Health, current.Health) ||
                !Mathf.Approximately(_lastData.MaxHealth, current.MaxHealth) ||
                !Mathf.Approximately(_lastData.Shield, current.Shield) ||
                !Mathf.Approximately(_lastData.MaxShield, current.MaxShield) ||
                _lastData.IsAlive != current.IsAlive)
            {
                Refresh(current);
            }
        }

        private static void SetFill(Image image, float normalized, bool visible)
        {
            image.type = Image.Type.Simple;
            RectTransform rect = image.rectTransform;
            Vector2 anchorMin = rect.anchorMin;
            Vector2 anchorMax = rect.anchorMax;
            Vector2 offsetMin = rect.offsetMin;
            Vector2 offsetMax = rect.offsetMax;
            anchorMin.x = 0f;
            anchorMax.x = Mathf.Clamp01(normalized);
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            image.enabled = visible;
        }

        private void RefreshVisibility(WorldHealthBarData data)
        {
            if (_canvas == null)
            {
                return;
            }

            bool injured = data.IsAlive &&
                           (data.Health < data.MaxHealth ||
                            data.MaxShield > 0f && data.Shield < data.MaxShield);
            SelectionVisualState state = _selectable != null
                ? _selectable.State
                : SelectionVisualState.Normal;
            bool selected = state == SelectionVisualState.Selected ||
                            state == SelectionVisualState.Inspected;
            VisibilityTarget visibilityTarget = GetComponentInParent<VisibilityTarget>();
            bool visible = visibilityTarget == null || visibilityTarget.IsVisible;
            _canvas.enabled = visible && data.IsAlive && (injured || selected);
        }

        private void CacheComponents()
        {
            if (_vitals == null)
            {
                _vitals = GetComponentInParent<VitalsComponent>();
            }

            if (_selectable == null)
            {
                _selectable = GetComponentInParent<GameplaySelectable>();
            }

            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }
        }

        private void BindEvents()
        {
            if (_vitals != null)
            {
                _vitals.WorldHealthBarDataChanged -= HandleHealthBarDataChanged;
                _vitals.WorldHealthBarDataChanged += HandleHealthBarDataChanged;
            }

            if (_selectable != null)
            {
                _selectable.StateChanged -= HandleSelectionStateChanged;
                _selectable.StateChanged += HandleSelectionStateChanged;
            }
        }

        private void UnbindEvents()
        {
            if (_vitals != null)
            {
                _vitals.WorldHealthBarDataChanged -= HandleHealthBarDataChanged;
            }

            if (_selectable != null)
            {
                _selectable.StateChanged -= HandleSelectionStateChanged;
            }
        }

        private bool TryBindWorldCamera()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null ||
                appRoot.Services.CurrentSceneContext == null)
            {
                return false;
            }

            _worldCamera = appRoot.Services.CurrentSceneContext.MainCamera;
            return _worldCamera != null;
        }
    }
}
