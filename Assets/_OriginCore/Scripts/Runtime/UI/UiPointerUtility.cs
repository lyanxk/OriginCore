using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OriginCore.UI
{
    public static class UiPointerUtility
    {
        private static readonly List<RaycastResult> RaycastResults =
            new List<RaycastResult>(16);

        private static EventSystem s_eventSystem;
        private static PointerEventData s_pointerEventData;

        public static bool IsScreenPointOverUi(Vector2 screenPoint)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.isActiveAndEnabled)
            {
                return false;
            }

            if (s_eventSystem != eventSystem || s_pointerEventData == null)
            {
                s_eventSystem = eventSystem;
                s_pointerEventData = new PointerEventData(eventSystem);
            }

            s_pointerEventData.Reset();
            s_pointerEventData.position = screenPoint;
            RaycastResults.Clear();
            eventSystem.RaycastAll(s_pointerEventData, RaycastResults);
            bool isOverUi = RaycastResults.Count > 0;
            RaycastResults.Clear();
            return isOverUi;
        }
    }
}
