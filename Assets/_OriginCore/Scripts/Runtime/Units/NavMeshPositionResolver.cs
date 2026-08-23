using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Units
{
    /// <summary>
    /// Resolves requested world positions against the baked navigation data without
    /// allowing a failed sample or warp to detach an agent from the NavMesh.
    /// </summary>
    public static class NavMeshPositionResolver
    {
        private static readonly Vector2[] SearchDirections =
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0.7071068f, 0.7071068f),
            new Vector2(-0.7071068f, 0.7071068f),
            new Vector2(0.7071068f, -0.7071068f),
            new Vector2(-0.7071068f, -0.7071068f)
        };

        public static bool TryResolveReachablePosition(
            NavMeshAgent agent,
            Vector3 requestedPosition,
            float initialSampleDistance,
            float maximumRecoveryDistance,
            out Vector3 resolvedPosition)
        {
            resolvedPosition = default;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            float initial = Mathf.Max(0.05f, initialSampleDistance);
            float maximum = Mathf.Max(initial, maximumRecoveryDistance);
            NavMeshPath path = new NavMeshPath();
            bool found = false;
            float bestDistanceSquared = float.PositiveInfinity;

            EvaluateReachableCandidate(
                agent,
                requestedPosition,
                initial,
                path,
                requestedPosition,
                ref found,
                ref bestDistanceSquared,
                ref resolvedPosition);

            float ring = initial;
            while (ring <= maximum + 0.001f)
            {
                float localSampleDistance = Mathf.Max(0.35f, initial * 0.5f);
                for (int i = 0; i < SearchDirections.Length; i++)
                {
                    Vector2 direction = SearchDirections[i];
                    Vector3 probe = requestedPosition +
                                    new Vector3(direction.x * ring, 0f, direction.y * ring);
                    EvaluateReachableCandidate(
                        agent,
                        probe,
                        localSampleDistance,
                        path,
                        requestedPosition,
                        ref found,
                        ref bestDistanceSquared,
                        ref resolvedPosition);
                }

                ring += initial;
            }

            return found;
        }

        public static bool TryResolvePosition(
            Vector3 requestedPosition,
            int areaMask,
            float initialSampleDistance,
            float maximumRecoveryDistance,
            out Vector3 resolvedPosition)
        {
            resolvedPosition = default;
            float initial = Mathf.Max(0.05f, initialSampleDistance);
            float maximum = Mathf.Max(initial, maximumRecoveryDistance);
            bool found = false;
            float bestDistanceSquared = float.PositiveInfinity;

            EvaluateSampleCandidate(
                requestedPosition,
                initial,
                areaMask,
                requestedPosition,
                ref found,
                ref bestDistanceSquared,
                ref resolvedPosition);

            float ring = initial;
            while (ring <= maximum + 0.001f)
            {
                float localSampleDistance = Mathf.Max(0.35f, initial * 0.5f);
                for (int i = 0; i < SearchDirections.Length; i++)
                {
                    Vector2 direction = SearchDirections[i];
                    Vector3 probe = requestedPosition +
                                    new Vector3(direction.x * ring, 0f, direction.y * ring);
                    EvaluateSampleCandidate(
                        probe,
                        localSampleDistance,
                        areaMask,
                        requestedPosition,
                        ref found,
                        ref bestDistanceSquared,
                        ref resolvedPosition);
                }

                ring += initial;
            }

            return found;
        }

        private static void EvaluateReachableCandidate(
            NavMeshAgent agent,
            Vector3 probe,
            float sampleDistance,
            NavMeshPath path,
            Vector3 requestedPosition,
            ref bool found,
            ref float bestDistanceSquared,
            ref Vector3 resolvedPosition)
        {
            if (!NavMesh.SamplePosition(
                    probe,
                    out NavMeshHit hit,
                    sampleDistance,
                    agent.areaMask) ||
                !agent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return;
            }

            float distanceSquared = (hit.position - requestedPosition).sqrMagnitude;
            if (found && distanceSquared >= bestDistanceSquared)
            {
                return;
            }

            found = true;
            bestDistanceSquared = distanceSquared;
            resolvedPosition = hit.position;
        }

        private static void EvaluateSampleCandidate(
            Vector3 probe,
            float sampleDistance,
            int areaMask,
            Vector3 requestedPosition,
            ref bool found,
            ref float bestDistanceSquared,
            ref Vector3 resolvedPosition)
        {
            if (!NavMesh.SamplePosition(
                    probe,
                    out NavMeshHit hit,
                    sampleDistance,
                    areaMask))
            {
                return;
            }

            float distanceSquared = (hit.position - requestedPosition).sqrMagnitude;
            if (found && distanceSquared >= bestDistanceSquared)
            {
                return;
            }

            found = true;
            bestDistanceSquared = distanceSquared;
            resolvedPosition = hit.position;
        }
    }
}
