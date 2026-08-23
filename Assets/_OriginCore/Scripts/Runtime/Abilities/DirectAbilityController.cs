using System;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Weapons;
using UnityEngine;

namespace OriginCore.Abilities
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AbilityLoadout))]
    public sealed class DirectAbilityController : MonoBehaviour
    {
        [SerializeField] private AbilityLoadout _loadout;
        [SerializeField] private HeroFormStateMachine _forms;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private DirectCombatInputResolver _combatInput;
        [SerializeField] private WeaponActionController _weaponActions;
        [SerializeField] private LayerMask _aimMask = ~0;
        [Min(1f), SerializeField] private float _maximumAimDistance = 100f;
        [Min(0.1f), SerializeField] private float _transformHoldSeconds = 1f;
        [Min(0), SerializeField] private int _dashActionPriority = 10;
        [Min(0), SerializeField] private int _abilityActionPriority = 20;
        [Min(0), SerializeField] private int _transformActionPriority = 50;

        private InputRouter _inputRouter;
        private float _transformHeld;

        public event Action<int, AbilityCastResult> DirectCastFinished;

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

            _transformHeld = 0f;
        }

        private void OnValidate()
        {
            CacheComponents();
            _maximumAimDistance = Mathf.Max(1f, _maximumAimDistance);
            _transformHoldSeconds = Mathf.Max(0.1f, _transformHoldSeconds);
            _dashActionPriority = Mathf.Max(0, _dashActionPriority);
            _abilityActionPriority = Mathf.Max(0, _abilityActionPriority);
            _transformActionPriority = Mathf.Max(0, _transformActionPriority);
        }

        private void HandleSnapshot(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || _loadout == null || !IsPossessedPawn())
            {
                _transformHeld = 0f;
                return;
            }

            if (snapshot.ActiveMap == GameplayInputMap.ACT)
            {
                if (_combatInput != null && _combatInput.OwnsWeaponActionInput)
                {
                    HandleAbilityButtons(
                        GameMode.ACT,
                        snapshot.ACT.Move,
                        snapshot.ACT.Ability1,
                        default(InputButtonState),
                        snapshot.ACT.Ability3,
                        snapshot.ACT.Ability4);
                }
                else
                {
                    HandleAbilityButtons(
                        GameMode.ACT,
                        snapshot.ACT.Move,
                        snapshot.ACT.Ability1,
                        snapshot.ACT.Ability2,
                        snapshot.ACT.Ability3,
                        snapshot.ACT.Ability4);
                }
                HandleTransform(snapshot.ACT.Transform);
            }
            else if (snapshot.ActiveMap == GameplayInputMap.FPS)
            {
                HandleAbilityButtons(
                    GameMode.FPS,
                    snapshot.FPS.Move,
                    snapshot.FPS.Ability1,
                    snapshot.FPS.Ability2,
                    snapshot.FPS.Ability3,
                    snapshot.FPS.Ability4);
                HandleTransform(snapshot.FPS.Transform);
            }
            else
            {
                _transformHeld = 0f;
            }
        }

        private void HandleAbilityButtons(
            GameMode mode,
            Vector2 move,
            InputButtonState first,
            InputButtonState second,
            InputButtonState third,
            InputButtonState fourth)
        {
            if (first.WasPressedThisFrame) TryCast(mode, 0, move);
            if (second.WasPressedThisFrame) TryCast(mode, 1, move);
            if (third.WasPressedThisFrame) TryCast(mode, 2, move);
            if (fourth.WasPressedThisFrame) TryCast(mode, 3, move);
        }

        private void HandleTransform(InputButtonState button)
        {
            if (button.IsPressed)
            {
                _transformHeld += Time.deltaTime;
            }

            if (!button.WasReleasedThisFrame)
            {
                return;
            }

            if (_transformHeld >= _transformHoldSeconds && _forms != null &&
                _forms.CanEnterNextForm(out _) &&
                (_weaponActions == null ||
                 _weaponActions.TryAuthorizeExternalAction(
                     _transformActionPriority,
                     false,
                     out _)))
            {
                _forms.TryEnterNextForm(out _);
            }

            _transformHeld = 0f;
        }

        private void TryCast(GameMode mode, int slot, Vector2 move)
        {
            AbilityDefinition[] abilities = _loadout.GetAbilities(mode);
            if (slot < 0 || slot >= abilities.Length || abilities[slot] == null)
            {
                DirectCastFinished?.Invoke(
                    slot,
                    AbilityCastResult.Fail(
                        string.Empty,
                        AbilityCastFailure.MissingDefinition,
                        "Ability slot is empty."));
                return;
            }

            AbilityTarget target = BuildAimTarget(abilities[slot], move);
            if (!_loadout.CanCastSlot(mode, slot, ref target, out AbilityCastResult preview))
            {
                DirectCastFinished?.Invoke(slot, preview);
                return;
            }

            int actionPriority = mode == GameMode.ACT && slot == 0
                ? _dashActionPriority
                : _abilityActionPriority;
            if (mode == GameMode.ACT && _weaponActions != null &&
                !_weaponActions.TryAuthorizeExternalAction(
                    actionPriority,
                    false,
                    out string arbitrationError))
            {
                DirectCastFinished?.Invoke(
                    slot,
                    AbilityCastResult.Fail(
                        abilities[slot].ContentId,
                        AbilityCastFailure.Locked,
                        arbitrationError));
                return;
            }

            _loadout.TryCastSlot(mode, slot, target, out AbilityCastResult result);
            DirectCastFinished?.Invoke(slot, result);
        }

        private AbilityTarget BuildAimTarget(AbilityDefinition definition, Vector2 move)
        {
            if (definition.TargetType == AbilityTargetType.Self ||
                definition.TargetType == AbilityTargetType.None)
            {
                return AbilityTarget.None;
            }

            Camera camera = ResolveAimCamera();
            Vector3 origin = camera != null ? camera.transform.position : transform.position + Vector3.up;
            Vector3 direction = camera != null ? camera.transform.forward : transform.forward;
            if (definition.TargetType == AbilityTargetType.Direction)
            {
                Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
                Vector3 right = camera != null
                    ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up)
                    : transform.right;
                if (forward.sqrMagnitude <= 0.0001f) forward = transform.forward;
                if (right.sqrMagnitude <= 0.0001f) right = transform.right;
                Vector2 clamped = Vector2.ClampMagnitude(move, 1f);
                Vector3 moveDirection = forward.normalized * clamped.y +
                                        right.normalized * clamped.x;
                return AbilityTarget.ForDirection(
                    moveDirection.sqrMagnitude > 0.0001f
                        ? moveDirection
                        : transform.forward);
            }

            float distance = definition.Range > 0f
                ? Mathf.Min(_maximumAimDistance, definition.Range)
                : _maximumAimDistance;
            if (Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    distance,
                    _aimMask,
                    QueryTriggerInteraction.Collide))
            {
                EntityIdentity entity = hit.collider != null
                    ? hit.collider.GetComponentInParent<EntityIdentity>()
                    : null;
                if (definition.TargetType == AbilityTargetType.FriendlyEntity ||
                    definition.TargetType == AbilityTargetType.HostileEntity ||
                    definition.TargetType == AbilityTargetType.AnyEntity)
                {
                    return AbilityTarget.ForEntity(entity);
                }

                return AbilityTarget.ForPoint(hit.point);
            }

            return definition.TargetType == AbilityTargetType.Point
                ? AbilityTarget.ForPoint(origin + direction * distance)
                : AbilityTarget.ForEntity(null);
        }

        private bool IsPossessedPawn()
        {
            return AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                   root.Services.PossessionService.CurrentPawn != null &&
                   root.Services.PossessionService.CurrentPawn.gameObject == gameObject;
        }

        private Camera ResolveAimCamera()
        {
            if (_aimCamera != null)
            {
                return _aimCamera;
            }

            if (AppRoot.TryGetInstance(out AppRoot root) && root.Services != null &&
                root.Services.CurrentSceneContext != null)
            {
                _aimCamera = root.Services.CurrentSceneContext.MainCamera;
            }

            return _aimCamera;
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
            if (_loadout == null) _loadout = GetComponent<AbilityLoadout>();
            if (_forms == null) _forms = GetComponent<HeroFormStateMachine>();
            if (_combatInput == null)
            {
                _combatInput = GetComponent<DirectCombatInputResolver>();
            }
            if (_weaponActions == null)
            {
                _weaponActions = GetComponent<WeaponActionController>();
            }
        }
    }
}
