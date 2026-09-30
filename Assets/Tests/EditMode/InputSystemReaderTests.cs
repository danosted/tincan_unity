#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Gas.Inputs;
using TinCan.Core.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan.Tests.EditMode
{
    /// <summary>The reader against a real (in-memory) actions asset: context gating, scripted input and the ability mask.</summary>
    public class InputSystemReaderTests
    {
        private InputActionAsset _asset = null!;
        private InputConfig _config = null!;
        private InputActionId _jump = null!;
        private InputActionId _move = null!;
        private PrimaryInput _primary = null!;
        private ScriptedInput _scripted = null!;
        private InputSystemReader _reader = null!;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = _asset.AddActionMap("Humanoid");
            var jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");

            _jump = InputActionId.Create("Humanoid/Jump", jump.id.ToString());
            _move = InputActionId.Create("Humanoid/Move", move.id.ToString());
            _primary = ScriptableObject.CreateInstance<PrimaryInput>();
            _primary.SetActions(new[] { _jump });

            _config = ScriptableObject.CreateInstance<InputConfig>();
            _config.Actions = _asset;
            _config.ActionIds.Add(_jump);
            _config.ActionIds.Add(_move);
            _config.GameplayInputs.Add(_primary);

            _scripted = new ScriptedInput();
            _reader = new InputSystemReader(_config, _scripted);
            _reader.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _reader.Dispose();
            Object.DestroyImmediate(_asset);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_primary);
        }

        [Test]
        public void Initialize_FindsEveryActionById_AndNumbersTheAbilityInputs()
        {
            Assert.That(_reader.Find(_jump), Is.Not.Null);
            Assert.That(_reader.Find(_jump)!.name, Is.EqualTo("Jump"));
            Assert.That(_primary.BitIndex, Is.Zero);
        }

        [Test]
        public void ActionOutsideEveryLiveContext_ReadsReleased_EvenWhenScripted()
        {
            _scripted.Press(_jump);
            _scripted.Press(_move, Vector2.up);

            Assert.That(_reader.IsPressed(_jump), Is.False);
            Assert.That(_reader.ReadVector2(_move), Is.EqualTo(Vector2.zero));
            Assert.That(_reader.GameplayInputMask(), Is.Zero);
            Assert.That(_reader.Find(_jump)!.enabled, Is.False);
        }

        [Test]
        public void EnabledAction_ReadsScriptedInput_AndSetsItsAbilityBit()
        {
            _reader.SetEnabled(new[] { _jump, _move });
            _scripted.Press(_jump);
            _scripted.Press(_move, Vector2.up);

            Assert.That(_reader.Find(_jump)!.enabled, Is.True);
            Assert.That(_reader.IsPressed(_jump), Is.True);
            Assert.That(_reader.ReadVector2(_move), Is.EqualTo(Vector2.up));
            Assert.That(_reader.GameplayInputMask(), Is.EqualTo(1UL));
        }

        [Test]
        public void Disabling_TurnsTheDeviceSideOffAgain()
        {
            _reader.SetEnabled(new[] { _jump });
            _reader.SetEnabled(new InputActionId[0]);

            Assert.That(_reader.Find(_jump)!.enabled, Is.False);
        }
    }
}
