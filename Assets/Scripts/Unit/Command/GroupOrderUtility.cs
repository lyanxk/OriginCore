using System.Collections.Generic;
using UnityEngine;

public static class GroupOrderUtility
{
    public const float DefaultSpacing = 1.4f;

    public static void BuildSpreadDestinations(
        int unitCount,
        Vector3 center,
        List<Vector3> output,
        float spacing = DefaultSpacing)
    {
        if (output == null)
            return;

        output.Clear();
        if (unitCount <= 0)
            return;

        if (unitCount == 1)
        {
            output.Add(center);
            return;
        }

        float step = Mathf.Max(0.1f, spacing);
        int cols = Mathf.CeilToInt(Mathf.Sqrt(unitCount));
        int rows = Mathf.CeilToInt(unitCount / (float)cols);
        float originX = (cols - 1) * 0.5f;
        float originZ = (rows - 1) * 0.5f;

        for (int i = 0; i < unitCount; i++)
        {
            int row = i / cols;
            int col = i % cols;

            Vector3 offset = new Vector3(
                (col - originX) * step,
                0f,
                (row - originZ) * step);

            output.Add(center + offset);
        }
    }
}
