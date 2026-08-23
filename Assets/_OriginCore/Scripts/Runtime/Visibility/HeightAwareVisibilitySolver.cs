using UnityEngine;

namespace OriginCore.Visibility
{
    public sealed class HeightAwareVisibilitySolver
    {
        private VisibilityElevationDefinition _elevation;
        private bool _compatible;
        private float[] _ground;
        private int[] _bands;
        private float[] _occluders;
        private byte[] _cliffs;
        private int _width;
        private int _height;
        private readonly RaycastHit[] _sampleHits = new RaycastHit[16];

        public VisibilityElevationDefinition Elevation => _elevation;
        public bool IsHeightAware => _compatible &&
                                     _elevation != null &&
                                     (!_elevation.IsFlatCompatibility ||
                                      _elevation.SamplesSceneGeometry);

        public bool Configure(
            VisibilityGrid grid,
            VisibilityElevationDefinition elevation,
            out string error)
        {
            _elevation = elevation;
            if (elevation == null)
            {
                error = "No elevation definition; flat compatibility visibility is active.";
                _compatible = false;
                return false;
            }
            _compatible = elevation.IsCompatible(grid, out error);
            if (_compatible)
            {
                BuildRuntimeData(grid);
            }
            return _compatible;
        }

        public int MarkVisible(VisibilityGrid grid, VisionEmitter emitter)
        {
            if (grid == null || emitter == null)
            {
                return 0;
            }
            if (!_compatible)
            {
                return grid.MarkVisibleCircle(emitter.transform.position, emitter.VisionRange);
            }
            if (!grid.TryWorldToCell(emitter.transform.position, out int originX, out int originZ))
            {
                return 0;
            }

            float radius = Mathf.Max(0f, emitter.VisionRange);
            int cellRadius = Mathf.CeilToInt(radius / grid.CellSize);
            int minX = Mathf.Max(0, originX - cellRadius);
            int maxX = Mathf.Min(grid.Width - 1, originX + cellRadius);
            int minZ = Mathf.Max(0, originZ - cellRadius);
            int maxZ = Mathf.Min(grid.Height - 1, originZ + cellRadius);
            float expandedRadius = radius + grid.CellSize * 0.5f;
            float radiusSquared = expandedRadius * expandedRadius;
            int changed = 0;
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 center = grid.CellToWorldCenter(x, z);
                    float dx = center.x - emitter.transform.position.x;
                    float dz = center.z - emitter.transform.position.z;
                    if (dx * dx + dz * dz > radiusSquared ||
                        !HasLineOfSight(originX, originZ, x, z, emitter))
                    {
                        continue;
                    }
                    if (grid.MarkVisibleCell(x, z))
                    {
                        changed++;
                    }
                }
            }
            return changed;
        }

        private bool HasLineOfSight(
            int originX,
            int originZ,
            int targetX,
            int targetZ,
            VisionEmitter emitter)
        {
            if (originX == targetX && originZ == targetZ)
            {
                return true;
            }
            int originBand = GetBand(originX, originZ);
            int targetBand = GetBand(targetX, targetZ);
            float eye = GetGround(originX, originZ) + emitter.EyeHeight;
            float targetHeight = GetGround(targetX, targetZ) +
                                 emitter.HeightAdvantage;
            int deltaX = targetX - originX;
            int deltaZ = targetZ - originZ;
            int steps = Mathf.Max(Mathf.Abs(deltaX), Mathf.Abs(deltaZ));
            int previousX = originX;
            int previousZ = originZ;
            for (int step = 1; step < steps; step++)
            {
                float t = step / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(originX, targetX, t));
                int z = Mathf.RoundToInt(Mathf.Lerp(originZ, targetZ, t));
                if (!emitter.IgnoresCliffs && targetBand > originBand &&
                    CrossesBlockingCliff(previousX, previousZ, x, z))
                {
                    return false;
                }
                float sightHeight = Mathf.Lerp(eye, targetHeight, t);
                if (GetOccluder(x, z) > sightHeight + 0.01f)
                {
                    return false;
                }
                previousX = x;
                previousZ = z;
            }
            if (!emitter.IgnoresCliffs && targetBand > originBand &&
                CrossesBlockingCliff(previousX, previousZ, targetX, targetZ))
            {
                return false;
            }
            return true;
        }

        private bool CrossesBlockingCliff(int fromX, int fromZ, int toX, int toZ)
        {
            VisibilityCliffEdge from = (VisibilityCliffEdge)GetCliff(fromX, fromZ);
            VisibilityCliffEdge to = (VisibilityCliffEdge)GetCliff(toX, toZ);
            if (toX > fromX && ((from & VisibilityCliffEdge.East) != 0 ||
                               (to & VisibilityCliffEdge.West) != 0)) return true;
            if (toX < fromX && ((from & VisibilityCliffEdge.West) != 0 ||
                               (to & VisibilityCliffEdge.East) != 0)) return true;
            if (toZ > fromZ && ((from & VisibilityCliffEdge.North) != 0 ||
                               (to & VisibilityCliffEdge.South) != 0)) return true;
            if (toZ < fromZ && ((from & VisibilityCliffEdge.South) != 0 ||
                               (to & VisibilityCliffEdge.North) != 0)) return true;
            return false;
        }

        private void BuildRuntimeData(VisibilityGrid grid)
        {
            _width = grid.Width;
            _height = grid.Height;
            int count = _width * _height;
            _ground = new float[count];
            _bands = new int[count];
            _occluders = new float[count];
            _cliffs = new byte[count];
            if (_elevation.SamplesSceneGeometry)
            {
                SampleSceneGeometry(grid);
                return;
            }
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = z * _width + x;
                    _ground[index] = _elevation.GetGroundHeight(x, z);
                    _bands[index] = _elevation.GetHeightBand(x, z);
                    _occluders[index] = _elevation.GetOccluderTop(x, z);
                    _cliffs[index] = (byte)_elevation.GetCliffMask(x, z);
                }
            }
        }

        private void SampleSceneGeometry(VisibilityGrid grid)
        {
            Bounds bounds = grid.WorldBounds;
            float originY = bounds.max.y + Mathf.Max(10f, bounds.size.y);
            float rayDistance = Mathf.Max(20f, bounds.size.y * 3f);
            float minimum = float.PositiveInfinity;
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 center = grid.CellToWorldCenter(x, z);
                    Vector3 origin = new Vector3(center.x, originY, center.z);
                    int hitCount = Physics.RaycastNonAlloc(
                        origin, Vector3.down, _sampleHits, rayDistance,
                        _elevation.SamplingMask, QueryTriggerInteraction.Ignore);
                    float ground = bounds.min.y;
                    for (int i = 0; i < hitCount; i++)
                    {
                        Collider collider = _sampleHits[i].collider;
                        if (collider == null ||
                            collider.GetComponentInParent<OriginCore.Gameplay.EntityIdentity>() != null)
                        {
                            continue;
                        }
                        ground = Mathf.Max(ground, _sampleHits[i].point.y);
                    }
                    int index = z * _width + x;
                    _ground[index] = ground;
                    _occluders[index] = ground;
                    minimum = Mathf.Min(minimum, ground);
                }
            }
            if (float.IsInfinity(minimum)) minimum = bounds.min.y;
            float bandStep = _elevation.SampledHeightBandStep;
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = z * _width + x;
                    _bands[index] = Mathf.Max(0,
                        Mathf.FloorToInt((_ground[index] - minimum) / bandStep + 0.1f));
                }
            }
            BuildSampledCliffs();
        }

        private void BuildSampledCliffs()
        {
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int band = GetBand(x, z);
                    byte mask = 0;
                    if (z + 1 < _height && GetBand(x, z + 1) != band)
                        mask |= (byte)VisibilityCliffEdge.North;
                    if (x + 1 < _width && GetBand(x + 1, z) != band)
                        mask |= (byte)VisibilityCliffEdge.East;
                    if (z > 0 && GetBand(x, z - 1) != band)
                        mask |= (byte)VisibilityCliffEdge.South;
                    if (x > 0 && GetBand(x - 1, z) != band)
                        mask |= (byte)VisibilityCliffEdge.West;
                    _cliffs[z * _width + x] = mask;
                }
            }
        }

        private int Index(int x, int z) => z * _width + x;
        private float GetGround(int x, int z) => _ground[Index(x, z)];
        private int GetBand(int x, int z) => _bands[Index(x, z)];
        private float GetOccluder(int x, int z) => _occluders[Index(x, z)];
        private byte GetCliff(int x, int z) => _cliffs[Index(x, z)];
    }
}
