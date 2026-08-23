using System.Collections;
using NUnit.Framework;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.SceneFlow;
using OriginCore.UI.RTS;
using OriginCore.Visibility;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class VisibilityPlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float TimeoutSeconds = 12f;

        [UnitySetUp]
        public IEnumerator LoadSystemTest()
        {
            yield return DestroyPersistentRoots();
            Time.timeScale = 1f;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while ((!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                    AppRoot.Instance.Services.CurrentSceneContext == null ||
                    AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem == null ||
                    AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem.Grid == null ||
                    AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem.TargetCount < 3) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(AppRoot.HasInstance, Is.True);
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem, Is.Not.Null,
                "Ensure the visibility scene assets are configured before running this test.");
            Assert.That(AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem.Grid,
                Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return DestroyPersistentRoots();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator HiddenEnemyCannotRenderSelectOrEnterFriendlyTargetingThenReacquires()
        {
            SceneContext context = AppRoot.Instance.Services.CurrentSceneContext;
            VisibilitySystem system = context.VisibilitySystem;
            VisibilityTarget target = FindEnemyBuildingTarget();
            VisionEmitter observer = FindFriendlyCombatEmitter();
            AttackCapability attack = observer.GetComponent<AttackCapability>();
            AutoTargetScanner scanner = observer.GetComponent<AutoTargetScanner>();
            Assert.That(attack, Is.Not.Null);
            Assert.That(scanner, Is.Not.Null);

            MoveAllFriendlyEmittersOutsideVision(target);
            system.TickNow();
            AssertTargetSuppressed(target);
            Assert.That(attack.IsValidHostileTarget(target.Identity), Is.False);
            Assert.That(scanner.IsValidVisibleHostile(target.Identity), Is.False);

            MoveEmitter(observer, target.transform.position + Vector3.left * 2f);
            system.TickNow();
            Physics.SyncTransforms();
            Assert.That(target.IsVisible, Is.True);
            Assert.That(target.Selectable.isActiveAndEnabled, Is.True);
            Assert.That(target.SelectionCollider.enabled, Is.True);
            Assert.That(target.GetComponentsInChildren<Renderer>(true),
                Has.Some.Matches<Renderer>(renderer => renderer != null && renderer.enabled));
            Assert.That(Selectable.TryResolve(target.SelectionCollider, out Selectable resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(target.Selectable));
            Assert.That(attack.IsValidHostileTarget(target.Identity), Is.True);
            Assert.That(scanner.IsValidVisibleHostile(target.Identity), Is.True);

            MoveAllFriendlyEmittersOutsideVision(target);
            system.TickNow();
            AssertTargetSuppressed(target);
            Assert.That(system.GetState(target.transform.position),
                Is.EqualTo(VisibilityCellState.Explored));

            MoveEmitter(observer, target.transform.position + Vector3.back * 2f);
            system.TickNow();
            Assert.That(target.IsVisible, Is.True,
                "Reacquisition must restore the live object in the same visibility tick.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingMemoryKeepsOnlyLastKnownTransformAndExpiresOnVisibleAbsence()
        {
            VisibilitySystem system =
                AppRoot.Instance.Services.CurrentSceneContext.VisibilitySystem;
            VisibilityTarget target = FindEnemyBuildingTarget();
            VisionEmitter observer = FindFriendlyCombatEmitter();

            MoveEmitter(observer, target.transform.position + Vector3.left * 2f);
            system.TickNow();
            BuildingMemoryGhost memory = FindMemoryFor(target);
            Assert.That(memory.HasMemory, Is.True);
            Assert.That(memory.IsGhostVisible, Is.False);
            Vector3 rememberedPosition = target.transform.position;

            MoveAllFriendlyEmittersOutsideVision(target);
            system.TickNow();
            Assert.That(target.IsVisible, Is.False);
            Assert.That(memory.IsGhostVisible, Is.True);
            Assert.That(memory.transform.position,
                Is.EqualTo(rememberedPosition).Using(Vector3ComparerWithEqualsOperator.Instance));

            target.transform.position = rememberedPosition + new Vector3(3f, 0f, 2f);
            Physics.SyncTransforms();
            system.TickNow();
            Assert.That(memory.IsGhostVisible, Is.True);
            Assert.That(memory.transform.position,
                Is.EqualTo(rememberedPosition).Using(Vector3ComparerWithEqualsOperator.Instance),
                "Hidden source movement must not update the stale memory transform.");

            MoveEmitter(observer, target.transform.position + Vector3.left * 2f);
            system.TickNow();
            Assert.That(target.IsVisible, Is.True);
            Assert.That(memory.IsGhostVisible, Is.False);
            Assert.That(memory.LastKnownPosition,
                Is.EqualTo(target.transform.position).Using(Vector3ComparerWithEqualsOperator.Instance));

            Vector3 finalKnownPosition = target.transform.position;
            MoveAllFriendlyEmittersOutsideVision(target);
            system.TickNow();
            Object.Destroy(target.gameObject);
            yield return null;
            system.TickNow();
            Assert.That(memory != null && memory.IsGhostVisible, Is.True,
                "A building destroyed outside vision keeps its stale memory.");

            MoveEmitter(observer, finalKnownPosition + Vector3.left * 2f);
            system.TickNow();
            yield return null;
            Assert.That(memory == null, Is.True,
                "The stale memory must disappear once its last-known cell is visible.");
        }

        [UnityTest]
        public IEnumerator MinimapClickCentersOnlyTheRtsCameraWithinOneGridCell()
        {
            GameServices services = AppRoot.Instance.Services;
            SceneContext context = services.CurrentSceneContext;
            MinimapView view = context.MinimapView;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsConfigured, Is.True);
            Assert.That(view.SharedFogTexture, Is.SameAs(context.VisibilitySystem.FogTexture));
            Assert.That(services.GameModeController.CurrentMode, Is.EqualTo(GameMode.RTS));

            Vector2 requested = new Vector2(0.75f, 0.72f);
            Vector3 expected = context.MinimapCoordinateBounds.NormalizedToWorld(requested);
            Assert.That(view.TryCenterAtNormalized(requested), Is.True);
            Vector3 actual = context.RtsCameraController.Rig.position;
            float planarError = Vector2.Distance(
                new Vector2(actual.x, actual.z),
                new Vector2(expected.x, expected.z));
            Assert.That(planarError,
                Is.LessThanOrEqualTo(context.VisibilitySystem.Config.CellSize));

            Assert.That(services.GameModeController.RequestMode(GameMode.ACT), Is.True);
            Vector3 beforeRejectedClick = context.RtsCameraController.Rig.position;
            Assert.That(view.TryCenterAtNormalized(new Vector2(0.2f, 0.2f)), Is.False);
            Assert.That(context.RtsCameraController.Rig.position,
                Is.EqualTo(beforeRejectedClick).Using(Vector3ComparerWithEqualsOperator.Instance));
            yield return null;
        }

        private static void AssertTargetSuppressed(VisibilityTarget target)
        {
            Assert.That(target.IsVisible, Is.False);
            Assert.That(target.Selectable.isActiveAndEnabled, Is.False);
            Assert.That(target.SelectionCollider.enabled, Is.False);
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.That(renderers[i].enabled, Is.False, renderers[i].name);
            }

            Assert.That(Selectable.TryResolve(target.SelectionCollider, out _), Is.False);
        }

        private static VisibilityTarget FindEnemyBuildingTarget()
        {
            VisibilityTarget[] targets = Object.FindObjectsOfType<VisibilityTarget>(true);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null && targets[i].RememberAsBuilding &&
                    targets[i].Faction != null &&
                    targets[i].Faction.Faction == FactionId.Enemy)
                {
                    return targets[i];
                }
            }

            Assert.Fail("P15 enemy building VisibilityTarget was not found.");
            return null;
        }

        private static VisionEmitter FindFriendlyCombatEmitter()
        {
            VisionEmitter[] emitters = Object.FindObjectsOfType<VisionEmitter>(true);
            for (int i = 0; i < emitters.Length; i++)
            {
                VisionEmitter emitter = emitters[i];
                if (emitter != null && emitter.Faction != null &&
                    emitter.Faction.Faction == FactionId.Friendly &&
                    emitter.GetComponent<AutoTargetScanner>() != null &&
                    emitter.GetComponent<NavMeshAgent>() != null)
                {
                    return emitter;
                }
            }

            Assert.Fail("A friendly combat VisionEmitter was not found.");
            return null;
        }

        private static BuildingMemoryGhost FindMemoryFor(VisibilityTarget target)
        {
            BuildingMemoryGhost[] memories =
                Object.FindObjectsOfType<BuildingMemoryGhost>(true);
            for (int i = 0; i < memories.Length; i++)
            {
                if (memories[i] != null && memories[i].SourceTarget == target)
                {
                    return memories[i];
                }
            }

            Assert.Fail("Building memory ghost was not created for the P15 fixture.");
            return null;
        }

        private static void MoveEmitter(VisionEmitter emitter, Vector3 position)
        {
            NavMeshAgent agent = emitter.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.enabled = false;
            }

            emitter.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void MoveAllFriendlyEmittersOutsideVision(
            VisibilityTarget target)
        {
            Vector3 targetPosition = target != null
                ? target.transform.position
                : Vector3.zero;
            VisionEmitter[] emitters = Object.FindObjectsOfType<VisionEmitter>(true);
            for (int i = 0; i < emitters.Length; i++)
            {
                VisionEmitter emitter = emitters[i];
                if (emitter == null || emitter.Faction == null ||
                    emitter.Faction.Faction != FactionId.Friendly)
                {
                    continue;
                }

                float separation = Mathf.Max(8f, emitter.VisionRange + 8f);
                MoveEmitter(
                    emitter,
                    targetPosition + Vector3.left * separation +
                    Vector3.back * (i * 0.25f));
            }
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
