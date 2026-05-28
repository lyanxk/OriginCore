using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Content
{
    public static class GameText
    {
        const string ResourcePath = "GameText/game_text_zh_cn";

        static readonly Dictionary<string, GameTextEntry> s_entries = new Dictionary<string, GameTextEntry>(StringComparer.Ordinal);
        static readonly Dictionary<string, string[]> s_lineGroups = new Dictionary<string, string[]>(StringComparer.Ordinal);
        static readonly string[] EmptyLines = new string[0];
        static bool s_loaded;

        public static string GetName(string id, string fallback = "")
        {
            return GetField(id, entry => entry.name, fallback);
        }

        public static string GetHotkey(string id, string fallback = "")
        {
            return GetField(id, entry => entry.hotkey, fallback);
        }

        public static string GetTooltip(string id, string fallback = "")
        {
            return GetField(id, entry => entry.tooltip, fallback);
        }

        public static string GetText(string id, string fallback = "")
        {
            return GetField(id, entry => entry.text, fallback);
        }

        public static string[] GetLines(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(id) && s_lineGroups.TryGetValue(id, out string[] lines)
                ? lines
                : EmptyLines;
        }

        public static string GetCjkCharacters()
        {
            EnsureLoaded();

            HashSet<char> seen = new HashSet<char>();
            StringBuilder builder = new StringBuilder(128);
            foreach (GameTextEntry entry in s_entries.Values)
            {
                AppendCjkCharacters(entry.name, seen, builder);
                AppendCjkCharacters(entry.hotkey, seen, builder);
                AppendCjkCharacters(entry.tooltip, seen, builder);
                AppendCjkCharacters(entry.text, seen, builder);
            }

            foreach (string[] lines in s_lineGroups.Values)
            {
                if (lines == null)
                    continue;

                for (int i = 0; i < lines.Length; i++)
                    AppendCjkCharacters(lines[i], seen, builder);
            }

            return builder.ToString();
        }

        static string GetField(string id, Func<GameTextEntry, string> selector, string fallback)
        {
            EnsureLoaded();
            if (!string.IsNullOrWhiteSpace(id) && s_entries.TryGetValue(id, out GameTextEntry entry))
            {
                string value = selector(entry);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return fallback ?? string.Empty;
        }

        static void EnsureLoaded()
        {
            if (s_loaded)
                return;

            s_loaded = true;
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"Game text file not found at Resources/{ResourcePath}.");
                return;
            }

            GameTextDocument document = JsonUtility.FromJson<GameTextDocument>(asset.text);
            if (document == null)
            {
                Debug.LogWarning($"Game text file at Resources/{ResourcePath} could not be parsed.");
                return;
            }

            RegisterSections(document.sections);
            RegisterLineGroups(document.lineGroups);
        }

        static void RegisterSections(GameTextSection[] sections)
        {
            if (sections == null)
                return;

            for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                GameTextEntry[] entries = sections[sectionIndex]?.entries;
                if (entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    GameTextEntry entry = entries[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                        continue;

                    s_entries[entry.id] = entry;
                }
            }
        }

        static void RegisterLineGroups(GameTextLineGroup[] lineGroups)
        {
            if (lineGroups == null)
                return;

            for (int i = 0; i < lineGroups.Length; i++)
            {
                GameTextLineGroup group = lineGroups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.id))
                    continue;

                s_lineGroups[group.id] = group.lines ?? EmptyLines;
            }
        }

        static void AppendCjkCharacters(string text, HashSet<char> seen, StringBuilder builder)
        {
            if (string.IsNullOrEmpty(text))
                return;

            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (!IsCjk(character) || !seen.Add(character))
                    continue;

                builder.Append(character);
            }
        }

        static bool IsCjk(char character)
        {
            return character >= '\u3400' && character <= '\u9fff';
        }

        [Serializable]
        public sealed class GameTextDocument
        {
            public GameTextSection[] sections;
            public GameTextLineGroup[] lineGroups;
        }

        [Serializable]
        public sealed class GameTextSection
        {
            public string id;
            public GameTextEntry[] entries;
        }

        [Serializable]
        public sealed class GameTextEntry
        {
            public string id;
            public string name;
            public string hotkey;
            public string tooltip;
            public string text;
        }

        [Serializable]
        public sealed class GameTextLineGroup
        {
            public string id;
            public string[] lines;
        }
    }
}
