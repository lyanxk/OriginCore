using System;
using OriginCore.Matches;

namespace OriginCore.Save
{
    public interface ISaveMigration
    {
        int SourceVersion { get; }
        int TargetVersion { get; }
        bool TryMigrate(SaveGameData data, out string error);
    }

    public static class SaveMigrationRegistry
    {
        private static readonly ISaveMigration[] Migrations =
        {
            new LegacyV1ToV2Migration(),
            new V2ToV3Migration()
        };

        public static bool TryMigrateToCurrent(SaveGameData data, out string error)
        {
            if (data == null)
            {
                error = "Save data is null.";
                return false;
            }

            if (data.schemaVersion > SaveGameData.CurrentSchemaVersion || data.schemaVersion < 1)
            {
                error = "Unsupported save schema version " + data.schemaVersion + ".";
                return false;
            }

            while (data.schemaVersion < SaveGameData.CurrentSchemaVersion)
            {
                ISaveMigration migration = FindMigration(data.schemaVersion);
                if (migration == null)
                {
                    error = "No migration exists for save schema version " + data.schemaVersion + ".";
                    return false;
                }

                if (!migration.TryMigrate(data, out error))
                {
                    return false;
                }

                if (data.schemaVersion != migration.TargetVersion)
                {
                    error = "Save migration did not advance to its declared target version.";
                    return false;
                }
            }

            data.EnsureCollections();
            error = string.Empty;
            return true;
        }

        private static ISaveMigration FindMigration(int sourceVersion)
        {
            for (int i = 0; i < Migrations.Length; i++)
            {
                if (Migrations[i].SourceVersion == sourceVersion)
                {
                    return Migrations[i];
                }
            }

            return null;
        }
    }

    internal sealed class V2ToV3Migration : ISaveMigration
    {
        public int SourceVersion => 2;
        public int TargetVersion => 3;

        public bool TryMigrate(SaveGameData data, out string error)
        {
            if (data == null || data.schemaVersion != SourceVersion)
            {
                error = "V2 migration received incompatible data.";
                return false;
            }
            data.EnsureCollections();
            if (string.Equals(
                    data.matchConfiguration.contentRevision,
                    "origin-core-dev-6",
                    StringComparison.Ordinal))
            {
                data.matchConfiguration.contentRevision = "origin-core-dev-7";
            }
            data.schemaVersion = TargetVersion;
            error = string.Empty;
            return true;
        }
    }

    internal sealed class LegacyV1ToV2Migration : ISaveMigration
    {
        public int SourceVersion => 1;
        public int TargetVersion => 2;

        public bool TryMigrate(SaveGameData data, out string error)
        {
            if (data == null || data.schemaVersion != SourceVersion)
            {
                error = "Legacy v1 migration received incompatible data.";
                return false;
            }

            data.EnsureCollections();
            if (!data.matchConfiguration.IsConfigured)
            {
                MatchConfiguration fallback = MatchConfiguration.CreateSystemTestFallback();
                fallback.sceneKey = string.IsNullOrWhiteSpace(data.sceneKey)
                    ? "system_test_0"
                    : data.sceneKey.Trim();
                data.matchConfiguration.CopyFrom(fallback);
            }

            data.matchConfiguration.sceneKey = data.sceneKey;
            data.schemaVersion = TargetVersion;
            error = string.Empty;
            return true;
        }
    }
}
