using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.EditMode
{
    public sealed class GameplayFoundationTests
    {
        private const string DefinitionsFolder = "Assets/_OriginCore/Data/Definitions";
        private const string AppRootPrefabPath = "Assets/_OriginCore/Prefabs/Core/PF_AppRoot.prefab";

        [Test]
        public void FactionRelationsAreSymmetricAndNeutralIsNeverHostile()
        {
            GameObject root = new GameObject("FactionRelationServiceTest");
            try
            {
                FactionRelationService relations = root.AddComponent<FactionRelationService>();
                Assert.That(relations.GetRelation(FactionId.Friendly, FactionId.Friendly),
                    Is.EqualTo(FactionRelation.Allied));
                Assert.That(relations.GetRelation(FactionId.Enemy, FactionId.Enemy),
                    Is.EqualTo(FactionRelation.Allied));
                Assert.That(relations.AreHostile(FactionId.Friendly, FactionId.Enemy), Is.True);
                Assert.That(relations.AreHostile(FactionId.Enemy, FactionId.Friendly), Is.True);
                Assert.That(relations.GetRelation(FactionId.Friendly, FactionId.Neutral),
                    Is.EqualTo(FactionRelation.Neutral));
                Assert.That(relations.GetRelation(FactionId.Neutral, FactionId.Enemy),
                    Is.EqualTo(FactionRelation.Neutral));
                Assert.That(relations.AreHostile(FactionId.Neutral, FactionId.Neutral), Is.False);

                relations.ConfigureFriendlyEnemyRelation(FactionRelation.Allied);
                Assert.That(relations.AreAllied(FactionId.Friendly, FactionId.Enemy), Is.True);
                Assert.That(relations.GetRelation(FactionId.Neutral, FactionId.Friendly),
                    Is.EqualTo(FactionRelation.Neutral));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DamageConsumesShieldBeforeHealthAndPublishesOnlyRealChanges()
        {
            GameObject root = new GameObject("DamageReceiverTest");
            root.SetActive(false);
            try
            {
                VitalsComponent vitals = root.AddComponent<VitalsComponent>();
                DamageReceiver receiver = root.AddComponent<DamageReceiver>();
                vitals.Configure(100f, 20f, 40f, true);
                receiver.Configure(vitals);
                root.SetActive(true);

                int healthEvents = 0;
                int shieldEvents = 0;
                int energyEvents = 0;
                int vitalsEvents = 0;
                int healthBarEvents = 0;
                int damageEvents = 0;
                vitals.HealthChanged += (_, __, ___) => healthEvents++;
                vitals.ShieldChanged += (_, __, ___) => shieldEvents++;
                vitals.EnergyChanged += (_, __, ___) => energyEvents++;
                vitals.VitalsChanged += (_, __, ___) => vitalsEvents++;
                vitals.WorldHealthBarDataChanged += (_, __) => healthBarEvents++;
                receiver.DamageApplied += (_, __, ___) => damageEvents++;

                Assert.That(receiver.TryReceiveDamage(new DamageInfo(30f), out DamageResult result), Is.True);
                Assert.That(result.ShieldDamage, Is.EqualTo(20f).Within(0.0001f));
                Assert.That(result.HealthDamage, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(result.Killed, Is.False);
                Assert.That(vitals.Shield, Is.Zero.Within(0.0001f));
                Assert.That(vitals.Health, Is.EqualTo(90f).Within(0.0001f));
                Assert.That(vitals.Energy, Is.EqualTo(40f).Within(0.0001f));
                Assert.That(healthEvents, Is.EqualTo(1));
                Assert.That(shieldEvents, Is.EqualTo(1));
                Assert.That(energyEvents, Is.Zero);
                Assert.That(vitalsEvents, Is.EqualTo(1));
                Assert.That(healthBarEvents, Is.EqualTo(1));
                Assert.That(damageEvents, Is.EqualTo(1));

                Assert.That(receiver.TryReceiveDamage(new DamageInfo(0f), out DamageResult noChange), Is.False);
                Assert.That(noChange.Changed, Is.False);
                Assert.That(vitals.SetHealth(90f), Is.False);
                Assert.That(vitals.SetShield(0f), Is.False);
                Assert.That(vitals.SetEnergy(40f), Is.False);
                Assert.That(vitalsEvents, Is.EqualTo(1));
                Assert.That(damageEvents, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeathIsRaisedOncePerLifeAndDamageDoesNotRepeatWhileDead()
        {
            GameObject root = new GameObject("DeathEventTest");
            root.SetActive(false);
            try
            {
                VitalsComponent vitals = root.AddComponent<VitalsComponent>();
                DamageReceiver receiver = root.AddComponent<DamageReceiver>();
                vitals.Configure(10f, 0f, 0f, true);
                receiver.Configure(vitals);
                root.SetActive(true);

                int deathEvents = 0;
                vitals.Died += _ => deathEvents++;

                Assert.That(receiver.TryReceiveDamage(new DamageInfo(100f), out DamageResult first), Is.True);
                Assert.That(first.Killed, Is.True);
                Assert.That(vitals.IsAlive, Is.False);
                Assert.That(deathEvents, Is.EqualTo(1));

                Assert.That(receiver.TryReceiveDamage(new DamageInfo(100f), out DamageResult repeated), Is.False);
                Assert.That(repeated.Changed, Is.False);
                Assert.That(vitals.SetHealth(0f), Is.False);
                Assert.That(deathEvents, Is.EqualTo(1));

                vitals.ResetToMaximum();
                Assert.That(vitals.IsAlive, Is.True);
                Assert.That(receiver.TryReceiveDamage(new DamageInfo(10f), out DamageResult second), Is.True);
                Assert.That(second.Killed, Is.True);
                Assert.That(deathEvents, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RegistryRegistersQueriesAndUnregistersWithoutStaleEntries()
        {
            GameObject serviceRoot = new GameObject("EntityRegistryTest");
            List<EntityIdentity> createdEntities = new List<EntityIdentity>();
            try
            {
                FactionRelationService relations = serviceRoot.AddComponent<FactionRelationService>();
                EntityRegistry registry = serviceRoot.AddComponent<EntityRegistry>();
                registry.Configure(relations);

                EntityIdentity worker = CreateRegisteredEntity(
                    registry, "worker-test", FactionId.Friendly, UnitRole.Worker, true);
                EntityIdentity enemy = CreateRegisteredEntity(
                    registry, "enemy-test", FactionId.Enemy, UnitRole.Combat, true);
                EntityIdentity neutral = CreateRegisteredEntity(
                    registry, "neutral-test", FactionId.Neutral, UnitRole.None, false);
                createdEntities.Add(worker);
                createdEntities.Add(enemy);
                createdEntities.Add(neutral);

                Assert.That(registry.Count, Is.EqualTo(3));
                Assert.That(registry.Register(worker), Is.False, "Duplicate registration must be rejected.");

                List<EntityIdentity> entities = new List<EntityIdentity>();
                Assert.That(registry.CollectByFaction(FactionId.Friendly, entities), Is.EqualTo(1));
                Assert.That(entities[0], Is.SameAs(worker));
                Assert.That(registry.CollectAllied(FactionId.Friendly, entities), Is.EqualTo(1));
                Assert.That(entities[0], Is.SameAs(worker));
                Assert.That(registry.CollectHostile(FactionId.Friendly, entities), Is.EqualTo(1));
                Assert.That(entities[0], Is.SameAs(enemy));
                Assert.That(registry.CollectNeutral(FactionId.Friendly, entities), Is.EqualTo(1));
                Assert.That(entities[0], Is.SameAs(neutral));
                Assert.That(registry.CollectByRole(UnitRole.Worker, entities), Is.EqualTo(1));
                Assert.That(entities[0], Is.SameAs(worker));

                List<Selectable> selectables = new List<Selectable>();
                Assert.That(registry.CollectSelectables(selectables), Is.EqualTo(2));
                Assert.That(registry.CollectCommandable(FactionId.Friendly, selectables), Is.EqualTo(1));
                Assert.That(selectables[0].Identity, Is.SameAs(worker));

                enemy.UnregisterFromRegistry();
                Assert.That(registry.Count, Is.EqualTo(2));
                Assert.That(registry.Contains(enemy), Is.False);

                Object.DestroyImmediate(neutral.gameObject);
                createdEntities.Remove(neutral);
                Assert.That(registry.Count, Is.EqualTo(1),
                    "Destroyed objects must not leave stale registry references.");
            }
            finally
            {
                for (int i = 0; i < createdEntities.Count; i++)
                {
                    if (createdEntities[i] != null)
                    {
                        createdEntities[i].UnregisterFromRegistry();
                        Object.DestroyImmediate(createdEntities[i].gameObject);
                    }
                }

                Object.DestroyImmediate(serviceRoot);
            }
        }

        [Test]
        public void SelectionIndicatorSwitchesAllFourStatesWithoutDuplicateEvents()
        {
            GameObject root = new GameObject("SelectionStateTest");
            root.SetActive(false);
            try
            {
                EntityIdentity identity = root.AddComponent<EntityIdentity>();
                FactionMember faction = root.AddComponent<FactionMember>();
                identity.ConfigureFallbackIdentity("selection-test", "Selection Test", UnitRole.Combat);

                GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                indicatorObject.name = "SelectionIndicator";
                indicatorObject.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(indicatorObject.GetComponent<Collider>());

                SelectionIndicatorController indicator =
                    root.AddComponent<SelectionIndicatorController>();
                indicator.Configure(indicatorObject, null, null, null);
                Selectable selectable = root.AddComponent<Selectable>();
                selectable.Configure(identity, faction, null, indicator);
                root.SetActive(true);

                int stateEvents = 0;
                selectable.StateChanged += (_, __, ___) => stateEvents++;
                Assert.That(selectable.State, Is.EqualTo(SelectionVisualState.Normal));
                Assert.That(indicatorObject.activeSelf, Is.False);

                Assert.That(selectable.SetState(SelectionVisualState.Preview), Is.True);
                Assert.That(indicator.State, Is.EqualTo(SelectionVisualState.Preview));
                Assert.That(indicatorObject.activeSelf, Is.True);
                Assert.That(selectable.SetState(SelectionVisualState.Selected), Is.True);
                Assert.That(indicator.State, Is.EqualTo(SelectionVisualState.Selected));
                Assert.That(selectable.SetState(SelectionVisualState.Selected), Is.False);
                Assert.That(selectable.SetState(SelectionVisualState.Inspected), Is.True);
                Assert.That(indicator.State, Is.EqualTo(SelectionVisualState.Inspected));
                Assert.That(selectable.SetState(SelectionVisualState.Normal), Is.True);
                Assert.That(indicatorObject.activeSelf, Is.False);
                Assert.That(stateEvents, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void P05AssetsBindDefinitionsServicesAndPrefabContracts()
        {
            string[] definitionNames =
            {
                "SO_Unit_Hero",
                "SO_Unit_Friendly",
                "SO_Unit_Enemy",
                "SO_Unit_Worker",
                "SO_Unit_ProducerBuilding"
            };
            string[] archetypeIds =
            {
                "unit.hero",
                "unit.friendly",
                "unit.enemy",
                "unit.worker",
                "building.producer"
            };
            for (int i = 0; i < definitionNames.Length; i++)
            {
                UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                    DefinitionsFolder + "/" + definitionNames[i] + ".asset");
                Assert.That(definition, Is.Not.Null, "Run the P05 Apply menu before this test.");
                Assert.That(definition.ArchetypeId, Is.EqualTo(archetypeIds[i]));
            }

            GameObject appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(AppRootPrefabPath);
            Assert.That(appRoot, Is.Not.Null);
            GameServices services = appRoot.GetComponent<GameServices>();
            EntityRegistry registry = appRoot.GetComponent<EntityRegistry>();
            FactionRelationService relations = appRoot.GetComponent<FactionRelationService>();
            Assert.That(services, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(relations, Is.Not.Null);
            Assert.That(services.EntityRegistry, Is.SameAs(registry));
            Assert.That(services.FactionRelations, Is.SameAs(relations));
            Assert.That(registry.FactionRelations, Is.SameAs(relations));

            string[] damageablePrefabPaths =
            {
                "Assets/_OriginCore/Prefabs/Units/PF_HeroPlaceholder.prefab",
                "Assets/_OriginCore/Prefabs/Units/PF_UnitFriendly.prefab",
                "Assets/_OriginCore/Prefabs/Units/PF_UnitEnemy.prefab",
                "Assets/_OriginCore/Prefabs/Units/PF_WorkerPlaceholder.prefab",
                "Assets/_OriginCore/Prefabs/Buildings/PF_ProducerBuilding.prefab"
            };
            for (int i = 0; i < damageablePrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(damageablePrefabPaths[i]);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponent<EntityIdentity>(), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponent<FactionMember>(), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponent<VitalsComponent>(), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponent<DamageReceiver>(), Is.Not.Null, prefab.name);
                Assert.That(prefab.GetComponent<Selectable>(), Is.Not.Null, prefab.name);
                SelectionIndicatorController indicator =
                    prefab.GetComponent<SelectionIndicatorController>();
                Assert.That(indicator, Is.Not.Null, prefab.name);
                Assert.That(indicator.IndicatorRoot, Is.Not.Null, prefab.name);
                Assert.That(indicator.IndicatorRoot.activeSelf, Is.False, prefab.name);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
            }

            GameObject resource = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_OriginCore/Prefabs/Buildings/PF_ResourceCrystal.prefab");
            Assert.That(resource, Is.Not.Null);
            Assert.That(resource.GetComponent<EntityIdentity>().ArchetypeId,
                Is.EqualTo("resource.crystal"));
            Assert.That(resource.GetComponent<FactionMember>().Faction, Is.EqualTo(FactionId.Neutral));
            Assert.That(resource.GetComponent<EntityIdentity>().Roles.HasAny(UnitRole.Building),
                Is.True);
            Assert.That(resource.GetComponent<VitalsComponent>(), Is.Null);
            Assert.That(resource.GetComponent<DamageReceiver>(), Is.Null);
            Assert.That(resource.GetComponent<Selectable>(), Is.Null);
            Assert.That(resource.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Null);
            Assert.That(resource.GetComponent<OriginCore.Units.NavMeshMovementDriver>(), Is.Null);
        }

        private static EntityIdentity CreateRegisteredEntity(
            EntityRegistry registry,
            string runtimeId,
            FactionId factionId,
            UnitRole roles,
            bool selectable)
        {
            GameObject root = new GameObject(runtimeId);
            EntityIdentity identity = root.AddComponent<EntityIdentity>();
            FactionMember faction = root.AddComponent<FactionMember>();
            identity.ConfigureFallbackIdentity(runtimeId + "-archetype", runtimeId, roles);
            identity.AssignRuntimeId(runtimeId);
            faction.SetFaction(factionId);
            if (selectable)
            {
                Selectable selectableComponent = root.AddComponent<Selectable>();
                selectableComponent.Configure(identity, faction, null, null);
            }

            Assert.That(identity.RegisterWith(registry), Is.True);
            return identity;
        }
    }
}
