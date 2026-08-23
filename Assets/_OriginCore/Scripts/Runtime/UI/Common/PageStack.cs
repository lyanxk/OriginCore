using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class PageStack : MonoBehaviour
    {
        [Serializable]
        private sealed class PageEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private GameObject _root;

            public PageEntry(string id, GameObject root)
            {
                _id = id;
                _root = root;
            }

            public string Id => _id ?? string.Empty;
            public GameObject Root => _root;
        }

        [SerializeField] private string _initialPageId = string.Empty;
        [SerializeField] private PageEntry[] _pages = Array.Empty<PageEntry>();

        private readonly List<string> _stack = new List<string>();

        public event Action<string> PageChanged;

        public string CurrentPageId => _stack.Count > 0
            ? _stack[_stack.Count - 1]
            : string.Empty;
        public int Depth => _stack.Count;

        private void OnEnable()
        {
            if (_stack.Count == 0 && !string.IsNullOrWhiteSpace(_initialPageId))
            {
                ResetTo(_initialPageId);
            }
            else
            {
                ApplyVisibility();
            }
        }

        public void Configure(string initialPageId, string[] pageIds, GameObject[] pageRoots)
        {
            if (pageIds == null || pageRoots == null || pageIds.Length != pageRoots.Length)
            {
                throw new ArgumentException("Page ids and roots must be non-null and have equal lengths.");
            }

            _initialPageId = initialPageId ?? string.Empty;
            _pages = new PageEntry[pageIds.Length];
            for (int i = 0; i < pageIds.Length; i++)
            {
                _pages[i] = new PageEntry(pageIds[i], pageRoots[i]);
            }

            _stack.Clear();
            if (!string.IsNullOrWhiteSpace(_initialPageId))
            {
                ResetTo(_initialPageId);
            }
            else
            {
                ApplyVisibility();
            }
        }

        public bool Push(string pageId)
        {
            if (!TryFindPage(pageId, out _))
            {
                return false;
            }

            if (string.Equals(CurrentPageId, pageId, StringComparison.Ordinal))
            {
                ApplyVisibility();
                return false;
            }

            _stack.Add(pageId);
            ApplyVisibility();
            PageChanged?.Invoke(CurrentPageId);
            return true;
        }

        public bool Pop()
        {
            if (_stack.Count <= 1)
            {
                return false;
            }

            _stack.RemoveAt(_stack.Count - 1);
            ApplyVisibility();
            PageChanged?.Invoke(CurrentPageId);
            return true;
        }

        public bool ResetTo(string pageId)
        {
            if (!TryFindPage(pageId, out _))
            {
                return false;
            }

            bool changed = _stack.Count != 1 ||
                           !string.Equals(CurrentPageId, pageId, StringComparison.Ordinal);
            _stack.Clear();
            _stack.Add(pageId);
            ApplyVisibility();
            if (changed)
            {
                PageChanged?.Invoke(CurrentPageId);
            }

            return changed;
        }

        public bool IsShowing(string pageId)
        {
            return string.Equals(CurrentPageId, pageId, StringComparison.Ordinal);
        }

        private void ApplyVisibility()
        {
            string current = CurrentPageId;
            for (int i = 0; i < _pages.Length; i++)
            {
                PageEntry page = _pages[i];
                if (page?.Root == null)
                {
                    continue;
                }

                bool active = string.Equals(page.Id, current, StringComparison.Ordinal);
                if (page.Root.activeSelf != active)
                {
                    page.Root.SetActive(active);
                }
            }
        }

        private bool TryFindPage(string pageId, out GameObject root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(pageId))
            {
                return false;
            }

            for (int i = 0; i < _pages.Length; i++)
            {
                PageEntry page = _pages[i];
                if (page != null &&
                    string.Equals(page.Id, pageId, StringComparison.Ordinal) &&
                    page.Root != null)
                {
                    root = page.Root;
                    return true;
                }
            }

            return false;
        }
    }
}
