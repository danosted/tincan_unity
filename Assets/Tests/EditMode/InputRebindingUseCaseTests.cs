#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="InputRebindingUseCase"/> against a real in-memory actions asset: the slots the Controls menu lists,
    /// conflicts decided by the contexts, saving and loading the overrides, and resetting.
    /// </summary>
    public class InputRebindingUseCaseTests
    {
        private sealed class MemoryStore : IInputBindingStore
        {
            public string? Json;
            public int Saves;
            public string? Load() => Json;
            public void Save(string overridesJson)
            {
                Json = overridesJson;
                Saves++;
            }
        }

        private InputActionAsset _asset = null!;
        private InputConfig _config = null!;
        private InputActionId _move = null!, _jump = null!, _pitch = null!, _cancel = null!;
        private InputContextSet _contexts = null!;
        private MemoryStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var humanoid = _asset.AddActionMap("Humanoid");
            var move = humanoid.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("up", "<Keyboard>/w").With("down", "<Keyboard>/s").With("left", "<Keyboard>/a").With("right", "<Keyboard>/d");
            var jump = humanoid.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            var airship = _asset.AddActionMap("Airship");
            var pitch = airship.AddAction("Pitch", InputActionType.Value, expectedControlLayout: "Axis");
            pitch.AddCompositeBinding("1DAxis").With("negative", "<Keyboard>/space").With("positive", "<Keyboard>/leftShift");
            var global = _asset.AddActionMap("Global");
            var cancel = global.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");

            _move = InputActionId.Create("Humanoid/Move", move.id.ToString());
            _jump = InputActionId.Create("Humanoid/Jump", jump.id.ToString());
            _pitch = InputActionId.Create("Airship/Pitch", pitch.id.ToString());
            _cancel = InputActionId.Create("Global/Cancel", cancel.id.ToString());

            _config = ScriptableObject.CreateInstance<InputConfig>();
            _config.Actions = _asset;
            _config.ActionIds.AddRange(new[] { _move, _jump, _pitch, _cancel });

            var onFoot = FakeInputContexts.Plain("Humanoid", InputContextActivation.WhilePossessing, 200, actions: new[] { _move, _jump });
            onFoot.Configure(InputContextActivation.WhilePossessing, 200, PossessedActorKind.Humanoid);
            var helm = FakeInputContexts.Plain("Airship", InputContextActivation.WhilePossessing, 300, actions: new[] { _pitch });
            helm.Configure(InputContextActivation.WhilePossessing, 300, PossessedActorKind.Ship);
            var always = FakeInputContexts.Plain("Global", InputContextActivation.Always, 0, actions: new[] { _cancel });
            _contexts = new InputContextSet(new[] { onFoot, helm, always });
            _store = new MemoryStore();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
            Object.DestroyImmediate(_config);
        }

        private (InputRebindingUseCase Bindings, InputSystemReader Reader) Create()
        {
            var reader = new InputSystemReader(_config, new ScriptedInput());
            var bindings = new InputRebindingUseCase(_config, reader, _contexts, new InputRebindState(), _store, new InputBindingConflictProcessor());
            bindings.Initialize();
            return (bindings, reader);
        }

        private static InputBindingSlot Slot(InputRebindingUseCase bindings, string label) => bindings.Slots.Single(s => s.Label == label);

        private static string PathOf(InputSystemReader reader, InputBindingSlot slot) =>
            reader.Find(slot.Action)!.bindings[slot.BindingIndex].effectivePath;

        [Test]
        public void Slots_ListEveryKey_CompositePartsOnTheirOwn()
        {
            var (bindings, reader) = Create();

            Assert.That(bindings.Slots.Select(s => s.Label), Is.EqualTo(new[]
            {
                "Humanoid/Move Forward", "Humanoid/Move Back", "Humanoid/Move Left", "Humanoid/Move Right",
                "Humanoid/Jump", "Airship/Pitch -", "Airship/Pitch +", "Global/Cancel",
            }));
            Assert.That(Slot(bindings, "Humanoid/Jump").Group, Is.EqualTo("Humanoid"));
            reader.Dispose();
        }

        [Test]
        public void KeyUsedInTheSameContext_IsRefused_AndTheOldKeyStays()
        {
            var (bindings, reader) = Create();
            var jump = Slot(bindings, "Humanoid/Jump");

            Assert.That(bindings.TryBind(jump, "<Keyboard>/w"), Is.False);
            Assert.That(PathOf(reader, jump), Is.EqualTo("<Keyboard>/space"));
            Assert.That(bindings.LastMessage, Does.Contain("Move Forward"));
            Assert.That(_store.Saves, Is.Zero);
            reader.Dispose();
        }

        [Test]
        public void KeyOfAnActionThatIsNeverLiveAtTheSameTime_IsAllowed()
        {
            var (bindings, reader) = Create();
            var jump = Slot(bindings, "Humanoid/Jump");

            Assert.That(bindings.TryBind(jump, "<Keyboard>/leftShift"), Is.True, "walking and the helm never share a frame");
            Assert.That(PathOf(reader, jump), Is.EqualTo("<Keyboard>/leftShift"));
            Assert.That(bindings.LastMessage, Is.Null);
            reader.Dispose();
        }

        [Test]
        public void AnAlwaysLiveAction_ConflictsWithEverything()
        {
            var (bindings, reader) = Create();

            Assert.That(bindings.TryBind(Slot(bindings, "Global/Cancel"), "<Keyboard>/space"), Is.False, "Space is Jump, and Cancel is always live");
            reader.Dispose();
        }

        [Test]
        public void AcceptedChange_IsSaved_AndLoadedNextSession()
        {
            var (bindings, reader) = Create();
            bindings.TryBind(Slot(bindings, "Humanoid/Jump"), "<Keyboard>/j");
            reader.Dispose();

            Assert.That(_store.Saves, Is.EqualTo(1));
            var (next, nextReader) = Create();

            Assert.That(PathOf(nextReader, Slot(next, "Humanoid/Jump")), Is.EqualTo("<Keyboard>/j"));
            nextReader.Dispose();
        }

        [Test]
        public void ResetAll_RestoresTheDefaults_AndSaves()
        {
            var (bindings, reader) = Create();
            var jump = Slot(bindings, "Humanoid/Jump");
            bindings.TryBind(jump, "<Keyboard>/j");

            bindings.ResetAll();

            Assert.That(PathOf(reader, jump), Is.EqualTo("<Keyboard>/space"));
            Assert.That(_store.Saves, Is.EqualTo(2));
            reader.Dispose();
        }
    }
}
