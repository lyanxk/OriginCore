using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Gameplay;
using OriginCore.SceneFlow;
using OriginCore.UI.RTS;
using OriginCore.Visibility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.EditMode
{
    public sealed class VisibilityFoundationTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        [Test]
        public void GridTransitionsVisibleToExploredAndRestoresNoTransientVisibility()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
            VisibilityGrid grid = new VisibilityGrid(bounds, 2f);
            Vector3 center = Vector3.zero;

            Assert.That(grid.Width, Is.EqualTo(10));
            Assert.That(grid.Height, Is.EqualTo(10));
            Assert.That(grid.GetState(center), Is.EqualTo(VisibilityCellState.Hidden));
            Assert.That(grid.MarkVisibleCircle(center, 4f), Is.GreaterThan(0));
            Assert.That(grid.GetState(center), Is.EqualTo(VisibilityCellState.Visible));

            grid.ClearVisible();
            Assert.That(grid.GetState(center), Is.EqualTo(VisibilityCellState.Explored));
            List<string> explored = new List<string>();
            grid.CaptureExploredCellIds(explored);
            Assert.That(explored.Count, Is.GreaterThan(0));
            explored.Add("invalid");
            explored.Add("999:999");

            VisibilityGrid restored = new VisibilityGrid(bounds, 2f);
            Assert.That(restored.RestoreExploredCellIds(explored), Is.GreaterThan(0));
            Assert.That(restored.GetState(center), Is.EqualTo(VisibilityCellState.Explored));
            for (int z = 0; z < restored.Height; z++)
            {
                for (int x = 0; x < restored.Width; x++)
                {
                    Assert.That(restored.GetState(x, z), Is.Not.EqualTo(
                        VisibilityCellState.Visible));
                }
            }
        }

        [Test]
        public void MinimapBoundsMapsCornersAndCenterWithoutAxisSwapping()
        {
            GameObject root = new GameObject("MinimapBoundsTest");
            try
            {
                root.transform.position = new Vector3(4f, 0f, -6f);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 2f, 0f);
                collider.size = new Vector3(40f, 4f, 20f);
                MinimapBounds mapping = root.AddComponent<MinimapBounds>();
                mapping.Configure(collider);

                Bounds world = collider.bounds;
                Assert.That(mapping.NormalizedToWorld(Vector2.zero).x,
                    Is.EqualTo(world.min.x).Within(0.001f));
                Assert.That(mapping.NormalizedToWorld(Vector2.zero).z,
                    Is.EqualTo(world.min.z).Within(0.001f));
                Assert.That(mapping.NormalizedToWorld(Vector2.one).x,
                    Is.EqualTo(world.max.x).Within(0.001f));
                Assert.That(mapping.NormalizedToWorld(Vector2.one).z,
                    Is.EqualTo(world.max.z).Within(0.001f));
                Assert.That(mapping.WorldToNormalized(world.center),
                    Is.EqualTo(new Vector2(0.5f, 0.5f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void P15AssetsPrefabsAndSystemTestSceneOwnOneCompleteVisibilityPipeline()
        {
            VisibilityConfig config = AssetDatabase.LoadAssetAtPath<VisibilityConfig>(
                "Assets/_OriginCore/Data/Visibility/SO_VisibilityConfig.asset");
            MinimapMapDefinition minimap =
                AssetDatabase.LoadAssetAtPath<MinimapMapDefinition>(
                    "Assets/_OriginCore/Data/Visibility/SO_SystemTestMinimapMap.asset");
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_OriginCore/Prefabs/UI/PF_HUDRoot.prefab");
            GameObject enemy = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_OriginCore/Prefabs/Units/PF_UnitEnemy.prefab");
            Assert.That(config, Is.Not.Null,
                "Ensure the visibility scene assets are configured before running this test.");
            Assert.That(config.UpdateHz, Is.InRange(5f, 10f));
            Assert.That(minimap, Is.Not.Null);
            Assert.That(minimap.Resolution, Is.EqualTo(256));
            Assert.That(hud.GetComponentsInChildren<MinimapView>(true).Length, Is.EqualTo(1));
            Assert.That(enemy.GetComponent<VisibilityTarget>(), Is.Not.Null);

            string[] emitterPrefabs =
            {
                "Assets/_OriginCore/Prefabs/Units/PF_HeroPlaceholder.prefab",
                "Assets/_OriginCore/Prefabs/Units/PF_UnitFriendly.prefab",
                "Assets/_OriginCore/Prefabs/Units/PF_WorkerPlaceholder.prefab",
                "Assets/_OriginCore/Prefabs/Buildings/PF_ProducerBuilding.prefab"
            };
            for (int i = 0; i < emitterPrefabs.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(emitterPrefabs[i]);
                Assert.That(prefab.GetComponent<VisionEmitter>(), Is.Not.Null, emitterPrefabs[i]);
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    SystemTestScenePath,
                    OpenSceneMode.Single);
                SceneContext context = FindSingle<SceneContext>(scene);
                VisibilitySystem system = FindSingle<VisibilitySystem>(scene);
                MinimapView view = FindSingle<MinimapView>(scene);
                Assert.That(context.VisibilitySystem, Is.SameAs(system));
                Assert.That(context.MinimapView, Is.SameAs(view));
                Assert.That(context.MinimapCoordinateBounds, Is.Not.Null);
                Assert.That(context.MinimapMapDefinition, Is.SameAs(minimap));
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<Camera>(true).Length,
                    Is.EqualTo(1));
                Assert.That(view.VisibilitySystem, Is.SameAs(system));
                Assert.That(view.WorldBounds, Is.SameAs(context.MinimapCoordinateBounds));
                Assert.That(view.MapDefinition, Is.SameAs(minimap));
                Assert.That(system.BoundsSource, Is.SameAs(context.VisibilityBounds));
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            List<T> matches = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                matches.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            Assert.That(matches.Count, Is.EqualTo(1), typeof(T).Name);
            return matches[0];
        }
    }
}
