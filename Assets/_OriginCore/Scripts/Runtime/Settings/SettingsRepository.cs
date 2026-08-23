using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OriginCore.Settings
{
    public sealed class SettingsRepository
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public SettingsRepository(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Settings file path is empty.", nameof(filePath));
            }

            FilePath = Path.GetFullPath(filePath);
        }

        public string FilePath { get; }

        public bool TryLoad(out SettingsData data, out string error)
        {
            data = null;
            error = string.Empty;
            if (!File.Exists(FilePath))
            {
                return true;
            }

            try
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                data = JsonUtility.FromJson<SettingsData>(json);
                if (data == null)
                {
                    error = "Settings JSON did not contain an object.";
                    return false;
                }

                data.Normalize();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = null;
                return false;
            }
        }

        public bool TrySave(SettingsData data, out string error)
        {
            error = string.Empty;
            if (data == null)
            {
                error = "SettingsData is null.";
                return false;
            }

            string temporaryPath = FilePath + ".tmp";
            string backupPath = FilePath + ".bak";
            try
            {
                data.Normalize();
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true), Utf8WithoutBom);
                if (File.Exists(FilePath))
                {
                    File.Replace(temporaryPath, FilePath, backupPath, true);
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, FilePath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                TryDelete(temporaryPath);
                return false;
            }
        }

        private static void TryDelete(string path)
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
                // Preserve the original repository error.
            }
        }
    }
}
