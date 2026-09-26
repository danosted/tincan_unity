#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.HumanoidMovement;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class HumanoidInputBufferTests
    {
        private static HumanoidInputState Input(uint sequence, bool jump = false, ulong mask = 0, float x = 0f) => new()
        {
            Sequence = sequence,
            IsJumping = jump,
            ActiveInputMask = mask,
            MovementDirection = new Vector3(x, 0f, 0f)
        };

        /// <summary>The owner's send pattern: each packet carries the newest input plus up to three before it.</summary>
        private static HumanoidInputState[] Window(uint newest, int size = 4)
        {
            var inputs = new List<HumanoidInputState>();
            for (uint sequence = newest >= (uint)size ? newest - (uint)size + 1 : 1; sequence <= newest; sequence++) inputs.Add(Input(sequence));
            return inputs.ToArray();
        }

        [Test]
        public void Consume_OnePerTick_InSequenceOrder()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(new[] { Input(2), Input(1), Input(3) });

            Assert.That(buffer.Consume().Sequence, Is.EqualTo(1));
            Assert.That(buffer.Consume().Sequence, Is.EqualTo(2));
            Assert.That(buffer.Consume().Sequence, Is.EqualTo(3));
            Assert.That(buffer.LastConsumedSequence, Is.EqualTo(3));
        }

        [Test]
        public void Receive_RedundantWindows_ConsumesEachInputOnce()
        {
            var buffer = new HumanoidInputBuffer();
            var consumed = new List<uint>();

            for (uint tick = 1; tick <= 10; tick++)
            {
                buffer.Receive(Window(tick));
                consumed.Add(buffer.Consume().Sequence);
            }

            Assert.That(consumed, Is.EqualTo(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
            Assert.That(buffer.Stats.Starved, Is.Zero);
            Assert.That(buffer.Stats.Skipped, Is.Zero);
        }

        [Test]
        public void Receive_LostPackets_RecoveredByRedundancy()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(Window(1));
            buffer.Consume();

            // Packets for ticks 2, 3 and 4 are lost; the tick-5 packet still carries 2..5.
            buffer.Consume();
            buffer.Consume();
            buffer.Receive(Window(5));

            var consumed = new List<uint>();
            for (int i = 0; i < 4; i++) consumed.Add(buffer.Consume().Sequence);

            Assert.That(consumed, Is.EqualTo(new uint[] { 2, 3, 4, 5 }));
            Assert.That(buffer.Stats.Starved, Is.EqualTo(2));
        }

        [Test]
        public void Receive_IgnoresAlreadyConsumedAndDuplicates()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(new[] { Input(1), Input(2) });
            buffer.Consume();
            buffer.Receive(new[] { Input(1), Input(2), Input(2) });

            Assert.That(buffer.Depth, Is.EqualTo(1));
            Assert.That(buffer.Consume().Sequence, Is.EqualTo(2));
        }

        [Test]
        public void Starved_RepeatsLastInputWithoutJump()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(new[] { Input(1, jump: true, x: 1f) });
            buffer.Consume();

            var repeat = buffer.Consume();

            Assert.That(repeat.MovementDirection.x, Is.EqualTo(1f));
            Assert.That(repeat.IsJumping, Is.False);
            Assert.That(buffer.Stats.Starved, Is.EqualTo(1));
        }

        [Test]
        public void Starved_BeforeAnyInput_ReturnsDefault()
        {
            var buffer = new HumanoidInputBuffer();

            Assert.That(buffer.Consume().Sequence, Is.Zero);
        }

        [Test]
        public void LateInputAfterStarvation_IsStillConsumed()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(new[] { Input(1) });
            buffer.Consume();
            buffer.Consume(); // starved, repeats 1

            buffer.Receive(new[] { Input(2, jump: true) });

            var next = buffer.Consume();
            Assert.That(next.Sequence, Is.EqualTo(2));
            Assert.That(next.IsJumping, Is.True);
        }

        [Test]
        public void Overflow_SkipsOldestButCarriesOneShotBits()
        {
            var buffer = new HumanoidInputBuffer(maxQueued: 2);
            buffer.Receive(new[] { Input(1, jump: true), Input(2, mask: 0b100), Input(3), Input(4, x: 1f) });

            var next = buffer.Consume();

            Assert.That(next.Sequence, Is.EqualTo(3));
            Assert.That(next.IsJumping, Is.True);
            Assert.That(next.ActiveInputMask, Is.EqualTo(0b100UL));
            Assert.That(buffer.Stats.Skipped, Is.EqualTo(2));

            var after = buffer.Consume();
            Assert.That(after.Sequence, Is.EqualTo(4));
            Assert.That(after.IsJumping, Is.False);
        }

        [Test]
        public void Stats_TrackDepth()
        {
            var buffer = new HumanoidInputBuffer();
            buffer.Receive(new[] { Input(1), Input(2), Input(3) });
            buffer.Consume();
            buffer.Consume();

            Assert.That(buffer.Stats.MaxDepth, Is.EqualTo(3));
            Assert.That(buffer.Stats.MeanDepth, Is.EqualTo(2.5f));
            Assert.That(buffer.Stats.Received, Is.EqualTo(3));
        }
    }

    public class HumanoidMovementUseCaseBufferedInputTests
    {
        private sealed class BufferedCharacter : Fakes.FakeHumanoidCharacterView, IBufferedInputSource
        {
            public int Advances;
            public uint SequenceAtAdvance = 7;
            public BufferedCharacter(Fakes.FakeHumanoidMovementView movement) : base(movement) { }

            public HumanoidInputBufferStats InputBufferStats => default;

            public void AdvanceInput()
            {
                Advances++;
                InputState = new HumanoidInputState { Sequence = SequenceAtAdvance };
            }
        }

        [Test]
        public void Tick_AdvancesBufferedInputOncePerTick()
        {
            var time = new Fakes.FakeTimeService();
            var registry = new Fakes.FakeActorRegistry();
            var abilities = new TinCan.Features.Abilities.AbilitySystemUseCase(new Fakes.FakeAbilityRegistry(), registry, time, new Fakes.FakeEventPublisher());
            var useCase = new HumanoidMovementUseCase(new Fakes.FakeInputService(), new Fakes.FakeNetworkService(), new HumanoidMovementProcessor(), abilities, registry, time);
            var movement = new Fakes.FakeHumanoidMovementView("Buffered");
            var character = new BufferedCharacter(movement);
            registry.Register(character);

            try
            {
                useCase.Tick();
                useCase.Tick();

                Assert.That(character.Advances, Is.EqualTo(2));
                Assert.That(character.InputState.Sequence, Is.EqualTo(7));
            }
            finally
            {
                movement.Destroy();
            }
        }
    }
}
