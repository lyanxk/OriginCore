using UnityEngine;

public enum OccupancySearchMode
{
    RingAroundTarget = 0,
    NearestValid = 1
}

public struct OccupancyStandingRequest
{
    public OccupancySearchMode SearchMode;
    public float UnitRadius;
    public float MinRadiusFromTarget;
    public float MaxRadiusFromTarget;
    public int RingSlotCount;
    public int RadiusSamples;
    public int AngleSamples;
    public bool RequirePathReachable;

    public static OccupancyStandingRequest CreateDefault(float unitRadius)
    {
        float safeRadius = Mathf.Max(0.15f, unitRadius);
        return new OccupancyStandingRequest
        {
            SearchMode = OccupancySearchMode.RingAroundTarget,
            UnitRadius = safeRadius,
            MinRadiusFromTarget = safeRadius * 1.5f,
            MaxRadiusFromTarget = Mathf.Max(safeRadius * 6f, 2.5f),
            RingSlotCount = 12,
            RadiusSamples = 6,
            AngleSamples = 24,
            RequirePathReachable = true
        };
    }

    public void ClampToSafeRange()
    {
        UnitRadius = Mathf.Max(0.15f, UnitRadius);
        MinRadiusFromTarget = Mathf.Max(0f, MinRadiusFromTarget);
        MaxRadiusFromTarget = Mathf.Max(MinRadiusFromTarget, MaxRadiusFromTarget);
        RingSlotCount = Mathf.Max(4, RingSlotCount);
        RadiusSamples = Mathf.Max(1, RadiusSamples);
        AngleSamples = Mathf.Max(6, AngleSamples);
    }
}
