using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.UI.Common
{
    [DisallowMultipleComponent]
    public sealed class ModalStack : MonoBehaviour
    {
        [Serializable]
        private sealed class ModalEntry
        {
            [SerializeField] private string _id;
            [SerializeField] private GameObject _root;

            public ModalEntry(string id, GameObject root)
            {
                _id = id;
                _root = root;
            }

            public string Id => _id ?? string.Empty;
            public GameObject Root => _root;
        }

        [SerializeField] private ModalEntry[] _modals = Array.Empty<ModalEntry>();

        private readonly List<string> _stack = new List<string>();

        public event Action<string> TopModalChanged;

        public string TopModalId => _stack.Count > 0
            ? _stack[_stack.Count - 1]
            : string.Empty;
        public int Depth => _stack.Count;
        public int LastClosedFrame { get; private set; } = -1;

        private void OnEnable()
        {
            ApplyVisibility();
        }

        public void Configure(string[] modalIds, GameObject[] modalRoots)
        {
            if (modalIds == null || modalRoots == null || modalIds.Length != modalRoots.Length)
            {
                throw new ArgumentException("Modal ids and roots must be non-null and have equal lengths.");
            }

            _modals = new ModalEntry[modalIds.Length];
            for (int i = 0; i < modalIds.Length; i++)
            {
                _modals[i] = new ModalEntry(modalIds[i], modalRoots[i]);
            }

            _stack.Clear();
            ApplyVisibility();
        }

        public bool Show(string modalId)
        {
            if (!TryFindModal(modalId, out _))
            {
                return false;
            }

            _stack.Remove(modalId);
            _stack.Add(modalId);
            ApplyVisibility();
            TopModalChanged?.Invoke(TopModalId);
            return true;
        }

        public bool CloseTop()
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            _stack.RemoveAt(_stack.Count - 1);
            LastClosedFrame = Time.frameCount;
            ApplyVisibility();
            TopModalChanged?.Invoke(TopModalId);
            return true;
        }

        public bool CloseAll()
        {
            if (_stack.Count == 0)
            {
                ApplyVisibility();
                return false;
            }

            _stack.Clear();
            LastClosedFrame = Time.frameCount;
            ApplyVisibility();
            TopModalChanged?.Invoke(string.Empty);
            return true;
        }

        public bool Contains(string modalId)
        {
            return _stack.Contains(modalId);
        }

        private void ApplyVisibility()
        {
            string current = TopModalId;
            for (int i = 0; i < _modals.Length; i++)
            {
                ModalEntry modal = _modals[i];
                if (modal?.Root == null)
                {
                    continue;
                }

                bool active = string.Equals(modal.Id, current, StringComparison.Ordinal);
                if (modal.Root.activeSelf != active)
                {
                    modal.Root.SetActive(active);
                }
            }
        }

        private bool TryFindModal(string modalId, out GameObject root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(modalId))
            {
                return false;
            }

            for (int i = 0; i < _modals.Length; i++)
            {
                ModalEntry modal = _modals[i];
                if (modal != null &&
                    string.Equals(modal.Id, modalId, StringComparison.Ordinal) &&
                    modal.Root != null)
                {
                    root = modal.Root;
                    return true;
                }
            }

            return false;
        }
    }
}
