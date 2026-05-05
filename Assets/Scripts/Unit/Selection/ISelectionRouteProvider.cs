using System.Collections.Generic;
using UnityEngine;

namespace Unit.Selection
{
    public interface ISelectionRouteProvider
    {
        bool TryBuildSelectionRoute(List<Vector3> points);
    }
}
