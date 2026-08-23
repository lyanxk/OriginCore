using System;
using System.Collections.Generic;
using OriginCore.Combat;
using UnityEngine;

namespace OriginCore.Content
{
    public enum WeaponEquipRole
    {
        MainHand = 0,
        Auxiliary = 1
    }

    [Flags]
    public enum WeaponActionStanceMask
    {
        None = 0,
        Grounded = 1 << 0,
        Airborne = 1 << 1,
        Any = Grounded | Airborne
    }

    [Flags]
    public enum WeaponActionButtonMask
    {
        None = 0,
        PrimaryAttack = 1 << 0,
        SecondaryAttack = 1 << 1,
        WeaponModifier = 1 << 2,
        DirectDash = 1 << 3
    }

    public enum WeaponDirectionRequirement
    {
        Any = 0,
        Neutral = 1,
        Forward = 2,
        Backward = 3,
        Left = 4,
        Right = 5,
        Horizontal = 6
    }

    public enum WeaponPressRequirement
    {
        Press = 0,
        ShortPress = 1,
        LongPress = 2,
        Hold = 3
    }

    public enum WeaponActionPhaseKind
    {
        Startup = 0,
        Active = 1,
        Recovery = 2,
        Sustained = 3
    }

    public enum WeaponActionMotionKind
    {
        None = 0,
        Dash = 1,
        TeleportToTargetFront = 2,
        Hover = 3,
        FollowTargetHeight = 4,
        LaunchWithTarget = 5,
        GroundSlam = 6,
        CrossThroughAndReturn = 7
    }

    public enum WeaponCollisionPolicy
    {
        Normal = 0,
        IgnoreUnits = 1,
        StopAtWorld = 2
    }

    public enum WeaponGravityPolicy
    {
        Normal = 0,
        Suspend = 1,
        ForceDownward = 2
    }

    public enum WeaponHitShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2,
        ForwardArc = 3,
        PathSweep = 4
    }

    public enum WeaponStatusType
    {
        None = 0,
        Interrupt = 1,
        Push = 2,
        Pull = 3,
        Launch = 4,
        LaunchPair = 5,
        GroundSlam = 6,
        DisplacementLock = 7,
        TemporalLock = 8
    }

    public enum WeaponStatusStackPolicy
    {
        Refresh = 0,
        Replace = 1,
        IgnoreIfPresent = 2,
        Stack = 3
    }

    [Serializable]
    public sealed class WeaponActionInputDefinition
    {
        [SerializeField] private WeaponActionButtonMask _requiredButtons =
            WeaponActionButtonMask.PrimaryAttack;
        [SerializeField] private WeaponDirectionRequirement _direction =
            WeaponDirectionRequirement.Any;
        [SerializeField] private WeaponPressRequirement _press =
            WeaponPressRequirement.Press;
        [SerializeField] private WeaponActionStanceMask _stances =
            WeaponActionStanceMask.Any;
        [SerializeField] private bool _requiresTargetLock;
        [SerializeField] private bool _requiresNoTargetLock;

        public WeaponActionButtonMask RequiredButtons => _requiredButtons;
        public WeaponDirectionRequirement Direction => _direction;
        public WeaponPressRequirement Press => _press;
        public WeaponActionStanceMask Stances => _stances;
        public bool RequiresTargetLock => _requiresTargetLock;
        public bool RequiresNoTargetLock => _requiresNoTargetLock;

        public void Configure(
            WeaponActionButtonMask requiredButtons,
            WeaponDirectionRequirement direction,
            WeaponPressRequirement press,
            WeaponActionStanceMask stances,
            bool requiresTargetLock,
            bool requiresNoTargetLock)
        {
            _requiredButtons = requiredButtons;
            _direction = direction;
            _press = press;
            _stances = stances;
            _requiresTargetLock = requiresTargetLock;
            _requiresNoTargetLock = requiresNoTargetLock && !requiresTargetLock;
        }

        internal void Normalize()
        {
            if (_requiredButtons == WeaponActionButtonMask.None)
            {
                _requiredButtons = WeaponActionButtonMask.PrimaryAttack;
            }

            if (_stances == WeaponActionStanceMask.None)
            {
                _stances = WeaponActionStanceMask.Any;
            }

            if (_requiresTargetLock)
            {
                _requiresNoTargetLock = false;
            }
        }
    }

    [Serializable]
    public sealed class WeaponActionPhaseDefinition
    {
        [SerializeField] private WeaponActionPhaseKind _kind =
            WeaponActionPhaseKind.Startup;
        [Min(0f), SerializeField] private float _duration;
        [SerializeField] private int _priority;
        [SerializeField] private bool _blocksDirectMovement = true;
        [SerializeField] private bool _canBeInterrupted = true;
        [SerializeField] private bool _canCancelIntoNextAction;
        [SerializeField] private WeaponGravityPolicy _gravityPolicy =
            WeaponGravityPolicy.Normal;

        public WeaponActionPhaseKind Kind => _kind;
        public float Duration => Mathf.Max(0f, _duration);
        public int Priority => _priority;
        public bool BlocksDirectMovement => _blocksDirectMovement;
        public bool CanBeInterrupted => _canBeInterrupted;
        public bool CanCancelIntoNextAction => _canCancelIntoNextAction;
        public WeaponGravityPolicy GravityPolicy => _gravityPolicy;

        public void Configure(
            WeaponActionPhaseKind kind,
            float duration,
            int priority,
            bool blocksDirectMovement,
            bool canBeInterrupted,
            bool canCancelIntoNextAction,
            WeaponGravityPolicy gravityPolicy)
        {
            _kind = kind;
            _duration = Mathf.Max(0f, duration);
            _priority = priority;
            _blocksDirectMovement = blocksDirectMovement;
            _canBeInterrupted = canBeInterrupted;
            _canCancelIntoNextAction = canCancelIntoNextAction;
            _gravityPolicy = gravityPolicy;
        }

        internal void Normalize()
        {
            _duration = Mathf.Max(0f, _duration);
        }
    }

    [Serializable]
    public sealed class WeaponActionMotionDefinition
    {
        [SerializeField] private WeaponActionMotionKind _kind;
        [SerializeField] private WeaponCollisionPolicy _collisionPolicy =
            WeaponCollisionPolicy.Normal;
        [Min(0f), SerializeField] private float _distance;
        [Min(0f), SerializeField] private float _speed;
        [SerializeField] private AnimationCurve _speedCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField] private bool _faceTarget;
        [Min(0f), SerializeField] private float _targetFrontOffset = 1.5f;

        public WeaponActionMotionKind Kind => _kind;
        public WeaponCollisionPolicy CollisionPolicy => _collisionPolicy;
        public float Distance => Mathf.Max(0f, _distance);
        public float Speed => Mathf.Max(0f, _speed);
        public AnimationCurve SpeedCurve => _speedCurve;
        public bool FaceTarget => _faceTarget;
        public float TargetFrontOffset => Mathf.Max(0f, _targetFrontOffset);

        public void Configure(
            WeaponActionMotionKind kind,
            WeaponCollisionPolicy collisionPolicy,
            float distance,
            float speed,
            AnimationCurve speedCurve,
            bool faceTarget,
            float targetFrontOffset)
        {
            _kind = kind;
            _collisionPolicy = collisionPolicy;
            _distance = Mathf.Max(0f, distance);
            _speed = Mathf.Max(0f, speed);
            _speedCurve = speedCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 1f);
            _faceTarget = faceTarget;
            _targetFrontOffset = Mathf.Max(0f, targetFrontOffset);
        }

        internal void Normalize()
        {
            _distance = Mathf.Max(0f, _distance);
            _speed = Mathf.Max(0f, _speed);
            _targetFrontOffset = Mathf.Max(0f, _targetFrontOffset);
            _speedCurve = _speedCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 1f);
        }
    }

    [Serializable]
    public sealed class WeaponStatusDefinition
    {
        [SerializeField] private WeaponStatusType _type;
        [Min(0f), SerializeField] private float _magnitude;
        [Min(0f), SerializeField] private float _duration;
        [SerializeField] private WeaponStatusStackPolicy _stackPolicy =
            WeaponStatusStackPolicy.Refresh;

        public WeaponStatusType Type => _type;
        public float Magnitude => Mathf.Max(0f, _magnitude);
        public float Duration => Mathf.Max(0f, _duration);
        public WeaponStatusStackPolicy StackPolicy => _stackPolicy;

        public void Configure(
            WeaponStatusType type,
            float magnitude,
            float duration,
            WeaponStatusStackPolicy stackPolicy)
        {
            _type = type;
            _magnitude = Mathf.Max(0f, magnitude);
            _duration = Mathf.Max(0f, duration);
            _stackPolicy = stackPolicy;
        }

        internal void Normalize()
        {
            _magnitude = Mathf.Max(0f, _magnitude);
            _duration = Mathf.Max(0f, _duration);
        }
    }

    [Serializable]
    public sealed class WeaponHitDefinition
    {
        [SerializeField] private WeaponActionPhaseKind _phase =
            WeaponActionPhaseKind.Active;
        [SerializeField] private WeaponHitShape _shape = WeaponHitShape.Sphere;
        [SerializeField] private Vector3 _localOffset = Vector3.forward;
        [SerializeField] private bool _centerOnTarget;
        [SerializeField] private bool _anchorAtActionStart;
        [SerializeField] private bool _onlyIntentTarget;
        [SerializeField] private Vector3 _size = Vector3.one;
        [Min(0f), SerializeField] private float _radius = 1f;
        [Range(0f, 360f), SerializeField] private float _arcDegrees = 120f;
        [Min(0f), SerializeField] private float _damageMultiplier = 1f;
        [Min(0f), SerializeField] private float _activeMainWeaponDamageMultiplier;
        [SerializeField] private bool _overrideLockedTargetDamage;
        [Min(0f), SerializeField] private float _lockedTargetDamageMultiplier;
        [Min(1), SerializeField] private int _repeatCount = 1;
        [Min(0f), SerializeField] private float _repeatInterval;
        [SerializeField] private bool _repeatUntilPhaseEnds;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private DamageFlags _damageFlags;
        [SerializeField] private WeaponStatusDefinition[] _statuses =
            Array.Empty<WeaponStatusDefinition>();

        public WeaponActionPhaseKind Phase => _phase;
        public WeaponHitShape Shape => _shape;
        public Vector3 LocalOffset => _localOffset;
        public bool CenterOnTarget => _centerOnTarget;
        public bool AnchorAtActionStart => _anchorAtActionStart;
        public bool OnlyIntentTarget => _onlyIntentTarget;
        public Vector3 Size => _size;
        public float Radius => Mathf.Max(0f, _radius);
        public float ArcDegrees => Mathf.Clamp(_arcDegrees, 0f, 360f);
        public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);
        public float ActiveMainWeaponDamageMultiplier =>
            Mathf.Max(0f, _activeMainWeaponDamageMultiplier);
        public bool OverrideLockedTargetDamage => _overrideLockedTargetDamage;
        public float LockedTargetDamageMultiplier =>
            Mathf.Max(0f, _lockedTargetDamageMultiplier);
        public int RepeatCount => Mathf.Max(1, _repeatCount);
        public float RepeatInterval => Mathf.Max(0f, _repeatInterval);
        public bool RepeatUntilPhaseEnds => _repeatUntilPhaseEnds;
        public DamageType DamageType => _damageType;
        public DamageFlags DamageFlags => _damageFlags;
        public WeaponStatusDefinition[] Statuses => _statuses;

        public void Configure(
            WeaponActionPhaseKind phase,
            WeaponHitShape shape,
            Vector3 localOffset,
            Vector3 size,
            float radius,
            float arcDegrees,
            float damageMultiplier,
            int repeatCount,
            float repeatInterval,
            DamageType damageType,
            DamageFlags damageFlags,
            WeaponStatusDefinition[] statuses,
            bool repeatUntilPhaseEnds = false,
            float activeMainWeaponDamageMultiplier = 0f,
            bool overrideLockedTargetDamage = false,
            float lockedTargetDamageMultiplier = 0f,
            bool centerOnTarget = false,
            bool anchorAtActionStart = false,
            bool onlyIntentTarget = false)
        {
            _phase = phase;
            _shape = shape;
            _localOffset = localOffset;
            _centerOnTarget = centerOnTarget;
            _anchorAtActionStart = anchorAtActionStart && !centerOnTarget;
            _onlyIntentTarget = onlyIntentTarget;
            _size = size;
            _radius = radius;
            _arcDegrees = arcDegrees;
            _damageMultiplier = damageMultiplier;
            _activeMainWeaponDamageMultiplier = activeMainWeaponDamageMultiplier;
            _overrideLockedTargetDamage = overrideLockedTargetDamage;
            _lockedTargetDamageMultiplier = lockedTargetDamageMultiplier;
            _repeatCount = repeatCount;
            _repeatInterval = repeatInterval;
            _repeatUntilPhaseEnds = repeatUntilPhaseEnds;
            _damageType = damageType;
            _damageFlags = damageFlags;
            _statuses = statuses ?? Array.Empty<WeaponStatusDefinition>();
            Normalize();
        }

        internal void Normalize()
        {
            _size = new Vector3(
                Mathf.Max(0f, _size.x),
                Mathf.Max(0f, _size.y),
                Mathf.Max(0f, _size.z));
            _radius = Mathf.Max(0f, _radius);
            _arcDegrees = Mathf.Clamp(_arcDegrees, 0f, 360f);
            _damageMultiplier = Mathf.Max(0f, _damageMultiplier);
            _activeMainWeaponDamageMultiplier =
                Mathf.Max(0f, _activeMainWeaponDamageMultiplier);
            _lockedTargetDamageMultiplier = Mathf.Max(0f, _lockedTargetDamageMultiplier);
            if (_centerOnTarget)
            {
                _anchorAtActionStart = false;
            }
            _repeatCount = Mathf.Max(1, _repeatCount);
            _repeatInterval = Mathf.Max(0f, _repeatInterval);
            _statuses = _statuses ?? Array.Empty<WeaponStatusDefinition>();
            for (int i = 0; i < _statuses.Length; i++)
            {
                _statuses[i]?.Normalize();
            }
        }
    }

    [Serializable]
    public sealed class WeaponActionDefinition
    {
        [SerializeField] private string _actionId = "weapon.action.new";
        [SerializeField] private string _displayName = "Action";
        [SerializeField] private WeaponActionInputDefinition _input =
            new WeaponActionInputDefinition();
        [SerializeField] private WeaponActionPhaseDefinition[] _phases =
            Array.Empty<WeaponActionPhaseDefinition>();
        [SerializeField] private WeaponActionMotionDefinition _motion =
            new WeaponActionMotionDefinition();
        [SerializeField] private WeaponHitDefinition[] _hits =
            Array.Empty<WeaponHitDefinition>();
        [SerializeField] private bool _persistentWhenUnequipped;
        [Min(0f), SerializeField] private float _maximumPersistentSeconds;
        [SerializeField] private WeaponActionButtonMask _cancelButtons;
        [Min(-1), SerializeField] private int _continueComboAtIndex = -1;
        [SerializeField] private string _requiredMainWeaponId;
        [SerializeField] private bool _comboOnly;

        public string ActionId => ContentIdUtility.Normalize(_actionId);
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? ActionId
            : _displayName.Trim();
        public WeaponActionInputDefinition Input => _input;
        public WeaponActionPhaseDefinition[] Phases => _phases;
        public WeaponActionMotionDefinition Motion => _motion;
        public WeaponHitDefinition[] Hits => _hits;
        public bool PersistentWhenUnequipped => _persistentWhenUnequipped;
        public float MaximumPersistentSeconds => Mathf.Max(0f, _maximumPersistentSeconds);
        public WeaponActionButtonMask CancelButtons => _cancelButtons;
        public int ContinueComboAtIndex => Mathf.Max(-1, _continueComboAtIndex);
        public string RequiredMainWeaponId =>
            ContentIdUtility.Normalize(_requiredMainWeaponId);
        public bool ComboOnly => _comboOnly;

        public void ConfigureIdentity(string actionId, string displayName)
        {
            _actionId = ContentIdUtility.Normalize(actionId);
            _displayName = string.IsNullOrWhiteSpace(displayName)
                ? _actionId
                : displayName.Trim();
        }

        public void Configure(
            string actionId,
            string displayName,
            WeaponActionInputDefinition input,
            WeaponActionPhaseDefinition[] phases,
            WeaponActionMotionDefinition motion,
            WeaponHitDefinition[] hits,
            bool persistentWhenUnequipped = false,
            float maximumPersistentSeconds = 0f,
            WeaponActionButtonMask cancelButtons = WeaponActionButtonMask.None,
            int continueComboAtIndex = -1,
            string requiredMainWeaponId = null,
            bool comboOnly = false)
        {
            _actionId = actionId;
            _displayName = displayName;
            _input = input;
            _phases = phases;
            _motion = motion;
            _hits = hits;
            _persistentWhenUnequipped = persistentWhenUnequipped;
            _maximumPersistentSeconds = maximumPersistentSeconds;
            _cancelButtons = cancelButtons;
            _continueComboAtIndex = continueComboAtIndex;
            _requiredMainWeaponId = requiredMainWeaponId;
            _comboOnly = comboOnly;
            Normalize();
        }

        internal void Normalize()
        {
            _actionId = ContentIdUtility.Normalize(_actionId);
            _displayName = string.IsNullOrWhiteSpace(_displayName)
                ? _actionId
                : _displayName.Trim();
            _input = _input ?? new WeaponActionInputDefinition();
            _input.Normalize();
            _phases = _phases ?? Array.Empty<WeaponActionPhaseDefinition>();
            for (int i = 0; i < _phases.Length; i++)
            {
                _phases[i]?.Normalize();
            }

            _motion = _motion ?? new WeaponActionMotionDefinition();
            _motion.Normalize();
            _hits = _hits ?? Array.Empty<WeaponHitDefinition>();
            for (int i = 0; i < _hits.Length; i++)
            {
                _hits[i]?.Normalize();
            }

            _maximumPersistentSeconds = Mathf.Max(0f, _maximumPersistentSeconds);
            _continueComboAtIndex = Mathf.Max(-1, _continueComboAtIndex);
            _requiredMainWeaponId = ContentIdUtility.Normalize(_requiredMainWeaponId);
        }

        internal bool TryValidate(out string error)
        {
            if (!ContentIdUtility.IsValid(ActionId))
            {
                error = "Weapon action has an invalid stable ID.";
                return false;
            }

            if (_input == null || _input.RequiredButtons == WeaponActionButtonMask.None)
            {
                error = "Weapon action '" + ActionId + "' has no input buttons.";
                return false;
            }

            if (_phases == null || _phases.Length == 0)
            {
                error = "Weapon action '" + ActionId + "' has no phases.";
                return false;
            }

            for (int i = 0; i < _phases.Length; i++)
            {
                if (_phases[i] == null)
                {
                    error = "Weapon action '" + ActionId + "' has a null phase.";
                    return false;
                }
            }

            for (int i = 0; i < _hits.Length; i++)
            {
                WeaponHitDefinition hit = _hits[i];
                if (hit == null)
                {
                    error = "Weapon action '" + ActionId + "' has a null hit definition.";
                    return false;
                }

                bool phaseExists = false;
                for (int phaseIndex = 0; phaseIndex < _phases.Length; phaseIndex++)
                {
                    if (_phases[phaseIndex] != null &&
                        _phases[phaseIndex].Kind == hit.Phase)
                    {
                        phaseExists = true;
                        break;
                    }
                }
                if (!phaseExists)
                {
                    error = "Weapon action '" + ActionId +
                            "' has a hit bound to a missing phase.";
                    return false;
                }

                if (hit.RepeatUntilPhaseEnds && hit.RepeatInterval <= 0f)
                {
                    error = "Weapon action '" + ActionId +
                            "' repeats until phase end without a positive interval.";
                    return false;
                }

                if (hit.ActiveMainWeaponDamageMultiplier > 0f &&
                    string.IsNullOrEmpty(RequiredMainWeaponId))
                {
                    error = "Weapon action '" + ActionId +
                            "' uses main-weapon damage without a required main weapon.";
                    return false;
                }
            }

            bool hasZeroDurationSustainedPhase = false;
            for (int i = 0; i < _phases.Length; i++)
            {
                if (_phases[i] != null &&
                    _phases[i].Kind == WeaponActionPhaseKind.Sustained &&
                    _phases[i].Duration <= 0f)
                {
                    hasZeroDurationSustainedPhase = true;
                    break;
                }
            }

            if ((_persistentWhenUnequipped || hasZeroDurationSustainedPhase) &&
                MaximumPersistentSeconds <= 0f &&
                (_input == null || !_input.RequiresTargetLock) &&
                _cancelButtons == WeaponActionButtonMask.None)
            {
                error = "Persistent weapon action '" + ActionId +
                        "' requires a maximum duration, a live target, or explicit cancel input.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class WeaponComboDefinition
    {
        [SerializeField] private string[] _actionIds = Array.Empty<string>();
        [Min(0f), SerializeField] private float _resetDelay = 1f;
        [Min(0f), SerializeField] private float _restartDelay;
        [SerializeField] private bool _suspendGravity;

        public string[] ActionIds => _actionIds;
        public float ResetDelay => Mathf.Max(0f, _resetDelay);
        public float RestartDelay => Mathf.Max(0f, _restartDelay);
        public bool SuspendGravity => _suspendGravity;

        public void Configure(
            string[] actionIds,
            float resetDelay,
            float restartDelay,
            bool suspendGravity)
        {
            _actionIds = actionIds ?? Array.Empty<string>();
            _resetDelay = resetDelay;
            _restartDelay = restartDelay;
            _suspendGravity = suspendGravity;
            Normalize();
        }

        internal void Normalize()
        {
            _actionIds = _actionIds ?? Array.Empty<string>();
            for (int i = 0; i < _actionIds.Length; i++)
            {
                _actionIds[i] = ContentIdUtility.Normalize(_actionIds[i]);
            }

            _resetDelay = Mathf.Max(0f, _resetDelay);
            _restartDelay = Mathf.Max(0f, _restartDelay);
        }
    }

    [CreateAssetMenu(
        fileName = "SO_WeaponActionSet_New",
        menuName = "OriginCore/Content/Weapon Action Set")]
    public sealed class WeaponActionSetDefinition : ContentDefinition
    {
        [SerializeField] private WeaponComboDefinition _groundCombo =
            new WeaponComboDefinition();
        [SerializeField] private WeaponComboDefinition _airCombo =
            new WeaponComboDefinition();
        [SerializeField] private WeaponActionDefinition[] _actions =
            Array.Empty<WeaponActionDefinition>();

        public WeaponComboDefinition GroundCombo => _groundCombo;
        public WeaponComboDefinition AirCombo => _airCombo;
        public WeaponActionDefinition[] Actions => _actions;

        public WeaponActionDefinition FindAction(string actionId)
        {
            string normalized = ContentIdUtility.Normalize(actionId);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            for (int i = 0; i < _actions.Length; i++)
            {
                WeaponActionDefinition action = _actions[i];
                if (action != null &&
                    string.Equals(action.ActionId, normalized, StringComparison.Ordinal))
                {
                    return action;
                }
            }

            return null;
        }

        public void Configure(
            string contentId,
            string displayName,
            WeaponComboDefinition groundCombo,
            WeaponComboDefinition airCombo,
            WeaponActionDefinition[] actions)
        {
            ConfigureIdentity(contentId, displayName);
            _groundCombo = groundCombo ?? new WeaponComboDefinition();
            _airCombo = airCombo ?? new WeaponComboDefinition();
            _actions = actions ?? Array.Empty<WeaponActionDefinition>();
            OnValidate();
        }

        public bool TryValidate(out string error)
        {
            if (!ContentIdUtility.IsValid(ContentId))
            {
                error = "Weapon action set has an invalid stable ID.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _actions.Length; i++)
            {
                WeaponActionDefinition action = _actions[i];
                if (action == null)
                {
                    error = "Weapon action set '" + ContentId + "' has a null action.";
                    return false;
                }

                if (!action.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(action.ActionId))
                {
                    error = "Weapon action set '" + ContentId +
                            "' contains duplicate action ID '" + action.ActionId + "'.";
                    return false;
                }
            }

            if (!TryValidateCombo(_groundCombo, ids, "ground", out error) ||
                !TryValidateCombo(_airCombo, ids, "air", out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _groundCombo = _groundCombo ?? new WeaponComboDefinition();
            _airCombo = _airCombo ?? new WeaponComboDefinition();
            _groundCombo.Normalize();
            _airCombo.Normalize();
            _actions = _actions ?? Array.Empty<WeaponActionDefinition>();
            for (int i = 0; i < _actions.Length; i++)
            {
                _actions[i]?.Normalize();
            }
        }

        private bool TryValidateCombo(
            WeaponComboDefinition combo,
            HashSet<string> actionIds,
            string label,
            out string error)
        {
            if (combo == null)
            {
                error = "Weapon action set '" + ContentId + "' has no " + label +
                        " combo definition.";
                return false;
            }

            string[] comboIds = combo.ActionIds;
            for (int i = 0; i < comboIds.Length; i++)
            {
                if (string.IsNullOrEmpty(comboIds[i]) || !actionIds.Contains(comboIds[i]))
                {
                    error = "Weapon action set '" + ContentId + "' references missing " +
                            label + " combo action '" + comboIds[i] + "'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
