using OriginCore.Gameplay;
using UnityEngine;

namespace OriginCore.Visibility
{
    [CreateAssetMenu(
        fileName = "SO_VisibilityConfig",
        menuName = "OriginCore/Visibility/Visibility Config")]
    public sealed class VisibilityConfig : ScriptableObject
    {
        [Min(0.1f), SerializeField] private float _cellSize = 2f;
        [Range(1f, 30f), SerializeField] private float _updateHz = 5f;
        [Range(0f, 1f), SerializeField] private float _hiddenAlpha = 0.9f;
        [Range(0f, 1f), SerializeField] private float _exploredAlpha = 0.48f;
        [Range(0f, 1f), SerializeField] private float _visibleAlpha;
        [SerializeField] private FactionId _observerFaction = FactionId.Friendly;

        public float CellSize => _cellSize;
        public float UpdateHz => _updateHz;
        public float UpdateInterval => 1f / Mathf.Max(1f, _updateHz);
        public float HiddenAlpha => _hiddenAlpha;
        public float ExploredAlpha => _exploredAlpha;
        public float VisibleAlpha => _visibleAlpha;
        public FactionId ObserverFaction => _observerFaction;

        public void Configure(
            float cellSize,
            float updateHz,
            float hiddenAlpha,
            float exploredAlpha,
            float visibleAlpha,
            FactionId observerFaction)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
            _updateHz = Mathf.Clamp(updateHz, 1f, 30f);
            _hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
            _exploredAlpha = Mathf.Clamp01(exploredAlpha);
            _visibleAlpha = Mathf.Clamp01(visibleAlpha);
            _observerFaction = observerFaction;
        }

        private void OnValidate()
        {
            _cellSize = Mathf.Max(0.1f, _cellSize);
            _updateHz = Mathf.Clamp(_updateHz, 1f, 30f);
            _hiddenAlpha = Mathf.Clamp01(_hiddenAlpha);
            _exploredAlpha = Mathf.Clamp01(_exploredAlpha);
            _visibleAlpha = Mathf.Clamp01(_visibleAlpha);
        }
    }
}
