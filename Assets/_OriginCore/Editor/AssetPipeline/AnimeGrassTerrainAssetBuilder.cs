using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OriginCore.Editor.AssetPipeline
{
    /// <summary>
    /// Rebuilds the normalized 4x4 m terrain prefabs from the preserved AI source models.
    /// This intentionally has no menu item; invoke Build from an editor script or automation.
    /// </summary>
    public static class AnimeGrassTerrainAssetBuilder
    {
        private const string SourceFolder =
            "Assets/_OriginCore/Art/Models/Environment/Terrain/AnimeGrass/SourceModels";
        private const string CollisionFolder =
            "Assets/_OriginCore/Art/Models/Environment/Terrain/AnimeGrass/Collision";
        private const string PrefabFolder =
            "Assets/_OriginCore/Prefabs/Environment/Terrain/AnimeGrass";
        private const string MaterialPath =
            "Assets/_OriginCore/Art/Materials/Environment/Terrain/AnimeGrass/MAT_Terrain_AnimeGrass.mat";

        private sealed class ModuleSpec
        {
            public string Key;
            public string Source;
            public float Height;
            public float Rotation;
            public ModuleShape Shape;
        }

        private enum ModuleShape
        {
            Flat,
            RampStraight,
            Plateau,
            CliffStraight,
            CliffOuterCorner,
            CliffInnerCorner
        }

        public static string Build()
        {
            EnsureFolder(CollisionFolder);
            EnsureFolder(PrefabFolder);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
                return $"ERROR: Missing terrain material at {MaterialPath}.";

            ModuleSpec[] specs =
            {
                Spec("Flat", "SM_Terrain_Grass_Flat_4x4.fbx", 0.35f, 0f, ModuleShape.Flat),
                Spec("Ramp_Straight", "SM_Terrain_Grass_Ramp_Straight_4x4.fbx", 1.35f, 180f,
                    ModuleShape.RampStraight),
                Spec("Plateau", "SM_Terrain_Grass_Plateau_4x4.fbx", 1.35f, 0f, ModuleShape.Plateau),
                Spec("Cliff_Straight", "SM_Terrain_Grass_Cliff_Straight_4x4.fbx", 1.35f, -90f,
                    ModuleShape.CliffStraight),
                Spec("Cliff_OuterCorner", "SM_Terrain_Grass_Cliff_OuterCorner_4x4.fbx", 1.35f, 0f,
                    ModuleShape.CliffOuterCorner),
                Spec("Cliff_InnerCorner", "SM_Terrain_Grass_Cliff_InnerCorner_4x4.fbx", 1.35f, 180f,
                    ModuleShape.CliffInnerCorner)
            };

            var report = new StringBuilder();
            foreach (ModuleSpec spec in specs)
                BuildModule(spec, material, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        private static ModuleSpec Spec(
            string key,
            string source,
            float height,
            float rotation,
            ModuleShape shape)
        {
            return new ModuleSpec
            {
                Key = key,
                Source = source,
                Height = height,
                Rotation = rotation,
                Shape = shape
            };
        }

        private static void BuildModule(ModuleSpec spec, Material material, StringBuilder report)
        {
            string sourcePath = $"{SourceFolder}/{spec.Source}";
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceAsset == null)
            {
                report.AppendLine($"MISSING {sourcePath}");
                return;
            }

            string meshName = $"SM_Terrain_Grass_{spec.Key}_Collision_4x4";
            string meshPath = $"{CollisionFolder}/{meshName}.asset";
            Mesh collisionMesh = BuildCollisionMesh(spec.Shape, meshName);
            ReplaceAsset(collisionMesh, meshPath);

            var root = new GameObject($"PF_Terrain_Grass_{spec.Key}_4x4") { layer = 9 };
            try
            {
                Transform fit = new GameObject("Visual").transform;
                fit.SetParent(root.transform, false);

                Transform orientation = new GameObject("Orientation").transform;
                orientation.SetParent(fit, false);
                orientation.localRotation = Quaternion.Euler(0f, spec.Rotation, 0f);

                var sourceInstance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
                if (sourceInstance == null)
                    throw new UnityException($"Could not instantiate {sourcePath}.");

                sourceInstance.name = "SourceModel";
                sourceInstance.transform.SetParent(orientation, false);
                sourceInstance.transform.localPosition = Vector3.zero;
                sourceInstance.transform.localRotation = Quaternion.identity;
                sourceInstance.transform.localScale = Vector3.one;

                Renderer[] visualRenderers = sourceInstance.GetComponentsInChildren<Renderer>(true);
                if (visualRenderers.Length == 0)
                    throw new UnityException($"No renderer found in {sourcePath}.");

                foreach (Renderer renderer in visualRenderers)
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                }

                Bounds before = CombinedBounds(visualRenderers);
                if (before.size.x < 0.0001f || before.size.y < 0.0001f || before.size.z < 0.0001f)
                    throw new UnityException($"Invalid renderer bounds in {sourcePath}: {before.size}.");

                fit.localScale = new Vector3(
                    4f / before.size.x,
                    spec.Height / before.size.y,
                    4f / before.size.z);
                Bounds afterScale = CombinedBounds(visualRenderers);
                fit.localPosition += new Vector3(0f, spec.Height * 0.5f, 0f) - afterScale.center;

                var simplified = new GameObject("SimplifiedMesh") { layer = 9 };
                simplified.transform.SetParent(root.transform, false);
                MeshFilter meshFilter = simplified.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = collisionMesh;
                MeshRenderer meshRenderer = simplified.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;
                MeshCollider meshCollider = simplified.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = collisionMesh;
                meshCollider.convex = false;

                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.12f, visualRenderers),
                    new LOD(0.03f, new Renderer[] { meshRenderer })
                });
                lodGroup.RecalculateBounds();

                StaticEditorFlags staticFlags = StaticEditorFlags.BatchingStatic |
                                                StaticEditorFlags.OccludeeStatic |
                                                StaticEditorFlags.ContributeGI |
                                                StaticEditorFlags.ReflectionProbeStatic;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, staticFlags);

                string prefabPath = $"{PrefabFolder}/{root.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                report.AppendLine($"{spec.Key}: {prefabPath} | {meshPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh BuildCollisionMesh(ModuleShape shape, string name)
        {
            switch (shape)
            {
                case ModuleShape.Flat:
                    return CreateBoxes(name, Box(-2f, 0f, -2f, 2f, 0.35f, 2f));
                case ModuleShape.RampStraight:
                    return CreateRamp(name);
                case ModuleShape.Plateau:
                    return CreateBoxes(name, Box(-2f, 0f, -2f, 2f, 1.35f, 2f));
                case ModuleShape.CliffStraight:
                    return CreateBoxes(
                        name,
                        Box(-2f, 0f, -2f, 2f, 0.35f, 2f),
                        Box(-2f, 0.35f, 0f, 2f, 1.35f, 2f));
                case ModuleShape.CliffOuterCorner:
                    return CreateBoxes(
                        name,
                        Box(-2f, 0f, -2f, 2f, 0.35f, 2f),
                        Box(0f, 0.35f, 0f, 2f, 1.35f, 2f));
                case ModuleShape.CliffInnerCorner:
                    return CreateBoxes(
                        name,
                        Box(-2f, 0f, -2f, 2f, 0.35f, 2f),
                        Box(-2f, 0.35f, 0f, 2f, 1.35f, 2f),
                        Box(0f, 0.35f, -2f, 2f, 1.35f, 0f));
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        private static Mesh CreateBoxes(string name, params Bounds[] boxes)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Bounds bounds in boxes)
                AddBox(bounds, vertices, normals, triangles);

            return CreateMesh(name, vertices, normals, triangles);
        }

        private static Mesh CreateRamp(string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            const float x0 = -2f;
            const float x1 = 2f;
            const float z0 = -2f;
            const float z1 = 2f;
            const float low = 0.35f;
            const float high = 1.35f;

            AddQuad(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0),
                new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), vertices, normals, triangles);
            AddQuad(new Vector3(x0, low, z0), new Vector3(x0, high, z1),
                new Vector3(x1, high, z1), new Vector3(x1, low, z0), vertices, normals, triangles);
            AddQuad(new Vector3(x0, 0f, z0), new Vector3(x0, low, z0),
                new Vector3(x1, low, z0), new Vector3(x1, 0f, z0), vertices, normals, triangles);
            AddQuad(new Vector3(x1, 0f, z1), new Vector3(x1, high, z1),
                new Vector3(x0, high, z1), new Vector3(x0, 0f, z1), vertices, normals, triangles);
            AddQuad(new Vector3(x0, 0f, z1), new Vector3(x0, high, z1),
                new Vector3(x0, low, z0), new Vector3(x0, 0f, z0), vertices, normals, triangles);
            AddQuad(new Vector3(x1, 0f, z0), new Vector3(x1, low, z0),
                new Vector3(x1, high, z1), new Vector3(x1, 0f, z1), vertices, normals, triangles);

            return CreateMesh(name, vertices, normals, triangles);
        }

        private static void AddBox(
            Bounds bounds,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            AddQuad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), vertices, normals, triangles);
            AddQuad(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), vertices, normals, triangles);
            AddQuad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), vertices, normals, triangles);
            AddQuad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), vertices, normals, triangles);
            AddQuad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), vertices, normals, triangles);
            AddQuad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), vertices, normals, triangles);
        }

        private static void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            int start = vertices.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Mesh CreateMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Bounds Box(
            float minX,
            float minY,
            float minZ,
            float maxX,
            float maxY,
            float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            return bounds;
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void ReplaceAsset(Object asset, string path)
        {
            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string current = "Assets";
            string[] parts = folder.Substring("Assets/".Length).Split('/');
            foreach (string part in parts)
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
