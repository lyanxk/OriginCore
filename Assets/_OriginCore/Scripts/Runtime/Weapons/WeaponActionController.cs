using System;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Weapons
{
    public enum WeaponActionEndReason
    {
        Completed = 0,
        Cancelled = 1,
        Interrupted = 2,
        WeaponSwitched = 3,
        FormChanged = 4,
        OwnerDisabled = 5,
        TargetInvalid = 6
    }

    public readonly struct WeaponActionIntent
    {
        public WeaponActionIntent(
            WeaponActionButtonMask buttons,
            Vector2 move,
            bool airborne,
            EntityIdentity target,
            float pressDuration,
            int frame)
        {
            Buttons = buttons;
            Move = Vector2.ClampMagnitude(move, 1f);
            Airborne = airborne;
            Target = target;
            PressDuration = Mathf.Max(0f, pressDuration);
            Frame = frame;
        }

        public WeaponActionButtonMask Buttons { get; }
        public Vector2 Move { get; }
        public bool Airborne { get; }
        public EntityIdentity Target { get; }
        public float PressDuration { get; }
        public int Frame { get; }
        public bool HasTarget => Target != null && Target.isActiveAndEnabled;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory))]
    public sealed class WeaponActionController : MonoBehaviour
    {
        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private HeroFormStateMachine _forms;
        [SerializeField] private CombatStatusController _statuses;
        [SerializeField] private GameModeController _gameModeController;
        [SerializeField] private FinalWingTargetingController _finalWingTargeting;

        private WeaponDefinition _sourceWeapon;
        private WeaponDefinition _mainWeaponAtStart;
        private WeaponActionDefinition _currentAction;
        private WeaponActionIntent _currentIntent;
        private int _phaseIndex = -1;
        private float _phaseElapsed;
        private float _actionElapsed;
        private float _runtimeClock;
        private int _groundComboIndex;
        private int _airComboIndex;
        private float _groundComboDeadline;
        private float _airComboDeadline;
        private float _groundComboReadyAt;
        private float _airComboReadyAt;
        private bool _currentWasCombo;
        private bool _currentComboAirborne;

        public event Action<WeaponActionController, WeaponDefinition, WeaponActionDefinition>
            ActionStarted;
        public event Action<WeaponActionController, WeaponActionDefinition,
            WeaponActionPhaseDefinition> PhaseChanged;
        public event Action<WeaponActionController, WeaponDefinition, WeaponActionDefinition,
            WeaponActionEndReason> ActionEnded;

        public WeaponDefinition SourceWeapon => _sourceWeapon;
        public WeaponDefinition MainWeaponAtStart => _mainWeaponAtStart;
        public WeaponActionDefinition CurrentAction => _currentAction;
        public WeaponActionIntent CurrentIntent => _currentIntent;
        public WeaponActionPhaseDefinition CurrentPhase =>
            _currentAction != null && _phaseIndex >= 0 &&
            _phaseIndex < _currentAction.Phases.Length
                ? _currentAction.Phases[_phaseIndex]
                : null;
        public bool IsExecuting => _currentAction != null;
        public bool BlocksDirectMovement =>
            CurrentPhase != null && CurrentPhase.BlocksDirectMovement;
        public WeaponGravityPolicy GravityPolicy => CurrentPhase != null
            ? CurrentPhase.GravityPolicy
            : WeaponGravityPolicy.Normal;
        public int CurrentPriority => CurrentPhase != null ? CurrentPhase.Priority : 0;
        public float PhaseElapsed => _phaseElapsed;
        public float ActionElapsed => _actionElapsed;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
        }

        private void Start()
        {
            CacheComponents();
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
            EndCurrentAction(WeaponActionEndReason.OwnerDisabled);
            ResetCombos();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _runtimeClock += deltaTime;
            if (_currentAction == null ||
                (_statuses != null && _statuses.IsTemporallyLocked))
            {
                return;
            }

            if (!_currentIntent.HasTarget &&
                _currentAction.Input.RequiresTargetLock)
            {
                EndCurrentAction(WeaponActionEndReason.TargetInvalid);
                return;
            }
            if (_currentAction.Input.RequiresTargetLock &&
                _sourceWeapon != null &&
                string.Equals(
                    _sourceWeapon.ContentId,
                    BuiltInWeaponActionLibrary.FinalWingWeaponId,
                    StringComparison.Ordinal) &&
                _finalWingTargeting != null &&
                !_finalWingTargeting.IsTargetValid(_currentIntent.Target))
            {
                EndCurrentAction(WeaponActionEndReason.TargetInvalid);
                return;
            }

            _phaseElapsed += deltaTime;
            _actionElapsed += deltaTime;
            if (_currentAction.MaximumPersistentSeconds > 0f &&
                _actionElapsed >= _currentAction.MaximumPersistentSeconds)
            {
                EndCurrentAction(WeaponActionEndReason.Completed);
                return;
            }

            AdvanceCompletedPhases();
        }

        public bool TryBegin(
            string actionId,
            WeaponActionIntent intent,
            out string error)
        {
            CacheComponents();
            return TryBegin(_inventory != null ? _inventory.ActiveWeapon : null,
                actionId, intent, false, out error);
        }

        public bool TryBegin(
            WeaponDefinition sourceWeapon,
            string actionId,
            WeaponActionIntent intent,
            out string error)
        {
            return TryBegin(sourceWeapon, actionId, intent, false, out error);
        }

        public bool TryBeginNextCombo(
            bool airborne,
            WeaponActionIntent intent,
            out string error)
        {
            CacheComponents();
            WeaponDefinition weapon = _inventory != null ? _inventory.ActiveWeapon : null;
            WeaponActionSetDefinition set = weapon != null ? weapon.ActActionSet : null;
            WeaponComboDefinition combo = set != null
                ? airborne ? set.AirCombo : set.GroundCombo
                : null;
            string[] ids = combo != null ? combo.ActionIds : null;
            if (ids == null || ids.Length == 0)
            {
                error = "The active weapon has no " +
                        (airborne ? "air" : "ground") + " combo.";
                return false;
            }

            int index = airborne ? _airComboIndex : _groundComboIndex;
            float deadline = airborne ? _airComboDeadline : _groundComboDeadline;
            float readyAt = airborne ? _airComboReadyAt : _groundComboReadyAt;
            if (_runtimeClock < readyAt)
            {
                error = "The combo restart delay has not elapsed.";
                return false;
            }
            if (_runtimeClock > deadline)
            {
                index = 0;
            }
            index = Mathf.Clamp(index, 0, ids.Length - 1);
            return TryBegin(weapon, ids[index], intent, true, out error);
        }

        public bool CanInterruptWith(WeaponActionDefinition next)
        {
            if (next == null)
            {
                return false;
            }

            if (_currentAction == null || CurrentPhase == null)
            {
                return true;
            }

            int nextPriority = ResolveInitialPriority(next);
            return CurrentPhase.CanCancelIntoNextAction &&
                   nextPriority >= CurrentPhase.Priority ||
                   CurrentPhase.CanBeInterrupted &&
                   nextPriority > CurrentPhase.Priority;
        }

        public bool CanAuthorizeExternalAction(
            int priority,
            bool preservePersistentAction = false)
        {
            if (_currentAction == null || CurrentPhase == null)
            {
                return true;
            }

            if (preservePersistentAction &&
                (_currentAction.PersistentWhenUnequipped ||
                 _sourceWeapon != null && _sourceWeapon.PersistsWhenUnequipped))
            {
                return true;
            }

            int requestedPriority = Mathf.Max(0, priority);
            return CurrentPhase.CanCancelIntoNextAction &&
                   requestedPriority >= CurrentPhase.Priority ||
                   CurrentPhase.CanBeInterrupted &&
                   requestedPriority > CurrentPhase.Priority;
        }

        public bool TryAuthorizeExternalAction(
            int priority,
            bool preservePersistentAction,
            out string error)
        {
            if (!CanAuthorizeExternalAction(priority, preservePersistentAction))
            {
                error = "The current weapon action phase rejects this external action.";
                return false;
            }

            bool shouldPreserve = preservePersistentAction &&
                _currentAction != null &&
                (_currentAction.PersistentWhenUnequipped ||
                 _sourceWeapon != null && _sourceWeapon.PersistsWhenUnequipped);
            if (_currentAction != null && !shouldPreserve)
            {
                EndCurrentAction(WeaponActionEndReason.Interrupted);
            }

            error = string.Empty;
            return true;
        }

        public bool IsWeaponAvailable(WeaponDefinition weapon)
        {
            CacheComponents();
            if (weapon == null || _inventory == null ||
                _inventory.FindState(weapon) == null || !IsFormAvailable(weapon))
            {
                return false;
            }
            return weapon.EquipRole == WeaponEquipRole.Auxiliary ||
                   _inventory.ActiveWeapon == weapon;
        }

        public bool Cancel(WeaponActionEndReason reason = WeaponActionEndReason.Cancelled)
        {
            if (_currentAction == null)
            {
                return false;
            }

            EndCurrentAction(reason);
            return true;
        }

        public void ResetTransientState()
        {
            EndCurrentAction(WeaponActionEndReason.Cancelled);
            ResetCombos();
        }

        private bool TryBegin(
            WeaponDefinition sourceWeapon,
            string actionId,
            WeaponActionIntent intent,
            bool isCombo,
            out string error)
        {
            CacheComponents();
            if (sourceWeapon == null || sourceWeapon.ActActionSet == null)
            {
                error = "The source weapon has no ACT action set.";
                return false;
            }

            if (_inventory == null || _inventory.FindState(sourceWeapon) == null)
            {
                error = "The source weapon is not owned by this pawn.";
                return false;
            }

            if (sourceWeapon.EquipRole == WeaponEquipRole.MainHand &&
                _inventory.ActiveWeapon != sourceWeapon)
            {
                error = "The source weapon is not the active main-hand weapon.";
                return false;
            }

            if (!IsFormAvailable(sourceWeapon))
            {
                error = "The current hero form does not unlock this weapon.";
                return false;
            }

            WeaponActionDefinition action = sourceWeapon.ActActionSet.FindAction(actionId);
            if (action == null)
            {
                error = "Unknown weapon action '" + actionId + "'.";
                return false;
            }

            if (!string.IsNullOrEmpty(action.RequiredMainWeaponId) &&
                (_inventory.ActiveWeapon == null ||
                 !string.Equals(
                     _inventory.ActiveWeapon.ContentId,
                     action.RequiredMainWeaponId,
                     StringComparison.Ordinal)))
            {
                error = "The required main-hand weapon is not active.";
                return false;
            }

            WeaponActionStanceMask stance = intent.Airborne
                ? WeaponActionStanceMask.Airborne
                : WeaponActionStanceMask.Grounded;
            if ((action.Input.Stances & stance) == 0)
            {
                error = "Weapon action stance requirements are not satisfied.";
                return false;
            }

            if (action.Input.RequiresTargetLock && !intent.HasTarget ||
                action.Input.RequiresNoTargetLock && intent.HasTarget)
            {
                error = "Weapon action target-lock requirements are not satisfied.";
                return false;
            }

            if (!CanInterruptWith(action))
            {
                error = "The current weapon action cannot be interrupted.";
                return false;
            }

            if (_currentAction != null)
            {
                EndCurrentAction(WeaponActionEndReason.Interrupted);
            }

            _sourceWeapon = sourceWeapon;
            _mainWeaponAtStart = _inventory != null ? _inventory.ActiveWeapon : null;
            _currentAction = action;
            _currentIntent = intent;
            _phaseIndex = -1;
            _phaseElapsed = 0f;
            _actionElapsed = 0f;
            _currentWasCombo = isCombo;
            _currentComboAirborne = intent.Airborne;
            ActionStarted?.Invoke(this, _sourceWeapon, _currentAction);
            EnterNextPhase();
            AdvanceCompletedPhases();
            error = string.Empty;
            return true;
        }

        private void AdvanceCompletedPhases()
        {
            int guard = 0;
            while (_currentAction != null && CurrentPhase != null &&
                   _phaseElapsed >= CurrentPhase.Duration && guard++ < 32)
            {
                if (CurrentPhase.Kind == WeaponActionPhaseKind.Sustained &&
                    CurrentPhase.Duration <= 0f)
                {
                    break;
                }
                _phaseElapsed -= CurrentPhase.Duration;
                if (!EnterNextPhase())
                {
                    EndCurrentAction(WeaponActionEndReason.Completed);
                    return;
                }
            }

            if (_currentAction != null && CurrentPhase == null)
            {
                EndCurrentAction(WeaponActionEndReason.Completed);
            }
        }

        private bool EnterNextPhase()
        {
            if (_currentAction == null)
            {
                return false;
            }

            WeaponActionPhaseDefinition[] phases = _currentAction.Phases;
            do
            {
                _phaseIndex++;
            }
            while (_phaseIndex < phases.Length && phases[_phaseIndex] == null);

            if (_phaseIndex >= phases.Length)
            {
                return false;
            }

            PhaseChanged?.Invoke(this, _currentAction, phases[_phaseIndex]);
            return true;
        }

        private void EndCurrentAction(WeaponActionEndReason reason)
        {
            if (_currentAction == null)
            {
                return;
            }

            WeaponDefinition weapon = _sourceWeapon;
            WeaponActionDefinition action = _currentAction;
            bool wasCombo = _currentWasCombo;
            bool comboAirborne = _currentComboAirborne;
            _sourceWeapon = null;
            _mainWeaponAtStart = null;
            _currentAction = null;
            _currentIntent = default;
            _phaseIndex = -1;
            _phaseElapsed = 0f;
            _actionElapsed = 0f;
            _currentWasCombo = false;
            _currentComboAirborne = false;

            if (reason == WeaponActionEndReason.Completed &&
                (wasCombo || action.ContinueComboAtIndex >= 0))
            {
                AdvanceCombo(weapon, action, comboAirborne);
            }
            else if (wasCombo)
            {
                ResetCombo(comboAirborne);
            }

            ActionEnded?.Invoke(this, weapon, action, reason);
        }

        private void AdvanceCombo(
            WeaponDefinition weapon,
            WeaponActionDefinition action,
            bool airborne)
        {
            WeaponComboDefinition combo = weapon != null && weapon.ActActionSet != null
                ? airborne ? weapon.ActActionSet.AirCombo : weapon.ActActionSet.GroundCombo
                : null;
            string[] ids = combo != null ? combo.ActionIds : null;
            if (ids == null || ids.Length == 0)
            {
                ResetCombo(airborne);
                return;
            }

            int next = action.ContinueComboAtIndex >= 0
                ? action.ContinueComboAtIndex
                : (airborne ? _airComboIndex : _groundComboIndex) + 1;
            if (next >= ids.Length)
            {
                next = 0;
                float restart = combo.RestartDelay;
                if (airborne)
                {
                    _airComboReadyAt = _runtimeClock + restart;
                    _airComboDeadline = _airComboReadyAt;
                }
                else
                {
                    _groundComboReadyAt = _runtimeClock + restart;
                    _groundComboDeadline = _groundComboReadyAt;
                }
            }
            else
            {
                float reset = combo.ResetDelay;
                if (airborne)
                {
                    _airComboReadyAt = 0f;
                    _airComboDeadline = _runtimeClock + reset;
                }
                else
                {
                    _groundComboReadyAt = 0f;
                    _groundComboDeadline = _runtimeClock + reset;
                }
            }

            if (airborne)
            {
                _airComboIndex = next;
            }
            else
            {
                _groundComboIndex = next;
            }
        }

        private void HandleActiveWeaponChanged(
            WeaponDefinition previous,
            WeaponDefinition current)
        {
            if (_currentAction == null || _sourceWeapon == null ||
                current == _sourceWeapon ||
                _sourceWeapon.PersistsWhenUnequipped ||
                _currentAction.PersistentWhenUnequipped)
            {
                return;
            }

            EndCurrentAction(WeaponActionEndReason.WeaponSwitched);
        }

        private void HandleFormChanged(
            HeroFormStateMachine machine,
            HeroFormDefinition previous,
            HeroFormDefinition current)
        {
            if (_sourceWeapon != null && !IsFormAvailable(_sourceWeapon))
            {
                EndCurrentAction(WeaponActionEndReason.FormChanged);
            }
            ResetCombos();
        }

        private bool IsFormAvailable(WeaponDefinition weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.RequiredFormId))
            {
                return true;
            }

            if (_forms == null || _forms.CurrentForm == null)
            {
                return false;
            }

            string cursor = _forms.CurrentForm.ContentId;
            HeroFormDefinition[] forms = _forms.HeroDefinition != null
                ? _forms.HeroDefinition.Forms
                : Array.Empty<HeroFormDefinition>();
            for (int guard = 0; guard <= forms.Length; guard++)
            {
                if (string.Equals(cursor, weapon.RequiredFormId, StringComparison.Ordinal))
                {
                    return true;
                }

                HeroFormDefinition form = FindForm(forms, cursor);
                if (form == null || string.IsNullOrEmpty(form.RequiredPreviousFormId))
                {
                    return false;
                }

                cursor = form.RequiredPreviousFormId;
            }

            return false;
        }

        private static HeroFormDefinition FindForm(
            HeroFormDefinition[] forms,
            string formId)
        {
            for (int i = 0; i < forms.Length; i++)
            {
                HeroFormDefinition form = forms[i];
                if (form != null &&
                    string.Equals(form.ContentId, formId, StringComparison.Ordinal))
                {
                    return form;
                }
            }

            return null;
        }

        private static int ResolveInitialPriority(WeaponActionDefinition action)
        {
            WeaponActionPhaseDefinition[] phases = action.Phases;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i] != null)
                {
                    return phases[i].Priority;
                }
            }

            return 0;
        }

        private void ResetCombos()
        {
            _groundComboIndex = 0;
            _airComboIndex = 0;
            _groundComboDeadline = 0f;
            _airComboDeadline = 0f;
            _groundComboReadyAt = 0f;
            _airComboReadyAt = 0f;
        }

        private void ResetCombo(bool airborne)
        {
            if (airborne)
            {
                _airComboIndex = 0;
                _airComboDeadline = 0f;
                _airComboReadyAt = 0f;
            }
            else
            {
                _groundComboIndex = 0;
                _groundComboDeadline = 0f;
                _groundComboReadyAt = 0f;
            }
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_inventory != null)
            {
                _inventory.ActiveWeaponChanged += HandleActiveWeaponChanged;
            }

            if (_forms != null)
            {
                _forms.FormChanged += HandleFormChanged;
            }

            if (_statuses != null)
            {
                _statuses.InterruptRequested += HandleInterruptRequested;
            }

            if (_gameModeController != null)
            {
                _gameModeController.ModeChanged += HandleModeChanged;
            }
        }

        private void UnbindEvents()
        {
            if (_inventory != null)
            {
                _inventory.ActiveWeaponChanged -= HandleActiveWeaponChanged;
            }

            if (_forms != null)
            {
                _forms.FormChanged -= HandleFormChanged;
            }

            if (_statuses != null)
            {
                _statuses.InterruptRequested -= HandleInterruptRequested;
            }

            if (_gameModeController != null)
            {
                _gameModeController.ModeChanged -= HandleModeChanged;
            }
        }

        private void HandleModeChanged(GameModeChange change)
        {
            if (change.Current != GameMode.ACT)
            {
                ResetTransientState();
            }
        }

        private void HandleInterruptRequested(CombatStatusController controller, int priority)
        {
            if (_currentAction != null && CurrentPhase != null &&
                CurrentPhase.CanBeInterrupted && priority >= CurrentPhase.Priority)
            {
                EndCurrentAction(WeaponActionEndReason.Interrupted);
            }
        }

        private void CacheComponents()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<WeaponInventory>();
            }
            if (_forms == null)
            {
                _forms = GetComponent<HeroFormStateMachine>();
            }
            if (_statuses == null)
            {
                _statuses = GetComponent<CombatStatusController>();
            }
            if (_finalWingTargeting == null)
            {
                _finalWingTargeting = GetComponent<FinalWingTargetingController>();
            }
            if (_gameModeController == null &&
                AppRoot.TryGetInstance(out AppRoot root) && root.Services != null)
            {
                _gameModeController = root.Services.GameModeController;
            }
        }
    }
}
