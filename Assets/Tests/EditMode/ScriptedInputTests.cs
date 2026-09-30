#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class ScriptedInputTests
    {
        private readonly InputActionId _move = InputActionId.Create("Humanoid/Move");
        private readonly InputActionId _jump = InputActionId.Create("Humanoid/Jump");
        private readonly InputActionId _sprint = InputActionId.Create("Humanoid/Sprint");
        private readonly InputActionId _interact = InputActionId.Create("Humanoid/Interact");

        [Test]
        public void PressAndRelease_ControlIsPressed()
        {
            var input = new ScriptedInput();

            input.Press(_sprint);
            Assert.That(input.IsPressed(_sprint), Is.True);

            input.Release(_sprint);
            Assert.That(input.IsPressed(_sprint), Is.False);
        }

        [Test]
        public void HeldValues_AddUp_AndReleaseOneAtATime()
        {
            var input = new ScriptedInput();

            input.Press(_move, Vector2.up);
            input.Press(_move, Vector2.right);
            Assert.That(input.Value(_move), Is.EqualTo(new Vector2(1f, 1f)));

            input.Release(_move, Vector2.up);
            Assert.That(input.Value(_move), Is.EqualTo(Vector2.right));
            Assert.That(input.IsPressed(_move), Is.True);

            input.Release(_move, Vector2.right);
            Assert.That(input.IsPressed(_move), Is.False);
        }

        [Test]
        public void Tap_IsVisibleToEveryReaderInTheFrameItIsRead_ThenSpentAfterLateTick()
        {
            var input = new ScriptedInput();
            input.Tap(_interact);

            Assert.That(input.WasTriggered(_interact), Is.True);
            Assert.That(input.WasTriggered(_interact), Is.True, "second reader in the same frame still sees it");

            input.LateTick();

            Assert.That(input.WasTriggered(_interact), Is.False);
        }

        [Test]
        public void Tap_UnreadTapSurvivesLateTick()
        {
            var input = new ScriptedInput();
            input.Tap(_jump);

            input.LateTick();

            Assert.That(input.WasTriggered(_jump), Is.True);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var input = new ScriptedInput();
            input.Press(_sprint);
            input.Tap(_interact);

            input.Clear();

            Assert.That(input.IsPressed(_sprint), Is.False);
            Assert.That(input.WasTriggered(_interact), Is.False);
        }
    }
}
