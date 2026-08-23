using System;
using System.Collections.Generic;
using OriginCore.Core;
using OriginCore.Save;
using OriginCore.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OriginCore.UI.Save
{
    public enum SavePanelMode
    {
        Load = 0,
        Save = 1
    }

    [DisallowMultipleComponent]
    public sealed class LoadGamePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _headerLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _emptyLabel;
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private SaveSlotRowView _rowTemplate;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private Button _newButton;
        [SerializeField] private Button _renameButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private ModalStack _modalStack;
        [SerializeField] private ConfirmDialog _confirmDialog;
        [SerializeField] private SaveNameDialog _saveNameDialog;

        private readonly List<SaveSlotRowView> _rows = new List<SaveSlotRowView>();
        private SaveService _saveService;
        private SaveSlotMetadata _selected;
        private string _selectedSlotId = string.Empty;
        private SavePanelMode _mode;
        private bool _exitAfterSave;
        private bool _buttonsBound;
        private bool _serviceBound;

        public bool IsOpen => gameObject.activeSelf;
        public SavePanelMode Mode => _mode;
        public bool ExitAfterSave => _exitAfterSave;
        public SaveSlotMetadata SelectedSlot => _selected;
        public SaveSlotRowView RowTemplate => _rowTemplate;
        public ModalStack ModalStack => _modalStack;
        public ConfirmDialog ConfirmDialog => _confirmDialog;
        public SaveNameDialog SaveNameDialog => _saveNameDialog;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BindButtons();
            TryResolveService();
            BindService();
            ApplyModeLabels();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindService();
        }

        private void OnDestroy()
        {
            UnbindService();
            if (!_buttonsBound)
            {
                return;
            }

            if (_primaryButton != null) _primaryButton.onClick.RemoveListener(Primary);
            if (_newButton != null) _newButton.onClick.RemoveListener(NewSave);
            if (_renameButton != null) _renameButton.onClick.RemoveListener(Rename);
            if (_deleteButton != null) _deleteButton.onClick.RemoveListener(Delete);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        }

        public void Configure(
            TMP_Text headerLabel,
            TMP_Text statusLabel,
            TMP_Text emptyLabel,
            RectTransform contentRoot,
            SaveSlotRowView rowTemplate,
            Button primaryButton,
            TMP_Text primaryButtonLabel,
            Button newButton,
            Button renameButton,
            Button deleteButton,
            Button closeButton,
            ModalStack modalStack,
            ConfirmDialog confirmDialog,
            SaveNameDialog saveNameDialog)
        {
            _headerLabel = headerLabel;
            _statusLabel = statusLabel;
            _emptyLabel = emptyLabel;
            _contentRoot = contentRoot;
            _rowTemplate = rowTemplate;
            _primaryButton = primaryButton;
            _primaryButtonLabel = primaryButtonLabel;
            _newButton = newButton;
            _renameButton = renameButton;
            _deleteButton = deleteButton;
            _closeButton = closeButton;
            _modalStack = modalStack;
            _confirmDialog = confirmDialog;
            _saveNameDialog = saveNameDialog;
            if (Application.isPlaying)
            {
                BindButtons();
            }
        }

        public void Show(SavePanelMode mode, bool exitAfterSave = false)
        {
            _mode = mode;
            _exitAfterSave = mode == SavePanelMode.Save && exitAfterSave;
            _selected = null;
            _selectedSlotId = string.Empty;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            TryResolveService();
            BindService();
            ApplyModeLabels();
            SetStatus(string.Empty);
            Refresh();
        }

        public bool TryHandleEscape()
        {
            if (!IsOpen)
            {
                return false;
            }

            if (_saveNameDialog != null && _saveNameDialog.TryHandleEscape())
            {
                return true;
            }

            if (_modalStack != null && _modalStack.CloseTop())
            {
                return true;
            }

            Close();
            return true;
        }

        public void Close()
        {
            _saveNameDialog?.Cancel();
            _modalStack?.CloseAll();
            _selected = null;
            _selectedSlotId = string.Empty;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void Refresh()
        {
            if (!Application.isPlaying || _rowTemplate == null || _contentRoot == null)
            {
                return;
            }

            ClearRows();
            TryResolveService();
            if (_saveService == null)
            {
                SetStatus("SAVE SERVICE UNAVAILABLE");
                SetEmpty(true, "NO SAVE SERVICE");
                UpdateActionState();
                return;
            }

            IReadOnlyList<SaveSlotMetadata> slots = _saveService.EnumerateSlots();
            _selected = null;
            for (int i = 0; i < slots.Count; i++)
            {
                SaveSlotMetadata metadata = slots[i];
                SaveSlotRowView row = Instantiate(_rowTemplate, _contentRoot);
                row.name = "SaveSlot_" + (string.IsNullOrWhiteSpace(metadata.SlotId)
                    ? i.ToString()
                    : metadata.SlotId);
                bool selected = string.Equals(
                    metadata.SlotId,
                    _selectedSlotId,
                    StringComparison.Ordinal);
                row.SetData(metadata, selected);
                row.Selected += HandleRowSelected;
                row.gameObject.SetActive(true);
                _rows.Add(row);
                if (selected)
                {
                    _selected = metadata;
                }
            }

            if (_selected == null)
            {
                _selectedSlotId = string.Empty;
            }

            SetEmpty(slots.Count == 0, "NO SAVES FOUND");
            UpdateActionState();
        }

        private void Primary()
        {
            if (_selected == null || _saveService == null)
            {
                SetStatus("SELECT A SAVE SLOT");
                return;
            }

            if (!_selected.IsReadable)
            {
                SetStatus("UNREADABLE SAVES CAN ONLY BE DELETED");
                return;
            }

            if (_mode == SavePanelMode.Load)
            {
                if (_saveService.TryBeginLoad(_selected.SlotId, out string error))
                {
                    SetStatus("LOADING " + _selected.DisplayName + "...");
                    Close();
                }
                else
                {
                    SetStatus(error);
                }

                return;
            }

            string slotId = _selected.SlotId;
            string displayName = _selected.DisplayName;
            if (_confirmDialog == null || !_confirmDialog.Show(
                    "OVERWRITE SAVE",
                    "OVERWRITE '" + displayName + "'?",
                    () => ExecuteOverwrite(slotId)))
            {
                SetStatus("OVERWRITE CONFIRMATION UNAVAILABLE");
            }
        }

        private void NewSave()
        {
            if (_mode != SavePanelMode.Save || _saveNameDialog == null)
            {
                return;
            }

            _saveNameDialog.Show("NEW SAVE", string.Empty, ExecuteNewSave);
        }

        private void Rename()
        {
            if (_selected == null || !_selected.IsReadable || _saveNameDialog == null)
            {
                SetStatus("SELECT A READABLE SAVE TO RENAME");
                return;
            }

            string slotId = _selected.SlotId;
            _saveNameDialog.Show(
                "RENAME SAVE",
                _selected.DisplayName,
                name => ExecuteRename(slotId, name));
        }

        private void Delete()
        {
            if (_selected == null || string.IsNullOrWhiteSpace(_selected.SlotId))
            {
                SetStatus("SELECT A SAVE TO DELETE");
                return;
            }

            string slotId = _selected.SlotId;
            string displayName = _selected.DisplayName;
            if (_confirmDialog == null || !_confirmDialog.Show(
                    "DELETE SAVE",
                    "DELETE '" + displayName + "'? THIS CANNOT BE UNDONE.",
                    () => ExecuteDelete(slotId)))
            {
                SetStatus("DELETE CONFIRMATION UNAVAILABLE");
            }
        }

        private void ExecuteNewSave(string displayName)
        {
            if (_saveService.TrySaveNew(
                    displayName,
                    _exitAfterSave,
                    out SaveSlotMetadata metadata,
                    out string error))
            {
                _selectedSlotId = metadata != null ? metadata.SlotId : string.Empty;
                SetStatus("SAVE COMPLETED");
                Refresh();
            }
            else
            {
                SetStatus(error);
            }
        }

        private void ExecuteOverwrite(string slotId)
        {
            if (_saveService.TryOverwrite(
                    slotId,
                    _exitAfterSave,
                    out SaveSlotMetadata metadata,
                    out string error))
            {
                _selectedSlotId = metadata != null ? metadata.SlotId : slotId;
                SetStatus("SAVE COMPLETED");
                Refresh();
            }
            else
            {
                SetStatus(error);
            }
        }

        private void ExecuteRename(string slotId, string displayName)
        {
            if (_saveService.TryRename(slotId, displayName, out string error))
            {
                _selectedSlotId = slotId;
                SetStatus("SAVE RENAMED");
                Refresh();
            }
            else
            {
                SetStatus(error);
            }
        }

        private void ExecuteDelete(string slotId)
        {
            if (_saveService.TryDelete(slotId, out string error))
            {
                _selectedSlotId = string.Empty;
                SetStatus("SAVE DELETED");
                Refresh();
            }
            else
            {
                SetStatus(error);
            }
        }

        private void HandleRowSelected(SaveSlotRowView selectedRow, SaveSlotMetadata metadata)
        {
            _selected = metadata;
            _selectedSlotId = metadata?.SlotId ?? string.Empty;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].SetSelected(_rows[i] == selectedRow);
            }

            SetStatus(metadata != null && !metadata.IsReadable
                ? metadata.Error
                : string.Empty);
            UpdateActionState();
        }

        private void TryResolveService()
        {
            if (_saveService != null)
            {
                return;
            }

            if (AppRoot.TryGetInstance(out AppRoot appRoot) && appRoot.Services != null)
            {
                _saveService = appRoot.Services.SaveService;
            }
        }

        private void BindService()
        {
            if (_serviceBound || _saveService == null)
            {
                return;
            }

            _saveService.SlotsChanged += Refresh;
            _saveService.BusyChanged += HandleBusyChanged;
            _saveService.OperationFinished += HandleOperationFinished;
            _serviceBound = true;
        }

        private void UnbindService()
        {
            if (!_serviceBound || _saveService == null)
            {
                _serviceBound = false;
                return;
            }

            _saveService.SlotsChanged -= Refresh;
            _saveService.BusyChanged -= HandleBusyChanged;
            _saveService.OperationFinished -= HandleOperationFinished;
            _serviceBound = false;
        }

        private void HandleBusyChanged(bool busy)
        {
            UpdateActionState();
            if (busy)
            {
                SetStatus("WORKING...");
            }
        }

        private void HandleOperationFinished(SaveOperationResult result)
        {
            if (!result.Succeeded)
            {
                SetStatus(result.Message);
            }
        }

        private void ApplyModeLabels()
        {
            if (_headerLabel != null)
            {
                _headerLabel.text = _mode == SavePanelMode.Load
                    ? "LOAD GAME"
                    : _exitAfterSave ? "SAVE AND EXIT" : "SAVE GAME";
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _mode == SavePanelMode.Load ? "LOAD" : "OVERWRITE";
            }

            if (_newButton != null)
            {
                _newButton.gameObject.SetActive(_mode == SavePanelMode.Save);
            }
        }

        private void UpdateActionState()
        {
            bool busy = _saveService != null && _saveService.IsBusy;
            bool hasSelection = _selected != null &&
                                !string.IsNullOrWhiteSpace(_selected.SlotId);
            bool readable = hasSelection && _selected.IsReadable;
            if (_primaryButton != null) _primaryButton.interactable = readable && !busy;
            if (_newButton != null) _newButton.interactable = !busy;
            if (_renameButton != null) _renameButton.interactable = readable && !busy;
            if (_deleteButton != null) _deleteButton.interactable = hasSelection && !busy;
            if (_closeButton != null) _closeButton.interactable = !busy;
        }

        private void BindButtons()
        {
            if (_buttonsBound || _primaryButton == null || _newButton == null ||
                _renameButton == null || _deleteButton == null || _closeButton == null)
            {
                return;
            }

            _primaryButton.onClick.AddListener(Primary);
            _newButton.onClick.AddListener(NewSave);
            _renameButton.onClick.AddListener(Rename);
            _deleteButton.onClick.AddListener(Delete);
            _closeButton.onClick.AddListener(Close);
            _buttonsBound = true;
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                SaveSlotRowView row = _rows[i];
                if (row == null)
                {
                    continue;
                }

                row.Selected -= HandleRowSelected;
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            _rows.Clear();
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message ?? string.Empty;
            }
        }

        private void SetEmpty(bool visible, string message)
        {
            if (_emptyLabel == null)
            {
                return;
            }

            _emptyLabel.gameObject.SetActive(visible);
            _emptyLabel.text = message ?? string.Empty;
        }
    }
}
