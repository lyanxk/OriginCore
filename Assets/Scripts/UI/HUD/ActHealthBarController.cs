using Core;
using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace UI.HUD
{
    public class ActHealthBarController : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] CanvasGroup canvasGroup;

        Health _health;
        float _lastFill = -1f;
        bool _visible;

        void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            DisableRaycastTargets();
            SetVisible(false);
        }

        void OnEnable()
        {
            RefreshBinding();
            RefreshFill();
        }

        void OnDisable()
        {
            Unbind();
        }

        void OnDestroy()
        {
            Unbind();
        }

        void Update()
        {
            RefreshBinding();

            ControlModeManager manager = ControlModeManager.Instance;
            bool shouldShow = manager != null
                              && manager.CurrentModeName == "ACT"
                              && _health != null;
            SetVisible(shouldShow);
        }

        void RefreshBinding()
        {
            ControlModeManager manager = ControlModeManager.Instance;
            Health nextHealth = ResolveControlledHealth(manager);
            if (nextHealth == _health)
                return;

            Bind(nextHealth);
        }

        void Bind(Health health)
        {
            Unbind();
            _health = health;

            if (_health == null)
            {
                RefreshFill();
                return;
            }

            _health.OnHpChanged += HandleHpChanged;
            HandleHpChanged(_health.CurrentHp, _health.MaxHp);
        }

        void Unbind()
        {
            if (_health != null)
                _health.OnHpChanged -= HandleHpChanged;

            _health = null;
        }

        void HandleHpChanged(float current, float max)
        {
            SetFill(max <= 0f ? 0f : current / max);
        }

        void RefreshFill()
        {
            if (_health == null)
                SetFill(0f);
            else
                HandleHpChanged(_health.CurrentHp, _health.MaxHp);
        }

        void SetFill(float value)
        {
            if (fill == null)
                return;

            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(_lastFill, clamped))
                return;

            _lastFill = clamped;
            fill.fillAmount = clamped;
        }

        void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;

            _visible = visible;

            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        static Health ResolveControlledHealth(ControlModeManager manager)
        {
            if (manager == null || manager.unit == null)
                return null;

            Health health = manager.unit.GetComponent<Health>();
            if (health != null)
                return health;

            health = manager.unit.GetComponentInChildren<Health>(true);
            if (health != null)
                return health;

            return manager.unit.GetComponentInParent<Health>();
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
