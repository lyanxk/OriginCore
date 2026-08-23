using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriginCore.SceneFlow
{
    [DisallowMultipleComponent]
    public sealed class WorldBoundaryWalls : MonoBehaviour
    {
        private const string RootName = "WorldBoundaryWalls";
        private const float DefaultThickness = 2f;
        private const float DefaultVerticalPadding = 100f;

        private readonly BoxCollider[] _walls = new BoxCollider[4];

        public Bounds WorldBounds { get; private set; }
        public bool IsConfigured { get; private set; }
        public int WallCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _walls.Length; i++)
                {
                    if (_walls[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static WorldBoundaryWalls EnsureForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                WorldBoundaryWalls existing =
                    roots[i].GetComponent<WorldBoundaryWalls>();
                if (existing != null)
                {
                    return existing;
                }
            }

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<WorldBoundaryWalls>();
        }

        public void Configure(
            Bounds worldBounds,
            float thickness = DefaultThickness,
            float verticalPadding = DefaultVerticalPadding)
        {
            thickness = Mathf.Max(0.1f, thickness);
            verticalPadding = Mathf.Max(0f, verticalPadding);
            Vector3 size = worldBounds.size;
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            size.z = Mathf.Max(0.1f, size.z);
            worldBounds.size = size;

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            WorldBounds = worldBounds;

            float wallHeight = size.y + verticalPadding * 2f;
            float wallY = worldBounds.center.y;
            ConfigureWall(
                0,
                "West",
                new Vector3(
                    worldBounds.min.x - thickness * 0.5f,
                    wallY,
                    worldBounds.center.z),
                new Vector3(thickness, wallHeight, size.z + thickness * 2f));
            ConfigureWall(
                1,
                "East",
                new Vector3(
                    worldBounds.max.x + thickness * 0.5f,
                    wallY,
                    worldBounds.center.z),
                new Vector3(thickness, wallHeight, size.z + thickness * 2f));
            ConfigureWall(
                2,
                "South",
                new Vector3(
                    worldBounds.center.x,
                    wallY,
                    worldBounds.min.z - thickness * 0.5f),
                new Vector3(size.x + thickness * 2f, wallHeight, thickness));
            ConfigureWall(
                3,
                "North",
                new Vector3(
                    worldBounds.center.x,
                    wallY,
                    worldBounds.max.z + thickness * 0.5f),
                new Vector3(size.x + thickness * 2f, wallHeight, thickness));

            IsConfigured = true;
        }

        public bool ContainsPlanar(Vector3 worldPosition)
        {
            return IsConfigured &&
                   worldPosition.x >= WorldBounds.min.x &&
                   worldPosition.x <= WorldBounds.max.x &&
                   worldPosition.z >= WorldBounds.min.z &&
                   worldPosition.z <= WorldBounds.max.z;
        }

        private void ConfigureWall(
            int index,
            string wallName,
            Vector3 center,
            Vector3 size)
        {
            BoxCollider wall = _walls[index];
            if (wall == null)
            {
                Transform existing = transform.Find(wallName);
                if (existing != null)
                {
                    wall = existing.GetComponent<BoxCollider>();
                }

                if (wall == null)
                {
                    GameObject child = new GameObject(wallName);
                    child.transform.SetParent(transform, false);
                    wall = child.AddComponent<BoxCollider>();
                }

                _walls[index] = wall;
            }

            wall.transform.SetPositionAndRotation(center, Quaternion.identity);
            wall.transform.localScale = Vector3.one;
            wall.center = Vector3.zero;
            wall.size = size;
            wall.isTrigger = false;
            wall.enabled = true;
        }
    }
}
