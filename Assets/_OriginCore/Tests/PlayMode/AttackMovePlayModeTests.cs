using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using OriginCore.Units;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.PlayMode
{
    public sealed class AttackMovePlayModeTests
    {
        private const string SystemTestScenePath =
            "Assets/_OriginCore/Scenes/90_SystemTest.unity";
        private const float TimeoutSeconds = 25f;

        [UnitySetUp]
        public IEnumerator LoadSystemTestScene()
        {
            yield return DestroyPersistentRoots();
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(SystemTestScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!IsP09Ready() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(IsP09Ready(), Is.True,
                "Ensure the RTS combat scene assets are configured before running this test.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadSystemTestScene()
        {
            yield return DestroyPersistentRoots();
        }

        [UnityTest]
        public IEnumerator AttackMoveDiscoversEngagesAndResumesOriginalRoute()
        {
            GameServices services = AppRoot.Instance.Services;
            EntityIdentity hero = FindEntity(services, UnitRole.Hero, null);
            EntityIdentity enemy = FindEntity(services, UnitRole.None, FactionId.Enemy);
            Assert.That(hero, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);

            SimpleEnemyBrain[] brains = Object.FindObjectsOfType<SimpleEnemyBrain>(true);
            for (int i = 0; i < brains.Length; i++)
            {
                brains[i].enabled = false;
                brains[i].CommandQueue?.StopAll();
            }

            hero.GetComponent<VitalsComponent>().ResetToMaximum();
            enemy.GetComponent<VitalsComponent>().ResetToMaximum();
            UnitCommandQueue heroQueue = hero.GetComponent<UnitCommandQueue>();
            NavMeshMovementDriver heroMovement = hero.GetComponent<NavMeshMovementDriver>();
            AttackCapability heroAttack = hero.GetComponent<AttackCapability>();
            AutoTargetScanner heroScanner = hero.GetComponent<AutoTargetScanner>();
            NavMeshAgent enemyAgent = enemy.GetComponent<NavMeshAgent>();
            HitFlash enemyFlash = enemy.GetComponent<HitFlash>();
            CombatDeathHandler enemyDeath = enemy.GetComponent<CombatDeathHandler>();
            Assert.That(heroQueue, Is.Not.Null);
            Assert.That(heroMovement, Is.Not.Null);
            Assert.That(heroAttack, Is.Not.Null);
            Assert.That(heroScanner, Is.Not.Null);
            Assert.That(enemyAgent, Is.Not.Null);
            Assert.That(enemyFlash, Is.Not.Null);
            Assert.That(enemyDeath, Is.Not.Null);

            heroQueue.StopAll();
            Assert.That(TryCreateRouteThroughEnemy(
                heroMovement.Agent,
                enemyAgent,
                out Vector3 destination), Is.True);
            Physics.SyncTransforms();
            yield return null;

            int attacksBefore = heroAttack.SuccessfulAttackCount;
            int scansBefore = heroScanner.ScanCount;
            AttackMoveCommand command = new AttackMoveCommand(destination);
            Assert.That(heroQueue.TryIssue(
                command,
                false,
                out CommandRejectionReason rejection), Is.True);
            Assert.That(rejection, Is.EqualTo(CommandRejectionReason.None));

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!command.Status.IsTerminal() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(command.Status, Is.EqualTo(CommandStatus.Succeeded),
                command.FailureReason);
            Assert.That(heroAttack.SuccessfulAttackCount, Is.GreaterThan(attacksBefore));
            Assert.That(heroScanner.ScanCount, Is.GreaterThan(scansBefore));
            Assert.That(enemyFlash.FlashCount, Is.GreaterThan(0));
            Assert.That(enemyDeath.IsHandled, Is.True);
            Assert.That(enemy.gameObject.activeSelf, Is.False,
                "Death must deactivate rather than destroy the target.");
            Assert.That(HorizontalDistance(hero.transform.position, destination),
                Is.LessThanOrEqualTo(heroMovement.Agent.stoppingDistance + 0.6f),
                "Attack Move must resume and finish its original route after combat.");
            Assert.That(heroQueue.TotalCommandCount, Is.Zero);
        }

        private static bool TryCreateRouteThroughEnemy(
            NavMeshAgent heroAgent,
            NavMeshAgent enemyAgent,
            out Vector3 destination)
        {
            destination = default(Vector3);
            Vector3[] directions =
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left,
                new Vector3(1f, 0f, 1f).normalized,
                new Vector3(-1f, 0f, 1f).normalized
            };
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 requested = heroAgent.transform.position + directions[i] * 8f;
                if (!NavMesh.SamplePosition(requested, out NavMeshHit destinationHit, 2f,
                        heroAgent.areaMask))
                {
                    continue;
                }

                NavMeshPath path = new NavMeshPath();
                if (!heroAgent.CalculatePath(destinationHit.position, path) ||
                    path.status != NavMeshPathStatus.PathComplete ||
                    HorizontalDistance(heroAgent.transform.position, destinationHit.position) < 6f)
                {
                    continue;
                }

                Vector3 requestedMidpoint = Vector3.Lerp(
                    heroAgent.transform.position,
                    destinationHit.position,
                    0.5f);
                if (!NavMesh.SamplePosition(requestedMidpoint, out NavMeshHit midpointHit, 1.5f,
                        enemyAgent.areaMask) || !enemyAgent.Warp(midpointHit.position))
                {
                    continue;
                }

                destination = destinationHit.position;
                return true;
            }

            return false;
        }

        private static EntityIdentity FindEntity(
            GameServices services,
            UnitRole requiredRole,
            FactionId? requiredFaction)
        {
            IReadOnlyList<EntityIdentity> entities = services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity entity = entities[i];
                if (entity == null ||
                    requiredRole != UnitRole.None && !entity.HasAnyRole(requiredRole))
                {
                    continue;
                }

                FactionMember faction = entity.GetComponent<FactionMember>();
                if (requiredFaction.HasValue &&
                    (faction == null || faction.Faction != requiredFaction.Value))
                {
                    continue;
                }

                return entity;
            }

            return null;
        }

        private static bool IsP09Ready()
        {
            if (!AppRoot.HasInstance || AppRoot.Instance.Services == null ||
                AppRoot.Instance.Services.CurrentSceneContext == null)
            {
                return false;
            }

            GameServices services = AppRoot.Instance.Services;
            EntityIdentity hero = FindEntity(services, UnitRole.Hero, null);
            EntityIdentity enemy = FindEntity(services, UnitRole.None, FactionId.Enemy);
            return hero != null && enemy != null &&
                   hero.GetComponent<UnitCommandQueue>() != null &&
                   hero.GetComponent<AttackCapability>() != null &&
                   hero.GetComponent<AutoTargetScanner>() != null &&
                   enemy.GetComponent<CombatDeathHandler>() != null &&
                   services.CurrentSceneContext.RtsCommandPanelView != null &&
                   services.CurrentSceneContext.RtsCommandPanelView.AttackButton != null;
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
