using UnityEngine;

namespace OriginCore.RTS
{
    public static class SelectionBoxCalculator
    {
        public static Rect FromScreenPoints(Vector2 start, Vector2 end)
        {
            Vector2 minimum = Vector2.Min(start, end);
            Vector2 maximum = Vector2.Max(start, end);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        public static bool ExceedsDragThreshold(
            Vector2 start,
            Vector2 current,
            float thresholdPixels)
        {
            float threshold = Mathf.Max(0f, thresholdPixels);
            return (current - start).sqrMagnitude >= threshold * threshold;
        }

        public static bool ContainsWorldPoint(
            Camera worldCamera,
            Rect screenRect,
            Vector3 worldPoint)
        {
            if (worldCamera == null)
            {
                return false;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
            return screenPoint.z > 0f && screenRect.Contains(
                new Vector2(screenPoint.x, screenPoint.y),
                true);
        }

        public static bool TryConvertToLocalRect(
            Rect screenRect,
            RectTransform container,
            Camera eventCamera,
            out Rect localRect)
        {
            localRect = default(Rect);
            if (container == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    screenRect.min,
                    eventCamera,
                    out Vector2 localMinimum) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    container,
                    screenRect.max,
                    eventCamera,
                    out Vector2 localMaximum))
            {
                return false;
            }

            Vector2 minimum = Vector2.Min(localMinimum, localMaximum);
            Vector2 maximum = Vector2.Max(localMinimum, localMaximum);
            localRect = Rect.MinMaxRect(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);
            return true;
        }
    }
}
