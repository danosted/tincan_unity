#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain;

namespace TinCan.Tests.EditMode
{
    public class ScriptedInputTests
    {
        [Test]
        public void PressAndRelease_ControlIsPressed()
        {
            var input = new ScriptedInput();

            input.Press(ActionNames.MoveForward);
            Assert.That(input.IsPressed(ActionNames.MoveForward), Is.True);

            input.Release(ActionNames.MoveForward);
            Assert.That(input.IsPressed(ActionNames.MoveForward), Is.False);
        }

        [Test]
        public void Tap_IsVisibleToEveryReaderInTheFrameItIsRead_ThenSpentAfterLateTick()
        {
            var input = new ScriptedInput();
            input.Tap(ActionNames.Interact);

            Assert.That(input.WasTriggered(ActionNames.Interact), Is.True);
            Assert.That(input.WasTriggered(ActionNames.Interact), Is.True, "second reader in the same frame still sees it");

            input.LateTick();

            Assert.That(input.WasTriggered(ActionNames.Interact), Is.False);
        }

        [Test]
        public void Tap_UnreadTapSurvivesLateTick()
        {
            var input = new ScriptedInput();
            input.Tap(ActionNames.Jump);

            input.LateTick();

            Assert.That(input.WasTriggered(ActionNames.Jump), Is.True);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var input = new ScriptedInput();
            input.Press(ActionNames.Sprint);
            input.Tap(ActionNames.Interact);

            input.Clear();

            Assert.That(input.IsPressed(ActionNames.Sprint), Is.False);
            Assert.That(input.WasTriggered(ActionNames.Interact), Is.False);
        }
    }
}
