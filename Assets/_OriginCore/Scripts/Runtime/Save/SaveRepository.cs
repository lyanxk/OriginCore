using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OriginCore.Save
{
    public sealed class SaveSlotMetadata
    {
        public SaveSlotMetadata(
            string slotId,
            string displayName,
            string savedAtUtc,
            string sceneKey,
            int schemaVersion,
            bool isReadable,
            string error)
        {
            SlotId = slotId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SlotId : displayName.Trim();
            SavedAtUtc = savedAtUtc ?? string.Empty;
            SceneKey = sceneKey ?? string.Empty;
            SchemaVersion = schemaVersion;
            IsReadable = isReadable;
            Error = error ?? string.Empty;
        }

        public string SlotId { get; }
        public string DisplayName { get; }
        public string SavedAtUtc { get; }
        public string SceneKey { get; }
        public int SchemaVersion { get; }
        public bool IsReadable { get; }
        public string Error { get; }

        public DateTime SavedAt
        {
            get
            {
                return DateTime.TryParse(
                    SavedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsed)
                    ? parsed.ToUniversalTime()
                    : DateTime.MinValue;
            }
        }
    }

    public sealed class SaveRepository
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public SaveRepository(string savesDirectory)
        {
            if (string.IsNullOrWhiteSpace(savesDirectory))
            {
                throw new ArgumentException("Save directory is empty.", nameof(savesDirectory));
            }

            SavesDirectory = Path.GetFullPath(savesDirectory);
        }

        public string SavesDirectory { get; }

        public static bool IsValidSlotId(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || slotId.Length > 64)
            {
                return false;
            }

            for (int i = 0; i < slotId.Length; i++)
            {
                char character = slotId[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        public static string CreateSlotId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public IReadOnlyList<SaveSlotMetadata> EnumerateSlots()
        {
            List<SaveSlotMetadata> slots = new List<SaveSlotMetadata>();
            EnumerateSlots(slots);
            return slots;
        }

        public int EnumerateSlots(List<SaveSlotMetadata> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (!Directory.Exists(SavesDirectory))
            {
                return 0;
            }

            string[] paths;
            try
            {
                paths = Directory.GetFiles(SavesDirectory, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception)
            {
                results.Add(new SaveSlotMetadata(
                    string.Empty,
                    "SAVE DIRECTORY UNREADABLE",
                    string.Empty,
                    string.Empty,
                    0,
                    false,
                    exception.Message));
                return results.Count;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                string slotId = Path.GetFileNameWithoutExtension(paths[i]);
                if (!IsValidSlotId(slotId))
                {
                    continue;
                }

                results.Add(ReadMetadata(slotId));
            }

            results.Sort(CompareMetadata);
            return results.Count;
        }

        public bool TryRead(string slotId, out SaveGameData data, out string error)
        {
            data = null;
            error = string.Empty;
            if (!TryGetSlotPath(slotId, out string path, out error))
            {
                return false;
            }

            if (!File.Exists(path))
            {
                error = "Save slot does not exist.";
                return false;
            }

            if (!TryReadPath(path, out data, out error))
            {
                return false;
            }

            if (!string.Equals(data.slotId, slotId, StringComparison.Ordinal))
            {
                error = "Save slot id does not match its file name.";
                data = null;
                return false;
            }

            return true;
        }

        public bool TryWrite(SaveGameData data, out string error)
        {
            error = string.Empty;
            if (data == null)
            {
                error = "SaveGameData is null.";
                return false;
            }

            data.NormalizeForWrite();
            if (!data.TryValidate(out error) ||
                !TryGetSlotPath(data.slotId, out string path, out error))
            {
                return false;
            }

            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            try
            {
                Directory.CreateDirectory(SavesDirectory);
                TryDeleteFile(temporaryPath);
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(data, true),
                    Utf8WithoutBom);

                if (File.Exists(path))
                {
                    TryDeleteFile(backupPath);
                    File.Replace(temporaryPath, path, backupPath, true);
                    TryDeleteFile(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                TryDeleteFile(temporaryPath);
                return false;
            }
        }

        public bool TryRename(string slotId, string displayName, out string error)
        {
            if (!TryRead(slotId, out SaveGameData data, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = "Save display name is empty.";
                return false;
            }

            string normalized = SaveGameData.NormalizeDisplayName(displayName);
            data.displayName = normalized;
            return TryWrite(data, out error);
        }

        public bool TryDelete(string slotId, out string error)
        {
            error = string.Empty;
            if (!TryGetSlotPath(slotId, out string path, out error))
            {
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    error = "Save slot does not exist.";
                    return false;
                }

                File.Delete(path);
                TryDeleteFile(path + ".tmp");
                TryDeleteFile(path + ".bak");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryGetMetadata(
            string slotId,
            out SaveSlotMetadata metadata,
            out string error)
        {
            metadata = null;
            error = string.Empty;
            if (!TryGetSlotPath(slotId, out string path, out error))
            {
                return false;
            }

            if (!File.Exists(path))
            {
                error = "Save slot does not exist.";
                return false;
            }

            metadata = ReadMetadata(slotId);
            if (!metadata.IsReadable)
            {
                error = metadata.Error;
                return false;
            }

            return true;
        }

        private SaveSlotMetadata ReadMetadata(string slotId)
        {
            string path = Path.Combine(SavesDirectory, slotId + ".json");
            if (!TryReadPath(path, out SaveGameData data, out string error))
            {
                SaveGameData partial = TryReadPartial(path);
                return new SaveSlotMetadata(
                    slotId,
                    partial != null ? partial.displayName : slotId,
                    partial != null ? partial.savedAtUtc : string.Empty,
                    partial != null ? partial.sceneKey : string.Empty,
                    partial != null ? partial.schemaVersion : 0,
                    false,
                    error);
            }

            if (!string.Equals(data.slotId, slotId, StringComparison.Ordinal))
            {
                return new SaveSlotMetadata(
                    slotId,
                    data.displayName,
                    data.savedAtUtc,
                    data.sceneKey,
                    data.schemaVersion,
                    false,
                    "Save slot id does not match its file name.");
            }

            return new SaveSlotMetadata(
                data.slotId,
                data.displayName,
                data.savedAtUtc,
                data.sceneKey,
                data.schemaVersion,
                true,
                string.Empty);
        }

        private static bool TryReadPath(
            string path,
            out SaveGameData data,
            out string error)
        {
            data = null;
            error = string.Empty;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Save file is empty.";
                    return false;
                }

                data = JsonUtility.FromJson<SaveGameData>(json);
                if (data == null)
                {
                    error = "Save JSON did not contain an object.";
                    return false;
                }

                if (!SaveMigrationRegistry.TryMigrateToCurrent(data, out error))
                {
                    data = null;
                    return false;
                }

                if (!data.TryValidate(out error))
                {
                    data = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Unreadable save: " + exception.Message;
                data = null;
                return false;
            }
        }

        private static SaveGameData TryReadPartial(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonUtility.FromJson<SaveGameData>(json);
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetSlotPath(string slotId, out string path, out string error)
        {
            path = string.Empty;
            if (!IsValidSlotId(slotId))
            {
                error = "Save slot id is invalid.";
                return false;
            }

            path = Path.Combine(SavesDirectory, slotId + ".json");
            error = string.Empty;
            return true;
        }

        private static int CompareMetadata(SaveSlotMetadata left, SaveSlotMetadata right)
        {
            int time = right.SavedAt.CompareTo(left.SavedAt);
            if (time != 0)
            {
                return time;
            }

            return string.Compare(left.SlotId, right.SlotId, StringComparison.Ordinal);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Cleanup is best effort and must not hide the primary repository result.
            }
        }
    }
}
