using Gameplay;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace UI
{
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "HealthBarUI")]
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] Color hostileFillColor = Color.red;

        Health _health;
        Transform _camTr;
        Color _defaultFillColor;

        void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = UnityEngine.Camera.main;

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
