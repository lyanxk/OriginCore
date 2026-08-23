using System;
using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.Visibility
{
    public enum VisibilityCellState : byte
    {
        Hidden = 0,
        Explored = 1,
        Visible = 2
    }

    public sealed class VisibilityGrid
    {
        private readonly VisibilityCellState[] _cells;

        public VisibilityGrid(Bounds worldBounds, float cellSize)
        {
            CellSize = Mathf.Max(0.1f, cellSize);
            WorldBounds = worldBounds;
            Width = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.x / CellSize));
            Height = Mathf.Max(1, Mathf.CeilToInt(worldBounds.size.z / CellSize));
            _cells = new VisibilityCellState[Width * Height];
        }

        public Bounds WorldBounds { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => _cells.Length;

        public VisibilityCellState GetState(int x, int z)
        {
            return IsValidCell(x, z)
                ? _cells[ToIndex(x, z)]
                : VisibilityCellState.Hidden;
        }

        public VisibilityCellState GetState(Vector3 worldPosition)
        {
            return TryWorldToCell(worldPosition, out int x, out int z)
                ? GetState(x, z)
                : VisibilityCellState.Hidden;
        }

        public bool TryWorldToCell(Vector3 worldPosition, out int x, out int z)
        {
            x = Mathf.FloorToInt((worldPosition.x - WorldBounds.min.x) / CellSize);
            z = Mathf.FloorToInt((worldPosition.z - WorldBounds.min.z) / CellSize);

            if (Mathf.Approximately(worldPosition.x, WorldBounds.max.x))
            {
                x = Width - 1;
            }

            if (Mathf.Approximately(worldPosition.z, WorldBounds.max.z))
            {
                z = Height - 1;
            }

            return IsValidCell(x, z) &&
                   worldPosition.x >= WorldBounds.min.x &&
                   worldPosition.x <= WorldBounds.max.x &&
                   worldPosition.z >= WorldBounds.min.z &&
                   worldPosition.z <= WorldBounds.max.z;
        }

        public Vector3 CellToWorldCenter(int x, int z)
        {
            if (!IsValidCell(x, z))
            {
                return WorldBounds.center;
            }

            float worldX = Mathf.Min(
                WorldBounds.max.x,
                WorldBounds.min.x + (x + 0.5f) * CellSize);
            float worldZ = Mathf.Min(
                WorldBounds.max.z,
                WorldBounds.min.z + (z + 0.5f) * CellSize);
            return new Vector3(worldX, WorldBounds.center.y, worldZ);
        }

        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
        }

        public void ClearVisible()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == VisibilityCellState.Visible)
                {
                    _cells[i] = VisibilityCellState.Explored;
                }
            }
        }

        public int PromoteExploredToVisible()
        {
            int changed = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != VisibilityCellState.Explored)
                {
                    continue;
                }

                _cells[i] = VisibilityCellState.Visible;
                changed++;
            }

            return changed;
        }

        public int MarkVisibleCircle(Vector3 worldPosition, float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            if (!TryWorldToCell(worldPosition, out int centerX, out int centerZ))
            {
                return 0;
            }

            if (safeRadius <= 0f)
            {
                return SetVisible(centerX, centerZ) ? 1 : 0;
            }

            int cellRadius = Mathf.CeilToInt(safeRadius / CellSize);
            int minX = Mathf.Max(0, centerX - cellRadius);
            int maxX = Mathf.Min(Width - 1, centerX + cellRadius);
            int minZ = Mathf.Max(0, centerZ - cellRadius);
            int maxZ = Mathf.Min(Height - 1, centerZ + cellRadius);
            float expandedRadius = safeRadius + CellSize * 0.5f;
            float radiusSquared = expandedRadius * expandedRadius;
            int changed = 0;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 cellCenter = CellToWorldCenter(x, z);
                    float deltaX = cellCenter.x - worldPosition.x;
                    float deltaZ = cellCenter.z - worldPosition.z;
                    if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared &&
                        SetVisible(x, z))
                    {
                        changed++;
                    }
                }
            }

            return changed;
        }

        public bool MarkVisibleCell(int x, int z)
        {
            return IsValidCell(x, z) && SetVisible(x, z);
        }

        public void CaptureExploredCellIds(List<string> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (GetState(x, z) != VisibilityCellState.Hidden)
                    {
                        destination.Add(x + ":" + z);
                    }
                }
            }
        }

        public int RestoreExploredCellIds(IReadOnlyList<string> cellIds)
        {
            Clear();
            if (cellIds == null)
            {
                return 0;
            }

            int restored = 0;
            for (int i = 0; i < cellIds.Count; i++)
            {
                if (!TryParseCellId(cellIds[i], out int x, out int z) ||
                    !IsValidCell(x, z))
                {
                    continue;
                }

                int index = ToIndex(x, z);
                if (_cells[index] == VisibilityCellState.Hidden)
                {
                    _cells[index] = VisibilityCellState.Explored;
                    restored++;
                }
            }

            return restored;
        }

        private bool SetVisible(int x, int z)
        {
            int index = ToIndex(x, z);
            if (_cells[index] == VisibilityCellState.Visible)
            {
                return false;
            }

            _cells[index] = VisibilityCellState.Visible;
            return true;
        }

        private bool IsValidCell(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        private int ToIndex(int x, int z)
        {
            return z * Width + x;
        }

        private static bool TryParseCellId(string value, out int x, out int z)
        {
            x = 0;
            z = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            int separator = value.IndexOf(':');
            return separator > 0 && separator < value.Length - 1 &&
                   int.TryParse(value.Substring(0, separator), out x) &&
                   int.TryParse(value.Substring(separator + 1), out z);
        }
    }
}
