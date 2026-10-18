#nullable enable
using System.Collections.Generic;
using System.Linq;
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

            var result = codec.Decode("{\"formatVersion\":1,\"blocks\":[{\"id\":1,\"part\":\"core.helm\",\"on\":{\"node\":1}}]}");

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Design!.Parts[0].PartId, Is.EqualTo("core.helm"));
        }

        [Test]
        public void Version2To3_MakesEachCellAPosition_TenTimesIt()
        {
            var file = JObject.Parse("{\"parts\":[{\"id\":1,\"part\":\"a\",\"cell\":[1,-2,3],\"rot\":1},{\"id\":2,\"part\":\"b\",\"cell\":[0,0.5,0]}]}");

            new ShipDesignMigrationV2().Migrate(file);

            var parts = (JArray)file[ShipDesignFormat.PartsField]!;
            Assert.That(parts[0][ShipDesignFormat.V3PartPosField]!.Values<int>(), Is.EqualTo(new[] { 10, -20, 30 }));
            Assert.That(parts[0][ShipDesignFormat.V2PartCellField], Is.Null);
            Assert.That(parts[1][ShipDesignFormat.V3PartPosField]![1]!.Value<double>(), Is.EqualTo(0.5), "not a whole number: left for the decoder to refuse");
        }

        [Test]
        public void Version3To4_HangsPartsOnTheFloorTheyStandOn_AndKeepsTheRestAsDropped()
        {
            var file = JObject.Parse("{\"parts\":[" +
                                     "{\"id\":10,\"part\":\"core.helm\",\"pos\":[0,0,0],\"rot\":1}," +
                                     "{\"id\":11,\"part\":\"hull.block\",\"pos\":[300,0,0],\"rot\":0}," +
                                     "{\"id\":12,\"part\":\"hull.block\",\"pos\":[10,0,10],\"rot\":7}]," +
                                     "\"frame\":{\"base\":-5,\"stories\":[30],\"nodes\":[{\"id\":1,\"story\":0,\"pos\":[-20,0,-20]},{\"id\":2,\"story\":0,\"pos\":[-20,0,20]}," +
                                     "{\"id\":3,\"story\":0,\"pos\":[20,0,20]},{\"id\":4,\"story\":0,\"pos\":[20,0,-20]}],\"edges\":[{\"id\":5,\"a\":1,\"b\":2}," +
                                     "{\"id\":6,\"a\":2,\"b\":3,\"rail\":true},{\"id\":7,\"a\":3,\"b\":4},{\"id\":8,\"a\":4,\"b\":1}]," +
                                     "\"faces\":[{\"id\":9,\"nodes\":[1,2,3,4],\"fill\":\"planks\"}]}}");

            new ShipDesignMigrationV3().Migrate(file);

            var parts = (JArray)file[ShipDesignFormat.PartsField]!;
            Assert.That(parts.Count, Is.EqualTo(1));
            Assert.That(parts[0][ShipDesignFormat.PartAnchorField]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("{\"face\":9,\"x\":0,\"z\":0}"));
            Assert.That(parts[0][ShipDesignFormat.V3PartPosField], Is.Null);
            var dropped = (JArray)file[ShipDesignFormat.DroppedField]!;
            Assert.That(dropped.Select(p => p["id"]!.Value<int>()), Is.EqualTo(new[] { 11, 12 }), "off the deck; tipped over");
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
