using System.Collections;
using NUnit.Framework;
using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class ProductionPlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float SceneLoadTimeoutSeconds = 10f;

        [UnitySetUp]
        public IEnumerator LoadSystemTestScene()
        {
            Time.timeScale = 1f;
            yield return DestroyPersistentRoots();
            // Destroying a previously paused AppRoot restores the time scale that
            // PauseService captured before pausing. Re-assert the fixture baseline
            // after destruction so another test cannot leave production near-frozen.
            Time.timeScale = 1f;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
            while (!IsP10Ready() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(IsP10Ready(), Is.True,
                "Ensure the production scene assets are configured before running this test.");
            SimpleEnemyBrain[] brains = Object.FindObjectsOfType<SimpleEnemyBrain>(true);
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i].enabled = false;
                brains[i].CommandQueue?.StopAll();
            }

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadSystemTestScene()
        {
            Time.timeScale = 1f;
            yield return DestroyPersistentRoots();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator PauseStopsProductionThenSpawnRalliesAndDeathReleasesOnce()
        {
            GameServices services = AppRoot.Instance.Services;
            ResourceService resources = services.ResourceService;
            ProductionQueue producer = Object.FindObjectOfType<ProductionQueue>();
            Assert.That(resources, Is.Not.Null);
            Assert.That(producer, Is.Not.Null);
            Assert.That(producer.TotalCount, Is.Zero);
            resources.ResetWallet();

            ProductionRecipe workerRecipe = FindRecipe(producer, UnitRole.Worker);
            Assert.That(workerRecipe, Is.Not.Null);
            ResourceSnapshot initial = resources.Snapshot;
            EntityIdentity produced = null;
            bool rallyCommandObserved = false;
            producer.UnitProduced += (queue, recipe, identity) =>
            {
                produced = identity;
                UnitCommandQueue unitQueue = identity != null
                    ? identity.GetComponent<UnitCommandQueue>()
                    : null;
                rallyCommandObserved = unitQueue != null && unitQueue.TotalCommandCount > 0;
            };

            Assert.That(producer.TryEnqueue(
                workerRecipe,
                out ProductionRejectionReason rejection), Is.True);
            Assert.That(rejection, Is.EqualTo(ProductionRejectionReason.None));
            Assert.That(resources.Snapshot.CommanderResource,
                Is.EqualTo(initial.CommanderResource - workerRecipe.Cost.CommanderResource));
            Assert.That(resources.Snapshot.Crystal,
                Is.EqualTo(initial.Crystal - workerRecipe.Cost.Crystal));
            Assert.That(resources.Snapshot.InfluenceUsed,
                Is.EqualTo(initial.InfluenceUsed + workerRecipe.Cost.Influence));

            yield return null;
            float pausedRemaining = producer.CurrentRemainingTime;
            Time.timeScale = 0f;
            float pauseDeadline = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < pauseDeadline)
            {
                yield return null;
            }

            Assert.That(producer.CurrentRemainingTime,
                Is.EqualTo(pausedRemaining).Within(0.001f),
                "Production countdown advanced while Time.timeScale was zero.");
            Time.timeScale = 1f;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (produced == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(produced, Is.Not.Null, producer.LastMessage);
            Assert.That(rallyCommandObserved, Is.True,
                "The produced unit did not receive its rally MoveCommand.");
            PopulationOwner population = produced.GetComponent<PopulationOwner>();
            Assert.That(population, Is.Not.Null);
            Assert.That(population.ReservedInfluence, Is.EqualTo(workerRecipe.Cost.Influence));
            Assert.That(population.HasReservation, Is.True);

            RallyPointController rally = producer.RallyPointController;
            Assert.That(rally, Is.Not.Null);
            Assert.That(rally.HasValidRallyPoint, Is.True);
            deadline = Time.realtimeSinceStartup + 10f;
            while (produced != null && produced.gameObject.activeSelf &&
                   HorizontalDistance(produced.transform.position, rally.RallyPoint) > 0.8f &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(HorizontalDistance(produced.transform.position, rally.RallyPoint),
                Is.LessThanOrEqualTo(0.8f),
                "The produced unit did not arrive at its rally point.");

            int commanderAfterProduction = resources.Snapshot.CommanderResource;
            VitalsComponent vitals = produced.GetComponent<VitalsComponent>();
            Assert.That(vitals, Is.Not.Null);
            Assert.That(vitals.SetHealth(0f), Is.True);
            yield return null;

            Assert.That(resources.Snapshot.InfluenceUsed, Is.EqualTo(initial.InfluenceUsed));
            Assert.That(resources.Snapshot.CommanderResource,
                Is.EqualTo(commanderAfterProduction),
                "Unit death must release Influence without refunding spent resources.");
            Assert.That(population.ReleaseInfluence(), Is.False);
            Assert.That(resources.Snapshot.InfluenceUsed, Is.EqualTo(initial.InfluenceUsed));
        }

        [UnityTest]
        public IEnumerator FullInfluenceRejectsProductionWithoutPartialPayment()
        {
            ResourceService resources = AppRoot.Instance.Services.ResourceService;
            ProductionQueue producer = Object.FindObjectOfType<ProductionQueue>();
            resources.ResetWallet();
            ProductionRecipe workerRecipe = FindRecipe(producer, UnitRole.Worker);
            Assert.That(workerRecipe, Is.Not.Null);

            int available = resources.Snapshot.InfluenceAvailable;
            Assert.That(resources.ReserveInfluence(available), Is.True);
            ResourceSnapshot before = resources.Snapshot;
            Assert.That(producer.TryEnqueue(
                workerRecipe,
                out ProductionRejectionReason rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(
                ProductionRejectionReason.InfluenceCapReached));
            Assert.That(resources.Snapshot, Is.EqualTo(before));
            Assert.That(producer.TotalCount, Is.Zero);
            Assert.That(resources.ReleaseInfluence(available), Is.True);
            yield return null;
        }

        private static ProductionRecipe FindRecipe(
            ProductionQueue producer,
            UnitRole role)
        {
            if (producer == null)
            {
                return null;
            }

            for (int i = 0; i < producer.Recipes.Count; i++)
            {
                ProductionRecipe recipe = producer.Recipes[i];
                if (recipe != null && recipe.Archetype != null &&
                    recipe.Archetype.Roles.HasAny(role))
                {
                    return recipe;
                }
            }

            return null;
        }

        private static bool IsP10Ready()
        {
            if (!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                AppRoot.Instance.Services.ResourceService == null ||
                AppRoot.Instance.Services.CurrentSceneContext == null)
            {
                return false;
            }

            var context = AppRoot.Instance.Services.CurrentSceneContext;
            ProductionQueue producer = Object.FindObjectOfType<ProductionQueue>();
            return context.ResourceHudView != null && context.RtsRosterView != null &&
                   context.ProductionPanelView != null && producer != null &&
                   producer.RallyPointController != null &&
                   producer.RallyPointController.HasValidRallyPoint &&
                   producer.Recipes.Count == 2;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static IEnumerator DestroyPersistentRoots()
        {
            AppRoot[] roots = Object.FindObjectsOfType<AppRoot>(true);
            for (int i = 0; i < roots.Length; i++)
            {
                Object.Destroy(roots[i].gameObject);
            }

            if (roots.Length > 0)
            {
                yield return null;
            }
        }
    }
}
