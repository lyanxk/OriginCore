using UnityEngine;

namespace OriginCore.FPS
{
    public enum SlideExitReason
    {
        None = 0,
        Duration = 1,
        Speed = 2,
        Collision = 3,
        Jump = 4,
        ModeExit = 5
    }

    public sealed class SlideState
    {
        public bool IsSliding { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Speed { get; private set; }
        public float Elapsed { get; private set; }
        public SlideExitReason LastExitReason { get; private set; }
        public int EntryCount { get; private set; }

        public bool TryBegin(
            Vector3 currentPlanarVelocity,
            Vector3 forward,
            bool grounded,
            FpsMovementConfig config)
        {
            if (IsSliding || !grounded || config == null)
            {
                return false;
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(
                currentPlanarVelocity,
                Vector3.up);
            float currentSpeed = planarVelocity.magnitude;
            if (currentSpeed + 0.0001f < config.SlideMinSpeed)
            {
                return false;
            }

            Vector3 planarForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                planarForward = planarVelocity;
            }

            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            IsSliding = true;
            Direction = planarForward.normalized;
            Speed = currentSpeed + config.SlideImpulse;
            Elapsed = 0f;
            LastExitReason = SlideExitReason.None;
            EntryCount++;
            return true;
        }

        public Vector3 Advance(float deltaTime, FpsMovementConfig config)
        {
            return Advance(deltaTime, config, 1f);
        }

        public Vector3 Advance(
            float deltaTime,
            FpsMovementConfig config,
            float frictionMultiplier)
        {
            if (!IsSliding || config == null || deltaTime <= 0f)
            {
                return IsSliding ? Direction * Speed : Vector3.zero;
            }

            Elapsed += deltaTime;
            Speed = Mathf.Max(
                0f,
                Speed - config.SlideFriction * Mathf.Max(0f, frictionMultiplier) * deltaTime);
            Vector3 velocity = Direction * Speed;
            if (Elapsed >= config.SlideDuration)
            {
                End(SlideExitReason.Duration);
            }
            else if (Speed <= config.SlideMinSpeed * 0.5f)
            {
                End(SlideExitReason.Speed);
            }

            return velocity;
        }

        public bool EndForCollision(CollisionFlags collisionFlags)
        {
            if (!IsSliding || (collisionFlags & CollisionFlags.Sides) == 0)
            {
                return false;
            }

            End(SlideExitReason.Collision);
            return true;
        }

        public bool End(SlideExitReason reason)
        {
            if (!IsSliding)
            {
                return false;
            }

            IsSliding = false;
            Speed = 0f;
            LastExitReason = reason;
            return true;
        }

        public void Reset()
        {
            IsSliding = false;
            Direction = Vector3.zero;
            Speed = 0f;
            Elapsed = 0f;
            LastExitReason = SlideExitReason.None;
            EntryCount = 0;
        }
    }
}
