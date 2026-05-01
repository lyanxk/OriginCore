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
        Action<CommandEntry> _onHoverEnter;
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
            Action<CommandEntry> onHoverEnter,
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
            _onHoverEnter?.Invoke(_entry);
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
                    hotkeyText.text = string.Empty;

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

            if (hotkeyText != null)
                hotkeyText.text = _entry.HotkeyText ?? string.Empty;

            if (disabledMask != null)
                disabledMask.SetActive(!_entry.Enabled);

            if (cooldownFill != null)
            {
                bool showCooldown = _entry.Cooldown01 > 0.001f;
                cooldownFill.gameObject.SetActive(showCooldown);
                cooldownFill.fillAmount = Mathf.Clamp01(_entry.Cooldown01);
            }

            if (button != null)
                button.interactable = _entry.Enabled && _entry.Type != CommandEntryType.Passive;
        }
    }
}
