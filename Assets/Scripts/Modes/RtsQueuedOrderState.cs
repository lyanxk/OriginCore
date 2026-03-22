public enum RtsQueuedOrderType
{
    None = 0,
    Move = 1,
    Attack = 2
}

public static class RtsQueuedOrderState
{
    public static RtsQueuedOrderType PendingOrder { get; private set; }
    public static bool HasPendingOrder => PendingOrder != RtsQueuedOrderType.None;

    public static void SetPendingOrder(RtsQueuedOrderType orderType)
    {
        PendingOrder = orderType;
    }

    public static void Clear()
    {
        PendingOrder = RtsQueuedOrderType.None;
    }
}
