using OriginCore.Combat;
using OriginCore.Core;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeStatBlock), typeof(HeroFormStateMachine))]
    public sealed class DirectModeFormModifierController : MonoBehaviour
    {
        private const string ModifierSource = "hero-form.direct-mode";

        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private HeroFormStateMachine _forms;

        private GameMode? _appliedMode;
        private string _appliedFormId = string.Empty;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyIfChanged(true);
        }

        private void Update()
        {
            ApplyIfChanged(false);
        }

        private void OnDisable()
        {
            _stats?.RemoveModifiersBySource(ModifierSource);
            _appliedMode = null;
            _appliedFormId = string.Empty;
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void ApplyIfChanged(bool force)
        {
            if (_stats == null || _forms == null ||
                !AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.GameModeController == null)
            {
                return;
            }

            GameMode mode = root.Services.GameModeController.CurrentMode;
            string formId = _forms.CurrentFormId;
            if (!force && _appliedMode == mode && _appliedFormId == formId)
            {
                return;
            }

            _stats.RemoveModifiersBySource(ModifierSource);
            if (mode.IsDirectControl() && _forms.CurrentForm != null &&
                !Mathf.Approximately(_forms.CurrentForm.DirectModeDamageMultiplier, 1f))
            {
                _stats.AddModifier(new StatModifierDefinition(
                    ModifierSource,
                    RuntimeStatId.DamageDealt,
                    StatModifierOperation.Multiply,
                    _forms.CurrentForm.DirectModeDamageMultiplier,
                    110,
                    0f,
                    StatStackingRule.ReplaceExisting));
            }

            _appliedMode = mode;
            _appliedFormId = formId;
        }

        private void CacheComponents()
        {
            if (_stats == null) _stats = GetComponent<RuntimeStatBlock>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
        }
    }
}
