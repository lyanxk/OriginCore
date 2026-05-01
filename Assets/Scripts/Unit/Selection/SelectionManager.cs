using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unit.Selection
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        readonly HashSet<Selectable> _selectedSet = new HashSet<Selectable>();
        readonly List<Selectable> _selected = new List<Selectable>(32);
        readonly HashSet<Selectable> _allSelectables = new HashSet<Selectable>();

        public IReadOnlyList<Selectable> Selected => _selected;
        public IEnumerable<Selectable> AllSelectables => _allSelectables;
        public int SelectedCount => _selected.Count;
        public Selectable Primary { get; private set; }

        public event Action<IReadOnlyList<Selectable>, Selectable> OnSelectionChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Register(Selectable selectable)
        {
            if (selectable == null) return;
            _allSelectables.Add(selectable);
        }

        public void Unregister(Selectable selectable)
        {
            if (selectable == null) return;

            _allSelectables.Remove(selectable);

            bool changed = false;
            if (_selectedSet.Remove(selectable))
            {
                _selected.Remove(selectable);
                selectable.SetSelected(false);
                changed = true;
            }

            if (Primary == selectable)
            {
                Primary = _selected.Count > 0 ? _selected[_selected.Count - 1] : null;
                changed = true;
            }

            if (changed)
                NotifySelectionChanged();
        }

        public void ClearSelection()
        {
            if (_selected.Count == 0 && Primary == null)
                return;

            for (int i = 0; i < _selected.Count; i++)
            {
                Selectable selectable = _selected[i];
                if (selectable != null)
                    selectable.SetSelected(false);
            }

            _selectedSet.Clear();
            _selected.Clear();
            Primary = null;

            NotifySelectionChanged();
        }

        public void AddSelection(Selectable selectable)
        {
            if (selectable == null) return;

            bool changed = false;
            if (_selectedSet.Add(selectable))
            {
                _selected.Add(selectable);
                selectable.SetSelected(true);
                changed = true;
            }

            if (Primary != selectable)
            {
                Primary = selectable;
                changed = true;
            }

            if (changed)
                NotifySelectionChanged();
        }

        public void RemoveSelection(Selectable selectable)
        {
            if (selectable == null) return;

            bool changed = false;
            if (_selectedSet.Remove(selectable))
            {
                _selected.Remove(selectable);
                selectable.SetSelected(false);
                changed = true;
            }

            if (Primary == selectable)
            {
                Primary = _selected.Count > 0 ? _selected[_selected.Count - 1] : null;
                changed = true;
            }

            if (changed)
                NotifySelectionChanged();
        }

        public void SetPrimary(Selectable selectable)
        {
            if (selectable == null)
            {
                if (Primary == null) return;
                Primary = null;
                NotifySelectionChanged();
                return;
            }

            bool changed = false;
            if (_selectedSet.Add(selectable))
            {
                _selected.Add(selectable);
                selectable.SetSelected(true);
                changed = true;
            }

            if (Primary != selectable)
            {
                Primary = selectable;
                changed = true;
            }

            if (changed)
                NotifySelectionChanged();
        }

        public bool IsSelected(Selectable selectable)
        {
            return selectable != null && _selectedSet.Contains(selectable);
        }

        void NotifySelectionChanged()
        {
            OnSelectionChanged?.Invoke(Selected, Primary);
        }
    }
}
