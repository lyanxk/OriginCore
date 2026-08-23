using System;
using UnityEngine;

namespace OriginCore.Debugging
{
    public readonly struct DebugOverlaySnapshot
    {
        public DebugOverlaySnapshot(
            string mode,
            int selectedCount,
            int queuedCommandCount,
            int resourceValue)
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? "None" : mode;
            SelectedCount = Mathf.Max(0, selectedCount);
            QueuedCommandCount = Mathf.Max(0, queuedCommandCount);
            ResourceValue = Mathf.Max(0, resourceValue);
        }

        public string Mode { get; }
        public int SelectedCount { get; }
        public int QueuedCommandCount { get; }
        public int ResourceValue { get; }
    }

    public interface IDebugOverlayService
    {
        bool IsAvailable { get; }
        bool IsVisible { get; }
        DebugOverlaySnapshot Snapshot { get; }
        event Action<DebugOverlaySnapshot> SnapshotChanged;
        void SetVisible(bool visible);
        void SetSnapshot(DebugOverlaySnapshot snapshot);
    }

    [DisallowMultipleComponent]
    public sealed class DebugOverlayService : MonoBehaviour, IDebugOverlayService
    {
        [SerializeField] private bool _visible;

        private DebugOverlaySnapshot _snapshot = new DebugOverlaySnapshot("None", 0, 0, 0);

        public bool IsAvailable => Debug.isDebugBuild || Application.isEditor;
        public bool IsVisible => IsAvailable && _visible;
        public DebugOverlaySnapshot Snapshot => _snapshot;

        public event Action<DebugOverlaySnapshot> SnapshotChanged;

        public void SetVisible(bool visible)
        {
            _visible = IsAvailable && visible;
        }

        public void SetSnapshot(DebugOverlaySnapshot snapshot)
        {
            _snapshot = snapshot;
            SnapshotChanged?.Invoke(_snapshot);
        }
    }
}
