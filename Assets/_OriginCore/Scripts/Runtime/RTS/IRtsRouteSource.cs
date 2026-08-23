using System.Collections.Generic;
using UnityEngine;

namespace OriginCore.RTS
{
    public enum RtsRouteStyle
    {
        None = 0,
        Standard = 1,
        Attack = 2
    }

    public readonly struct RtsRouteSegment
    {
        public RtsRouteSegment(Vector3 start, Vector3 end, RtsRouteStyle style)
        {
            Start = start;
            End = end;
            Style = style;
        }

        public Vector3 Start { get; }
        public Vector3 End { get; }
        public RtsRouteStyle Style { get; }
    }

    public interface IRtsRouteSource
    {
        bool TryBuildRoute(List<Vector3> points);
    }

    public interface IRtsStyledRouteSource : IRtsRouteSource
    {
        bool TryBuildStyledRoute(List<RtsRouteSegment> segments);
    }
}
