using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.RTS.Commands;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class RtsRosterView : MonoBehaviour
    {
        [SerializeField] private SelectionService _selectionService;
        [SerializeField] private RtsCommandIssuer _commandIssuer;
        [SerializeField] private ModeHudPresenter _modeHudPresenter;
        [SerializeField] private GameObject _contentRoot;
        [SerializeField] private Button _idleWorkerButton;
        [SerializeField] private Button _combatButton;
        [SerializeField] private TMP_Text _idleWorkerLabel;
        [SerializeField] private TMP_Text _combatLabel;

        private EntityRegistry _entityRegistry;
        private FactionRelationService _factionRelations;
        private bool _eventsBound;

        public SelectionService SelectionService => _selectionService;
        public RtsCommandIssuer CommandIssuer => _commandIssuer;
        public ModeHudPresenter ModeHudPresenter => _modeHudPresenter;
        public GameObject ContentRoot => _contentRoot;
        public Button IdleWorkerButton => _idleWorkerButton;
        public Button CombatButton => _combatButton;
        public TMP_Text IdleWorkerLabel => _idleWorkerLabel;
        public TMP_Text CombatLabel => _combatLabel;
        public int IdleWorkerCount { get; private set; }
        public int CombatAndHeroCount { get; private set; }

        private void OnEnable()
        {
            TryResolveServices();
            BindEvents();
            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        private void Start()
        {
            if (_entityRegistry == null && TryResolveServices())
            {
                UnbindEvents();
                BindEvents();
                Refresh();
            }
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public void Configure(
            SelectionService selectionService,
            RtsCommandIssuer commandIssuer,
            ModeHudPresenter modeHudPresenter,
            GameObject contentRoot,
            Button idleWorkerButton,
            Button combatButton,
            TMP_Text idleWorkerLabel,
            TMP_Text combatLabel)
        {
            UnbindEvents();
            _selectionService = selectionService;
            _commandIssuer = commandIssuer;
            _modeHudPresenter = modeHudPresenter;
            _contentRoot = contentRoot;
            _idleWorkerButton = idleWorkerButton;
            _combatButton = combatButton;
            _idleWorkerLabel = idleWorkerLabel;
            _combatLabel = combatLabel;
            if (isActiveAndEnabled)
            {
                TryResolveServices();
                BindEvents();
            }

            Refresh();
            ApplyMode(_modeHudPresenter != null ? _modeHudPresenter.Mode : GameMode.RTS);
        }

        public void Refresh()
        {
            IdleWorkerCount = 0;
            CombatAndHeroCount = 0;
            if (_entityRegistry != null && _selectionService != null &&
                _factionRelations != null)
            {
                var entities = _entityRegistry.Entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    EntityIdentity identity = entities[i];
                    if (identity == null ||
                        !identity.TryGetComponent(out FactionMember faction) ||
                        !_factionRelations.AreAllied(
                            _selectionService.ObserverFaction,
                            faction.Faction) ||
                        !identity.TryGetComponent(out OriginCore.Gameplay.Selectable selectable) ||
                        !selectable.IsAlive)
                    {
                        continue;
                    }

                    if (identity.HasAnyRole(UnitRole.Worker))
                    {
                        UnitCommandQueue queue = identity.GetComponent<UnitCommandQueue>();
                        if (queue == null || !queue.HasCurrentCommand)
                        {
                            IdleWorkerCount++;
                        }
                    }

                    if (identity.HasAnyRole(UnitRole.Combat | UnitRole.Hero))
                    {
                        CombatAndHeroCount++;
                    }
                }
            }

            if (_idleWorkerLabel != null)
            {
                _idleWorkerLabel.text = "IDLE WORKERS\n" + IdleWorkerCount;
            }

            if (_combatLabel != null)
            {
                _combatLabel.text = "COMBAT / HERO\n" + CombatAndHeroCount;
            }

            if (_idleWorkerButton != null)
            {
                _idleWorkerButton.interactable = IdleWorkerCount > 0;
            }

            if (_combatButton != null)
            {
                _combatButton.interactable = CombatAndHeroCount > 0;
            }
        }

        private bool TryResolveServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            _entityRegistry = appRoot.Services.EntityRegistry;
            _factionRelations = appRoot.Services.FactionRelations;
            return _entityRegistry != null && _factionRelations != null;
        }

        private void BindEvents()
        {
            if (_eventsBound)
            {
                return;
            }

            if (_entityRegistry != null)
            {
                _entityRegistry.EntityRegistered += HandleEntityChanged;
                _entityRegistry.EntityUnregistered += HandleEntityChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged += HandleSelectionChanged;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.CommandStateChanged += HandleCommandStateChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented += HandleModePresented;
            }

            _idleWorkerButton?.onClick.AddListener(HandleIdleWorkersClicked);
            _combatButton?.onClick.AddListener(HandleCombatClicked);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound)
            {
                return;
            }

            if (_entityRegistry != null)
            {
                _entityRegistry.EntityRegistered -= HandleEntityChanged;
                _entityRegistry.EntityUnregistered -= HandleEntityChanged;
            }

            if (_selectionService != null)
            {
                _selectionService.SelectionChanged -= HandleSelectionChanged;
            }

            if (_commandIssuer != null)
            {
                _commandIssuer.CommandStateChanged -= HandleCommandStateChanged;
            }

            if (_modeHudPresenter != null)
            {
                _modeHudPresenter.ModePresented -= HandleModePresented;
            }

            _idleWorkerButton?.onClick.RemoveListener(HandleIdleWorkersClicked);
            _combatButton?.onClick.RemoveListener(HandleCombatClicked);
            _eventsBound = false;
        }

        private void HandleEntityChanged(EntityIdentity identity)
        {
            Refresh();
        }

        private void HandleSelectionChanged()
        {
            Refresh();
        }

        private void HandleCommandStateChanged()
        {
            Refresh();
        }

        private void HandleIdleWorkersClicked()
        {
            _selectionService?.SelectIdleWorkers();
        }

        private void HandleCombatClicked()
        {
            _selectionService?.SelectCombatAndHero();
        }

        private void HandleModePresented(GameMode mode)
        {
            ApplyMode(mode);
        }

        private void ApplyMode(GameMode mode)
        {
            if (_contentRoot != null && _contentRoot.activeSelf != (mode == GameMode.RTS))
            {
                _contentRoot.SetActive(mode == GameMode.RTS);
            }
        }
    }
}
