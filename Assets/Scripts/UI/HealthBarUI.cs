using Gameplay;
using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Color hostileFillColor = Color.red;

        Health _health;
        Canvas _canvas;
        Transform _camTr;
        Color _defaultFillColor;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace)
                _canvas.worldCamera = UnityEngine.Camera.main;

            if (fill != null)
                _defaultFillColor = fill.color;

            DisableRaycastTargets();
            _camTr = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
        }

        public void Bind(Health health)
        {
            if (_health != null)
                _health.OnHpChanged -= OnHpChanged;

            _health = health;
            if (_health == null)
                return;

            ApplyFillColor();
            _health.OnHpChanged += OnHpChanged;
            OnHpChanged(_health.CurrentHp, _health.MaxHp);
        }

        void OnDestroy()
        {
            if (_health != null)
                _health.OnHpChanged -= OnHpChanged;
        }

        void LateUpdate()
        {
            ApplyModeVisibility();

            if (_camTr == null)
                return;

            transform.forward = _camTr.forward;
        }

        void OnHpChanged(float current, float max)
        {
            if (fill == null)
                return;

            fill.fillAmount = max <= 0f ? 0f : current / max;
        }

        void ApplyFillColor()
        {
            if (fill == null)
                return;

            TeamAffiliation teamAffiliation = _health != null ? _health.TeamAffiliation : null;
            bool isHostile = teamAffiliation != null
                             && TeamAffiliation.IsHostile(teamAffiliation.Team, TeamType.Friendly);
            fill.color = isHostile ? hostileFillColor : _defaultFillColor;
        }

        void ApplyModeVisibility()
        {
            if (_canvas == null)
                return;

            bool shouldShow = !ShouldHideForCurrentMode();
            if (_canvas.enabled != shouldShow)
                _canvas.enabled = shouldShow;
        }

        bool ShouldHideForCurrentMode()
        {
            if (_health == null)
                return false;

            ControlModeManager manager = ControlModeManager.Instance;
            if (manager == null || manager.unit == null)
                return false;

            string modeName = manager.CurrentModeName;
            if (modeName != "ACT" && modeName != "FPS")
                return false;

            Transform healthTransform = _health.transform;
            Transform controlledTransform = manager.unit.transform;
            return healthTransform == controlledTransform ||
                   healthTransform.IsChildOf(controlledTransform) ||
                   controlledTransform.IsChildOf(healthTransform);
        }

        void DisableRaycastTargets()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }
    }
}
