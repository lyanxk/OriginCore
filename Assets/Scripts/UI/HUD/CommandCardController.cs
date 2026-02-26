using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CommandCardController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Transform slotRoot;
    [SerializeField] CommandSlotView slotPrefab;
    [Min(1)]
    [SerializeField] int slotCount = 12;
    [SerializeField] TooltipController tooltipController;

    [Header("World Command")]
    [SerializeField] Camera worldCamera;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] InputIntentSource inputSource;

    readonly List<CommandSlotView> _slotViews = new List<CommandSlotView>(16);
    readonly List<CommandEntry> _visibleEntries = new List<CommandEntry>(16);

    SelectionManager _selection;

    public int SlotCount => slotCount;
    public IReadOnlyList<CommandEntry> VisibleEntries => _visibleEntries;

    void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (inputSource == null)
            inputSource = FindObjectOfType<InputIntentSource>();

        EnsureSlots();
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
        if (slotIndex < 0 || slotIndex >= _visibleEntries.Count)
            return false;

        CommandEntry entry = _visibleEntries[slotIndex];
        if (!entry.Enabled)
            return false;

        bool executed = entry.Type == CommandEntryType.Ability
            ? ExecuteAbility(entry.Id)
            : ExecuteCommand(entry.Id);

        if (executed)
            RefreshFromManager();

        return executed;
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

        UnitUIDataSource primaryData = effectivePrimary.GetComponent<UnitUIDataSource>();
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

            UnitUIDataSource dataSource = selectable.GetComponent<UnitUIDataSource>();
            if (dataSource != null)
            {
                allCanMove &= dataSource.CanMove;
                allCanAttack &= dataSource.CanAttack;
                allCanStop &= dataSource.CanStop;
            }
            else
            {
                bool hasExecutor = selectable.GetComponent<CommandExecutor>() != null;
                bool hasMotor = selectable.GetComponent<UnitBase>() != null;
                bool hasCombat = selectable.GetComponent<UnitCombat>() != null;

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

    void ApplyEntryVisibilityRules(Selectable primary, UnitUIDataSource primaryData)
    {
        bool showBaseCommands = ShouldShowBaseCommands(primary, primaryData);

        for (int i = _visibleEntries.Count - 1; i >= 0; i--)
        {
            CommandEntry entry = _visibleEntries[i];

            if (entry.Type == CommandEntryType.Ability)
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

    static bool ShouldShowBaseCommands(Selectable primary, UnitUIDataSource primaryData)
    {
        if (primaryData != null && (primaryData.CanMove || primaryData.CanAttack || primaryData.CanStop))
            return true;

        if (primary == null)
            return false;

        bool hasMotor = primary.GetComponent<UnitBase>() != null;
        bool hasExecutor = primary.GetComponent<CommandExecutor>() != null;
        bool hasCombat = primary.GetComponent<UnitCombat>() != null;
        return hasMotor || hasExecutor || hasCombat;
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
            bool hasEntry = i < _visibleEntries.Count;
            slot.gameObject.SetActive(hasEntry);
            if (!hasEntry)
                continue;

            CommandEntry entry = _visibleEntries[i];
            if (hasEntry)
                entry.HotkeyText = GetSlotHotkeyText(i, entry.HotkeyText);

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

        UnitUIDataSource dataSource = primary.GetComponent<UnitUIDataSource>();
        if (dataSource != null)
            return dataSource.TryActivateAbility(abilityId);

        AbilityInputRouter router = primary.GetComponent<AbilityInputRouter>();
        return router != null && router.TryActivate(abilityId);
    }

    bool ExecuteCommand(string commandId)
    {
        if (_selection == null || _selection.SelectedCount == 0)
            return false;

        switch (commandId)
        {
            case CommandEntryIds.Move:
            {
                if (!TryGetGroundPoint(out Vector3 point))
                    return false;

                return IssueMove(point);
            }

            case CommandEntryIds.Attack:
            {
                if (!TryGetGroundPoint(out Vector3 point))
                    return false;

                return IssueAttack(point);
            }

            case CommandEntryIds.Stop:
                return IssueStop();

            default:
                return false;
        }
    }

    bool IssueMove(Vector3 destination)
    {
        bool append = GetAppendMode();
        bool issued = false;

        IReadOnlyList<Selectable> selected = _selection.Selected;
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (selectable == null) continue;

            CommandExecutor executor = selectable.GetComponent<CommandExecutor>();
            if (executor == null) continue;

            executor.Enqueue(new MoveCommand(destination), append);
            issued = true;
        }

        return issued;
    }

    bool IssueAttack(Vector3 destination)
    {
        bool append = GetAppendMode();
        bool issued = false;

        IReadOnlyList<Selectable> selected = _selection.Selected;
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (selectable == null) continue;

            UnitCombat combat = selectable.GetComponent<UnitCombat>();
            CommandExecutor executor = selectable.GetComponent<CommandExecutor>();
            if (combat == null || executor == null) continue;

            executor.Enqueue(new AttackCommand(destination), append);
            issued = true;
        }

        return issued;
    }

    bool IssueStop()
    {
        bool issued = false;

        IReadOnlyList<Selectable> selected = _selection.Selected;
        for (int i = 0; i < selected.Count; i++)
        {
            Selectable selectable = selected[i];
            if (selectable == null) continue;

            CommandExecutor executor = selectable.GetComponent<CommandExecutor>();
            if (executor == null) continue;

            executor.Enqueue(new StopCommand(), append: false);
            issued = true;
        }

        return issued;
    }

    bool TryGetGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (worldCamera == null)
            return false;

        Vector2 pointer = GetPointerScreenPosition();
        Ray ray = worldCamera.ScreenPointToRay(pointer);

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            return false;

        point = hit.point;
        return true;
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

        return Input.mousePosition;
    }

    bool GetAppendMode()
    {
        if (inputSource != null)
            return inputSource.Current.Shift;

        if (Keyboard.current == null)
            return false;

        return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
    }

    static string GetSlotHotkeyText(int slotIndex, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        switch (slotIndex)
        {
            case 0: return "Q";
            case 1: return "W";
            case 2: return "E";
            case 3: return "R";
            case 4: return "A";
            case 5: return "S";
            case 6: return "D";
            case 7: return "F";
            case 8: return "Z";
            case 9: return "X";
            case 10: return "C";
            case 11: return "V";
            default: return fallback;
        }
    }
}
