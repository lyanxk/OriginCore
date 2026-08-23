using System;
using System.Collections.Generic;
using OriginCore.Abilities;
using OriginCore.Buildings;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Debugging;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Progression;
using OriginCore.UI;
using OriginCore.UI.RTS;
using OriginCore.Units;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.RTS.Commands
{
    public enum RtsCommandFeedbackKind
    {
        None = 0,
        Issued = 1,
        Stopped = 2,
        NoSelection = 3,
        NoMovableUnits = 4,
        InvalidGround = 5,
        QueueFull = 6,
        PathFailed = 7,
        InvalidTarget = 8,
        NoCombatUnits = 9,
        CommandFailed = 10,
        NoProducer = 11,
        NoAbility = 12
    }

    public readonly struct RtsCommandFeedback
    {
        public RtsCommandFeedback(RtsCommandFeedbackKind kind, string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public RtsCommandFeedbackKind Kind { get; }
        public string Message { get; }
        public bool IsError => Kind == RtsCommandFeedbackKind.NoSelection ||
                               Kind == RtsCommandFeedbackKind.NoMovableUnits ||
                               Kind == RtsCommandFeedbackKind.InvalidGround ||
                               Kind == RtsCommandFeedbackKind.QueueFull ||
                               Kind == RtsCommandFeedbackKind.PathFailed ||
                               Kind == RtsCommandFeedbackKind.InvalidTarget ||
                               Kind == RtsCommandFeedbackKind.NoCombatUnits ||
                               Kind == RtsCommandFeedbackKind.CommandFailed ||
                               Kind == RtsCommandFeedbackKind.NoProducer ||
                               Kind == RtsCommandFeedbackKind.NoAbility;
    }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class RtsCommandIssuer : MonoBehaviour
    {
        private const int TargetRayBufferSize = 64;

        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private LayerMask _groundMask = ~0;
        [Min(0.1f), SerializeField] private float _maximumRayDistance = 500f;
        [Min(0.05f), SerializeField] private float _navMeshSampleDistance = 1.5f;
        [SerializeField] private BuildingPlacementPreview _buildingPlacementPreview;
        [SerializeField] private AbilityRangePreview _abilityRangePreview;

        private readonly HashSet<UnitCommandQueue> _trackedQueues =
            new HashSet<UnitCommandQueue>();
        private readonly RaycastHit[] _targetRayHits =
            new RaycastHit[TargetRayBufferSize];
        private readonly List<PromotionController> _promotionBuffer =
            new List<PromotionController>(16);
        private InputRouter _inputRouter;
        private GameModeController _modeController;
        private EntityRegistry _entityRegistry;
        private PossessionService _possessionService;
        private IDebugOverlayService _debugOverlay;
        private bool _issuingCommand;
        private AbilityDefinition _targetingAbility;
        private BuildingDefinition _targetingBuilding;
        private ProductionQueue _targetingProductionQueue;

        public event Action<CommandTargetingChange> TargetingChanged;
        public event Action<RtsCommandFeedback> FeedbackChanged;
        public event Action CommandStateChanged;

        public SelectionService SelectionService => _selectionService;
        public Camera WorldCamera => _worldCamera;
        public LayerMask GroundMask => _groundMask;
        public float MaximumRayDistance => _maximumRayDistance;
        public float NavMeshSampleDistance => _navMeshSampleDistance;
        public CommandTargetingState TargetingState { get; private set; }
        public string TargetingPrompt { get; private set; } = string.Empty;
        public AbilityDefinition TargetingAbility => _targetingAbility;
        public BuildingDefinition TargetingBuilding => _targetingBuilding;
        public ProductionQueue TargetingProductionQueue => _targetingProductionQueue;
        public RtsCommandFeedback LastFeedback { get; private set; }
        public int LastIssuedCount { get; private set; }
        public int LastRejectedCount { get; private set; }
        public int TrackedQueueCount => _trackedQueues.Count;
        public BuildingPlacementPreview BuildingPlacementPreview =>
            _buildingPlacementPreview;
        public AbilityRangePreview AbilityRangePreview => _abilityRangePreview;
        public bool IsServiceBound => _inputRouter != null && _modeController != null &&
                                      _entityRegistry != null && _possessionService != null;

        private void OnEnable()
        {
            EnsureBuildingPlacementPreview();
            EnsureAbilityRangePreview();
            TryBindServices();
        }

        private void Start()
        {
            if (!IsServiceBound)
            {
                TryBindServices();
            }
        }

        private void OnDisable()
        {
            UnbindServices();
            SetTargetingState(CommandTargetingState.None);
        }

        private void OnValidate()
        {
            _maximumRayDistance = Mathf.Max(0.1f, _maximumRayDistance);
            _navMeshSampleDistance = Mathf.Max(0.05f, _navMeshSampleDistance);
        }

        public void Configure(
            SelectionService selectionService,
            Camera worldCamera,
            LayerMask groundMask,
            float maximumRayDistance = 500f,
            float navMeshSampleDistance = 1.5f)
        {
            if (_selectionService != null && _selectionService != selectionService)
            {
                _selectionService.SetPointerSelectionSuppressed(false);
            }

            _selectionService = selectionService;
            _worldCamera = worldCamera;
            _groundMask = groundMask;
            _maximumRayDistance = Mathf.Max(0.1f, maximumRayDistance);
            _navMeshSampleDistance = Mathf.Max(0.05f, navMeshSampleDistance);
            _selectionService?.SetPointerSelectionSuppressed(
                TargetingState != CommandTargetingState.None);
        }

        public bool TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            GameServices services = appRoot.Services;
            if (services.InputRouter == null || services.GameModeController == null ||
                services.EntityRegistry == null || services.PossessionService == null)
            {
                return false;
            }

            if (_inputRouter == services.InputRouter &&
                _modeController == services.GameModeController &&
                _entityRegistry == services.EntityRegistry &&
                _possessionService == services.PossessionService)
            {
                return true;
            }

            UnbindServices();
            _inputRouter = services.InputRouter;
            _modeController = services.GameModeController;
            _entityRegistry = services.EntityRegistry;
            _possessionService = services.PossessionService;
            _debugOverlay = services.DebugOverlay;

            _inputRouter.SnapshotReady += HandleSnapshotReady;
            _modeController.ModeChanged += HandleModeChanged;
            _entityRegistry.EntityRegistered += HandleEntityRegistered;
            _entityRegistry.EntityUnregistered += HandleEntityUnregistered;
            _possessionService.AutopilotCancellationRequested +=
                HandleAutopilotCancellationRequested;

            IReadOnlyList<EntityIdentity> entities = _entityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                TrackQueue(entities[i]);
            }

            PublishCommandState();
            return true;
        }

        public bool BeginMoveTargeting()
        {
            if (_modeController != null && _modeController.CurrentMode != GameMode.RTS)
            {
                return false;
            }

            return SetTargetingState(CommandTargetingState.Move);
        }

        public bool BeginAttackTargeting()
        {
            if (_modeController != null && _modeController.CurrentMode != GameMode.RTS)
            {
                return false;
            }

            return SetTargetingState(CommandTargetingState.Attack);
        }

        public bool BeginSetRallyTargeting()
        {
            if (_modeController != null && _modeController.CurrentMode != GameMode.RTS)
            {
                return false;
            }

            if (!HasSelectedRallyController())
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoProducer,
                    "Select a friendly producer before setting a rally point.");
                return false;
            }

            return SetTargetingState(CommandTargetingState.SetRally);
        }

        public bool BeginTargeting(CommandTargetingState state)
        {
            return SetTargetingState(state);
        }

        public bool ActivateSelectedAbilitySlot(int slotIndex)
        {
            AbilityDefinition definition = FindSelectedAbility(slotIndex);
            if (definition == null)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoAbility,
                    "The selected units have no ability in that slot.");
                return false;
            }

            return ActivateAbility(
                definition,
                _inputRouter != null &&
                _inputRouter.CurrentSnapshot.RTS.QueueModifier.IsPressed);
        }

        public bool ActivateAbility(AbilityDefinition definition, bool append = false)
        {
            if (definition == null || (definition.Modes & AbilityModeMask.RTS) == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoAbility, "RTS ability is unavailable.");
                return false;
            }

            if (definition.TargetType == AbilityTargetType.None ||
                definition.TargetType == AbilityTargetType.Self)
            {
                return IssueAbility(definition, AbilityTarget.None, append);
            }

            _targetingAbility = definition;
            return SetTargetingState(CommandTargetingState.Ability);
        }

        public bool ActivateBuilding(BuildingDefinition definition)
        {
            if (definition == null || FindSelectedBuilder(definition) == null)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.CommandFailed,
                    "No selected worker can construct that building.");
                return false;
            }

            _targetingBuilding = definition;
            return SetTargetingState(CommandTargetingState.Build);
        }

        public bool BeginProductionDeployment(ProductionQueue productionQueue)
        {
            if (productionQueue == null || !productionQueue.AwaitingWorldDeployment)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoProducer,
                    "No completed production batch is awaiting deployment.");
                return false;
            }

            _targetingProductionQueue = productionQueue;
            return SetTargetingState(CommandTargetingState.DeployProduction);
        }

        public bool CancelTargeting()
        {
            return SetTargetingState(CommandTargetingState.None);
        }

        public bool IssueMove(Vector3 destination, bool append)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select at least one unit first.");
                return false;
            }

            int eligibleCount = 0;
            int scheduledCount = 0;
            int queueFullCount = 0;
            int pathFailureCount = 0;
            string pathFailureReason = string.Empty;
            _issuingCommand = true;
            try
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    Selectable selectable = selected[i];
                    if (selectable == null ||
                        !selectable.TryGetComponent(out UnitCommandQueue queue) ||
                        queue.MovementDriver == null)
                    {
                        continue;
                    }

                    eligibleCount++;
                    MoveCommand command = new MoveCommand(destination);
                    if (!queue.TryIssue(command, append, out CommandRejectionReason rejection))
                    {
                        LastRejectedCount++;
                        if (rejection == CommandRejectionReason.QueueFull)
                        {
                            queueFullCount++;
                        }

                        continue;
                    }

                    if (command.Status == CommandStatus.Failed)
                    {
                        pathFailureCount++;
                        LastRejectedCount++;
                        if (string.IsNullOrEmpty(pathFailureReason))
                        {
                            pathFailureReason = command.FailureReason;
                        }

                        continue;
                    }

                    scheduledCount++;
                }
            }
            finally
            {
                _issuingCommand = false;
            }

            LastIssuedCount = scheduledCount;
            if (eligibleCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoMovableUnits,
                    "The current selection has no movable units.");
                return false;
            }

            if (pathFailureCount > 0)
            {
                string message = string.IsNullOrWhiteSpace(pathFailureReason)
                    ? "One or more units could not find a complete path."
                    : pathFailureReason;
                SetFeedback(RtsCommandFeedbackKind.PathFailed, message);
            }
            else if (queueFullCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.QueueFull,
                    "Command queue full: at most five commands may wait per unit.");
            }
            else if (scheduledCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    append
                        ? "Move appended for " + scheduledCount + " unit(s)."
                        : "Move issued to " + scheduledCount + " unit(s).");
            }

            PublishCommandState();
            return scheduledCount > 0;
        }

        public bool IssueAttack(EntityIdentity target, bool append)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select at least one unit first.");
                return false;
            }

            int combatUnitCount = 0;
            int eligibleCount = 0;
            int scheduledCount = 0;
            int queueFullCount = 0;
            string failureReason = string.Empty;
            _issuingCommand = true;
            try
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    Selectable selectable = selected[i];
                    if (selectable == null ||
                        !selectable.TryGetComponent(out UnitCommandQueue queue) ||
                        queue.AttackCapability == null)
                    {
                        continue;
                    }

                    combatUnitCount++;
                    if (!queue.AttackCapability.IsValidHostileTarget(target))
                    {
                        continue;
                    }

                    eligibleCount++;
                    AttackCommand command = new AttackCommand(target);
                    if (!queue.TryIssue(command, append, out CommandRejectionReason rejection))
                    {
                        LastRejectedCount++;
                        if (rejection == CommandRejectionReason.QueueFull)
                        {
                            queueFullCount++;
                        }

                        continue;
                    }

                    if (command.Status == CommandStatus.Failed)
                    {
                        LastRejectedCount++;
                        if (string.IsNullOrWhiteSpace(failureReason))
                        {
                            failureReason = command.FailureReason;
                        }

                        continue;
                    }

                    scheduledCount++;
                }
            }
            finally
            {
                _issuingCommand = false;
            }

            LastIssuedCount = scheduledCount;
            if (combatUnitCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoCombatUnits,
                    "The current selection has no units able to attack.");
            }
            else if (eligibleCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.InvalidTarget,
                    "Choose a living hostile target; allied and neutral entities cannot be attacked.");
            }
            else if (!string.IsNullOrWhiteSpace(failureReason))
            {
                SetFeedback(RtsCommandFeedbackKind.CommandFailed, failureReason);
            }
            else if (queueFullCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.QueueFull,
                    "Command queue full: at most five commands may wait per unit.");
            }
            else if (scheduledCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    append
                        ? "Attack appended for " + scheduledCount + " unit(s)."
                        : "Attack issued to " + scheduledCount + " unit(s).");
            }

            PublishCommandState();
            return scheduledCount > 0;
        }

        public bool IssueGather(ResourceNode target, bool append)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select at least one worker first.");
                return false;
            }

            if (target == null || target.IsDepleted)
            {
                SetFeedback(RtsCommandFeedbackKind.InvalidTarget, "Choose a non-depleted resource node.");
                return false;
            }

            int eligibleCount = 0;
            int scheduledCount = 0;
            int queueFullCount = 0;
            string failureReason = string.Empty;
            _issuingCommand = true;
            try
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    Selectable selectable = selected[i];
                    if (selectable == null ||
                        !selectable.TryGetComponent(out Gatherer gatherer) ||
                        gatherer == null ||
                        !selectable.TryGetComponent(out UnitCommandQueue queue) ||
                        queue.MovementDriver == null)
                    {
                        continue;
                    }

                    eligibleCount++;
                    GatherCommand command = new GatherCommand(target);
                    if (!queue.TryIssue(command, append, out CommandRejectionReason rejection))
                    {
                        LastRejectedCount++;
                        if (rejection == CommandRejectionReason.QueueFull)
                        {
                            queueFullCount++;
                        }
                        continue;
                    }

                    if (command.Status == CommandStatus.Failed)
                    {
                        LastRejectedCount++;
                        if (string.IsNullOrWhiteSpace(failureReason))
                        {
                            failureReason = command.FailureReason;
                        }
                        continue;
                    }

                    scheduledCount++;
                }
            }
            finally
            {
                _issuingCommand = false;
            }

            LastIssuedCount = scheduledCount;
            if (eligibleCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoMovableUnits, "The current selection has no workers able to gather.");
            }
            else if (!string.IsNullOrWhiteSpace(failureReason))
            {
                SetFeedback(RtsCommandFeedbackKind.CommandFailed, failureReason);
            }
            else if (queueFullCount > 0)
            {
                SetFeedback(RtsCommandFeedbackKind.QueueFull, "Command queue full: at most five commands may wait per unit.");
            }
            else if (scheduledCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    append
                        ? "Gather appended for " + scheduledCount + " worker(s)."
                        : "Gather issued to " + scheduledCount + " worker(s).");
            }

            PublishCommandState();
            return scheduledCount > 0;
        }

        public bool IssueAbility(
            AbilityDefinition definition,
            AbilityTarget target,
            bool append)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0 ||
                definition == null)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select an ability user first.");
                return false;
            }

            int eligibleCount = 0;
            int issuedCount = 0;
            string failureReason = string.Empty;
            _issuingCommand = true;
            try
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    Selectable selectable = selected[i];
                    if (selectable == null ||
                        !selectable.TryGetComponent(out UnitCommandQueue queue) ||
                        !selectable.TryGetComponent(out AbilityLoadout loadout) ||
                        !OwnsRtsAbility(loadout, definition))
                    {
                        continue;
                    }

                    eligibleCount++;
                    AbilityTarget perUnitTarget = target;
                    if (definition.TargetType == AbilityTargetType.Direction && target.HasPoint)
                    {
                        Vector3 direction = target.Point - queue.Identity.transform.position;
                        perUnitTarget = AbilityTarget.ForDirection(direction);
                    }

                    AbilityChannelController channel =
                        selectable.GetComponent<AbilityChannelController>();
                    IRtsCommand command = channel != null && channel.Supports(definition)
                        ? (IRtsCommand)new AutoRepeatAbilityCommand(definition, perUnitTarget)
                        : new AbilityCommand(definition, perUnitTarget);
                    if (!queue.TryIssue(
                            command,
                            append,
                            out CommandRejectionReason rejection) ||
                        command.Status == CommandStatus.Failed)
                    {
                        LastRejectedCount++;
                        if (string.IsNullOrEmpty(failureReason))
                        {
                            failureReason = command.Status == CommandStatus.Failed
                                ? command.FailureReason
                                : rejection == CommandRejectionReason.QueueFull
                                    ? "Command queue is full."
                                    : "Ability command was rejected.";
                        }

                        continue;
                    }

                    issuedCount++;
                }
            }
            finally
            {
                _issuingCommand = false;
            }

            LastIssuedCount = issuedCount;
            if (eligibleCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoAbility,
                    "No selected unit owns '" + definition.DisplayName + "'.");
            }
            else if (issuedCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.CommandFailed,
                    string.IsNullOrEmpty(failureReason)
                        ? "Ability command could not be issued."
                        : failureReason);
            }
            else
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    definition.DisplayName + " issued to " + issuedCount + " unit(s).");
            }

            PublishCommandState();
            return issuedCount > 0;
        }

        public bool IssueBuild(BuildingDefinition definition, Vector3 position, bool append)
        {
            Builder builder = FindSelectedBuilder(definition);
            UnitCommandQueue queue = builder != null
                ? builder.GetComponent<UnitCommandQueue>()
                : null;
            if (builder == null || queue == null)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.CommandFailed,
                    "No selected worker has a construction command queue.");
                return false;
            }

            BuildCommand command = new BuildCommand(definition, position);
            bool issued = queue.TryIssue(
                command,
                append,
                out CommandRejectionReason rejection);
            LastIssuedCount = issued ? 1 : 0;
            LastRejectedCount = issued ? 0 : 1;
            if (!issued || command.Status == CommandStatus.Failed)
            {
                SetFeedback(
                    rejection == CommandRejectionReason.QueueFull
                        ? RtsCommandFeedbackKind.QueueFull
                        : RtsCommandFeedbackKind.CommandFailed,
                    command.Status == CommandStatus.Failed
                        ? command.FailureReason
                        : "Construction command was rejected.");
                return false;
            }

            SetFeedback(
                RtsCommandFeedbackKind.Issued,
                definition.DisplayName + (append ? " construction appended." : " construction issued."));
            PublishCommandState();
            return true;
        }

        public bool PromoteSelected(bool append = false)
        {
            _promotionBuffer.Clear();
            if (_selectionService != null)
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    PromotionController promotion = selected[i] != null
                        ? selected[i].GetComponent<PromotionController>()
                        : null;
                    if (promotion != null)
                    {
                        _promotionBuffer.Add(promotion);
                    }
                }
            }

            int issuedCount = 0;
            string failureReason = string.Empty;
            for (int i = 0; i < _promotionBuffer.Count; i++)
            {
                PromotionController promotion = _promotionBuffer[i];
                UnitCommandQueue queue = promotion != null
                    ? promotion.GetComponent<UnitCommandQueue>()
                    : null;
                if (queue == null)
                {
                    continue;
                }

                PromotionCommand command = new PromotionCommand();
                if (queue.TryIssue(command, append, out _) &&
                    command.Status != CommandStatus.Failed)
                {
                    issuedCount++;
                }
                else if (string.IsNullOrEmpty(failureReason))
                {
                    failureReason = command.FailureReason;
                }
            }

            LastIssuedCount = issuedCount;
            LastRejectedCount = Mathf.Max(0, _promotionBuffer.Count - issuedCount);
            SetFeedback(
                issuedCount > 0
                    ? RtsCommandFeedbackKind.Issued
                    : RtsCommandFeedbackKind.CommandFailed,
                issuedCount > 0
                    ? "Promotion issued to " + issuedCount + " unit(s)."
                    : string.IsNullOrEmpty(failureReason)
                        ? "No selected unit can be promoted."
                        : failureReason);
            PublishCommandState();
            return issuedCount > 0;
        }

        public bool IssueAttackMove(Vector3 destination, bool append)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select at least one unit first.");
                return false;
            }

            int eligibleCount = 0;
            int scheduledCount = 0;
            int queueFullCount = 0;
            string failureReason = string.Empty;
            _issuingCommand = true;
            try
            {
                IReadOnlyList<Selectable> selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    Selectable selectable = selected[i];
                    if (selectable == null ||
                        !selectable.TryGetComponent(out UnitCommandQueue queue) ||
                        queue.MovementDriver == null || queue.AttackCapability == null ||
                        queue.TargetScanner == null)
                    {
                        continue;
                    }

                    eligibleCount++;
                    AttackMoveCommand command = new AttackMoveCommand(destination);
                    if (!queue.TryIssue(command, append, out CommandRejectionReason rejection))
                    {
                        LastRejectedCount++;
                        if (rejection == CommandRejectionReason.QueueFull)
                        {
                            queueFullCount++;
                        }

                        continue;
                    }

                    if (command.Status == CommandStatus.Failed)
                    {
                        LastRejectedCount++;
                        if (string.IsNullOrWhiteSpace(failureReason))
                        {
                            failureReason = command.FailureReason;
                        }

                        continue;
                    }

                    scheduledCount++;
                }
            }
            finally
            {
                _issuingCommand = false;
            }

            LastIssuedCount = scheduledCount;
            if (eligibleCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoCombatUnits,
                    "The current selection has no movable combat units.");
            }
            else if (!string.IsNullOrWhiteSpace(failureReason))
            {
                SetFeedback(RtsCommandFeedbackKind.PathFailed, failureReason);
            }
            else if (queueFullCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.QueueFull,
                    "Command queue full: at most five commands may wait per unit.");
            }
            else if (scheduledCount > 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    append
                        ? "Attack Move appended for " + scheduledCount + " unit(s)."
                        : "Attack Move issued to " + scheduledCount + " unit(s).");
            }

            PublishCommandState();
            return scheduledCount > 0;
        }

        public bool IssueContextGroundCommand(Vector3 destination, bool append)
        {
            // A selected producer owns the building-context RMB meaning. Explicit
            // Move/Attack/Set Rally targeting continues to use left-button release.
            if (HasSelectedRallyController())
            {
                return IssueSetRally(destination);
            }

            return IssueMove(destination, append);
        }

        public int StopSelected()
        {
            CancelTargeting();
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select at least one unit first.");
                return 0;
            }

            int handledUnits = 0;
            int cancelledCommands = 0;
            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                Selectable selectable = selected[i];
                if (selectable == null ||
                    !selectable.TryGetComponent(out UnitCommandQueue queue))
                {
                    continue;
                }

                handledUnits++;
                cancelledCommands += queue.StopAll();
            }

            if (handledUnits == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoMovableUnits,
                    "The current selection has no command queue to stop.");
                return 0;
            }

            SetFeedback(
                RtsCommandFeedbackKind.Stopped,
                "Stopped " + handledUnits + " unit(s); cleared " +
                cancelledCommands + " command(s).");
            PublishCommandState();
            return cancelledCommands;
        }

        public bool IssueSetRally(Vector3 destination)
        {
            LastIssuedCount = 0;
            LastRejectedCount = 0;
            if (_selectionService == null || _selectionService.SelectedCount == 0)
            {
                SetFeedback(RtsCommandFeedbackKind.NoSelection, "Select a producer first.");
                return false;
            }

            int eligibleCount = 0;
            int updatedCount = 0;
            string failureReason = string.Empty;
            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                Selectable selectable = selected[i];
                if (selectable == null ||
                    !selectable.TryGetComponent(out RallyPointController rally))
                {
                    continue;
                }

                eligibleCount++;
                if (rally.TrySetRallyPoint(destination, out string error))
                {
                    updatedCount++;
                }
                else
                {
                    LastRejectedCount++;
                    if (string.IsNullOrWhiteSpace(failureReason))
                    {
                        failureReason = error;
                    }
                }
            }

            LastIssuedCount = updatedCount;
            if (eligibleCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.NoProducer,
                    "The current selection contains no producer with a rally point.");
            }
            else if (updatedCount == 0)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.InvalidGround,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "Choose reachable NavMesh ground for the rally point."
                        : failureReason);
            }
            else
            {
                SetFeedback(
                    RtsCommandFeedbackKind.Issued,
                    "Rally point updated for " + updatedCount + " producer(s).");
            }

            PublishCommandState();
            return updatedCount > 0;
        }

        public bool TryResolveGroundPoint(Vector2 screenPoint, out Vector3 destination)
        {
            destination = default(Vector3);
            Camera worldCamera = _worldCamera != null
                ? _worldCamera
                : _selectionService != null ? _selectionService.WorldCamera : null;
            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    _maximumRayDistance,
                    _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (!NavMeshPositionResolver.TryResolvePosition(
                    hit.point,
                    NavMesh.AllAreas,
                    _navMeshSampleDistance,
                    Mathf.Max(6f, _navMeshSampleDistance),
                    out Vector3 resolvedPosition))
            {
                return false;
            }

            destination = resolvedPosition;
            return true;
        }

        public bool TryResolveEntityTarget(Vector2 screenPoint, out EntityIdentity target)
        {
            target = null;
            Camera worldCamera = _worldCamera != null
                ? _worldCamera
                : _selectionService != null ? _selectionService.WorldCamera : null;
            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPoint);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _targetRayHits,
                _maximumRayDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _targetRayHits[i];
                if (hit.collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                EntityIdentity candidate = hit.collider.GetComponentInParent<EntityIdentity>();
                if (candidate == null)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                target = candidate;
            }

            return target != null;
        }

        public bool TryResolveWorldPoint(Vector2 screenPoint, out Vector3 point)
        {
            point = default(Vector3);
            Camera worldCamera = _worldCamera != null
                ? _worldCamera
                : _selectionService != null ? _selectionService.WorldCamera : null;
            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    _maximumRayDistance,
                    _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            point = hit.point;
            return true;
        }

        public int GetSelectedCommandCount()
        {
            return CountSelectedQueues(false);
        }

        public int GetSelectedWaitingCount()
        {
            return CountSelectedQueues(true);
        }

        public int GetTotalCommandCount()
        {
            int count = 0;
            foreach (UnitCommandQueue queue in _trackedQueues)
            {
                if (queue != null)
                {
                    count += queue.TotalCommandCount;
                }
            }

            return count;
        }

        private void UnbindServices()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshotReady;
            }

            if (_modeController != null)
            {
                _modeController.ModeChanged -= HandleModeChanged;
            }

            if (_entityRegistry != null)
            {
                _entityRegistry.EntityRegistered -= HandleEntityRegistered;
                _entityRegistry.EntityUnregistered -= HandleEntityUnregistered;
            }

            if (_possessionService != null)
            {
                _possessionService.AutopilotCancellationRequested -=
                    HandleAutopilotCancellationRequested;
            }

            foreach (UnitCommandQueue queue in _trackedQueues)
            {
                UnsubscribeQueue(queue);
            }

            _trackedQueues.Clear();
            _inputRouter = null;
            _modeController = null;
            _entityRegistry = null;
            _possessionService = null;
            _debugOverlay = null;
        }

        private void HandleSnapshotReady(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || snapshot.ActiveMap != GameplayInputMap.RTS ||
                _modeController == null || _modeController.CurrentMode != GameMode.RTS)
            {
                _buildingPlacementPreview?.Hide();
                _abilityRangePreview?.Hide();
                return;
            }

            RtsInputSnapshot input = snapshot.RTS;
            if (TargetingState == CommandTargetingState.Build)
            {
                UpdateBuildingPlacementPreview(input.Point);
            }
            else if (TargetingState == CommandTargetingState.Ability)
            {
                UpdateAbilityRangePreview(input.Point);
            }
            if (input.Stop.WasPressedThisFrame)
            {
                StopSelected();
                return;
            }

            if (TargetingState != CommandTargetingState.None)
            {
                if (!input.Select.WasReleasedThisFrame)
                {
                    return;
                }

                if (UiPointerUtility.IsScreenPointOverUi(input.Point))
                {
                    return;
                }

                ConfirmTargeting(input);
                return;
            }

            if (input.Attack.WasPressedThisFrame)
            {
                BeginAttackTargeting();
                return;
            }

            if (input.Promote.WasPressedThisFrame)
            {
                PromoteSelected(input.QueueModifier.IsPressed);
                return;
            }

            if (input.Ability1.WasPressedThisFrame ||
                input.Ability2.WasPressedThisFrame ||
                input.Ability3.WasPressedThisFrame ||
                input.Ability4.WasPressedThisFrame)
            {
                int slot = input.Ability1.WasPressedThisFrame ? 0 :
                    input.Ability2.WasPressedThisFrame ? 1 :
                    input.Ability3.WasPressedThisFrame ? 2 : 3;
                ActivateSelectedAbilitySlot(slot);
                return;
            }

            if (!input.Command.WasPressedThisFrame)
            {
                return;
            }

            if (UiPointerUtility.IsScreenPointOverUi(input.Point))
            {
                return;
            }

            if (TryResolveEntityTarget(input.Point, out EntityIdentity entityTarget) &&
                entityTarget.TryGetComponent(out ResourceNode resourceNode))
            {
                IssueGather(resourceNode, input.QueueModifier.IsPressed);
                return;
            }

            if (!TryResolveGroundPoint(input.Point, out Vector3 destination))
            {
                SetFeedback(
                    RtsCommandFeedbackKind.InvalidGround,
                    "Choose a reachable point on the NavMesh ground.");
                return;
            }

            IssueContextGroundCommand(destination, input.QueueModifier.IsPressed);
        }

        private void ConfirmTargeting(RtsInputSnapshot input)
        {
            switch (TargetingState)
            {
                case CommandTargetingState.Move:
                    if (!TryResolveGroundPoint(input.Point, out Vector3 destination))
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidGround,
                            "Choose a reachable point on the NavMesh ground.");
                        return;
                    }

                    IssueMove(destination, input.QueueModifier.IsPressed);
                    CancelTargeting();
                    break;
                case CommandTargetingState.Attack:
                    bool append = input.QueueModifier.IsPressed;
                    if (TryResolveEntityTarget(input.Point, out EntityIdentity target))
                    {
                        if (IssueAttack(target, append))
                        {
                            CancelTargeting();
                        }

                        return;
                    }

                    if (!TryResolveGroundPoint(input.Point, out Vector3 attackDestination))
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidGround,
                            "Choose a hostile target or reachable NavMesh ground.");
                        return;
                    }

                    if (IssueAttackMove(attackDestination, append))
                    {
                        CancelTargeting();
                    }

                    break;
                case CommandTargetingState.SetRally:
                    if (!TryResolveGroundPoint(input.Point, out Vector3 rallyDestination))
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidGround,
                            "Choose reachable NavMesh ground for the rally point.");
                        return;
                    }

                    if (IssueSetRally(rallyDestination))
                    {
                        CancelTargeting();
                    }

                    break;
                case CommandTargetingState.Ability:
                    ConfirmAbilityTargeting(input);
                    break;
                case CommandTargetingState.Build:
                    ConfirmBuildTargeting(input);
                    break;
                case CommandTargetingState.DeployProduction:
                    ConfirmProductionDeployment(input);
                    break;
            }
        }

        private void ConfirmBuildTargeting(RtsInputSnapshot input)
        {
            BuildingDefinition definition = _targetingBuilding;
            if (definition == null || !TryResolveWorldPoint(input.Point, out Vector3 point))
            {
                SetFeedback(RtsCommandFeedbackKind.InvalidGround, "Choose visible buildable ground.");
                return;
            }

            if (IssueBuild(definition, point, input.QueueModifier.IsPressed))
            {
                CancelTargeting();
            }
        }

        private void ConfirmProductionDeployment(RtsInputSnapshot input)
        {
            ProductionQueue queue = _targetingProductionQueue;
            if (queue == null || !queue.AwaitingWorldDeployment ||
                !TryResolveWorldPoint(input.Point, out Vector3 point))
            {
                SetFeedback(RtsCommandFeedbackKind.InvalidTarget, "Choose a visible deployment point.");
                return;
            }

            VisibilitySystem visibility = AppRoot.TryGetInstance(out AppRoot appRoot) &&
                                          appRoot.Services != null &&
                                          appRoot.Services.CurrentSceneContext != null
                ? appRoot.Services.CurrentSceneContext.VisibilitySystem
                : null;
            if (visibility != null && visibility.GetState(point) != VisibilityCellState.Visible)
            {
                SetFeedback(
                    RtsCommandFeedbackKind.InvalidTarget,
                    "Production can only deploy at a currently visible point.");
                return;
            }

            if (!queue.TryDeployCurrent(point, out string error))
            {
                SetFeedback(
                    RtsCommandFeedbackKind.InvalidTarget,
                    string.IsNullOrEmpty(error) ? "Deployment failed." : error);
                return;
            }

            SetFeedback(RtsCommandFeedbackKind.Issued, "Production batch deployed.");
            CancelTargeting();
        }

        private void ConfirmAbilityTargeting(RtsInputSnapshot input)
        {
            AbilityDefinition definition = _targetingAbility;
            if (definition == null)
            {
                CancelTargeting();
                return;
            }

            AbilityTarget target;
            switch (definition.TargetType)
            {
                case AbilityTargetType.Point:
                case AbilityTargetType.Direction:
                    if (!TryResolveWorldPoint(input.Point, out Vector3 point))
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidTarget,
                            "Choose a valid world point for the ability.");
                        return;
                    }

                    if (AppRoot.TryGetInstance(out AppRoot visibilityRoot) &&
                        visibilityRoot.Services != null &&
                        visibilityRoot.Services.CurrentSceneContext != null &&
                        visibilityRoot.Services.CurrentSceneContext.VisibilitySystem != null &&
                        visibilityRoot.Services.CurrentSceneContext.VisibilitySystem.GetState(point) !=
                        VisibilityCellState.Visible)
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidTarget,
                            "Choose a currently visible point for the ability.");
                        return;
                    }

                    target = AbilityTarget.ForPoint(point);
                    break;
                case AbilityTargetType.FriendlyEntity:
                case AbilityTargetType.HostileEntity:
                case AbilityTargetType.AnyEntity:
                    if (!TryResolveEntityTarget(input.Point, out EntityIdentity entity))
                    {
                        SetFeedback(
                            RtsCommandFeedbackKind.InvalidTarget,
                            "Choose a valid entity target for the ability.");
                        return;
                    }

                    target = AbilityTarget.ForEntity(entity);
                    break;
                default:
                    target = AbilityTarget.None;
                    break;
            }

            if (IssueAbility(definition, target, input.QueueModifier.IsPressed))
            {
                CancelTargeting();
            }
        }

        private void HandleModeChanged(GameModeChange change)
        {
            if (change.Current != GameMode.RTS)
            {
                CancelTargeting();
            }

            CommandStateChanged?.Invoke();
        }

        private void HandleAutopilotCancellationRequested(
            HybridControlDriver pawn,
            InputSnapshot snapshot)
        {
            if (pawn != null && pawn.TryGetComponent(out UnitCommandQueue queue))
            {
                queue.StopAll();
            }
        }

        private void HandleEntityRegistered(EntityIdentity identity)
        {
            TrackQueue(identity);
            PublishCommandState();
        }

        private void HandleEntityUnregistered(EntityIdentity identity)
        {
            if (identity != null && identity.TryGetComponent(out UnitCommandQueue queue))
            {
                UntrackQueue(queue);
            }

            PublishCommandState();
        }

        private void HandleQueueChanged(UnitCommandQueue queue)
        {
            PublishCommandState();
        }

        private void HandleCommandFinished(
            UnitCommandQueue queue,
            IRtsCommand command,
            CommandStatus status)
        {
            if (!_issuingCommand && status == CommandStatus.Failed)
            {
                bool pathCommand = command is MoveCommand || command is AttackMoveCommand;
                SetFeedback(
                    pathCommand
                        ? RtsCommandFeedbackKind.PathFailed
                        : RtsCommandFeedbackKind.CommandFailed,
                    string.IsNullOrWhiteSpace(command.FailureReason)
                        ? pathCommand
                            ? "A unit could not complete its NavMesh command."
                            : "A unit could not complete its command."
                        : command.FailureReason);
            }

            PublishCommandState();
        }

        private bool SetTargetingState(CommandTargetingState state)
        {
            CommandTargetingState previous = TargetingState;
            if (state != CommandTargetingState.Ability)
            {
                _targetingAbility = null;
                _abilityRangePreview?.Hide();
            }
            else
            {
                EnsureAbilityRangePreview();
            }

            if (state != CommandTargetingState.Build)
            {
                _targetingBuilding = null;
                _buildingPlacementPreview?.Hide();
            }
            else
            {
                EnsureBuildingPlacementPreview();
            }

            if (state != CommandTargetingState.DeployProduction)
            {
                _targetingProductionQueue = null;
            }

            string prompt = state == CommandTargetingState.Ability && _targetingAbility != null
                ? _targetingAbility.DisplayName + ": release left mouse to confirm."
                : state == CommandTargetingState.Build && _targetingBuilding != null
                    ? _targetingBuilding.DisplayName + ": release left mouse to place."
                    : GetPrompt(state);
            _selectionService?.SetPointerSelectionSuppressed(
                state != CommandTargetingState.None);
            if (previous == state && string.Equals(TargetingPrompt, prompt, StringComparison.Ordinal))
            {
                return false;
            }

            TargetingState = state;
            TargetingPrompt = prompt;
            TargetingChanged?.Invoke(new CommandTargetingChange(previous, state, prompt));
            CommandStateChanged?.Invoke();
            return true;
        }

        private void EnsureBuildingPlacementPreview()
        {
            if (_buildingPlacementPreview != null)
            {
                return;
            }

            Transform existing = transform.Find("BuildingPlacementPreview");
            if (existing != null)
            {
                _buildingPlacementPreview =
                    existing.GetComponent<BuildingPlacementPreview>();
            }

            if (_buildingPlacementPreview != null)
            {
                return;
            }

            GameObject previewObject = new GameObject("BuildingPlacementPreview");
            previewObject.transform.SetParent(transform, false);
            _buildingPlacementPreview =
                previewObject.AddComponent<BuildingPlacementPreview>();
        }

        private void EnsureAbilityRangePreview()
        {
            if (_abilityRangePreview != null)
            {
                return;
            }

            Transform existing = transform.Find("AbilityRangePreview");
            if (existing != null)
            {
                _abilityRangePreview = existing.GetComponent<AbilityRangePreview>();
            }

            if (_abilityRangePreview != null)
            {
                return;
            }

            GameObject previewObject = new GameObject("AbilityRangePreview");
            previewObject.transform.SetParent(transform, false);
            _abilityRangePreview = previewObject.AddComponent<AbilityRangePreview>();
        }

        private void UpdateAbilityRangePreview(Vector2 screenPoint)
        {
            EnsureAbilityRangePreview();
            AbilityDefinition definition = _targetingAbility;
            Selectable caster = FindSelectedAbilityCaster(definition);
            if (definition == null || caster == null ||
                UiPointerUtility.IsScreenPointOverUi(screenPoint))
            {
                _abilityRangePreview.Hide();
                return;
            }

            bool hasTargetPoint = false;
            Vector3 targetPosition = ResolveIndicatorBase(caster);
            switch (definition.TargetType)
            {
                case AbilityTargetType.FriendlyEntity:
                case AbilityTargetType.HostileEntity:
                case AbilityTargetType.AnyEntity:
                    if (TryResolveEntityTarget(screenPoint, out EntityIdentity entity))
                    {
                        Selectable entitySelectable = entity.GetComponent<Selectable>();
                        targetPosition = entitySelectable != null
                            ? ResolveIndicatorBase(entitySelectable)
                            : entity.transform.position;
                        hasTargetPoint = true;
                    }
                    break;
                default:
                    hasTargetPoint = TryResolveWorldPoint(screenPoint, out targetPosition);
                    break;
            }

            Vector3 casterPosition = ResolveIndicatorBase(caster);
            Vector2 casterFlat = new Vector2(casterPosition.x, casterPosition.z);
            Vector2 targetFlat = new Vector2(targetPosition.x, targetPosition.z);
            bool withinCastRange = definition.Range <= 0.05f ||
                                   Vector2.Distance(casterFlat, targetFlat) <=
                                   definition.Range + 0.05f;
            bool visible = !hasTargetPoint || IsWorldPointVisible(targetPosition);
            _abilityRangePreview.Show(
                casterPosition,
                targetPosition,
                definition.Range,
                definition.Radius,
                hasTargetPoint,
                hasTargetPoint && withinCastRange && visible);
        }

        private Selectable FindSelectedAbilityCaster(AbilityDefinition definition)
        {
            if (_selectionService == null || definition == null)
            {
                return null;
            }

            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                Selectable selectable = selected[i];
                AbilityLoadout loadout = selectable != null
                    ? selectable.GetComponent<AbilityLoadout>()
                    : null;
                if (loadout != null && OwnsRtsAbility(loadout, definition))
                {
                    return selectable;
                }
            }

            return null;
        }

        private static Vector3 ResolveIndicatorBase(Selectable selectable)
        {
            Vector3 position = selectable.transform.position;
            Collider selectionCollider = selectable.SelectionCollider;
            if (selectionCollider != null && selectionCollider.enabled)
            {
                position.y = selectionCollider.bounds.min.y;
            }

            return position;
        }

        private static bool IsWorldPointVisible(Vector3 point)
        {
            return !AppRoot.TryGetInstance(out AppRoot appRoot) ||
                   appRoot.Services == null ||
                   appRoot.Services.CurrentSceneContext == null ||
                   appRoot.Services.CurrentSceneContext.VisibilitySystem == null ||
                   appRoot.Services.CurrentSceneContext.VisibilitySystem.GetState(point) ==
                   VisibilityCellState.Visible;
        }

        private void UpdateBuildingPlacementPreview(Vector2 screenPoint)
        {
            EnsureBuildingPlacementPreview();
            BuildingDefinition definition = _targetingBuilding;
            Builder builder = FindSelectedBuilder(definition);
            if (definition == null || builder == null ||
                UiPointerUtility.IsScreenPointOverUi(screenPoint) ||
                !TryResolveWorldPoint(screenPoint, out Vector3 requestedPosition))
            {
                _buildingPlacementPreview.Hide();
                return;
            }

            bool valid = builder.TryValidatePlacement(
                definition,
                requestedPosition,
                out Vector3 placementPosition,
                out _,
                out _);
            _buildingPlacementPreview.Show(
                definition,
                valid ? placementPosition : requestedPosition,
                valid);
        }

        private static string GetPrompt(CommandTargetingState state)
        {
            switch (state)
            {
                case CommandTargetingState.Move:
                    return "MOVE: left-click reachable ground; hold Shift to append.";
                case CommandTargetingState.Attack:
                    return "ATTACK: left-click an enemy or ground; hold Shift to append.";
                case CommandTargetingState.SetRally:
                    return "SET RALLY: left-click a reachable point.";
                case CommandTargetingState.Ability:
                    return "ABILITY: left-click a valid target.";
                case CommandTargetingState.Build:
                    return "BUILD: left-click visible buildable ground.";
                case CommandTargetingState.DeployProduction:
                    return "DEPLOY: left-click a visible free position.";
                default:
                    return string.Empty;
            }
        }

        private AbilityDefinition FindSelectedAbility(int slotIndex)
        {
            if (_selectionService == null || slotIndex < 0)
            {
                return null;
            }

            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                AbilityLoadout loadout = selected[i] != null
                    ? selected[i].GetComponent<AbilityLoadout>()
                    : null;
                AbilityDefinition[] abilities = loadout != null
                    ? loadout.RtsAbilities
                    : null;
                if (abilities != null && slotIndex < abilities.Length &&
                    abilities[slotIndex] != null)
                {
                    return abilities[slotIndex];
                }
            }

            return null;
        }

        private static bool OwnsRtsAbility(
            AbilityLoadout loadout,
            AbilityDefinition definition)
        {
            AbilityDefinition[] abilities = loadout.RtsAbilities;
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] != null && string.Equals(
                        abilities[i].ContentId,
                        definition.ContentId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private Builder FindSelectedBuilder(BuildingDefinition definition)
        {
            if (_selectionService == null || definition == null)
            {
                return null;
            }

            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                Builder builder = selected[i] != null
                    ? selected[i].GetComponent<Builder>()
                    : null;
                if (builder != null && builder.Offers(definition))
                {
                    return builder;
                }
            }

            return null;
        }

        private void SetFeedback(RtsCommandFeedbackKind kind, string message)
        {
            LastFeedback = new RtsCommandFeedback(kind, message);
            FeedbackChanged?.Invoke(LastFeedback);
            CommandStateChanged?.Invoke();
        }

        private int CountSelectedQueues(bool waitingOnly)
        {
            if (_selectionService == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                Selectable selectable = selected[i];
                if (selectable != null &&
                    selectable.TryGetComponent(out UnitCommandQueue queue))
                {
                    count += waitingOnly ? queue.WaitingCount : queue.TotalCommandCount;
                }
            }

            return count;
        }

        private bool HasSelectedRallyController()
        {
            if (_selectionService == null)
            {
                return false;
            }

            IReadOnlyList<Selectable> selected = _selectionService.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != null &&
                    selected[i].GetComponent<RallyPointController>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void TrackQueue(EntityIdentity identity)
        {
            if (identity != null && identity.TryGetComponent(out UnitCommandQueue queue))
            {
                TrackQueue(queue);
            }
        }

        private void TrackQueue(UnitCommandQueue queue)
        {
            if (queue == null || !_trackedQueues.Add(queue))
            {
                return;
            }

            queue.QueueChanged += HandleQueueChanged;
            queue.CommandFinished += HandleCommandFinished;
        }

        private void UntrackQueue(UnitCommandQueue queue)
        {
            if (queue == null || !_trackedQueues.Remove(queue))
            {
                return;
            }

            UnsubscribeQueue(queue);
        }

        private void UnsubscribeQueue(UnitCommandQueue queue)
        {
            if (queue == null)
            {
                return;
            }

            queue.QueueChanged -= HandleQueueChanged;
            queue.CommandFinished -= HandleCommandFinished;
        }

        private void PublishCommandState()
        {
            if (_debugOverlay != null)
            {
                DebugOverlaySnapshot previous = _debugOverlay.Snapshot;
                _debugOverlay.SetSnapshot(new DebugOverlaySnapshot(
                    previous.Mode,
                    previous.SelectedCount,
                    GetTotalCommandCount(),
                    previous.ResourceValue));
            }

            CommandStateChanged?.Invoke();
        }
    }
}
