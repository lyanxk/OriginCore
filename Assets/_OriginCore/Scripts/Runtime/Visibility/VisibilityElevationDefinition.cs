using System;
using UnityEngine;

namespace OriginCore.Visibility
{
    [Flags]
    public enum VisibilityCliffEdge : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    [CreateAssetMenu(
        fileName = "SO_VisibilityElevation_New",
        menuName = "OriginCore/Visibility/Elevation Definition")]
    public sealed class VisibilityElevationDefinition : ScriptableObject
    {
        [SerializeField] private string _revision = "flat-compat-1";
        [SerializeField] private bool _flatCompatibility;
        [SerializeField] private bool _sampleSceneGeometry;
        [Min(0.1f), SerializeField] private float _sampledHeightBandStep = 3f;
        [SerializeField] private LayerMask _samplingMask = Physics.DefaultRaycastLayers;
        [SerializeField] private Bounds _worldBounds =
            new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
        [Min(0.1f), SerializeField] private float _cellSize = 2f;
        [Min(1), SerializeField] private int _width = 50;
        [Min(1), SerializeField] private int _height = 50;
        [SerializeField] private float[] _groundHeight = Array.Empty<float>();
        [SerializeField] private int[] _heightBand = Array.Empty<int>();
        [SerializeField] private float[] _occluderHeight = Array.Empty<float>();
        [SerializeField] private byte[] _cliffMask = Array.Empty<byte>();

        public string Revision => string.IsNullOrWhiteSpace(_revision)
            ? "unversioned"
            : _revision.Trim();
        public Bounds WorldBounds => _worldBounds;
        public float CellSize => Mathf.Max(0.1f, _cellSize);
        public int Width => Mathf.Max(1, _width);
        public int Height => Mathf.Max(1, _height);
        public bool IsFlatCompatibility => _flatCompatibility;
        public bool SamplesSceneGeometry => _sampleSceneGeometry;
        public float SampledHeightBandStep => Mathf.Max(0.1f, _sampledHeightBandStep);
        public LayerMask SamplingMask => _samplingMask;

        public bool IsCompatible(VisibilityGrid grid, out string error)
        {
            if (grid == null)
            {
                error = "Visibility grid is missing.";
                return false;
            }
            if (_flatCompatibility || _sampleSceneGeometry)
            {
                error = string.Empty;
                return true;
            }
            if (Width != grid.Width || Height != grid.Height ||
                !Mathf.Approximately(CellSize, grid.CellSize) ||
                !Approximately(_worldBounds, grid.WorldBounds))
            {
                error = "Elevation bounds, cell size or dimensions do not match the visibility grid.";
                return false;
            }
            int count = Width * Height;
            if ((_groundHeight?.Length ?? 0) != count ||
                (_heightBand?.Length ?? 0) != count ||
                (_occluderHeight?.Length ?? 0) != count ||
                (_cliffMask?.Length ?? 0) != count)
            {
                error = "Elevation arrays must contain exactly width * height cells.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public float GetGroundHeight(int x, int z) =>
            TryIndex(x, z, out int index) ? _groundHeight[index] : _worldBounds.min.y;

        public int GetHeightBand(int x, int z) =>
            TryIndex(x, z, out int index) ? _heightBand[index] : 0;

        public float GetOccluderTop(int x, int z) =>
            TryIndex(x, z, out int index)
                ? Mathf.Max(_groundHeight[index], _occluderHeight[index])
                : _worldBounds.min.y;

        public VisibilityCliffEdge GetCliffMask(int x, int z) =>
            TryIndex(x, z, out int index)
                ? (VisibilityCliffEdge)_cliffMask[index]
                : VisibilityCliffEdge.None;

        public void ConfigureFlat(Bounds bounds, float cellSize, string revision)
        {
            _worldBounds = bounds;
            _cellSize = Mathf.Max(0.1f, cellSize);
            _width = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / _cellSize));
            _height = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / _cellSize));
            _revision = string.IsNullOrWhiteSpace(revision) ? "flat-compat-1" : revision.Trim();
            _flatCompatibility = true;
            int count = _width * _height;
            _groundHeight = new float[count];
            _heightBand = new int[count];
            _occluderHeight = new float[count];
            _cliffMask = new byte[count];
            for (int i = 0; i < count; i++)
            {
                _groundHeight[i] = bounds.min.y;
                _occluderHeight[i] = bounds.min.y;
            }
        }

        private bool TryIndex(int x, int z, out int index)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height)
            {
                index = -1;
                return false;
            }
            index = z * Width + x;
            return true;
        }

        private static bool Approximately(Bounds left, Bounds right)
        {
            return (left.center - right.center).sqrMagnitude <= 0.0001f &&
                   (left.size - right.size).sqrMagnitude <= 0.0001f;
        }
    }
}
