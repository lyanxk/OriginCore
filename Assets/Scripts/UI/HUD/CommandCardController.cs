using System.Collections.Generic;
using Input;
using Modes;
using Unit.Ability;
using Unit.Selection;
using Unit.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.HUD
{
    public class CommandCardController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] Transform slotRoot;
        [SerializeField] CommandSlotView slotPrefab;
        [Min(1)]
        [SerializeField] int slotCount = 12;
        [SerializeField] TooltipController tooltipController;

        [Header("Input")]
        [SerializeField] InputIntentSource inputSource;

        readonly List<CommandSlotView> _slotViews = new List<CommandSlotView>(16);
        readonly List<CommandEntry> _visibleEntries = new List<CommandEntry>(16);
        readonly List<CommandEntry?> _slottedEntries = new List<CommandEntry?>(16);

        SelectionManager _selection;

        public int SlotCount => slotCount;
        public IReadOnlyList<CommandEntry> VisibleEntries => _visibleEntries;

        void Awake()
        {
            if (inputSource == null)
                inputSource = FindObjectOfType<InputIntentSource>();

            EnsureSlots();
            EnsureSlotEntryBuffer();
        }

        void OnEnable()
        {
            BindSelectionManager();
            RefreshFromManager();
        }

        void OnDisable()
        {
            UnbindSelectionManager();

            if (tooltipController != null)
                tooltipController.Hide();
        }

        void Update()
        {
            if (_selection != SelectionManager.Instance)
            {
                UnbindSelectionManager();
                BindSelectionManager();
                RefreshFromManager();
                return;
            }

            if (_selection != null && _selection.SelectedCount > 0)
                RefreshFromManager();
        }

        public bool TryExecuteBySlot(int slotIndex)
        {
            if (!TryGetEntryAtSlot(slotIndex, out CommandEntry entry))
                return false;

            if (!entry.Enabled || entry.Type == CommandEntryType.Passive)
                return false;

            bool executed;
            if (entry.Type == CommandEntryType.Ability)
                executed = ExecuteAbility(entry.Id);
            else if (entry.Type == CommandEntryType.Production)
                executed = ExecuteProduction(entry.Id);
            else
                executed = ExecuteCommand(entry.Id);

            if (executed)
                RefreshFromManager();

            return executed;
        }

        public bool TryGetEntryAtSlot(int slotIndex, out CommandEntry entry)
        {
            entry = default;
            if (slotIndex < 0 || slotIndex >= _slottedEntries.Count)
                return false;

            CommandEntry? slottedEntry = _slottedEntries[slotIndex];
            if (!slottedEntry.HasValue)
                return false;

            entry = slottedEntry.Value;
            return true;
        }

        void BindSelectionManager()
        {
            _selection = SelectionManager.Instance;
            if (_selection != null)
                _selection.OnSelectionChanged += HandleSelectionChanged;
        }

        void UnbindSelectionManager()
        {
            if (_selection != null)
                _selection.OnSelectionChanged -= HandleSelectionChanged;

            _selection = null;
        }

        void HandleSelectionChanged(IReadOnlyList<Selectable> selected, Selectable primary)
        {
            RefreshEntries(selected, primary);
        }

        void RefreshFromManager()
        {
            if (_selection == null)
            {
                RefreshEntries(null, null);
                return;
            }

            RefreshEntries(_selection.Selected, _selection.Primary);
        }

        void RefreshEntries(IReadOnlyList<Selectable> selected, Selectable primary)
        {
            _visibleEntries.Clear();
            ClearSlotEntries();

            if (selected == null || selected.Count == 0)
            {
                BindSlots();
                return;
            }

            Selectable effectivePrimary = ResolvePrimary(selected, primary);
            if (effectivePrimary == null)
            {
                BindSlots();
                return;
            }

            ICommandCardDataSource primaryData = effectivePrimary.CommandCardDataSource;
            if (primaryData == null)
            {
                BindSlots();
                return;
            }

            IReadOnlyList<CommandEntry> entries = primaryData.GetCommandEntries();
            for (int i = 0; i < entries.Count; i++)
                _visibleEntries.Add(entries[i]);

            if (selected.Count > 1)
                ApplyGroupAvailability(selected);

            ApplyEntryVisibilityRules(effectivePrimary, primaryData);
            AssignEntriesToSlots();
            BindSlots();
        }

        void ApplyGroupAvailability(IReadOnlyList<Selectable> selected)
        {
            bool allCanMove = true;
            bool allCanAttack = true;
            bool allCanStop = true;

            for (int i = 0; i < selected.Count; i++)
            {
                Selectable selectable = selected[i];
                if (selectable == null)
                {
                    allCanMove = false;
                    allCanAttack = false;
                    allCanStop = false;
                    continue;
                }

                ICommandCardDataSource dataSource = selectable.CommandCardDataSource;
                if (dataSource != null)
                {
                    allCanMove &= dataSource.CanMove;
                    allCanAttack &= dataSource.CanAttack;
                    allCanStop &= dataSource.CanStop;
                }
                else
                {
                    bool hasExecutor = selectable.CommandExecutor != null;
                    bool hasMotor = selectable.Motor != null;
                    bool hasCombat = selectable.Combat != null;

                    allCanMove &= hasExecutor && hasMotor;
                    allCanAttack &= hasExecutor && hasCombat;
                    allCanStop &= hasExecutor;
                }
            }

            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                CommandEntry entry = _visibleEntries[i];

                if (entry.Id == CommandEntryIds.Move)
                    entry.Enabled = entry.Enabled && allCanMove;
                else if (entry.Id == CommandEntryIds.Attack)
                    entry.Enabled = entry.Enabled && allCanAttack;
                else if (entry.Id == CommandEntryIds.Stop)
                    entry.Enabled = entry.Enabled && allCanStop;

                _visibleEntries[i] = entry;
            }
        }

        void ApplyEntryVisibilityRules(Selectable primary, ICommandCardDataSource primaryData)
        {
            bool showBaseCommands = ShouldShowBaseCommands(primary, primaryData);

            for (int i = _visibleEntries.Count - 1; i >= 0; i--)
            {
                CommandEntry entry = _visibleEntries[i];

                if (entry.Type == CommandEntryType.Ability || entry.Type == CommandEntryType.Passive)
                {
                    if (!entry.Enabled)
                        _visibleEntries.RemoveAt(i);
                    continue;
                }

                if (IsBaseCommand(entry.Id))
                {
                    if (!showBaseCommands)
                        _visibleEntries.RemoveAt(i);
                    continue;
                }

                if (!entry.Enabled)
                    _visibleEntries.RemoveAt(i);
            }
        }

        static bool IsBaseCommand(string commandId)
        {
            return commandId == CommandEntryIds.Move
                   || commandId == CommandEntryIds.Attack
                   || commandId == CommandEntryIds.Stop;
        }

        static bool ShouldShowBaseCommands(Selectable primary, ICommandCardDataSource primaryData)
        {
            if (primaryData != null && (primaryData.CanMove || primaryData.CanAttack || primaryData.CanStop))
                return true;

            if (primary == null)
                return false;

            return primary.Motor != null || primary.CommandExecutor != null || primary.Combat != null;
        }

        Selectable ResolvePrimary(IReadOnlyList<Selectable> selected, Selectable primary)
        {
            if (primary != null)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (selected[i] == primary)
                        return primary;
                }
            }

            return selected[selected.Count - 1];
        }

        void EnsureSlots()
        {
            if (slotRoot == null || slotPrefab == null)
                return;

            while (_slotViews.Count < slotCount)
            {
                CommandSlotView slot = Instantiate(slotPrefab, slotRoot);
                _slotViews.Add(slot);
            }

            while (_slotViews.Count > slotCount)
            {
                int last = _slotViews.Count - 1;
                CommandSlotView slot = _slotViews[last];
                _slotViews.RemoveAt(last);

                if (slot != null)
                    Destroy(slot.gameObject);
            }
        }

        void BindSlots()
        {
            EnsureSlots();
            if (tooltipController != null)
                tooltipController.Hide();

            for (int i = 0; i < _slotViews.Count; i++)
            {
                CommandSlotView slot = _slotViews[i];
                slot.gameObject.SetActive(true);

                bool hasEntry = TryGetEntryAtSlot(i, out CommandEntry entry);
                if (hasEntry)
                    entry.HotkeyText = ResolveDisplayedHotkeyText(entry, i);

                slot.Bind(
                    i,
                    hasEntry,
                    entry,
                    HandleSlotClicked,
                    HandleSlotHoverEnter,
                    HandleSlotHoverExit);
            }
        }

        void HandleSlotClicked(int slotIndex)
        {
            TryExecuteBySlot(slotIndex);
        }

        void HandleSlotHoverEnter(CommandEntry entry)
        {
            if (tooltipController == null)
                return;

            tooltipController.Show(entry, GetPointerScreenPosition());
        }

        void HandleSlotHoverExit()
        {
            if (tooltipController != null)
                tooltipController.Hide();
        }

        bool ExecuteAbility(string abilityId)
        {
            if (_selection == null)
                return false;

            Selectable primary = ResolvePrimary(_selection.Selected, _selection.Primary);
            if (primary == null)
                return false;

            ICommandCardDataSource dataSource = primary.CommandCardDataSource;
            bool append = IsQueueAppendRequested();
            if (dataSource != null)
                return dataSource.TryActivateAbility(abilityId, append);

            AbilityInputRouter router = primary.AbilityRouter;
            return router != null && router.TryActivate(abilityId, append);
        }

        bool ExecuteCommand(string commandId)
        {
            if (_selection == null || _selection.SelectedCount == 0)
                return false;

            switch (commandId)
            {
                case CommandEntryIds.Move:
                    RtsQueuedOrderState.SetPendingOrder(RtsQueuedOrderType.Move);
                    return true;

                case CommandEntryIds.Attack:
                    RtsQueuedOrderState.SetPendingOrder(RtsQueuedOrderType.Attack);
                    return true;

                case CommandEntryIds.Stop:
                    RtsQueuedOrderState.Clear();
                    return IssueStop();

                default:
                    return false;
            }
        }

        bool ExecuteProduction(string productionId)
        {
            if (_selection == null || _selection.SelectedCount == 0)
                return false;

            Selectable primary = ResolvePrimary(_selection.Selected, _selection.Primary);
            if (primary == null)
                return false;

            ICommandCardDataSource dataSource = primary.CommandCardDataSource;
            return dataSource != null && dataSource.TryProduce(productionId);
        }

        bool IssueStop()
        {
            return RtsOrderDispatcher.TryIssueStop(_selection.Selected);
        }

        Vector2 GetPointerScreenPosition()
        {
            if (inputSource != null)
            {
                Vector2 pos = inputSource.Current.PointerScreenPos;
                if (pos.sqrMagnitude > 0f)
                    return pos;
            }

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return UnityEngine.Input.mousePosition;
        }

        string ResolveDisplayedHotkeyText(CommandEntry entry, int slotIndex)
        {
            string hotkeyToken = CommandHotkeyUtility.ResolveHotkeyToken(entry, slotIndex);
            if (string.IsNullOrEmpty(hotkeyToken))
                return string.Empty;

            if (inputSource == null)
                return hotkeyToken;

            return inputSource.GetCommandBindingDisplayString(hotkeyToken);
        }

        bool IsQueueAppendRequested()
        {
            if (inputSource != null)
                return inputSource.Current.Shift;

            if (Keyboard.current != null)
                return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        void AssignEntriesToSlots()
        {
            EnsureSlotEntryBuffer();

            for (int i = 0; i < _slottedEntries.Count; i++)
                _slottedEntries[i] = null;

            int nextAutoSlot = 0;
            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                CommandEntry entry = _visibleEntries[i];
                if (TryAssignPreferredSlot(entry))
                    continue;

                while (nextAutoSlot < _slottedEntries.Count && _slottedEntries[nextAutoSlot].HasValue)
                    nextAutoSlot++;

                if (nextAutoSlot >= _slottedEntries.Count)
                    break;

                entry.SlotIndex = nextAutoSlot;
                _slottedEntries[nextAutoSlot] = entry;
                nextAutoSlot++;
            }
        }

        bool TryAssignPreferredSlot(CommandEntry entry)
        {
            if (entry.SlotIndex < 0 || entry.SlotIndex >= _slottedEntries.Count)
                return false;

            if (_slottedEntries[entry.SlotIndex].HasValue)
                return false;

            _slottedEntries[entry.SlotIndex] = entry;
            return true;
        }

        void ClearSlotEntries()
        {
            EnsureSlotEntryBuffer();
            for (int i = 0; i < _slottedEntries.Count; i++)
                _slottedEntries[i] = null;
        }

        void EnsureSlotEntryBuffer()
        {
            while (_slottedEntries.Count < slotCount)
                _slottedEntries.Add(null);

            while (_slottedEntries.Count > slotCount)
                _slottedEntries.RemoveAt(_slottedEntries.Count - 1);
        }
    }
}
