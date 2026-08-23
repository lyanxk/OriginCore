using System;
using OriginCore.ACT;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInventory), typeof(WeaponActionController))]
    public sealed class DirectCombatInputResolver : MonoBehaviour
    {
        [SerializeField] private WeaponInventory _inventory;
        [SerializeField] private WeaponActionController _actions;
        [SerializeField] private HybridPawnMotor _motor;
        [SerializeField] private TargetLockController _targetLock;
        [SerializeField] private FinalWingTargetingController _finalWingTargeting;
        [SerializeField] private CombatStatusController _statuses;
        [Range(0.01f, 0.5f), SerializeField] private float _chordWindow = 0.12f;
        [Range(0.05f, 1f), SerializeField] private float _longPressThreshold = 0.25f;
        [Range(0.05f, 0.95f), SerializeField] private float _directionThreshold = 0.35f;

        private InputRouter _inputRouter;
        private bool _hasPending;
        private WeaponActionButtonMask _pendingButtons;
        private Vector2 _pendingMove;
        private EntityIdentity _pendingTarget;
        private bool _pendingAirborne;
        private bool _pendingReleased;
        private float _pendingElapsed;
        private int _pendingFrame;
        private int _lastProcessedFrame = -1;

        public event Action<DirectCombatInputResolver, WeaponDefinition,
            WeaponActionDefinition> IntentResolved;
        public event Action<DirectCombatInputResolver, string> IntentRejected;

        public bool OwnsWeaponActionInput =>
            _inventory != null &&
            (_inventory.ActiveWeapon != null &&
             _inventory.ActiveWeapon.ActActionSet != null ||
             _inventory.ActAuxiliary != null &&
             _inventory.ActAuxiliary.ActActionSet != null);
        public float ChordWindow => _chordWindow;
        public float LongPressThreshold => _longPressThreshold;

        private void OnEnable()
        {
            CacheComponents();
            TryBindInput();
        }

        private void Start()
        {
            TryBindInput();
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
                _inputRouter = null;
            }
            ClearPending();
            _lastProcessedFrame = -1;
        }

        private void OnValidate()
        {
            CacheComponents();
            _chordWindow = Mathf.Clamp(_chordWindow, 0.01f, 0.5f);
            _longPressThreshold = Mathf.Max(_chordWindow, _longPressThreshold);
            _directionThreshold = Mathf.Clamp(_directionThreshold, 0.05f, 0.95f);
        }

        public void ProcessSnapshot(InputSnapshot snapshot, float deltaTime)
        {
            if (snapshot.Frame == _lastProcessedFrame)
            {
                return;
            }
            _lastProcessedFrame = snapshot.Frame;

            if (snapshot.GameplaySuppressed ||
                snapshot.ActiveMap != GameplayInputMap.ACT ||
                !IsPossessedPawn() || !OwnsWeaponActionInput ||
                _statuses != null && _statuses.BlocksActions)
            {
                ClearPending();
                return;
            }

            ActInputSnapshot input = snapshot.ACT;
            WeaponActionButtonMask pressed = WeaponActionButtonMask.None;
            if (input.Primary.WasPressedThisFrame)
            {
                pressed |= WeaponActionButtonMask.PrimaryAttack;
            }
            if (input.Secondary.WasPressedThisFrame)
            {
                pressed |= WeaponActionButtonMask.SecondaryAttack;
            }
            if (input.WeaponModifier.WasPressedThisFrame)
            {
                pressed |= WeaponActionButtonMask.WeaponModifier;
            }

            if (pressed != WeaponActionButtonMask.None)
            {
                WeaponActionDefinition current = _actions.CurrentAction;
                if (current != null &&
                    (current.CancelButtons & pressed) != 0)
                {
                    _actions.Cancel();
                    ClearPending();
                    return;
                }

                if (!_hasPending)
                {
                    BeginPending(input, snapshot.Frame);
                }
                _pendingButtons |= pressed;
            }

            if (!_hasPending && _actions.CurrentAction == null)
            {
                WeaponActionButtonMask heldButtons = GetHeldWeaponButtons(input);
                EntityIdentity auxiliaryTarget = _finalWingTargeting != null
                    ? _finalWingTargeting.ResolveTarget(true)
                    : null;
                bool repeatsMain = HasHoldCandidate(
                    _inventory.ActiveWeapon,
                    heldButtons,
                    input.Move,
                    _motor != null && !_motor.IsGrounded,
                    _targetLock != null && _targetLock.CurrentTarget != null);
                bool repeatsAuxiliary = HasHoldCandidate(
                    _inventory.ActAuxiliary,
                    heldButtons,
                    input.Move,
                    _motor != null && !_motor.IsGrounded,
                    auxiliaryTarget != null);
                if (repeatsMain || repeatsAuxiliary)
                {
                    BeginPending(input, snapshot.Frame);
                    _pendingButtons = heldButtons;
                    _pendingElapsed = _chordWindow;
                }
            }

            if (!_hasPending)
            {
                return;
            }

            _pendingElapsed += Mathf.Max(0f, deltaTime);
            if (input.WeaponModifier.IsPressed)
            {
                _pendingButtons |= WeaponActionButtonMask.WeaponModifier;
            }
            if (input.Primary.WasReleasedThisFrame ||
                input.Secondary.WasReleasedThisFrame ||
                input.WeaponModifier.WasReleasedThisFrame)
            {
                _pendingReleased = true;
            }

            bool longPressCandidate = HasLongPressCandidate(
                _inventory.ActiveWeapon,
                _pendingButtons,
                _pendingMove,
                _pendingAirborne,
                _pendingTarget != null) ||
                HasLongPressCandidate(
                    _inventory.ActAuxiliary,
                    _pendingButtons,
                    _pendingMove,
                    _pendingAirborne,
                    _pendingTarget != null);
            if (longPressCandidate && !_pendingReleased &&
                _pendingElapsed < _longPressThreshold)
            {
                return;
            }

            if (!_pendingReleased && _pendingElapsed < _chordWindow)
            {
                return;
            }

            ResolvePending(input);
        }

        public void ClearTransientState()
        {
            ClearPending();
            _lastProcessedFrame = -1;
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            ProcessSnapshot(snapshot, Time.deltaTime);
        }

        private void BeginPending(ActInputSnapshot input, int frame)
        {
            _hasPending = true;
            _pendingButtons = WeaponActionButtonMask.None;
            _pendingMove = input.Move;
            _pendingTarget = _targetLock != null ? _targetLock.CurrentTarget : null;
            _pendingAirborne = _motor != null && !_motor.IsGrounded;
            _pendingReleased = false;
            _pendingElapsed = 0f;
            _pendingFrame = frame;
        }

        private void ResolvePending(ActInputSnapshot input)
        {
            WeaponActionIntent intent = new WeaponActionIntent(
                _pendingButtons,
                _pendingMove,
                _pendingAirborne,
                _pendingTarget,
                _pendingElapsed,
                _pendingFrame);
            EntityIdentity auxiliaryTarget = _finalWingTargeting != null
                ? _finalWingTargeting.ResolveTarget(true)
                : _pendingTarget;
            WeaponActionIntent auxiliaryIntent = new WeaponActionIntent(
                _pendingButtons,
                _pendingMove,
                _pendingAirborne,
                auxiliaryTarget,
                _pendingElapsed,
                _pendingFrame);
            WeaponDefinition bestWeapon = null;
            WeaponActionDefinition bestAction = null;
            WeaponActionIntent bestIntent = intent;
            int bestScore = int.MinValue;
            FindBestAction(
                _inventory.ActiveWeapon,
                intent,
                input,
                ref bestWeapon,
                ref bestAction,
                ref bestIntent,
                ref bestScore);
            FindBestAction(
                _inventory.ActAuxiliary,
                auxiliaryIntent,
                input,
                ref bestWeapon,
                ref bestAction,
                ref bestIntent,
                ref bestScore);

            if (bestAction != null)
            {
                if (_actions.TryBegin(
                        bestWeapon,
                        bestAction.ActionId,
                        bestIntent,
                        out string error))
                {
                    IntentResolved?.Invoke(this, bestWeapon, bestAction);
                }
                else
                {
                    IntentRejected?.Invoke(this, error);
                }
                ClearPending();
                return;
            }

            WeaponActionButtonMask attackButtons =
                _pendingButtons &
                (WeaponActionButtonMask.PrimaryAttack |
                 WeaponActionButtonMask.SecondaryAttack |
                 WeaponActionButtonMask.WeaponModifier);
            if (attackButtons == WeaponActionButtonMask.PrimaryAttack &&
                _inventory.ActiveWeapon != null &&
                _inventory.ActiveWeapon.ActActionSet != null)
            {
                if (!_actions.TryBeginNextCombo(
                        _pendingAirborne,
                        intent,
                        out string comboError))
                {
                    IntentRejected?.Invoke(this, comboError);
                }
            }

            ClearPending();
        }

        private void FindBestAction(
            WeaponDefinition weapon,
            WeaponActionIntent intent,
            ActInputSnapshot input,
            ref WeaponDefinition bestWeapon,
            ref WeaponActionDefinition bestAction,
            ref WeaponActionIntent bestIntent,
            ref int bestScore)
        {
            WeaponActionSetDefinition set = weapon != null ? weapon.ActActionSet : null;
            if (set == null || !_actions.IsWeaponAvailable(weapon))
            {
                return;
            }

            WeaponActionDefinition[] candidates = set.Actions;
            for (int i = 0; i < candidates.Length; i++)
            {
                WeaponActionDefinition candidate = candidates[i];
                if (candidate == null || candidate.ComboOnly ||
                    !Matches(candidate.Input, intent, input) ||
                    !MatchesRequiredMainWeapon(candidate) ||
                    !_actions.CanInterruptWith(candidate))
                {
                    continue;
                }

                int score = Score(candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestWeapon = weapon;
                    bestAction = candidate;
                    bestIntent = intent;
                }
            }
        }

        private bool MatchesRequiredMainWeapon(WeaponActionDefinition action)
        {
            return action != null &&
                   (string.IsNullOrEmpty(action.RequiredMainWeaponId) ||
                    _inventory.ActiveWeapon != null &&
                    string.Equals(
                        _inventory.ActiveWeapon.ContentId,
                        action.RequiredMainWeaponId,
                        StringComparison.Ordinal));
        }

        private bool Matches(
            WeaponActionInputDefinition definition,
            WeaponActionIntent intent,
            ActInputSnapshot liveInput)
        {
            if (definition == null ||
                (intent.Buttons & definition.RequiredButtons) !=
                definition.RequiredButtons)
            {
                return false;
            }

            WeaponActionStanceMask stance = intent.Airborne
                ? WeaponActionStanceMask.Airborne
                : WeaponActionStanceMask.Grounded;
            if ((definition.Stances & stance) == 0 ||
                definition.RequiresTargetLock && !intent.HasTarget ||
                definition.RequiresNoTargetLock && intent.HasTarget ||
                !MatchesDirection(definition.Direction, intent.Move))
            {
                return false;
            }

            switch (definition.Press)
            {
                case WeaponPressRequirement.ShortPress:
                    return _pendingReleased &&
                           intent.PressDuration < _longPressThreshold;
                case WeaponPressRequirement.LongPress:
                    return intent.PressDuration >= _longPressThreshold;
                case WeaponPressRequirement.Hold:
                    return _pendingReleased || AreRequiredButtonsHeld(
                        definition.RequiredButtons,
                        liveInput);
                default:
                    return true;
            }
        }

        private bool MatchesDirection(
            WeaponDirectionRequirement requirement,
            Vector2 move)
        {
            switch (requirement)
            {
                case WeaponDirectionRequirement.Neutral:
                    return move.magnitude < _directionThreshold;
                case WeaponDirectionRequirement.Forward:
                    return move.y >= _directionThreshold &&
                           Mathf.Abs(move.y) >= Mathf.Abs(move.x);
                case WeaponDirectionRequirement.Backward:
                    return move.y <= -_directionThreshold &&
                           Mathf.Abs(move.y) >= Mathf.Abs(move.x);
                case WeaponDirectionRequirement.Left:
                    return move.x <= -_directionThreshold &&
                           Mathf.Abs(move.x) > Mathf.Abs(move.y);
                case WeaponDirectionRequirement.Right:
                    return move.x >= _directionThreshold &&
                           Mathf.Abs(move.x) > Mathf.Abs(move.y);
                case WeaponDirectionRequirement.Horizontal:
                    return Mathf.Abs(move.x) >= _directionThreshold &&
                           Mathf.Abs(move.x) > Mathf.Abs(move.y);
                default:
                    return true;
            }
        }

        private bool HasLongPressCandidate(
            WeaponDefinition weapon,
            WeaponActionButtonMask buttons,
            Vector2 move,
            bool airborne,
            bool hasTarget)
        {
            WeaponActionSetDefinition set = weapon != null ? weapon.ActActionSet : null;
            if (set == null)
            {
                return false;
            }

            WeaponActionStanceMask stance = airborne
                ? WeaponActionStanceMask.Airborne
                : WeaponActionStanceMask.Grounded;
            WeaponActionDefinition[] actions = set.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                WeaponActionInputDefinition candidate = actions[i] != null
                    ? actions[i].Input
                    : null;
                if (candidate != null &&
                    candidate.Press == WeaponPressRequirement.LongPress &&
                    (buttons & (WeaponActionButtonMask.PrimaryAttack |
                                WeaponActionButtonMask.SecondaryAttack |
                                WeaponActionButtonMask.WeaponModifier)) ==
                    candidate.RequiredButtons &&
                    (candidate.Stances & stance) != 0 &&
                    (!candidate.RequiresTargetLock || hasTarget) &&
                    (!candidate.RequiresNoTargetLock || !hasTarget) &&
                    MatchesDirection(candidate.Direction, move))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasHoldCandidate(
            WeaponDefinition weapon,
            WeaponActionButtonMask buttons,
            Vector2 move,
            bool airborne,
            bool hasTarget)
        {
            if (buttons == WeaponActionButtonMask.None)
            {
                return false;
            }

            WeaponActionSetDefinition set = weapon != null ? weapon.ActActionSet : null;
            if (set == null)
            {
                return false;
            }

            WeaponActionStanceMask stance = airborne
                ? WeaponActionStanceMask.Airborne
                : WeaponActionStanceMask.Grounded;
            WeaponActionDefinition[] actions = set.Actions;
            for (int i = 0; i < actions.Length; i++)
            {
                WeaponActionInputDefinition candidate = actions[i] != null
                    ? actions[i].Input
                    : null;
                if (candidate != null &&
                    candidate.Press == WeaponPressRequirement.Hold &&
                    buttons == candidate.RequiredButtons &&
                    (candidate.Stances & stance) != 0 &&
                    (!candidate.RequiresTargetLock || hasTarget) &&
                    (!candidate.RequiresNoTargetLock || !hasTarget) &&
                    MatchesDirection(candidate.Direction, move))
                {
                    return true;
                }
            }

            return false;
        }

        private static WeaponActionButtonMask GetHeldWeaponButtons(
            ActInputSnapshot input)
        {
            WeaponActionButtonMask buttons = WeaponActionButtonMask.None;
            if (input.Primary.IsPressed)
            {
                buttons |= WeaponActionButtonMask.PrimaryAttack;
            }
            if (input.Secondary.IsPressed)
            {
                buttons |= WeaponActionButtonMask.SecondaryAttack;
            }
            if (input.WeaponModifier.IsPressed)
            {
                buttons |= WeaponActionButtonMask.WeaponModifier;
            }
            return buttons;
        }

        private static bool AreRequiredButtonsHeld(
            WeaponActionButtonMask buttons,
            ActInputSnapshot input)
        {
            if ((buttons & WeaponActionButtonMask.PrimaryAttack) != 0 &&
                !input.Primary.IsPressed)
            {
                return false;
            }
            if ((buttons & WeaponActionButtonMask.SecondaryAttack) != 0 &&
                !input.Secondary.IsPressed)
            {
                return false;
            }
            if ((buttons & WeaponActionButtonMask.WeaponModifier) != 0 &&
                !input.WeaponModifier.IsPressed)
            {
                return false;
            }
            return true;
        }

        private static int Score(WeaponActionDefinition action)
        {
            WeaponActionInputDefinition input = action.Input;
            int buttons = CountBits((int)input.RequiredButtons);
            int score = buttons * 1000;
            if (input.RequiresTargetLock)
            {
                score += 200;
            }
            if (input.Direction != WeaponDirectionRequirement.Any)
            {
                score += 100;
            }
            if (input.Press != WeaponPressRequirement.Press)
            {
                score += 50;
            }

            WeaponActionPhaseDefinition[] phases = action.Phases;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i] != null)
                {
                    score += phases[i].Priority;
                    break;
                }
            }
            return score;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        private void ClearPending()
        {
            _hasPending = false;
            _pendingButtons = WeaponActionButtonMask.None;
            _pendingMove = Vector2.zero;
            _pendingTarget = null;
            _pendingAirborne = false;
            _pendingReleased = false;
            _pendingElapsed = 0f;
            _pendingFrame = 0;
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private void TryBindInput()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null ||
                root.Services.InputRouter == null)
            {
                return;
            }

            InputRouter next = root.Services.InputRouter;
            if (_inputRouter == next)
            {
                return;
            }
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshot;
            }
            _inputRouter = next;
            _inputRouter.SnapshotReady += HandleSnapshot;
        }

        private void CacheComponents()
        {
            if (_inventory == null) _inventory = GetComponent<WeaponInventory>();
            if (_actions == null) _actions = GetComponent<WeaponActionController>();
            if (_motor == null) _motor = GetComponent<HybridPawnMotor>();
            if (_targetLock == null) _targetLock = GetComponent<TargetLockController>();
            if (_finalWingTargeting == null)
            {
                _finalWingTargeting = GetComponent<FinalWingTargetingController>();
            }
            if (_statuses == null) _statuses = GetComponent<CombatStatusController>();
        }
    }
}
