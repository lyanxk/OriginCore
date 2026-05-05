using UnityEngine;
using UnityEngine.AI;

namespace Navigation
{
    public static class NavMeshRoadNetwork
    {
        public const float DefaultSampleRadius = 2f;
        public const float DefaultStartSampleRadius = 1f;

        public static bool TrySample(
            Vector3 worldPosition,
            float sampleRadius,
            out Vector3 sampledPosition,
            int areaMask = NavMesh.AllAreas)
        {
            float radius = Mathf.Max(0.05f, sampleRadius);
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, radius, areaMask))
            {
                sampledPosition = hit.position;
                return true;
            }

            sampledPosition = worldPosition;
            return false;
        }

        public static bool TryResolveDestination(
            Vector3 destination,
            float sampleRadius,
            out Vector3 resolvedDestination,
            int areaMask = NavMesh.AllAreas)
        {
            return TrySample(destination, sampleRadius, out resolvedDestination, areaMask);
        }

        public static bool TryBuildPath(
            Vector3 worldStart,
            Vector3 worldDestination,
            NavMeshPath outputPath,
            float startSampleRadius = DefaultStartSampleRadius,
            float destinationSampleRadius = DefaultSampleRadius,
            int areaMask = NavMesh.AllAreas)
        {
            if (outputPath == null)
                return false;

            if (!TrySample(worldStart, startSampleRadius, out Vector3 start, areaMask))
                return false;

            if (!TrySample(worldDestination, destinationSampleRadius, out Vector3 destination, areaMask))
                return false;

            bool hasPath = NavMesh.CalculatePath(start, destination, areaMask, outputPath);
            if (!hasPath)
                return false;

            return outputPath.status == NavMeshPathStatus.PathComplete &&
                   outputPath.corners != null &&
                   outputPath.corners.Length > 0;
        }

        public static bool IsReachable(
            Vector3 worldStart,
            Vector3 worldDestination,
            NavMeshPath cachePath,
            float startSampleRadius = DefaultStartSampleRadius,
            float destinationSampleRadius = DefaultSampleRadius,
            int areaMask = NavMesh.AllAreas)
        {
            return TryBuildPath(
                worldStart,
                worldDestination,
                cachePath,
                startSampleRadius,
                destinationSampleRadius,
                areaMask);
        }
    }
}
