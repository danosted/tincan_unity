#nullable enable
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Brings an older design file up to <see cref="ShipDesignFormat.CurrentVersion"/>, one version at a time, before it
    /// is decoded. <see cref="Current"/> holds the game's migrations (none while the format is at version 1).
    /// </summary>
    public sealed class ShipDesignMigrator
    {
        private readonly Dictionary<int, IShipDesignMigration> _byFromVersion;
        private readonly int _targetVersion;

        public ShipDesignMigrator(IEnumerable<IShipDesignMigration> migrations, int targetVersion = ShipDesignFormat.CurrentVersion)
        {
            _byFromVersion = migrations.ToDictionary(m => m.FromVersion);
            _targetVersion = targetVersion;
        }

        public static ShipDesignMigrator Current { get; } = new(System.Array.Empty<IShipDesignMigration>());

        /// <summary>Migrates the file from its version to the target, or says which step is missing.</summary>
        public bool TryMigrate(JObject file, int fromVersion, out string? error)
        {
            for (int version = fromVersion; version < _targetVersion; version++)
            {
                if (!_byFromVersion.TryGetValue(version, out var migration))
                {
                    error = $"No migration from format version {version}.";
                    return false;
                }

                migration.Migrate(file);
                file[ShipDesignFormat.FormatVersionField] = version + 1;
            }

            error = null;
            return true;
        }
    }
}
