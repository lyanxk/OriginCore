using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.SceneFlow;
using OriginCore.UI.RTS;
using OriginCore.Units;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace OriginCore.Tests.EditMode
{
    public sealed class RtsCommandFoundationTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";

        private static readonly string[] MovablePrefabPaths =
        {
            "Assets/_OriginCore/Prefabs/Units/PF_HeroPlaceholder.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_UnitFriendly.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_UnitEnemy.prefab",
            "Assets/_OriginCore/Prefabs/Units/PF_WorkerPlaceholder.prefab"
        };

        [Test]
        public void MovablePrefabsOwnOneCompleteCommandPipelineAndBuildingStaysImmobile()
        {
            for (int i = 0; i < MovablePrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    MovablePrefabPaths[i]);
                Assert.That(prefab, Is.Not.Null,
                    "Ensure the RTS command scene assets are configured before running this test.");
                EntityIdentity identity = prefab.GetComponent<EntityIdentity>();
                NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
                NavMeshMovementDriver movement =
                    prefab.GetComponent<NavMeshMovementDriver>();
                UnitCommandQueue queue = prefab.GetComponent<UnitCommandQueue>();
                CommandQueuePathView pathView = prefab.GetComponent<CommandQueuePathView>();
                Assert.That(identity, Is.Not.Null, prefab.name);
                Assert.That(identity.Definition, Is.Not.Null, prefab.name);
                Assert.That(agent, Is.Not.Null, prefab.name);
                Assert.That(movement, Is.Not.Null, prefab.name);
                Assert.That(queue, Is.Not.Null, prefab.name);
                Assert.That(pathView, Is.Not.Null, prefab.name);
                Assert.That(movement.Agent, Is.SameAs(agent), prefab.name);
                Assert.That(movement.Identity, Is.SameAs(identity), prefab.name);
                Assert.That(queue.Identity, Is.SameAs(identity), prefab.name);
                Assert.That(queue.MovementDriver, Is.SameAs(movement), prefab.name);
                Assert.That(pathView.CommandQueue, Is.SameAs(queue), prefab.name);
                Assert.That(pathView.Selectable, Is.SameAs(prefab.GetComponent<Selectable>()),
                    prefab.name);
                Assert.That(agent.speed, Is.EqualTo(identity.Definition.MoveSpeed).Within(0.001f));
                Assert.That(agent.acceleration,
                    Is.EqualTo(identity.Definition.Acceleration).Within(0.001f));
                Assert.That(agent.angularSpeed,
                    Is.EqualTo(identity.Definition.AngularSpeed).Within(0.001f));
                Assert.That(agent.stoppingDistance,
                    Is.EqualTo(identity.Definition.StoppingDistance).Within(0.001f));
            }

            GameObject building = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_OriginCore/Prefabs/Buildings/PF_ProducerBuilding.prefab");
            Assert.That(building, Is.Not.Null);
            Assert.That(building.GetComponent<NavMeshAgent>(), Is.Null);
            Assert.That(building.GetComponent<NavMeshMovementDriver>(), Is.Null);
            Assert.That(building.GetComponent<UnitCommandQueue>(), Is.Null);
            CommandQueuePathView buildingRoute =
                building.GetComponent<CommandQueuePathView>();
            Assert.That(buildingRoute, Is.Not.Null,
                "P10 gives the immobile producer a rally route view.");
            Assert.That(buildingRoute.CommandQueue, Is.Null,
                "The producer route must not be backed by a movable-unit command queue.");
            Assert.That(buildingRoute.RouteSourceComponent,
                Is.SameAs(building.GetComponent<OriginCore.Buildings.RallyPointController>()));
            Assert.That(buildingRoute.Selectable,
                Is.SameAs(building.GetComponent<Selectable>()));
        }

        [Test]
        public void SystemTestSerializesOneCompleteP08ContextAndBakedNavMesh()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(SystemTestScenePath, OpenSceneMode.Single);
                List<SceneContext> contexts = FindComponentsInScene<SceneContext>(scene);
                List<RtsCommandIssuer> issuers = FindComponentsInScene<RtsCommandIssuer>(scene);
                List<RtsCommandPanelView> panels =
                    FindComponentsInScene<RtsCommandPanelView>(scene);
                List<NavMeshSurface> surfaces = FindComponentsInScene<NavMeshSurface>(scene);

                Assert.That(contexts.Count, Is.EqualTo(1));
                Assert.That(issuers.Count, Is.EqualTo(1));
                Assert.That(panels.Count, Is.EqualTo(1));
                Assert.That(surfaces.Count, Is.EqualTo(1));
                Assert.That(surfaces[0].navMeshData, Is.Not.Null);

                SceneContext context = contexts[0];
                Assert.That(context.RtsCommandIssuer, Is.SameAs(issuers[0]));
                Assert.That(context.RtsCommandPanelView, Is.SameAs(panels[0]));
                Assert.That(issuers[0].SelectionService, Is.SameAs(context.SelectionService));
                Assert.That(issuers[0].WorldCamera, Is.SameAs(context.MainCamera));
                Assert.That(panels[0].CommandIssuer, Is.SameAs(issuers[0]));
                Assert.That(panels[0].SelectionService, Is.SameAs(context.SelectionService));
                Assert.That(panels[0].MoveButton, Is.Not.Null);
                Assert.That(panels[0].StopButton, Is.Not.Null);
                Assert.That(panels[0].CancelButton, Is.Not.Null);
                Assert.That(panels[0].ContentRoot.activeSelf, Is.True);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        [Test]
        public void CommandTargetingStateReservesOnlyDocumentedP08AndFutureValues()
        {
            Assert.That((int)CommandTargetingState.None, Is.EqualTo(0));
            Assert.That((int)CommandTargetingState.Move, Is.EqualTo(1));
            Assert.That((int)CommandTargetingState.Attack, Is.EqualTo(2));
            Assert.That((int)CommandTargetingState.SetRally, Is.EqualTo(3));
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
