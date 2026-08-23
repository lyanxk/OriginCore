using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.SceneFlow;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OriginCore.Tests.EditMode
{
    public sealed class PlaceholderTestbedTests
    {
        private const string MaterialsFolder = "Assets/_OriginCore/Art/Materials";
        private const string SystemTestScenePath = "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const string NavMeshDataPath =
            "Assets/_OriginCore/Scenes/90_SystemTest/NavMesh-Navigation.asset";

        private static readonly string[] MaterialNames =
        {
            "MAT_Friendly",
            "MAT_Hero",
            "MAT_Enemy",
            "MAT_Neutral",
            "MAT_Ground",
            "MAT_Obstacle",
            "MAT_Selected",
            "MAT_FogHidden",
            "MAT_FogExplored",
            "MAT_GhostBuilding"
        };

        private static readonly string[] PrefabPaths =
        {
            "Assets/_OriginCore/Prefabs/Units/PF_HeroPlaceholder.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_UnitFriendly.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_UnitEnemy.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_WorkerPlaceholder.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_SelectionIndicator.prefab",
            "Assets/_OriginCore/Prefabs/Buildings/PF_ProducerBuilding.prefab",
            "Assets/_OriginCore/Prefabs/Buildings/PF_ResourceCrystal.prefab",
            "Assets/_OriginCore/Prefabs/Buildings/PF_RallyMarker.prefab"
        };

        [Test]
        public void RequiredMaterialsAndPrimitivePrefabsExist()
        {
            for (int i = 0; i < MaterialNames.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    MaterialsFolder + "/" + MaterialNames[i] + ".mat");
                Assert.That(material, Is.Not.Null, "Missing material " + MaterialNames[i]);
                Assert.That(material.shader, Is.Not.Null);
                StringAssert.Contains("Universal Render Pipeline", material.shader.name);
            }

            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
                Assert.That(prefab, Is.Not.Null, "Missing prefab " + PrefabPaths[i]);
                Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).Length, Is.GreaterThan(0));
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
            }
        }

        [Test]
        public void HeroPrefabContainsRequiredP03Anchors()
        {
            GameObject hero = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[0]);
            Assert.That(hero, Is.Not.Null);
            Assert.That(hero.GetComponent<Collider>(), Is.Not.Null);

            string[] children =
            {
                "VisualRoot",
                "HeadMarker",
                "ForwardMarker",
                "SelectionIndicator",
                "ActCameraTarget",
                "FpsCameraTarget",
                "HealthBarAnchor",
                "GroundProbe"
            };

            for (int i = 0; i < children.Length; i++)
            {
                Assert.That(FindDescendant(hero.transform, children[i]), Is.Not.Null,
                    "Hero is missing " + children[i]);
            }
        }

        [Test]
        public void SystemTestContainsBakedNavMeshAndRegisteredSpawnPoints()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(SystemTestScenePath, OpenSceneMode.Single);
                Assert.That(FindRoot(scene, "P03_Testbed"), Is.Not.Null);

                List<Camera> cameras = FindComponentsInScene<Camera>(scene);
                List<AudioListener> listeners = FindComponentsInScene<AudioListener>(scene);
                List<NavMeshSurface> surfaces = FindComponentsInScene<NavMeshSurface>(scene);
                List<SceneContext> contexts = FindComponentsInScene<SceneContext>(scene);

                Assert.That(cameras.Count, Is.EqualTo(1));
                Assert.That(listeners.Count, Is.EqualTo(1));
                Assert.That(surfaces.Count, Is.EqualTo(1));
                Assert.That(contexts.Count, Is.EqualTo(1));
                Assert.That(surfaces[0].navMeshData, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(surfaces[0].navMeshData), Is.EqualTo(NavMeshDataPath));

                surfaces[0].AddData();
                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
                Assert.That(triangulation.vertices.Length, Is.GreaterThan(0));

                SceneContext context = contexts[0];
                Assert.That(context.MainCamera, Is.Not.Null);
                Assert.That(cameras, Does.Contain(context.MainCamera));
                Assert.That(context.MinimapMapDefinition, Is.Not.Null);
                Assert.That(context.DefaultPawn, Is.Not.Null);
                Assert.That(context.HudRoot, Is.Not.Null);
                Assert.That(context.MinimapBounds, Is.Not.Null);
                Assert.That(context.VisibilityBounds, Is.Not.Null);
                Assert.That(context.SpawnPoints.Count, Is.EqualTo(7));

                for (int i = 0; i < context.SpawnPoints.Count; i++)
                {
                    NavMeshHit hit;
                    Assert.That(
                        NavMesh.SamplePosition(context.SpawnPoints[i].position, out hit, 1f, NavMesh.AllAreas),
                        Is.True,
                        "Spawn point " + i + " is off the NavMesh.");
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return result;
        }
    }
}
