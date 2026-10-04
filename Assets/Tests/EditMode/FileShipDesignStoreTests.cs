#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="FileShipDesignStore"/> and <see cref="ShipDesignKeys"/>, in a temporary folder.</summary>
    public class FileShipDesignStoreTests
    {
        private string _directory = null!;
        private readonly ShipDesignJsonCodec _codec = new(ShipDesignLimits.Default);

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "TinCanShipDesigns_" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private FileShipDesignStore Store(int maxTextLength = 1_000_000) =>
            new(_directory, new Dictionary<string, string> { ["Starter"] = _codec.Encode(Design((Helm, 0, 0, 0, 0))) }, _codec, maxTextLength);

        [Test]
        public void ASavedDesign_LoadsBack_AndIsListed()
        {
            var store = Store();
            var design = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0));

            Assert.That(store.TrySave("My Ship", design, out var error), Is.True, error);
            var loaded = store.Load("My Ship");

            Assert.That(loaded.Succeeded, Is.True, loaded.Error);
            Assert.That(ShipDesignHash.Compute(loaded.Design!), Is.EqualTo(ShipDesignHash.Compute(design)));
            Assert.That(File.Exists(Path.Combine(_directory, "My Ship.ship.json")), Is.True);
            Assert.That(store.List().Select(l => l.ToString()), Is.EqualTo(new[] { "Starter (built in)", "My Ship" }));
        }

        [Test]
        public void SavingAgain_ReplacesTheFile_AndLeavesNoTemporaryFile()
        {
            var store = Store();
            store.TrySave("A", Design((Helm, 0, 0, 0, 0)), out _);
            store.TrySave("A", Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0)), out _);

            Assert.That(store.Load("A").Design!.Parts.Count, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        }

        [Test]
        public void BuiltInNames_AreReserved_SoTheBuiltInAlwaysLoads()
        {
            var store = Store();
            Assert.That(store.Load("Starter").Design!.Parts.Count, Is.EqualTo(1));

            Assert.That(store.TrySave("Starter", Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0)), out var error), Is.False);
            Assert.That(error, Does.Contain("built-in"));

            // A file saved before names were reserved does not hide the built-in either.
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "Starter.ship.json"), _codec.Encode(Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0))));
            Assert.That(store.Load("Starter").Design!.Parts.Count, Is.EqualTo(1));
            Assert.That(store.List().Select(l => l.ToString()), Is.EqualTo(new[] { "Starter (built in)" }));
            Assert.That(store.TryDelete("Starter"), Is.False);
        }

        [Test]
        public void Delete_RemovesASavedDesign_ButNeverABuiltIn()
        {
            var store = Store();
            store.TrySave("Gone", Design((Helm, 0, 0, 0, 0)), out _);

            Assert.That(store.TryDelete("Gone"), Is.True);
            Assert.That(store.Load("Gone").Succeeded, Is.False);
            Assert.That(store.TryDelete("Gone"), Is.False);
            Assert.That(store.TryDelete("Starter"), Is.False);
            Assert.That(store.Load("Starter").Succeeded, Is.True);
        }

        [Test]
        public void KeysThatCouldLeaveTheFolder_AreRefused()
        {
            var store = Store();

            Assert.That(store.TrySave("../evil", Design(), out var error), Is.False);
            Assert.That(error, Does.Contain("not a design name"));
            Assert.That(store.Load("..\\evil").Succeeded, Is.False);
            Assert.That(store.Load("Nope").Error, Does.Contain("no design"));
        }

        [Test]
        public void AnOversizedFile_IsRefusedBeforeItIsRead()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "Big.ship.json"), new string(' ', 5000));

            Assert.That(Store(maxTextLength: 1024).Load("Big").Error, Does.Contain("at most 1024"));
        }

        [TestCase("My Ship", "My Ship")]
        [TestCase("  Sky/Whale: II  ", "Sky_Whale_ II")]
        [TestCase("Ålesund", "_lesund")]
        [TestCase("", "Ship")]
        [TestCase("***", "___")]
        public void Keys_AreMadeFromNames(string name, string key)
        {
            Assert.That(ShipDesignKeys.FromName(name), Is.EqualTo(key));
            Assert.That(ShipDesignKeys.IsValid(key), Is.True);
        }
    }
}
