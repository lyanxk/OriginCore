using System;
using UnityEngine;

namespace OriginCore.Content
{
    public interface IContentDefinition
    {
        string ContentId { get; }
        string DisplayName { get; }
    }

    public abstract class ContentDefinition : ScriptableObject, IContentDefinition
    {
        [SerializeField] private string _contentId = "content.new";
        [SerializeField] private string _displayName = "Content";

        public string ContentId => _contentId;
        public string DisplayName => _displayName;

        public void ConfigureIdentity(string contentId, string displayName)
        {
            _contentId = ContentIdUtility.Normalize(contentId);
            _displayName = string.IsNullOrWhiteSpace(displayName)
                ? "Content"
                : displayName.Trim();
        }

        protected virtual void OnValidate()
        {
            _contentId = ContentIdUtility.Normalize(_contentId);
            _displayName = string.IsNullOrWhiteSpace(_displayName)
                ? "Content"
                : _displayName.Trim();
        }
    }

    public static class ContentIdUtility
    {
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (!string.Equals(value, normalized, StringComparison.Ordinal) ||
                normalized.Length > 96 || !IsAsciiLowerOrDigit(normalized[0]) ||
                !IsAsciiLowerOrDigit(normalized[normalized.Length - 1]))
            {
                return false;
            }

            char previous = '\0';
            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                bool delimiter = character == '.' || character == '_' || character == '-';
                if (!IsAsciiLowerOrDigit(character) && !delimiter)
                {
                    return false;
                }

                if (delimiter && (previous == '.' || previous == '_' || previous == '-'))
                {
                    return false;
                }

                previous = character;
            }

            return true;
        }

        private static bool IsAsciiLowerOrDigit(char value)
        {
            return value >= 'a' && value <= 'z' || value >= '0' && value <= '9';
        }
    }
}
