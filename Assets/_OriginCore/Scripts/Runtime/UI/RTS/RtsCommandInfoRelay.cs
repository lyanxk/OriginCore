using UnityEngine;
using UnityEngine.EventSystems;

namespace OriginCore.UI.RTS
{
    [DisallowMultipleComponent]
    public sealed class RtsCommandInfoRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private RtsCommandPanelView _owner;
        private int _slotCode;

        public void Configure(RtsCommandPanelView owner, int slotCode)
        {
            _owner = owner;
            _slotCode = slotCode;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.ShowCommandInfo(_slotCode);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.ClearCommandInfo();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _owner?.ShowCommandInfo(_slotCode);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _owner?.ClearCommandInfo();
        }
    }
}
