using OriginCore.Buildings;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class ProductionPanelView : MonoBehaviour
    {
        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private RtsCommandIssuer _commandIssuer;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private ProductionRecipe _workerRecipe;
        [SerializeField] private ProductionRecipe _combatRecipe;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private Button _workerButton;
        [SerializeField] private Button _combatButton;
        [SerializeField] private Button _setRallyButton;
        [SerializeField] private TMP_Text _workerButtonLabel;
        [SerializeField] private TMP_Text _combatButtonLabel;
        [SerializeField] private TMP_Text _progressLabel;
        [SerializeField] private TMP_Text _queueLabel;
        [SerializeField] private TMP_Text _feedbackLabel;

        private ResourceService _resourceService;
        private ProductionQueue _activeQueue;
        private bool _globalEventsBound;

        public SelectionService SelectionService => _selectionService;
        public RtsCommandIssuer CommandIssuer => _commandIssuer;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public ProductionRecipe WorkerRecipe => _workerRecipe;
        public ProductionRecipe CombatRecipe => _combatRecipe;
        public GameObject ContentRoot => _contentRoot;
        public Button WorkerButton => _workerButton;
        public Button CombatButton => _combatButton;
        public Button SetRallyButton => _setRallyButton;
        public TMP_Text ProgressLabel => _progressLabel;
        public TMP_Text QueueLabel => _queueLabel;
        public TMP_Text FeedbackLabel => _feedbackLabel;
        public ProductionQueue ActiveQueue => _activeQueue;

        private void OnEnable()
        {
            TryResolveResourceService();
            BindGlobalEvents();
            ResolveActiveQueue();
            Refresh();
        }

        private void Start()
        {
            if (_resourceService == null && TryResolveResourceService())
            {
                UnbindGlobalEvents();
                BindGlobalEvents();
                Refresh();
            }
        }

        private void OnDisable()
        {
            SetActiveQueue(null);
            UnbindGlobalEvents();
        }

        public void Configure(
            SelectionService selectionService,
            RtsCommandIssuer commandIssuer,
            ModeHudPresenter modeHudPresenter,
            ProductionRecipe workerRecipe,
            ProductionRecipe combatRecipe,
            GameObject contentRoot,
            Button workerButton,
            Button combatButton,
            Button setRallyButton,
            TMP_Text workerButtonLabel,
            TMP_Text combatButtonLabel,
            TMP_Text progressLabel,
            TMP_Text queueLabel,
            TMP_Text feedbackLabel)
        {
            SetActiveQueue(null);
            UnbindGlobalEvents();
            _selectionService = selectionService;
            _commandIssuer = commandIssuer;
            _modeHudPresenter = modeHudPresenter;
            _workerRecipe = workerRecipe;
            _combatRecipe = combatRecipe;
            _contentRoot = contentRoot;
            _workerButton = workerButton;
            _combatButton = combatButton;
            _setRallyButton = setRallyButton;
            _workerButtonLabel = workerButtonLabel;
            _combatButtonLabel = combatButtonLabel;
            _progressLabel = progressLabel;
            _queueLabel = queueLabel;
            _feedbackLabel = feedbackLabel;
            if (isActiveAndEnabled)
            {
                TryResolveResourceService();
                BindGlobalEvents();
                ResolveActiveQueue();
            }

            Refresh();
        }

        public void Refresh()
        {
            bool isRts = _modeHudPresenter == null ||
                         _modeHudPresenter.Mode == GameMode.RTS;
            bool hasProducer = _activeQueue != null;
            bool visible = isRts && hasProducer;
            if (_contentRoot != null && _contentRoot.activeSelf != visible)
            {
                _contentRoot.SetActive(visible);
            }

            bool targeting = _commandIssuer != null &&
                             _commandIssuer.TargetingState != CommandTargetingState.None;
            if (_workerButton != null)
            {
                _workerButton.interactable = CanEnqueue(_workerRecipe) && !targeting;
            }

            if (_combatButton != null)
            {
                _combatButton.interactable = CanEnqueue(_combatRecipe) && !targeting;
            }

            if (_setRallyButton != null)
            {
                _setRallyButton.interactable = hasProducer && !targeting;
            }

            SetRecipeButtonLabel(_workerButtonLabel, _workerRecipe);
            SetRecipeButtonLabel(_combatButtonLabel, _combatRecipe);

            if (_progressLabel != null)
            {
                if (_activeQueue != null && _activeQueue.CurrentRecipe != null)
                {
                    _progressLabel.text = "PRODUCING  " +
                                          _activeQueue.CurrentRecipe.DisplayName + "  " +
                                          Mathf.RoundToInt(
                                              _activeQueue.NormalizedProgress * 100f) + "%  (" +
                                          _activeQueue.CurrentRemainingTime.ToString("0.0") + "s)";
                }
                else
                {
                    _progressLabel.text = "PRODUCTION IDLE";
                }
            }

            if (_queueLabel != null)
            {
                _queueLabel.text = _activeQueue != null
                    ? "QUEUE  " + _activeQueue.TotalCount + " / " +
                      _activeQueue.Capacity + "   WAITING  " +
                      _activeQueue.WaitingCount
                    : "QUEUE  0 / 5";
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = _activeQueue != null
                    ? _activeQueue.LastMessage
                    : string.Empty;
            }
        }

        private bool CanEnqueue(ProductionRecipe recipe)
        {
            return _activeQueue != null && recipe != null &&
                   _activeQueue.TotalCount < _activeQueue.Capacity &&
                   _resourceService != null &&
                   _resourceService.CanAfford(recipe.Cost);
        }

        private void ResolveActiveQueue()
        {
            ProductionQueue next = null;
            if (_selectionService != null)
            {
                var selected = _selectionService.Selected;
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i] != null &&
                        selected[i].TryGetComponent(out ProductionQueue queue))
                    {
                        next = queue;
                        break;
                    }
                }
            }

            SetActiveQueue(next);
        }

        private void SetActiveQueue(ProductionQueue queue)
        {
            if (_activeQueue == queue)
            {
                return;
            }

            if (_activeQueue != null)
            {
                _activeQueue.QueueChanged -= HandleQueueChanged;
                _activeQueue.ProgressChanged -= HandleProgressChanged;
                _activeQueue.ProductionRejected -= HandleProductionRejected;
                _activeQueue.UnitProduced -= HandleUnitProduced;
            }

            _activeQueue = queue;
            if (_activeQueue != null)
            {
                _activeQueue.QueueChanged += HandleQueueChanged;
                _activeQueue.ProgressChanged += HandleProgressChanged;
                _activeQueue.ProductionRejected += HandleProductionRejected;
                _activeQueue.UnitProduced += HandleUnitProduced;
            }
        }

        private bool TryResolveResourceService()
        {
            if (_resourceService != null)
            {
                return true;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _resourceService = appRoot.Services.ResourceService;
            return _resourceService != null;
        }

        private void BindGlobalEvents()
        {
            if (_globalEventsBound)
            {
                return;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged += HandleSelectionChanged;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.TargetingChanged += HandleTargetingChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented += HandleModePresented;
            }

            if (_resourceService != null)
            {
                _resourceService.ResourceChanged += HandleResourceChanged;
            }

            _workerButton?.onClick.AddListener(HandleWorkerClicked);
            _combatButton?.onClick.AddListener(HandleCombatClicked);
            _setRallyButton?.onClick.AddListener(HandleSetRallyClicked);
            _globalEventsBound = true;
        }

        private void UnbindGlobalEvents()
        {
            if (!_globalEventsBound)
            {
                return;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged -= HandleSelectionChanged;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.TargetingChanged -= HandleTargetingChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= HandleModePresented;
            }

            if (_resourceService != null)
            {
                _resourceService.ResourceChanged -= HandleResourceChanged;
            }

            _workerButton?.onClick.RemoveListener(HandleWorkerClicked);
            _combatButton?.onClick.RemoveListener(HandleCombatClicked);
            _setRallyButton?.onClick.RemoveListener(HandleSetRallyClicked);
            _globalEventsBound = false;
        }

        private void HandleWorkerClicked()
        {
            _activeQueue?.TryEnqueue(_workerRecipe);
        }

        private void HandleCombatClicked()
        {
            _activeQueue?.TryEnqueue(_combatRecipe);
        }

        private void HandleSetRallyClicked()
        {
            _commandIssuer?.BeginSetRallyTargeting();
        }

        private void HandleSelectionChanged()
        {
            ResolveActiveQueue();
            Refresh();
        }

        private void HandleTargetingChanged(CommandTargetingChange change)
        {
            Refresh();
        }

        private void HandleModePresented(GameMode mode)
        {
            Refresh();
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            Refresh();
        }

        private void HandleQueueChanged(ProductionQueue queue)
        {
            Refresh();
        }

        private void HandleProgressChanged(
            ProductionQueue queue,
            ProductionOrderSnapshot snapshot)
        {
            Refresh();
        }

        private void HandleProductionRejected(
            ProductionQueue queue,
            ProductionRecipe recipe,
            ProductionRejectionReason reason,
            string message)
        {
            Refresh();
        }

        private void HandleUnitProduced(
            ProductionQueue queue,
            ProductionRecipe recipe,
            OriginCore.Gameplay.EntityIdentity identity)
        {
            Refresh();
        }

        private static void SetRecipeButtonLabel(
            TMP_Text label,
            ProductionRecipe recipe)
        {
            if (label == null)
            {
                return;
            }

            label.text = recipe != null
                ? recipe.DisplayName.ToUpperInvariant() + "\n" +
                  recipe.Cost.CommanderResource + " C  " +
                  recipe.Cost.Crystal + " X  " +
                  recipe.Cost.Influence + " I"
                : "UNAVAILABLE";
        }
    }
}
