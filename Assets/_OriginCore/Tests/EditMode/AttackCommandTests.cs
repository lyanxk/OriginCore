using System.Collections.Generic;
using NUnit.Framework;
using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS.Commands;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OriginCore.Tests.EditMode
{
    public sealed class AttackCommandTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void InRangeAttackRespectsCooldownAppliesDamageAndCompletesAfterDeath()
        {
            FactionRelationService relations = CreateRelations();
            UnitDefinition attackerDefinition = CreateDefinition(3f, 25f, 0.5f);
            EntityIdentity attacker = CreateUnit(
                "Attacker",
                FactionId.Friendly,
                attackerDefinition,
                100f);
            EntityIdentity target = CreateUnit(
                "Target",
                FactionId.Enemy,
                null,
                40f);
            target.transform.position = Vector3.forward;

            AttackCapability capability = attacker.gameObject.AddComponent<AttackCapability>();
            capability.Configure(
                attacker,
                attacker.GetComponent<FactionMember>(),
                attacker.GetComponent<VitalsComponent>(),
                relations,
                null,
                ~0,
                false);
            AttackCommand command = new AttackCommand(target);
            command.Begin(new UnitCommandContext(
                null,
                attacker.gameObject,
                attacker,
                null,
                capability,
                null));

            Assert.That(command.Status, Is.EqualTo(CommandStatus.Running));
            command.Tick(0.016f);
            Assert.That(target.GetComponent<VitalsComponent>().Health, Is.EqualTo(15f));
            Assert.That(capability.SuccessfulAttackCount, Is.EqualTo(1));

            command.Tick(0.25f);
            Assert.That(target.GetComponent<VitalsComponent>().Health, Is.EqualTo(15f),
                "Cooldown must prevent a second damage application.");
            capability.AdvanceCooldown(0.5f);
            command.Tick(0.016f);
            Assert.That(target.GetComponent<VitalsComponent>().IsAlive, Is.False);
            Assert.That(capability.SuccessfulAttackCount, Is.EqualTo(2));

            command.Tick(0.016f);
            Assert.That(command.Status, Is.EqualTo(CommandStatus.Succeeded));
            Assert.That(command.FailureReason, Is.Empty);
        }

        [TestCase(FactionId.Friendly)]
        [TestCase(FactionId.Neutral)]
        public void AlliedAndNeutralTargetsAreRejectedWithoutDamage(FactionId targetFaction)
        {
            FactionRelationService relations = CreateRelations();
            EntityIdentity attacker = CreateUnit(
                "Attacker",
                FactionId.Friendly,
                CreateDefinition(3f, 25f, 0.5f),
                100f);
            EntityIdentity target = CreateUnit("InvalidTarget", targetFaction, null, 40f);
            target.transform.position = Vector3.forward;
            AttackCapability capability = attacker.gameObject.AddComponent<AttackCapability>();
            capability.Configure(
                attacker,
                attacker.GetComponent<FactionMember>(),
                attacker.GetComponent<VitalsComponent>(),
                relations,
                null,
                ~0,
                false);
            AttackCommand command = new AttackCommand(target);

            command.Begin(new UnitCommandContext(
                null,
                attacker.gameObject,
                attacker,
                null,
                capability,
                null));

            Assert.That(command.Status, Is.EqualTo(CommandStatus.Failed));
            Assert.That(command.FailureReason, Is.Not.Empty);
            Assert.That(target.GetComponent<VitalsComponent>().Health, Is.EqualTo(40f));
            Assert.That(capability.SuccessfulAttackCount, Is.Zero);
        }

        private FactionRelationService CreateRelations()
        {
            GameObject root = new GameObject("FactionRelations");
            _createdObjects.Add(root);
            return root.AddComponent<FactionRelationService>();
        }

        private UnitDefinition CreateDefinition(
            float attackRange,
            float attackDamage,
            float attackCooldown)
        {
            UnitDefinition definition = ScriptableObject.CreateInstance<UnitDefinition>();
            _createdObjects.Add(definition);
            definition.Configure(
                "test.attacker",
                "Test Attacker",
                UnitRole.Combat,
                FactionId.Friendly,
                4f,
                16f,
                720f,
                0.2f,
                12f,
                100f,
                0f,
                0f,
                attackRange,
                attackDamage,
                attackCooldown);
            return definition;
        }

        private EntityIdentity CreateUnit(
            string name,
            FactionId factionId,
            UnitDefinition definition,
            float maxHealth)
        {
            GameObject root = new GameObject(name);
            _createdObjects.Add(root);
            EntityIdentity identity = root.AddComponent<EntityIdentity>();
            if (definition != null)
            {
                identity.ConfigureDefinition(definition);
            }

            FactionMember faction = root.AddComponent<FactionMember>();
            faction.SetFaction(factionId);
            VitalsComponent vitals = root.AddComponent<VitalsComponent>();
            vitals.Configure(maxHealth, 0f, 0f, true);
            DamageReceiver receiver = root.AddComponent<DamageReceiver>();
            receiver.Configure(vitals);
            return identity;
        }
    }
}
