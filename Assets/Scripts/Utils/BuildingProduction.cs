using System;
using System.Collections.Generic;
using Unit.Command;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public struct BuildingProductionSlot
{
    public GameObject UnitPrefab;
    public Sprite Icon;
    public string DisplayName;
    public string HotkeyText;

    [TextArea]
    public string Tooltip;
}

[DisallowMultipleComponent]
public class BuildingProduction : MonoBehaviour, IGroundCommandReceiver, ISelectionRouteProvider
{
    const string GroundLayerName = "Ground";
    const string LegacyGroundLayerName = "Groud";
    const int OverlapBufferSize = 64;
    const int AngleSampleCount = 24;
    const int RadiusSampleCount = 5;
    const float SpawnSearchPadding = 2f;
    const float MinRingOffset = 0.2f;
    const float NavSampleRadius = 0.8f;
    const float DefaultBuildingRadius = 1f;
    const float DefaultUnitClearance = 0.5f;
    const float RallyPointSampleRadius = 2f;

    public const int MaxSlotCount = 8;

    [Header("Production List")]
    [SerializeField] BuildingProductionSlot[] slots = new BuildingProductionSlot[MaxSlotCount];

    readonly Queue<int> _productionQueue = new Queue<int>(16);
    readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
    readonly HashSet<Collider> _buildingColliderSet = new HashSet<Collider>();
    readonly Dictionary<int, float> _unitClearanceRadiusCache = new Dictionary<int, float>(MaxSlotCount);

    TeamAffiliation _teamAffiliation;
    Vector3 _buildingCenter;
    Vector2 _buildingHalfExtents = Vector2.one * DefaultBuildingRadius;
    float _buildingRadius = DefaultBuildingRadius;
    int _occupancyBuildingId;
    [SerializeField, HideInInspector] bool _hasRallyPoint;
    [SerializeField, HideInInspector] bool _usesDefaultRallyPoint = true;
    [SerializeField, HideInInspector] Vector3 _rallyPoint;

    void Awake()
    {
        EnsureSlotArray();
        CacheUnitClearanceRadii();
        CacheTeam();
        CacheBuildingColliderProfile();
        EnsureRallyPoint();
    }

    void OnEnable()
    {
        SyncBuildingOccupancy();
    }

    void OnDisable()
    {
        ReleaseBuildingOccupancy();
    }

    void OnDestroy()
    {
        ReleaseBuildingOccupancy();
    }

    void OnValidate()
    {
        EnsureSlotArray();
        CacheUnitClearanceRadii();
        CacheTeam();
        CacheBuildingColliderProfile();
        EnsureRallyPoint();
        SyncBuildingOccupancy();
    }

    void Update()
    {
        SyncBuildingOccupancy();
        TryProcessQueueHead();
    }

    public bool TryGetSlot(int index, out BuildingProductionSlot slot)
    {
        slot = default;
        if (index < 0 || index >= MaxSlotCount)
            return false;

        if (slots == null || index >= slots.Length)
            return false;

        slot = slots[index];
        return slot.UnitPrefab != null;
    }

    public bool TryProduce(string productionId)
    {
        if (!CommandEntryIds.TryParseProductionIndex(productionId, out int index))
            return false;

        return TryProduceAt(index);
    }

    public bool TryProduceAt(int index)
    {
        if (!TryGetSlot(index, out _))
            return false;

        _productionQueue.Enqueue(index);
        TryProcessQueueHead();
        return true;
    }

    void CacheTeam()
    {
        if (_teamAffiliation != null)
            return;

        TryGetComponent(out _teamAffiliation);
    }

    void CacheUnitClearanceRadii()
    {
        _unitClearanceRadiusCache.Clear();
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            GameObject unitPrefab = slots[i].UnitPrefab;
            if (unitPrefab == null)
                continue;

            int prefabId = unitPrefab.GetInstanceID();
            if (_unitClearanceRadiusCache.ContainsKey(prefabId))
                continue;

            _unitClearanceRadiusCache[prefabId] = ComputeUnitClearanceRadius(unitPrefab);
        }
    }

    void CacheBuildingColliderProfile()
    {
        _buildingColliderSet.Clear();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        bool hasBounds = false;
        Bounds mergedBounds = default;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            _buildingColliderSet.Add(collider);

            if (!hasBounds)
            {
                mergedBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                mergedBounds.Encapsulate(collider.bounds);
            }
        }

        _buildingRadius = hasBounds
            ? Mathf.Max(mergedBounds.extents.x, mergedBounds.extents.z)
            : DefaultBuildingRadius;
        _buildingCenter = hasBounds ? mergedBounds.center : transform.position;
        _buildingHalfExtents = hasBounds
            ? new Vector2(
                Mathf.Max(0.1f, mergedBounds.extents.x),
                Mathf.Max(0.1f, mergedBounds.extents.z))
            : Vector2.one * DefaultBuildingRadius;
    }

    bool TryProcessQueueHead()
    {
        if (_productionQueue.Count <= 0)
            return false;

        int slotIndex = _productionQueue.Peek();
        if (!TryGetSlot(slotIndex, out BuildingProductionSlot slot))
        {
            _productionQueue.Dequeue();
            return true;
        }

        if (!TryFindSpawnPosition(slot.UnitPrefab, out Vector3 spawnPos))
            return false;

        GameObject spawned = Instantiate(slot.UnitPrefab, spawnPos, transform.rotation);
        _productionQueue.Dequeue();
        InheritTeam(spawned);
        DispatchSpawnedUnitToRally(spawned);
        return true;
    }

    bool TryFindSpawnPosition(GameObject unitPrefab, out Vector3 spawnPos)
    {
        spawnPos = transform.position;

        float unitClearance = ResolveUnitClearanceRadius(unitPrefab);
        float minRadius = Mathf.Max(0f, _buildingRadius + MinRingOffset);
        float maxRadius = _buildingRadius + SpawnSearchPadding;
        if (maxRadius < minRadius)
            maxRadius = minRadius;

        Vector3 center = _buildingCenter;
        Vector3 forward = transform.forward;
        float baseAngle = Mathf.Atan2(forward.z, forward.x);

        for (int radiusStep = 0; radiusStep < RadiusSampleCount; radiusStep++)
        {
            float radiusT = RadiusSampleCount <= 1
                ? 0f
                : radiusStep / (float)(RadiusSampleCount - 1);

            float radius = Mathf.Lerp(minRadius, maxRadius, radiusT);
            for (int angleStep = 0; angleStep < AngleSampleCount; angleStep++)
            {
                float angleT = angleStep / (float)AngleSampleCount;
                float angle = baseAngle + angleT * Mathf.PI * 2f;

                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                candidate.y = center.y;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, NavSampleRadius, NavMesh.AllAreas))
                    candidate = navHit.position;

                if (!IsPositionAvailable(candidate, unitClearance))
                    continue;

                spawnPos = candidate;
                return true;
            }
        }

        return false;
    }

    bool IsPositionAvailable(Vector3 worldPos, float clearanceRadius)
    {
        if (OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
        {
            if (occupancy.IsPositionOccupied(worldPos, clearanceRadius, ignoreBuildingOwner: this))
                return false;
        }

        // Check the unit body volume above the floor so support surfaces do not block valid spawn points.
        Vector3 clearanceCenter = worldPos + Vector3.up * Mathf.Max(clearanceRadius, 0.05f);
        int hitCount = Physics.OverlapSphereNonAlloc(
            clearanceCenter,
            clearanceRadius,
            _overlapBuffer,
            GetSpawnBlockerMask(),
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (IsBuildingCollider(hit))
                continue;

            return false;
        }

        return true;
    }

    static int GetSpawnBlockerMask()
    {
        int mask = ~0;
        int groundLayer = ResolveGroundLayer();
        if (groundLayer >= 0)
            mask &= ~(1 << groundLayer);

        return mask;
    }

    static int ResolveGroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        if (groundLayer >= 0)
            return groundLayer;

        return LayerMask.NameToLayer(LegacyGroundLayerName);
    }

    bool IsBuildingCollider(Collider collider)
    {
        if (collider == null)
            return true;

        return _buildingColliderSet.Contains(collider) || collider.transform.IsChildOf(transform);
    }

    float ResolveUnitClearanceRadius(GameObject unitPrefab)
    {
        if (unitPrefab == null)
            return DefaultUnitClearance;

        int prefabId = unitPrefab.GetInstanceID();
        if (_unitClearanceRadiusCache.TryGetValue(prefabId, out float cachedRadius))
            return cachedRadius;

        float radius = ComputeUnitClearanceRadius(unitPrefab);
        _unitClearanceRadiusCache[prefabId] = radius;
        return radius;
    }

    static float ComputeUnitClearanceRadius(GameObject unitPrefab)
    {
        if (unitPrefab == null)
            return DefaultUnitClearance;

        float radius = 0f;
        bool foundCharacterController = false;
        bool foundCapsule = false;
        bool foundSphere = false;
        bool foundBox = false;
        Collider[] colliders = unitPrefab.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
            return DefaultUnitClearance;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            switch (collider)
            {
                case CharacterController characterController when !foundCharacterController:
                    foundCharacterController = true;
                    radius = Mathf.Max(radius, characterController.radius);
                    break;

                case CapsuleCollider capsule when !foundCapsule:
                    foundCapsule = true;
                    radius = Mathf.Max(radius, capsule.radius);
                    break;

                case SphereCollider sphere when !foundSphere:
                    foundSphere = true;
                    radius = Mathf.Max(radius, sphere.radius);
                    break;

                case BoxCollider box when !foundBox:
                    foundBox = true;
                    radius = Mathf.Max(radius, Mathf.Max(box.size.x, box.size.z) * 0.5f);
                    break;
            }

            if (foundCharacterController && foundCapsule && foundSphere && foundBox)
                break;
        }

        return Mathf.Max(radius, DefaultUnitClearance);
    }

    void InheritTeam(GameObject spawned)
    {
        if (_teamAffiliation == null || spawned == null)
            return;

        if (spawned.TryGetComponent(out TeamAffiliation spawnedTeam))
            spawnedTeam.SetTeam(_teamAffiliation.Team);
    }

    public bool TryIssueGroundCommand(Vector3 worldPoint, bool append)
    {
        SetRallyPoint(worldPoint);
        return true;
    }

    public bool TryBuildSelectionRoute(List<Vector3> points)
    {
        if (points == null)
            return false;

        points.Clear();
        if (!_hasRallyPoint)
            return false;

        Vector3 center = _buildingCenter;
        if ((center - _rallyPoint).sqrMagnitude <= 0.01f)
            return false;

        points.Add(center);
        points.Add(_rallyPoint);
        return true;
    }

    void EnsureRallyPoint()
    {
        if (_hasRallyPoint && !_usesDefaultRallyPoint)
            return;

        // Default the rally point in front of the building so fresh spawns already have a sensible first order.
        _rallyPoint = ResolveDefaultRallyPoint();
        _hasRallyPoint = true;
        _usesDefaultRallyPoint = true;
    }

    void SetRallyPoint(Vector3 worldPoint)
    {
        if (NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, RallyPointSampleRadius, NavMesh.AllAreas))
            _rallyPoint = navHit.position;
        else
            _rallyPoint = worldPoint;

        _hasRallyPoint = true;
        _usesDefaultRallyPoint = false;
    }

    Vector3 ResolveDefaultRallyPoint()
    {
        Vector3 fallbackPoint = _buildingCenter + transform.forward * (_buildingRadius + SpawnSearchPadding);
        if (NavMesh.SamplePosition(fallbackPoint, out NavMeshHit navHit, RallyPointSampleRadius, NavMesh.AllAreas))
            return navHit.position;

        return fallbackPoint;
    }

    void DispatchSpawnedUnitToRally(GameObject spawned)
    {
        if (!_hasRallyPoint || spawned == null)
            return;

        // New units inherit a single move order so the visible command route matches the building's rally line.
        CommandExecutor spawnedExecutor = ResolveSpawnedComponent<CommandExecutor>(spawned);
        if (spawnedExecutor != null)
        {
            spawnedExecutor.Enqueue(new MoveCommand(_rallyPoint), append: false);
            return;
        }

        UnitBase spawnedMotor = ResolveSpawnedComponent<UnitBase>(spawned);
        if (spawnedMotor != null)
        {
            OccupancyStandingRequest request = OccupancyStandingRequest.CreateDefault(spawnedMotor.OccupancyRadius);
            request.SearchMode = OccupancySearchMode.RingAroundTarget;

            if (OccupancySystem.Instance.TryAssignStandingPoint(spawnedMotor, _rallyPoint, request, out Vector3 standPoint))
                spawnedMotor.SetDestination(standPoint);
            else
                spawnedMotor.SetDestination(_rallyPoint);
        }
    }

    static T ResolveSpawnedComponent<T>(GameObject spawned) where T : Component
    {
        if (spawned == null)
            return null;

        T resolved = spawned.GetComponent<T>();
        if (resolved != null)
            return resolved;

        return spawned.GetComponentInChildren<T>(true);
    }

    void EnsureSlotArray()
    {
        if (slots == null || slots.Length != MaxSlotCount)
            Array.Resize(ref slots, MaxSlotCount);
    }

    void SyncBuildingOccupancy()
    {
        if (!Application.isPlaying)
            return;

        _occupancyBuildingId = OccupancySystem.Instance.RegisterOrUpdateBuilding(
            this,
            _buildingCenter,
            _buildingHalfExtents,
            Mathf.Max(_buildingRadius, DefaultBuildingRadius));
    }

    void ReleaseBuildingOccupancy()
    {
        if (!Application.isPlaying)
            return;

        if (_occupancyBuildingId == 0)
            return;

        if (OccupancySystem.TryGetInstance(out OccupancySystem occupancy))
            occupancy.UnregisterBuilding(this);

        _occupancyBuildingId = 0;
    }
}
