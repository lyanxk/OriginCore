using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    // 当前选中集合
    readonly HashSet<Selectable> _selected = new();

    // 可被选择的单位集合（用于框选遍历）
    readonly HashSet<Selectable> _allSelectables = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // --- 供 Selectable 自动注册 ---
    public void Register(Selectable s)
    {
        if (s != null) _allSelectables.Add(s);
    }

    public void Unregister(Selectable s)
    {
        if (s == null) return;

        _allSelectables.Remove(s);

        // 如果正在选中，取消
        if (_selected.Remove(s))
            s.SetSelected(false);
    }

    // --- 对外 API：选中控制 ---
    public void ClearSelection()
    {
        foreach (var s in _selected)
        {
            if (s != null) s.SetSelected(false);
        }
        _selected.Clear();
    }

    public void AddSelection(Selectable s)
    {
        if (s == null) return;
        if (_selected.Add(s))
            s.SetSelected(true);
    }

    public void RemoveSelection(Selectable s)
    {
        if (s == null) return;
        if (_selected.Remove(s))
            s.SetSelected(false);
    }

    public bool IsSelected(Selectable s) => s != null && _selected.Contains(s);

    // --- 给框选用：遍历所有可选对象 ---
    public IEnumerable<Selectable> AllSelectables => _allSelectables;

    // --- 给命令系统用：遍历当前选中 ---
    public IEnumerable<Selectable> Selected => _selected;

    public int SelectedCount => _selected.Count;
}