#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipDesignMigrator"/>: older files are brought up one version at a time before decoding. Exercised with
    /// test migrations towards an imagined version 3, since the real format is still at 1.
    /// </summary>
    public class ShipDesignMigratorTests
    {
        private sealed class RecordingMigration : IShipDesignMigration
        {
            private readonly List<int> _log;

            public RecordingMigration(int fromVersion, List<int> log)
            {
                FromVersion = fromVersion;
                _log = log;
            }

            public int FromVersion { get; }

            public void Migrate(JObject file)
            {
                _log.Add(FromVersion);
                file["migratedFrom" + FromVersion] = true;
            }
        }

        [Test]
        public void AnOldFile_RunsEachStep_InOrder_AndEndsAtTheTargetVersion()
        {
            var log = new List<int>();
            var migrator = new ShipDesignMigrator(new IShipDesignMigration[] { new RecordingMigration(2, log), new RecordingMigration(1, log) }, 3);
            var file = JObject.Parse("{\"formatVersion\":1}");

            Assert.That(migrator.TryMigrate(file, 1, out var error), Is.True, error);
            Assert.That(log, Is.EqualTo(new[] { 1, 2 }));
            Assert.That((int)file[ShipDesignFormat.FormatVersionField]!, Is.EqualTo(3));
        }

        [Test]
        public void AMissingStep_IsAnError()
        {
            var migrator = new ShipDesignMigrator(new IShipDesignMigration[] { new RecordingMigration(2, new List<int>()) }, 3);

            Assert.That(migrator.TryMigrate(JObject.Parse("{}"), 1, out var error), Is.False);
            Assert.That(error, Does.Contain("version 1"));
        }

        [Test]
        public void TheCodec_MigratesBeforeReading()
        {
            var migrator = new ShipDesignMigrator(new IShipDesignMigration[] { new RenamePartsField() }, 2);
            var codec = new ShipDesignJsonCodec(ShipDesignLimits.Default, migrator);

            var result = codec.Decode("{\"formatVersion\":1,\"blocks\":[{\"id\":1,\"part\":\"core.helm\",\"cell\":[0,0,0]}]}");

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Design!.Parts[0].PartId, Is.EqualTo("core.helm"));
        }

        [Test]
        public void TheGame_HasAStepForEveryOldVersion()
        {
            var file = JObject.Parse("{}");
            for (int version = 1; version < ShipDesignFormat.CurrentVersion; version++)
            {
                Assert.That(ShipDesignMigrator.Current.TryMigrate(file, version, out var error), Is.True, error);
            }
        }

        /// <summary>An imagined version 1 that called the parts list "blocks".</summary>
        private sealed class RenamePartsField : IShipDesignMigration
        {
            public int FromVersion => 1;

            public void Migrate(JObject file)
            {
                file[ShipDesignFormat.PartsField] = file["blocks"];
                file.Remove("blocks");
            }
        }
    }
}
