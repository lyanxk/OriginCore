using System;
using OriginCore.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Save
{
    [DisallowMultipleComponent]
    public sealed class SaveSlotRowView : MonoBehaviour
    {
        [SerializeField] private Button _selectButton;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _detailLabel;

        private SaveSlotMetadata _metadata;
        private bool _buttonBound;

        public event Action<SaveSlotRowView, SaveSlotMetadata> Selected;

        public SaveSlotMetadata Metadata => _metadata;

        private void OnEnable()
        {
            BindButton();
        }

        private void OnDestroy()
        {
            if (_buttonBound && _selectButton != null)
            {
                _selectButton.onClick.RemoveListener(Select);
            }
        }

        public void Configure(
            Button selectButton,
            Image background,
            TMP_Text nameLabel,
            TMP_Text detailLabel)
        {
            _selectButton = selectButton;
            _background = background;
            _nameLabel = nameLabel;
            _detailLabel = detailLabel;
            if (Application.isPlaying)
            {
                BindButton();
            }
        }

        public void SetData(SaveSlotMetadata metadata, bool selected)
        {
            _metadata = metadata;
            if (_nameLabel != null)
            {
                _nameLabel.text = metadata != null
                    ? metadata.DisplayName
                    : "UNKNOWN SLOT";
                _nameLabel.color = metadata != null && metadata.IsReadable
                    ? Color.white
                    : new Color(1f, 0.45f, 0.35f, 1f);
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = BuildDetail(metadata);
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (_background != null)
            {
                _background.color = selected
                    ? new Color(0.12f, 0.55f, 0.7f, 1f)
                    : new Color(0.035f, 0.13f, 0.18f, 0.98f);
            }
        }

        private void Select()
        {
            if (_metadata != null)
            {
                Selected?.Invoke(this, _metadata);
            }
        }

        private void BindButton()
        {
            if (_buttonBound || _selectButton == null)
            {
                return;
            }

            _selectButton.onClick.AddListener(Select);
            _buttonBound = true;
        }

        private static string BuildDetail(SaveSlotMetadata metadata)
        {
            if (metadata == null)
            {
                return "NO METADATA";
            }

            if (!metadata.IsReadable)
            {
                return "UNREADABLE | " + metadata.Error;
            }

            string date = metadata.SavedAt == DateTime.MinValue
                ? "UNKNOWN DATE"
                : metadata.SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return date + " | " + metadata.SceneKey;
        }
    }
}
