#nullable enable
using System;
using NUnit.Framework;

namespace TinCan.Core.Humanoid.Tests
{
    /// <summary>Presses read once each, from inside the movement step (<see cref="InputPresses"/>).</summary>
    public class InputPressesTests
    {
        private const int Bit = 3;
        private static readonly Guid Player = Guid.NewGuid();

        private static HumanoidInputState Input(uint sequence, bool down) =>
            new() { Sequence = sequence, ActiveInputMask = down ? 1UL << Bit : 0UL };

        [Test]
        public void APress_CountsOnce_WhileHeld()
        {
            var presses = new InputPresses();

            Assert.That(presses.Pressed(Player, Input(1, true), Bit), Is.True);
            Assert.That(presses.Pressed(Player, Input(2, true), Bit), Is.False, "still held");
            Assert.That(presses.Pressed(Player, Input(3, false), Bit), Is.False);
            Assert.That(presses.Pressed(Player, Input(4, true), Bit), Is.True, "pressed again");
        }

        [Test]
        public void AReplay_OfAnInputAlreadyRead_NeverPressesAgain()
        {
            var presses = new InputPresses();

            presses.Pressed(Player, Input(5, true), Bit);
            presses.Pressed(Player, Input(6, false), Bit);

            Assert.That(presses.Pressed(Player, Input(5, true), Bit), Is.False);
        }

        [Test]
        public void EachPlayer_HasTheirOwnPresses()
        {
            var presses = new InputPresses();

            Assert.That(presses.Pressed(Player, Input(1, true), Bit), Is.True);
            Assert.That(presses.Pressed(Guid.NewGuid(), Input(1, true), Bit), Is.True);
        }
    }
}
