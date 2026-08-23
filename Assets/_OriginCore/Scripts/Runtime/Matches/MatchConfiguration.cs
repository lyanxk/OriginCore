using System;
using System.Collections.Generic;
using OriginCore.Content;

namespace OriginCore.Matches
{
    public enum MatchGameModeSource
    {
        Story = 0,
        Skirmish = 1
    }

    [Serializable]
    public sealed class MatchConfiguration
    {
        public MatchGameModeSource gameModeSource = MatchGameModeSource.Skirmish;
        public string mapId = string.Empty;
        public string sceneKey = string.Empty;
        public string missionId = string.Empty;
        public string commanderId = string.Empty;
        public string heroId = string.Empty;
        public string[] actWeaponIds = new string[4];
        public string[] fpsAvailableWeaponIds = new string[6];
        public string fpsEquippedPrimary = string.Empty;
        public string fpsEquippedSecondary = string.Empty;
        public string fpsEquippedMelee = string.Empty;
        public string contentRevision = string.Empty;

        public static MatchConfiguration CreateSystemTestFallback()
        {
            return new MatchConfiguration
            {
                gameModeSource = MatchGameModeSource.Skirmish,
                mapId = "map.system-test-0",
                sceneKey = "system_test_0",
                commanderId = "commander.placeholder",
                heroId = "hero.placeholder",
                actWeaponIds = new[]
                {
                    "weapon.y.timeslot-key", "weapon.y.sequence-key",
                    "weapon.y.final-wing", string.Empty
                },
                fpsAvailableWeaponIds = new[]
                {
                    "weapon.gun", "weapon.revolver", "weapon.sword",
                    string.Empty, string.Empty, string.Empty
                },
                fpsEquippedPrimary = "weapon.gun",
                fpsEquippedSecondary = "weapon.revolver",
                fpsEquippedMelee = "weapon.sword",
                contentRevision = "origin-core-dev-7"
            };
        }

        public MatchConfiguration Clone()
        {
            return new MatchConfiguration
            {
                gameModeSource = gameModeSource,
                mapId = mapId,
                sceneKey = sceneKey,
                missionId = missionId,
                commanderId = commanderId,
                heroId = heroId,
                actWeaponIds = CloneArray(actWeaponIds),
                fpsAvailableWeaponIds = CloneArray(fpsAvailableWeaponIds),
                fpsEquippedPrimary = fpsEquippedPrimary,
                fpsEquippedSecondary = fpsEquippedSecondary,
                fpsEquippedMelee = fpsEquippedMelee,
                contentRevision = contentRevision
            };
        }

        public void Normalize()
        {
            mapId = ContentIdUtility.Normalize(mapId);
            sceneKey = string.IsNullOrWhiteSpace(sceneKey) ? string.Empty : sceneKey.Trim();
            missionId = ContentIdUtility.Normalize(missionId);
            commanderId = ContentIdUtility.Normalize(commanderId);
            heroId = ContentIdUtility.Normalize(heroId);
            actWeaponIds = NormalizeSlots(actWeaponIds, 4);
            fpsAvailableWeaponIds = NormalizeSlots(fpsAvailableWeaponIds, 6);
            fpsEquippedPrimary = ContentIdUtility.Normalize(fpsEquippedPrimary);
            fpsEquippedSecondary = ContentIdUtility.Normalize(fpsEquippedSecondary);
            fpsEquippedMelee = ContentIdUtility.Normalize(fpsEquippedMelee);
            contentRevision = string.IsNullOrWhiteSpace(contentRevision)
                ? string.Empty
                : contentRevision.Trim();
        }

        public bool TryValidate(ContentCatalog catalog, out string error)
        {
            Normalize();
            if (catalog == null)
            {
                error = "Content catalog is unavailable.";
                return false;
            }

            if (!catalog.TryValidate(out error))
            {
                return false;
            }

            if (!catalog.TryGetMap(mapId, out MapDefinition map))
            {
                error = "Unknown map id '" + mapId + "'.";
                return false;
            }

            if (!string.Equals(sceneKey, map.SceneKey, StringComparison.Ordinal))
            {
                error = "Match scene key does not match map '" + mapId + "'.";
                return false;
            }

            if (!catalog.TryGetCommander(commanderId, out _))
            {
                error = "Unknown commander id '" + commanderId + "'.";
                return false;
            }

            if (!catalog.TryGetHero(heroId, out HeroDefinition hero))
            {
                error = "Unknown hero id '" + heroId + "'.";
                return false;
            }

            EnsureRequiredActWeapons(hero, actWeaponIds);

            if (!string.IsNullOrEmpty(missionId) && !catalog.TryGetMission(missionId, out _))
            {
                error = "Unknown mission id '" + missionId + "'.";
                return false;
            }

            if (!ValidateWeapons(catalog, actWeaponIds, "ACT", out error) ||
                !ValidateWeapons(catalog, fpsAvailableWeaponIds, "FPS", out error))
            {
                return false;
            }

            if (!ValidateEquippedWeapon(catalog, fpsEquippedPrimary, fpsAvailableWeaponIds, out error) ||
                !ValidateEquippedWeapon(catalog, fpsEquippedSecondary, fpsAvailableWeaponIds, out error) ||
                !ValidateEquippedWeapon(catalog, fpsEquippedMelee, fpsAvailableWeaponIds, out error))
            {
                return false;
            }

            if (!string.Equals(contentRevision, catalog.Revision, StringComparison.Ordinal))
            {
                error = "Content revision '" + contentRevision + "' does not match catalog revision '" +
                        catalog.Revision + "'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool EnsureRequiredActWeapons(
            HeroDefinition hero,
            string[] actSlots)
        {
            WeaponDefinition[] requiredWeapons = hero != null
                ? hero.RequiredActWeapons
                : null;
            if (requiredWeapons == null || requiredWeapons.Length == 0 ||
                actSlots == null || actSlots.Length == 0)
            {
                return false;
            }

            string[] requiredIds = new string[actSlots.Length];
            int requiredCount = 0;
            for (int i = 0;
                 i < requiredWeapons.Length && requiredCount < requiredIds.Length;
                 i++)
            {
                WeaponDefinition weapon = requiredWeapons[i];
                string requiredId = weapon != null
                    ? ContentIdUtility.Normalize(weapon.ContentId)
                    : string.Empty;
                if (string.IsNullOrEmpty(requiredId) ||
                    ContainsId(requiredIds, requiredCount, requiredId))
                {
                    continue;
                }

                requiredIds[requiredCount++] = requiredId;
            }

            if (requiredCount == 0)
            {
                return false;
            }

            string[] existing = new string[actSlots.Length];
            int existingCount = 0;
            for (int i = 0; i < actSlots.Length; i++)
            {
                string value = ContentIdUtility.Normalize(actSlots[i]);
                if (string.IsNullOrEmpty(value) ||
                    ContainsId(requiredIds, requiredCount, value))
                {
                    continue;
                }

                if (!ContainsId(existing, existingCount, value))
                {
                    existing[existingCount++] = value;
                }
            }

            string[] normalized = new string[actSlots.Length];
            int destination = 0;
            for (int i = 0; i < requiredCount; i++)
            {
                normalized[destination++] = requiredIds[i];
            }

            for (int i = 0; i < existingCount && destination < actSlots.Length; i++)
            {
                normalized[destination++] = existing[i];
            }

            bool changed = false;
            for (int i = 0; i < actSlots.Length; i++)
            {
                if (!string.Equals(
                        actSlots[i],
                        normalized[i],
                        StringComparison.Ordinal))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return false;
            }

            for (int i = 0; i < actSlots.Length; i++)
            {
                actSlots[i] = normalized[i];
            }

            return true;
        }

        private static bool ContainsId(
            string[] values,
            int count,
            string candidate)
        {
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ValidateWeapons(
            ContentCatalog catalog,
            string[] ids,
            string mode,
            out string error)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i]))
                {
                    continue;
                }

                if (!catalog.TryGetWeapon(ids[i], out _))
                {
                    error = mode + " loadout contains unknown weapon '" + ids[i] + "'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateEquippedWeapon(
            ContentCatalog catalog,
            string equippedId,
            string[] availableIds,
            out string error)
        {
            if (string.IsNullOrEmpty(equippedId))
            {
                error = string.Empty;
                return true;
            }

            if (!catalog.TryGetWeapon(equippedId, out _))
            {
                error = "Equipped weapon '" + equippedId + "' is unknown.";
                return false;
            }

            for (int i = 0; i < availableIds.Length; i++)
            {
                if (string.Equals(equippedId, availableIds[i], StringComparison.Ordinal))
                {
                    error = string.Empty;
                    return true;
                }
            }

            error = "Equipped weapon '" + equippedId + "' is not in the FPS availability list.";
            return false;
        }

        private static string[] NormalizeSlots(string[] values, int slotCount)
        {
            string[] normalized = new string[Math.Max(0, slotCount)];
            if (values == null)
            {
                return normalized;
            }

            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            int count = Math.Min(values.Length, normalized.Length);
            for (int i = 0; i < count; i++)
            {
                string value = ContentIdUtility.Normalize(values[i]);
                if (string.IsNullOrEmpty(value) || !used.Add(value))
                {
                    continue;
                }

                normalized[i] = value;
            }

            return normalized;
        }

        private static string[] CloneArray(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new string[0];
            }

            string[] clone = new string[values.Length];
            Array.Copy(values, clone, values.Length);
            return clone;
        }
    }
}
