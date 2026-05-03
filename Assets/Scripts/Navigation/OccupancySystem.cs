using System.Collections.Generic;
using Unit.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace Navigation
{
    [DisallowMultipleComponent]
    public sealed class OccupancySystem : MonoBehaviour
    {
        struct UnitEntry
        {
            public UnitBase Motor;
            public float Radius;
        }

        struct BuildingEntry
        {
            public Object Owner;
            public Vector3 Center;
            public Vector2 HalfExtents;
            public float Radius;
        }

        struct ClaimEntry
        {
            public int UnitId;
            public Vector3 Position;
            public float Radius;
        }

        static OccupancySystem s_instance;

        Dictionary<int, UnitEntry> _units;
        Dictionary<int, BuildingEntry> _buildings;
        Dictionary<int, ClaimEntry> _claimsByUnit;
        NavMeshPath _reachabilityPath;
        List<int> _deadUnitIds;
        List<int> _deadBuildingIds;

        public static OccupancySystem Instance
        {
            get
            {
                if (s_instance != null)
                    return s_instance;

                s_instance = FindObjectOfType<OccupancySystem>();
                if (s_instance != null)
                    return s_instance;

                var root = new GameObject(nameof(OccupancySystem));
                s_instance = root.AddComponent<OccupancySystem>();
                return s_instance;
            }
        }

        public static bool TryGetInstance(out OccupancySystem system)
        {
            system = s_instance ?? FindObjectOfType<OccupancySystem>();
            return system != null;
        }

        void Awake()
        {
            EnsureInitialized();

            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        public void RegisterOrUpdateUnit(UnitBase unit, float radius)
        {
            EnsureInitialized();

            if (unit == null)
                return;

            int unitId = unit.GetInstanceID();
            _units[unitId] = new UnitEntry
            {
                Motor = unit,
                Radius = Mathf.Max(0.15f, radius)
            };
        }

        public void UnregisterUnit(UnitBase unit)
        {
            EnsureInitialized();

            if (unit == null)
                return;

            int unitId = unit.GetInstanceID();
            _units.Remove(unitId);
            _claimsByUnit.Remove(unitId);
        }

        public int RegisterOrUpdateBuilding(Object owner, Vector3 center, Vector2 halfExtents, float radius)
        {
            EnsureInitialized();

            int ownerId = GetOwnerId(owner);
            if (ownerId == 0)
                return 0;

            _buildings[ownerId] = new BuildingEntry
            {
                Owner = owner,
                Center = center,
                HalfExtents = new Vector2(
                    Mathf.Max(0.1f, halfExtents.x),
                    Mathf.Max(0.1f, halfExtents.y)),
                Radius = Mathf.Max(0.25f, radius)
            };

            return ownerId;
        }

        public int RegisterOrUpdateBuilding(Object owner, Vector3 center, float radius)
        {
            float safeRadius = Mathf.Max(0.25f, radius);
            return RegisterOrUpdateBuilding(owner, center, new Vector2(safeRadius, safeRadius), safeRadius);
        }

        public void UnregisterBuilding(Object owner)
        {
            EnsureInitialized();

            int ownerId = GetOwnerId(owner);
            if (ownerId == 0)
                return;

            _buildings.Remove(ownerId);
        }

        public bool TryGetClaim(UnitBase unit, out Vector3 point)
        {
            EnsureInitialized();

            point = default;
            if (unit == null)
                return false;

            int unitId = unit.GetInstanceID();
            if (!_claimsByUnit.TryGetValue(unitId, out ClaimEntry claim))
                return false;

            point = claim.Position;
            return true;
        }

        public void ReleaseClaim(UnitBase unit)
        {
            EnsureInitialized();

            if (unit == null)
                return;

            _claimsByUnit.Remove(unit.GetInstanceID());
        }

        public bool IsPositionOccupied(
            Vector3 position,
            float radius,
            UnitBase ignoreUnit = null,
            Object ignoreBuildingOwner = null)
        {
            EnsureInitialized();
            PruneDeadEntries();

            int ignoreUnitId = ignoreUnit != null ? ignoreUnit.GetInstanceID() : 0;
            int ignoreBuildingId = GetOwnerId(ignoreBuildingOwner);
            float checkRadius = Mathf.Max(0.05f, radius);

            foreach (KeyValuePair<int, BuildingEntry> pair in _buildings)
            {
                if (pair.Key == ignoreBuildingId)
                    continue;

                BuildingEntry building = pair.Value;
                if (IsOverlappingBuildingXZ(position, checkRadius, building))
                    return true;
            }

            foreach (KeyValuePair<int, UnitEntry> pair in _units)
            {
                if (pair.Key == ignoreUnitId)
                    continue;

                UnitEntry other = pair.Value;
                if (other.Motor == null)
                    continue;

                if (IsOverlappingXZ(position, checkRadius, other.Motor.transform.position, other.Radius))
                    return true;
            }

            foreach (KeyValuePair<int, ClaimEntry> pair in _claimsByUnit)
            {
                if (pair.Key == ignoreUnitId)
                    continue;

                ClaimEntry claim = pair.Value;
                if (IsOverlappingXZ(position, checkRadius, claim.Position, claim.Radius))
                    return true;
            }

            return false;
        }

        public bool IsOverlappingBuilding(
            Vector3 position,
            float radius,
            Object ignoreBuildingOwner = null)
        {
            EnsureInitialized();
            PruneDeadEntries();

            int ignoreBuildingId = GetOwnerId(ignoreBuildingOwner);
            float checkRadius = Mathf.Max(0.05f, radius);

            foreach (KeyValuePair<int, BuildingEntry> pair in _buildings)
            {
                if (pair.Key == ignoreBuildingId)
                    continue;

                if (IsOverlappingBuildingXZ(position, checkRadius, pair.Value))
                    return true;
            }

            return false;
        }

        public bool TryAssignStandingPoint(
            UnitBase unit,
            Vector3 targetCenter,
            OccupancyStandingRequest request,
            out Vector3 standingPoint)
        {
            EnsureInitialized();

            standingPoint = targetCenter;
            if (unit == null)
                return false;

            request.ClampToSafeRange();
            RegisterOrUpdateUnit(unit, request.UnitRadius);

            bool found = request.SearchMode == OccupancySearchMode.RingAroundTarget
                ? TryFindFromRing(unit, targetCenter, request, out standingPoint)
                : TryFindNearestValid(unit, targetCenter, request, out standingPoint);

            if (!found && request.SearchMode == OccupancySearchMode.RingAroundTarget)
                found = TryFindNearestValid(unit, targetCenter, request, out standingPoint);

            if (!found)
                return false;

            int unitId = unit.GetInstanceID();
            _claimsByUnit[unitId] = new ClaimEntry
            {
                UnitId = unitId,
                Position = standingPoint,
                Radius = request.UnitRadius
            };
            return true;
        }

        public Vector3 ComputeUnitSeparation(UnitBase self, Vector3 position, float radius, float extraRange)
        {
            EnsureInitialized();
            PruneDeadEntries();
            if (self == null)
                return Vector3.zero;

            int selfId = self.GetInstanceID();
            float range = Mathf.Max(0f, extraRange);
            Vector3 push = Vector3.zero;

            foreach (KeyValuePair<int, UnitEntry> pair in _units)
            {
                if (pair.Key == selfId)
                    continue;

                UnitEntry other = pair.Value;
                if (other.Motor == null)
                    continue;

                Vector3 delta = position - other.Motor.transform.position;
                delta.y = 0f;

                float distance = delta.magnitude;
                float targetDistance = radius + other.Radius + range;
                if (distance >= targetDistance)
                    continue;

                if (distance <= 0.001f)
                {
                    float seed = (selfId * 0.6180339f) % 1f;
                    Vector3 pseudo = Quaternion.Euler(0f, seed * 360f, 0f) * Vector3.forward;
                    push += pseudo;
                    continue;
                }

                float t = 1f - distance / targetDistance;
                push += delta / distance * t;
            }

            return push;
        }

        public Vector3 ComputeBuildingAvoidance(
            Vector3 position,
            Vector3 desiredDirection,
            float radius,
            float extraRange,
            float radialStrength = 1f,
            float tangentStrength = 1f)
        {
            EnsureInitialized();
            PruneDeadEntries();

            float range = Mathf.Max(0f, extraRange);
            float safeRadialStrength = Mathf.Max(0f, radialStrength);
            float safeTangentStrength = Mathf.Max(0f, tangentStrength);
            Vector3 radialPush = Vector3.zero;
            Vector3 tangentPush = Vector3.zero;
            Vector3 desired = desiredDirection;
            desired.y = 0f;
            if (desired.sqrMagnitude > 1e-6f)
                desired.Normalize();

            foreach (KeyValuePair<int, BuildingEntry> pair in _buildings)
            {
                BuildingEntry building = pair.Value;
                float targetDistance = radius + range;
                float signedDistance = GetSignedDistanceToBuildingXZ(position, building, out Vector3 normal);
                if (signedDistance >= targetDistance)
                    continue;

                float normalizedGap = Mathf.Clamp01((targetDistance - signedDistance) / Mathf.Max(0.001f, targetDistance));
                float radialWeight = normalizedGap * normalizedGap;
                if (signedDistance < 0f)
                {
                    radialWeight += 1f + Mathf.Clamp01(-signedDistance / Mathf.Max(0.001f, targetDistance));
                }

                radialPush += normal * radialWeight * safeRadialStrength;

                if (desired.sqrMagnitude <= 1e-6f || safeTangentStrength <= 0f)
                    continue;

                Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                if (Vector3.Dot(tangent, desired) < 0f)
                    tangent = -tangent;

                float tangentAlignment = Mathf.Max(0f, Vector3.Dot(tangent, desired));
                float tangentWeight = tangentAlignment * radialWeight;
                tangentPush += tangent * tangentWeight * safeTangentStrength;
            }

            return radialPush + tangentPush;
        }

        public bool TryGetBuildingPathSteering(
            Vector3 position,
            Vector3 desiredDirection,
            float unitRadius,
            float lookAheadDistance,
            float extraClearance,
            out Vector3 steering)
        {
            EnsureInitialized();
            PruneDeadEntries();

            steering = Vector3.zero;

            Vector3 desired = desiredDirection;
            desired.y = 0f;
            if (desired.sqrMagnitude <= 1e-6f)
                return false;

            desired.Normalize();
            float lookAhead = Mathf.Max(unitRadius, lookAheadDistance);
            float clearance = Mathf.Max(0f, extraClearance);
            float bestThreat = float.MaxValue;
            const int SampleCount = 4;

            foreach (KeyValuePair<int, BuildingEntry> pair in _buildings)
            {
                BuildingEntry building = pair.Value;
                for (int sampleIndex = 1; sampleIndex <= SampleCount; sampleIndex++)
                {
                    float t = sampleIndex / (float)SampleCount;
                    Vector3 samplePoint = position + desired * lookAhead * t;
                    float signedDistance = GetSignedDistanceToBuildingXZ(samplePoint, building, out Vector3 radial);
                    float targetDistance = unitRadius + clearance;
                    if (signedDistance >= targetDistance)
                        continue;

                    Vector3 tangentA = Vector3.Cross(Vector3.up, radial);
                    Vector3 tangentB = -tangentA;
                    Vector3 tangent = Vector3.Dot(tangentA, desired) >= Vector3.Dot(tangentB, desired)
                        ? tangentA
                        : tangentB;

                    float urgency = 1f - t;
                    float proximity = Mathf.Clamp01((targetDistance - signedDistance) / Mathf.Max(0.001f, targetDistance));
                    float overlapBoost = signedDistance < 0f
                        ? 1f + Mathf.Clamp01(-signedDistance / Mathf.Max(0.001f, targetDistance))
                        : 1f;
                    float weight = Mathf.Max(0.2f, urgency) * proximity * overlapBoost;
                    Vector3 candidateSteer = tangent * weight + radial * weight * 0.75f;
                    float threat = t * lookAhead + Mathf.Max(0f, signedDistance) * 0.35f;
                    if (threat >= bestThreat || candidateSteer.sqrMagnitude <= 1e-6f)
                        continue;

                    bestThreat = threat;
                    steering = candidateSteer;
                }
            }

            return steering.sqrMagnitude > 1e-6f;
        }

        public bool TryFindNearestLegalPoint(
            UnitBase unit,
            Vector3 origin,
            float radius,
            float searchRadius,
            out Vector3 legalPoint)
        {
            EnsureInitialized();

            legalPoint = origin;
            if (unit == null)
                return false;

            OccupancyStandingRequest request = OccupancyStandingRequest.CreateDefault(radius);
            request.SearchMode = OccupancySearchMode.NearestValid;
            request.MinRadiusFromTarget = 0f;
            request.MaxRadiusFromTarget = Mathf.Max(radius, searchRadius);
            request.RequirePathReachable = false;
            request.RadiusSamples = 6;
            request.AngleSamples = 24;
            request.ClampToSafeRange();

            return TryFindNearestValid(unit, origin, request, out legalPoint);
        }

        bool TryFindFromRing(
            UnitBase unit,
            Vector3 targetCenter,
            OccupancyStandingRequest request,
            out Vector3 standingPoint)
        {
            standingPoint = targetCenter;
            int unitId = unit.GetInstanceID();
            float ringStart = Mathf.Max(request.MinRadiusFromTarget, request.UnitRadius);
            float ringEnd = Mathf.Max(ringStart, request.MaxRadiusFromTarget);
            int ringSamples = Mathf.Max(1, request.RadiusSamples);
            int slots = Mathf.Max(4, request.RingSlotCount);

            for (int ringIndex = 0; ringIndex < ringSamples; ringIndex++)
            {
                float t = ringSamples <= 1 ? 0f : ringIndex / (float)(ringSamples - 1);
                float ringRadius = Mathf.Lerp(ringStart, ringEnd, t);

                int slotStart = Mathf.Abs(unitId) % slots;
                float baseAngle = (Mathf.Abs(unitId) * 0.381966f) * Mathf.PI * 2f;
                for (int i = 0; i < slots; i++)
                {
                    int slotIndex = (slotStart + i) % slots;
                    float angle = baseAngle + slotIndex * (Mathf.PI * 2f / slots);
                    Vector3 candidate = targetCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;

                    if (!TryResolveCandidate(unit, candidate, request, out Vector3 resolvedCandidate))
                        continue;

                    standingPoint = resolvedCandidate;
                    return true;
                }
            }

            return false;
        }

        bool TryFindNearestValid(
            UnitBase unit,
            Vector3 targetCenter,
            OccupancyStandingRequest request,
            out Vector3 standingPoint)
        {
            standingPoint = targetCenter;
            int angleSamples = Mathf.Max(6, request.AngleSamples);
            int radiusSamples = Mathf.Max(1, request.RadiusSamples);
            float minRadius = Mathf.Max(0f, request.MinRadiusFromTarget);
            float maxRadius = Mathf.Max(minRadius, request.MaxRadiusFromTarget);
            float bestSqr = float.MaxValue;
            bool found = false;

            if (TryResolveCandidate(unit, targetCenter, request, out Vector3 centerCandidate))
            {
                standingPoint = centerCandidate;
                bestSqr = FlattenedSqr(centerCandidate - targetCenter);
                found = true;
            }

            for (int radiusIndex = 0; radiusIndex < radiusSamples; radiusIndex++)
            {
                float t = radiusSamples <= 1 ? 0f : radiusIndex / (float)(radiusSamples - 1);
                float radius = Mathf.Lerp(minRadius, maxRadius, t);
                for (int angleIndex = 0; angleIndex < angleSamples; angleIndex++)
                {
                    float angle = angleIndex / (float)angleSamples * Mathf.PI * 2f;
                    Vector3 candidate = targetCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                    if (!TryResolveCandidate(unit, candidate, request, out Vector3 resolved))
                        continue;

                    float sqr = FlattenedSqr(resolved - targetCenter);
                    if (sqr >= bestSqr)
                        continue;

                    bestSqr = sqr;
                    standingPoint = resolved;
                    found = true;
                }
            }

            return found;
        }

        bool TryResolveCandidate(
            UnitBase unit,
            Vector3 candidate,
            OccupancyStandingRequest request,
            out Vector3 resolvedCandidate)
        {
            resolvedCandidate = candidate;
            if (!NavMeshRoadNetwork.TrySample(candidate, request.UnitRadius + 0.6f, out resolvedCandidate))
                return false;

            if (IsPositionOccupied(resolvedCandidate, request.UnitRadius, unit))
                return false;

            if (!request.RequirePathReachable)
                return true;

            if (unit == null)
                return false;

            Vector3 unitPos = unit.transform.position;
            return NavMeshRoadNetwork.IsReachable(
                unitPos,
                resolvedCandidate,
                _reachabilityPath,
                NavMeshRoadNetwork.DefaultStartSampleRadius,
                request.UnitRadius + 0.6f);
        }

        void PruneDeadEntries()
        {
            EnsureInitialized();

            if (_units.Count > 0)
            {
                _deadUnitIds.Clear();
                foreach (KeyValuePair<int, UnitEntry> pair in _units)
                {
                    UnitBase motor = pair.Value.Motor;
                    if (motor == null)
                        _deadUnitIds.Add(pair.Key);
                }

                for (int i = 0; i < _deadUnitIds.Count; i++)
                {
                    int id = _deadUnitIds[i];
                    _units.Remove(id);
                    _claimsByUnit.Remove(id);
                }
            }

            if (_buildings.Count > 0)
            {
                _deadBuildingIds.Clear();
                foreach (KeyValuePair<int, BuildingEntry> pair in _buildings)
                {
                    if (pair.Value.Owner == null)
                        _deadBuildingIds.Add(pair.Key);
                }

                for (int i = 0; i < _deadBuildingIds.Count; i++)
                    _buildings.Remove(_deadBuildingIds[i]);
            }
        }

        void EnsureInitialized()
        {
            _units ??= new Dictionary<int, UnitEntry>(128);
            _buildings ??= new Dictionary<int, BuildingEntry>(128);
            _claimsByUnit ??= new Dictionary<int, ClaimEntry>(128);
            _deadUnitIds ??= new List<int>(16);
            _deadBuildingIds ??= new List<int>(16);
            _reachabilityPath ??= new NavMeshPath();
        }

        static int GetOwnerId(Object owner)
        {
            return owner != null ? owner.GetInstanceID() : 0;
        }

        static bool IsOverlappingXZ(Vector3 a, float aRadius, Vector3 b, float bRadius)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            float minDistance = Mathf.Max(0f, aRadius) + Mathf.Max(0f, bRadius);
            return delta.sqrMagnitude <= minDistance * minDistance;
        }

        static bool IsOverlappingBuildingXZ(Vector3 position, float radius, BuildingEntry building)
        {
            return GetSignedDistanceToBuildingXZ(position, building, out _) <= Mathf.Max(0f, radius);
        }

        static float GetSignedDistanceToBuildingXZ(Vector3 position, BuildingEntry building, out Vector3 normal)
        {
            Vector3 local = position - building.Center;
            local.y = 0f;

            Vector2 half = building.HalfExtents;
            float clampedX = Mathf.Clamp(local.x, -half.x, half.x);
            float clampedZ = Mathf.Clamp(local.z, -half.y, half.y);
            Vector3 closest = new Vector3(
                building.Center.x + clampedX,
                position.y,
                building.Center.z + clampedZ);

            Vector3 delta = position - closest;
            delta.y = 0f;
            float outsideDistance = delta.magnitude;
            if (outsideDistance > 0.001f)
            {
                normal = delta / outsideDistance;
                return outsideDistance;
            }

            float left = local.x + half.x;
            float right = half.x - local.x;
            float back = local.z + half.y;
            float forward = half.y - local.z;
            float minEdge = left;
            normal = Vector3.left;

            if (right < minEdge)
            {
                minEdge = right;
                normal = Vector3.right;
            }

            if (back < minEdge)
            {
                minEdge = back;
                normal = Vector3.back;
            }

            if (forward < minEdge)
            {
                minEdge = forward;
                normal = Vector3.forward;
            }

            return -Mathf.Max(0f, minEdge);
        }

        static float FlattenedSqr(Vector3 delta)
        {
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
    }
}
