using System;
using TMPro;
using Unit.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.HUD
{
    public class CommandSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color CooldownOverlayColor = new Color(0f, 0f, 0f, 0.6f);

        [SerializeField] Button button;
        [SerializeField] Graphic backgroundGraphic;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text hotkeyText;
        [SerializeField] GameObject disabledMask;
        [SerializeField] Image cooldownFill;

        int _slotIndex;
        bool _hasEntry;
        CommandEntry _entry;

        Action<int> _onClick;
        Action<int, CommandEntry> _onHoverEnter;
        Action _onHoverExit;

        void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (backgroundGraphic == null && button != null)
                backgroundGraphic = button.targetGraphic as Graphic;

            if (button != null)
                button.onClick.AddListener(HandleClicked);
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(
            int slotIndex,
            bool hasEntry,
            CommandEntry entry,
            Action<int> onClick,
            Action<int, CommandEntry> onHoverEnter,
            Action onHoverExit)
        {
            _slotIndex = slotIndex;
            _hasEntry = hasEntry;
            _entry = entry;

            _onClick = onClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

            ApplyVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_hasEntry) return;
            _onHoverEnter?.Invoke(_slotIndex, _entry);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hasEntry) return;
            _onHoverExit?.Invoke();
        }

        void HandleClicked()
        {
            if (!_hasEntry) return;
            _onClick?.Invoke(_slotIndex);
        }

        void ApplyVisual()
        {
            if (!_hasEntry)
            {
                if (backgroundGraphic != null)
                    backgroundGraphic.enabled = false;

                if (iconImage != null)
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }

                if (nameText != null)
                    nameText.text = string.Empty;

                if (hotkeyText != null)
                    SetHotkeyTextVisible(false);

                if (disabledMask != null)
                    disabledMask.SetActive(false);

                if (cooldownFill != null)
                {
                    cooldownFill.gameObject.SetActive(false);
                    cooldownFill.fillAmount = 0f;
                }

                if (button != null)
                    button.interactable = false;

                return;
            }

            if (backgroundGraphic != null)
                backgroundGraphic.enabled = true;

            if (iconImage != null)
            {
                iconImage.sprite = _entry.Icon;
                iconImage.enabled = _entry.Icon != null;
            }

            if (nameText != null)
                nameText.text = _entry.Name ?? string.Empty;

            SetHotkeyTextVisible(false);

            float cooldown01 = Mathf.Clamp01(_entry.Cooldown01);
            bool isCoolingDown = cooldown01 > 0.001f;

            if (disabledMask != null)
                disabledMask.SetActive(!_entry.Enabled && !isCoolingDown);

            if (cooldownFill != null)
            {
                cooldownFill.gameObject.SetActive(isCoolingDown);
                if (isCoolingDown)
                {
                    cooldownFill.color = CooldownOverlayColor;
                    cooldownFill.type = Image.Type.Filled;
                    cooldownFill.fillMethod = Image.FillMethod.Radial360;
                    cooldownFill.fillOrigin = (int)Image.Origin360.Top;
                    cooldownFill.fillClockwise = false;
                    cooldownFill.fillAmount = cooldown01;
                }
                else
                {
                    cooldownFill.fillAmount = 0f;
                }
            }

            if (button != null)
                button.interactable = _entry.Enabled && _entry.Type != CommandEntryType.Passive;
        }

        void SetHotkeyTextVisible(bool visible)
        {
            if (hotkeyText == null)
                return;

            hotkeyText.text = string.Empty;
            hotkeyText.gameObject.SetActive(visible);
        }
    }
}
