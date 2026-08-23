using System.Collections.Generic;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory), typeof(RuntimeStatBlock))]
    public sealed class FinalWingPassiveController : MonoBehaviour,
        IIncomingDamageModifier
    {
        private const string MovementSource = "weapon.y.final-wing.passive.move";
        private const string FlightSource = "weapon.y.final-wing.passive.flight";
        private const string FocusActionId = "action.y.final-wing.focus";
        private const int AutomaticFeathersPerMainHit = 18;
        private const float AutomaticFeatherInterval = 0.1f;

        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private FlightController _flight;
        [SerializeField] private HeroFormStateMachine _forms;
        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private CombatHitboxResolver _hitboxes;

        private readonly Queue<EntityIdentity> _automaticVolleyTargets =
            new Queue<EntityIdentity>();
        private int _suppressionTokens;
        private int _automaticFeathersRemaining;
        private float _nextAutomaticFeather;
        private EntityIdentity _automaticTarget;
        private bool _passivesApplied;

        public bool IsAvailable => _inventory != null &&
            _inventory.ActAuxiliary != null &&
            _inventory.ActAuxiliary.ContentId == BuiltInWeaponActionLibrary.FinalWingWeaponId &&
            _forms != null && _forms.CurrentFormId == "form.y.final";
        public bool PassivesEnabled => IsAvailable && _suppressionTokens == 0;

        private void Awake() => CacheComponents();

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
            RefreshPassives();
        }

        private void OnDisable()
        {
            UnbindEvents();
            _suppressionTokens = 0;
            ClearAutomaticVolleys();
            RemovePassives();
        }

        private void Update()
        {
            RefreshPassives();
            if (!PassivesEnabled || Time.time + 0.0001f < _nextAutomaticFeather)
            {
                return;
            }

            if ((_automaticFeathersRemaining <= 0 ||
                 _automaticTarget == null || !_automaticTarget.isActiveAndEnabled) &&
                !TryStartNextAutomaticVolley())
            {
                return;
            }

            FireAutomaticFeather();
            _automaticFeathersRemaining--;
            _nextAutomaticFeather = Time.time + AutomaticFeatherInterval;
        }

        public float ModifyIncomingDamage(DamageInfo damageInfo, float currentAmount)
        {
            // Final Wing blocks before DamageTaken and Armor, keeping the order deterministic.
            return PassivesEnabled ? Mathf.Max(0f, currentAmount) * 0.5f : currentAmount;
        }

        private void HandleHitApplied(
            CombatHitboxResolver resolver,
            WeaponActionHitResult result)
        {
            if (!PassivesEnabled || !result.Damage.Changed || result.Weapon == null ||
                result.Weapon == _inventory.ActAuxiliary)
            {
                return;
            }
            _automaticVolleyTargets.Enqueue(result.Target);
            if (_automaticFeathersRemaining <= 0)
            {
                TryStartNextAutomaticVolley();
                _nextAutomaticFeather = Time.time;
            }
        }

        private void FireAutomaticFeather()
        {
            WeaponDefinition wing = _inventory.ActAuxiliary;
            WeaponActionDefinition volley = wing?.ActActionSet?.FindAction(
                "action.y.final-wing.volley");
            if (wing == null || volley == null || volley.Hits.Length == 0)
            {
                _automaticFeathersRemaining = 0;
                return;
            }
            var intent = new WeaponActionIntent(
                WeaponActionButtonMask.SecondaryAttack,
                Vector2.zero,
                false,
                _automaticTarget,
                0f,
                Time.frameCount);
            _hitboxes.Resolve(wing, volley, volley.Hits[0], intent);
        }

        private void HandleActionStarted(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action)
        {
            if (action != null && action.ActionId == FocusActionId)
            {
                _suppressionTokens++;
                ClearAutomaticVolleys();
                RefreshPassives();
            }
        }

        private void HandleActionEnded(
            WeaponActionController controller,
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            WeaponActionEndReason reason)
        {
            if (action != null && action.ActionId == FocusActionId)
            {
                _suppressionTokens = Mathf.Max(0, _suppressionTokens - 1);
                RefreshPassives();
            }
        }

        private void RefreshPassives()
        {
            bool available = IsAvailable;
            if (!available &&
                (_automaticFeathersRemaining > 0 || _automaticVolleyTargets.Count > 0))
            {
                ClearAutomaticVolleys();
            }

            bool shouldApply = available && _suppressionTokens == 0;
            if (shouldApply == _passivesApplied) return;
            if (shouldApply)
            {
                _stats.AddModifier(new StatModifierDefinition(
                    MovementSource, RuntimeStatId.MoveSpeed,
                    StatModifierOperation.Multiply, 1.2f, 0, 0f,
                    StatStackingRule.ReplaceExisting));
                _flight?.SetRuntimeHorizontalMultiplier(FlightSource, 3f);
                _passivesApplied = true;
            }
            else
            {
                RemovePassives();
            }
        }

        private void RemovePassives()
        {
            _stats?.RemoveModifiersBySource(MovementSource);
            _flight?.RemoveRuntimeHorizontalMultiplier(FlightSource);
            _passivesApplied = false;
        }

        private bool TryStartNextAutomaticVolley()
        {
            _automaticTarget = null;
            _automaticFeathersRemaining = 0;
            while (_automaticVolleyTargets.Count > 0)
            {
                EntityIdentity target = _automaticVolleyTargets.Dequeue();
                if (target == null || !target.isActiveAndEnabled)
                {
                    continue;
                }

                _automaticTarget = target;
                _automaticFeathersRemaining = AutomaticFeathersPerMainHit;
                return true;
            }

            return false;
        }

        private void ClearAutomaticVolleys()
        {
            _automaticVolleyTargets.Clear();
            _automaticTarget = null;
            _automaticFeathersRemaining = 0;
            _nextAutomaticFeather = 0f;
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_actions != null)
            {
                _actions.ActionStarted += HandleActionStarted;
                _actions.ActionEnded += HandleActionEnded;
            }
            if (_hitboxes != null) _hitboxes.HitApplied += HandleHitApplied;
        }

        private void UnbindEvents()
        {
            if (_actions != null)
            {
                _actions.ActionStarted -= HandleActionStarted;
                _actions.ActionEnded -= HandleActionEnded;
            }
            if (_hitboxes != null) _hitboxes.HitApplied -= HandleHitApplied;
        }

        private void CacheComponents()
        {
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_stats == null) _stats = GetComponent<RuntimeStatBlock>();
            if (_flight == null) _flight = GetComponent<FlightController>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
            if (_actions == null) _actions = GetComponent<WeaponActionController>();
            if (_hitboxes == null) _hitboxes = GetComponent<CombatHitboxResolver>();
        }
    }
}
