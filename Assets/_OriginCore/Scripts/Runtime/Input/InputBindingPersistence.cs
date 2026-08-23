using System;
using OriginCore.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OriginCore.Input
{
    public static class InputBindingPersistence
    {
        public const string PlayerPrefsKey = "OriginCore.Settings.BindingOverridesJson";

        public static string CaptureOverrides(InputActionAsset actionsAsset)
        {
            if (actionsAsset == null)
            {
                throw new ArgumentNullException(nameof(actionsAsset));
            }

            return actionsAsset.SaveBindingOverridesAsJson();
        }

        public static void CaptureIntoSettings(InputActionAsset actionsAsset, SettingsData settingsData)
        {
            if (settingsData == null)
            {
                throw new ArgumentNullException(nameof(settingsData));
            }

            settingsData.BindingOverridesJson = CaptureOverrides(actionsAsset);
        }

        public static bool TryApplyFromSettings(
            InputActionAsset actionsAsset,
            SettingsData settingsData,
            out string error)
        {
            if (settingsData == null)
            {
                error = "SettingsData is null.";
                return false;
            }

            return TryApplyOverrides(actionsAsset, settingsData.BindingOverridesJson, out error);
        }

        public static bool TryApplyOverrides(
            InputActionAsset actionsAsset,
            string bindingOverridesJson,
            out string error)
        {
            if (actionsAsset == null)
            {
                error = "InputActionAsset is null.";
                return false;
            }

            try
            {
                actionsAsset.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(bindingOverridesJson))
                {
                    actionsAsset.LoadBindingOverridesFromJson(bindingOverridesJson);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                actionsAsset.RemoveAllBindingOverrides();
                error = exception.Message;
                return false;
            }
        }

        public static void SaveSettingsToPlayerPrefs(SettingsData settingsData)
        {
            if (settingsData == null)
            {
                throw new ArgumentNullException(nameof(settingsData));
            }

            PlayerPrefs.SetString(PlayerPrefsKey, settingsData.BindingOverridesJson);
            PlayerPrefs.Save();
        }

        public static bool TryLoadSettingsFromPlayerPrefs(SettingsData settingsData, out string error)
        {
            if (settingsData == null)
            {
                error = "SettingsData is null.";
                return false;
            }

            settingsData.BindingOverridesJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            error = string.Empty;
            return true;
        }

        public static void ClearPersistedOverrides(InputActionAsset actionsAsset, SettingsData settingsData)
        {
            if (actionsAsset == null)
            {
                throw new ArgumentNullException(nameof(actionsAsset));
            }

            if (settingsData == null)
            {
                throw new ArgumentNullException(nameof(settingsData));
            }

            actionsAsset.RemoveAllBindingOverrides();
            settingsData.BindingOverridesJson = string.Empty;
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
