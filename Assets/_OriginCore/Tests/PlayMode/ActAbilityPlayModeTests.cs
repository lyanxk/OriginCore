using NUnit.Framework;
using OriginCore.ACT;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Tests.PlayMode
{
    public sealed class ActAbilityPlayModeTests
    {
        [Test]
        public void LaunchCreatesAVisibleArcAndReturnsToTheLandingHeight()
        {
            GameObject target = new GameObject("ACT_Launch_Target");
            target.SetActive(false);
            CombatStatusController statuses =
                target.AddComponent<CombatStatusController>();
            CombatMotionController motion =
                target.AddComponent<CombatMotionController>();
            target.SetActive(true);

            try
            {
                Vector3 start = target.transform.position;
                var launch = new WeaponStatusDefinition();
                launch.Configure(
                    WeaponStatusType.Launch,
                    4f,
                    0.4f,
                    WeaponStatusStackPolicy.Refresh);

                statuses.Apply(launch, null, Vector3.up);
                Assert.That(motion.HasDisplacement, Is.True);
                motion.Tick(0.2f);
                Assert.That(target.transform.position.y, Is.GreaterThan(start.y + 3.5f));

                motion.Tick(0.2f);
                Assert.That(motion.HasDisplacement, Is.False);
                Assert.That(target.transform.position.y,
                    Is.EqualTo(start.y).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void FlightGlidesAndDoubleJumpTogglesActiveFlight()
        {
            GameObject root = new GameObject("ACT_Flight_Test");
            root.SetActive(false);
            CharacterController character = root.AddComponent<CharacterController>();
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            HybridControlDriver driver = root.AddComponent<HybridControlDriver>();
            HybridPawnMotor motor = root.AddComponent<HybridPawnMotor>();
            FlightController flight = root.AddComponent<FlightController>();
            ActMovementController movement = root.AddComponent<ActMovementController>();
            GameObject target = new GameObject("ActCameraTarget");
            target.transform.SetParent(root.transform, false);
            MovementConfig config = ScriptableObject.CreateInstance<MovementConfig>();

            try
            {
                config.Configure(
                    6f, 24f, 0.35f, -24f, 1.5f, 1.2f, 1.1f,
                    0.15f, 0.12f, 1.5f, 9f, 0.25f, 720f, 0.12f,
                    -2f, 0.12f, -35f, 65f);
                motor.Configure(character);
                driver.Configure(agent, character, HybridControlState.Manual, 2f);
                movement.Configure(
                    driver,
                    motor,
                    config,
                    target.transform,
                    Physics.DefaultRaycastLayers,
                    Physics.DefaultRaycastLayers,
                    0);
                root.SetActive(true);

                Assert.That(flight.SetFlightEnabled(true), Is.True);
                movement.Step(CreateInput(false), Vector3.forward, Vector3.right, 0.05f);
                Assert.That(flight.FlightEnabled, Is.True);
                Assert.That(flight.IsGliding, Is.True);

                movement.Step(CreateInput(true), Vector3.forward, Vector3.right, 0.05f);
                movement.Step(CreateInput(false), Vector3.forward, Vector3.right, 0.05f);
                movement.Step(CreateInput(true), Vector3.forward, Vector3.right, 0.05f);
                Assert.That(flight.FlightAvailable, Is.True);
                Assert.That(flight.FlightEnabled, Is.False,
                    "The second Space press inside the tap window must cancel flight.");

                movement.Step(CreateInput(false), Vector3.forward, Vector3.right, 0.05f);
                movement.Step(CreateInput(true), Vector3.forward, Vector3.right, 0.05f);
                movement.Step(CreateInput(false), Vector3.forward, Vector3.right, 0.05f);
                movement.Step(CreateInput(true), Vector3.forward, Vector3.right, 0.05f);
                Assert.That(flight.FlightEnabled, Is.True,
                    "A later double Space press must re-enter flight while the form allows it.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(config);
            }
        }

        private static ActInputSnapshot CreateInput(bool jumpPressed)
        {
            InputButtonState jump = new InputButtonState(
                jumpPressed,
                jumpPressed,
                false);
            return new ActInputSnapshot(
                Vector2.zero,
                Vector2.zero,
                jump,
                default,
                default,
                default,
                default,
                default,
                default,
                default);
        }
    }
}
