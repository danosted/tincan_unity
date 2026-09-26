#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class HumanoidReconciliationProcessorTests
    {
        private readonly HumanoidReconciliationProcessor _processor = new();

        private static HumanoidAuthoritativeState State(Vector3 position, Transform? platform = null, byte epoch = 0, Vector3 velocity = default, float vertical = 0f) =>
            new(1, epoch, platform, position, velocity, vertical);

        [Test]
        public void Evaluate_WithinTolerance_Matches()
        {
            var decision = _processor.Evaluate(State(new Vector3(1f, 0f, 0.01f)), true, State(new Vector3(1f, 0f, 0f)), 0);

            Assert.That(decision.Action, Is.EqualTo(ReconciliationAction.Match));
        }

        [Test]
        public void Evaluate_Divergence_CorrectsByTheDifference()
        {
            var decision = _processor.Evaluate(
                State(new Vector3(1.5f, 0f, 0f), velocity: new Vector3(2f, 0f, 0f), vertical: -2f), true,
                State(new Vector3(1f, 0f, 0f), velocity: new Vector3(1f, 0f, 0f), vertical: 0f), 0);

            Assert.That(decision.Action, Is.EqualTo(ReconciliationAction.Correct));
            Assert.That(decision.PositionError, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            Assert.That(decision.HorizontalVelocityError, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(decision.VerticalVelocityError, Is.EqualTo(-2f));
        }

        [Test]
        public void Evaluate_VelocityOnlyDivergence_Corrects()
        {
            var decision = _processor.Evaluate(State(Vector3.zero, velocity: new Vector3(3f, 0f, 0f)), true, State(Vector3.zero), 0);

            Assert.That(decision.Action, Is.EqualTo(ReconciliationAction.Correct));
        }

        [Test]
        public void Evaluate_NoPrediction_Ignores()
        {
            Assert.That(_processor.Evaluate(State(Vector3.zero), false, default, 0).Action, Is.EqualTo(ReconciliationAction.Ignore));
        }

        [Test]
        public void Evaluate_NewTeleportEpoch_SnapsEvenWithoutPrediction()
        {
            Assert.That(_processor.Evaluate(State(Vector3.zero, epoch: 1), false, default, 0).Action, Is.EqualTo(ReconciliationAction.Snap));
        }

        [Test]
        public void Evaluate_LargeDivergence_Snaps()
        {
            var decision = _processor.Evaluate(State(new Vector3(10f, 0f, 0f)), true, State(Vector3.zero), 0);

            Assert.That(decision.Action, Is.EqualTo(ReconciliationAction.Snap));
        }

        [Test]
        public void Evaluate_DifferentPlatform_Snaps()
        {
            var ship = new GameObject("Ship");
            try
            {
                var decision = _processor.Evaluate(State(Vector3.zero, ship.transform), true, State(Vector3.zero), 0);
                Assert.That(decision.Action, Is.EqualTo(ReconciliationAction.Snap));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }
    }

    public class HumanoidPredictionHistoryTests
    {
        private static HumanoidAuthoritativeState At(uint sequence, float x) => new(sequence, 0, null, new Vector3(x, 0f, 0f), Vector3.zero, 0f);

        [Test]
        public void DropThrough_RemovesAcknowledged()
        {
            var history = new HumanoidPredictionHistory();
            for (uint i = 1; i <= 5; i++) history.Record(default, At(i, i));

            history.DropThrough(3);

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.TryGet(3, out _), Is.False);
            Assert.That(history.TryGet(4, out var four), Is.True);
            Assert.That(four.LocalPosition.x, Is.EqualTo(4f));
        }

        [Test]
        public void Replace_KeepsInputAndUpdatesState()
        {
            var history = new HumanoidPredictionHistory();
            history.Record(new HumanoidInputState { Sequence = 1, IsJumping = true }, At(1, 1f));

            history.Replace(0, At(1, 5f));

            Assert.That(history.Entries[0].Input.IsJumping, Is.True);
            Assert.That(history.TryGet(1, out var one), Is.True);
            Assert.That(one.LocalPosition.x, Is.EqualTo(5f));
        }

        [Test]
        public void Record_BeyondCapacity_DropsOldest()
        {
            var history = new HumanoidPredictionHistory(capacity: 2);
            history.Record(default, At(1, 1f));
            history.Record(default, At(2, 2f));
            history.Record(default, At(3, 3f));

            Assert.That(history.TryGet(1, out _), Is.False);
            Assert.That(history.Count, Is.EqualTo(2));
        }

        [Test]
        public void AuthoritativeState_FromWorld_RoundTripsThroughPlatformFrame()
        {
            var ship = new GameObject("Ship");
            try
            {
                ship.transform.SetPositionAndRotation(new Vector3(10f, 5f, 0f), Quaternion.Euler(0f, 90f, 0f));
                var state = HumanoidAuthoritativeState.FromWorld(1, 0, ship.transform, new Vector3(11f, 5f, 0f), new Vector3(0f, 0f, 2f), 0f);

                Assert.That(Vector3.Distance(state.WorldPosition, new Vector3(11f, 5f, 0f)), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(state.WorldHorizontalVelocity, new Vector3(0f, 0f, 2f)), Is.LessThan(1e-4f));
                Assert.That(Vector3.Distance(state.LocalPosition, new Vector3(0f, 0f, 1f)), Is.LessThan(1e-4f)); // +90° yaw: local +Z is world +X
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }
    }

    public class HumanoidMovementPredictionUseCaseTests
    {
        /// <summary>A humanoid that stamps sequences like the real mediator and exposes the prediction contract.</summary>
        private sealed class PredictedCharacter : FakeHumanoidCharacterView, IHumanoidCharacterView, IPredictedHumanoid
        {
            private HumanoidInputState _input;
            private uint _nextSequence;
            private readonly Queue<HumanoidAuthoritativeState> _incoming = new();

            public PredictedCharacter(FakeHumanoidMovementView movement) : base(movement) { }

            public new HumanoidInputState InputState
            {
                get => _input;
                set
                {
                    if (IsLocallyPredicted) value.Sequence = ++_nextSequence;
                    _input = value;
                }
            }

            public bool IsLocallyPredicted { get; set; }
            public bool PublishesAuthoritativeState { get; set; }
            public HumanoidPredictionStats PredictionStats { get; } = new();
            public List<HumanoidAuthoritativeState> Published { get; } = new();

            public void Deliver(HumanoidAuthoritativeState state) => _incoming.Enqueue(state);

            public bool TryTakeAuthoritativeState(out HumanoidAuthoritativeState state)
            {
                state = default;
                if (_incoming.Count == 0) return false;

                state = _incoming.Dequeue();
                return true;
            }

            public void PublishAuthoritativeState(in HumanoidAuthoritativeState state) => Published.Add(state);
        }

        private FakeTimeService _time = null!;
        private FakeActorRegistry _registry = null!;
        private FakeInputService _inputService = null!;
        private HumanoidMovementUseCase _useCase = null!;
        private FakeHumanoidMovementView _movement = null!;
        private PredictedCharacter _character = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService();
            _registry = new FakeActorRegistry();
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _registry, _time, new FakeEventPublisher());
            _inputService = new FakeInputService();
            _useCase = new HumanoidMovementUseCase(_inputService, new FakeNetworkService(), new HumanoidMovementProcessor(), abilities, _registry, _time);
            _movement = new FakeHumanoidMovementView("Predicted") { Gravity = 0f };
            _character = new PredictedCharacter(_movement);
            _registry.Register(_character);
        }

        [TearDown]
        public void TearDown() => _movement.Destroy();

        private void MakeOwner()
        {
            _character.IsLocallyPredicted = true;
            _character.AuthoritativeSetPossessor(0); // captured by the local client (FakeNetworkService.LocalClientId == 0)
        }

        private static HumanoidAuthoritativeState Server(uint sequence, Vector3 position, byte epoch = 0) =>
            new(sequence, epoch, null, position, Vector3.zero, 0f);

        [Test]
        public void Owner_MatchingAck_DoesNotCorrect()
        {
            MakeOwner();
            _useCase.Tick();
            _character.Deliver(Server(1, _movement.Transform.position));

            _useCase.Tick();

            Assert.That(_character.PredictionStats.Matches, Is.EqualTo(1));
            Assert.That(_character.PredictionStats.Corrections, Is.Zero);
        }

        [Test]
        public void Owner_DivergentAck_ShiftsByTheError()
        {
            MakeOwner();
            _useCase.Tick();
            Vector3 predicted = _movement.Transform.position;
            _character.Deliver(Server(1, predicted + new Vector3(0.5f, 0f, 0f)));

            _useCase.Tick();

            Assert.That(_character.PredictionStats.Corrections, Is.EqualTo(1));
            Assert.That(Vector3.Distance(_movement.Transform.position, predicted + new Vector3(0.5f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Owner_AfterCorrection_NextAckOnCorrectedPathMatches()
        {
            MakeOwner();
            _useCase.Tick();                                                   // predicts seq 1
            Vector3 start = _movement.Transform.position;
            _character.Deliver(Server(1, start + new Vector3(0.5f, 0f, 0f)));
            _useCase.Tick();                                                   // corrects, predicts seq 2
            _character.Deliver(Server(2, start + new Vector3(0.5f, 0f, 0f)));

            _useCase.Tick();

            Assert.That(_character.PredictionStats.Corrections, Is.EqualTo(1));
            Assert.That(_character.PredictionStats.Matches, Is.EqualTo(1));
        }

        [Test]
        public void Owner_DivergentAck_ReplaysUnacknowledgedInputsFromServerState()
        {
            // Reference run: walk forward four ticks with no server feedback.
            MakeOwner();
            _inputService.PressedActions.Add(ActionNames.MoveForward);
            Vector3 start = _movement.Transform.position;
            for (int i = 0; i < 4; i++) _useCase.Tick();
            Vector3 uncorrected = _movement.Transform.position - start;

            // Same walk, but after three ticks the server says input 1 left us 1 m further along X.
            _movement.Transform.position = start;
            var replayed = new PredictedCharacter(_movement) { IsLocallyPredicted = true };
            replayed.AuthoritativeSetPossessor(0);
            _registry.Unregister(_character);
            _registry.Register(replayed);
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _registry, _time, new FakeEventPublisher());
            var useCase = new HumanoidMovementUseCase(_inputService, new FakeNetworkService(), new HumanoidMovementProcessor(), abilities, _registry, _time);

            useCase.Tick();
            Vector3 afterFirst = _movement.Transform.position;
            var velocityAfterFirst = new Vector3(0f, 0f, 30f * _time.DeltaTime); // acceleration 30 m/s², from rest
            useCase.Tick();
            useCase.Tick();
            replayed.Deliver(new HumanoidAuthoritativeState(1, 0, null, afterFirst + new Vector3(1f, 0f, 0f), velocityAfterFirst, 0f));
            useCase.Tick();

            Assert.That(replayed.PredictionStats.Corrections, Is.EqualTo(1));
            Vector3 expected = uncorrected + new Vector3(1f, 0f, 0f);
            Assert.That(Vector3.Distance(_movement.Transform.position - start, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void Owner_TeleportEpoch_SnapsToServer()
        {
            MakeOwner();
            _useCase.Tick();
            _character.Deliver(Server(1, new Vector3(20f, 0f, 20f), epoch: 1));

            _useCase.Tick();

            Assert.That(_character.PredictionStats.Snaps, Is.EqualTo(1));
            Assert.That(Vector3.Distance(_movement.Transform.position, new Vector3(20f, 0f, 20f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Server_RemoteOwned_PublishesStateForTheInputItSimulated()
        {
            _character.PublishesAuthoritativeState = true;
            _character.InputState = new HumanoidInputState { Sequence = 42 };

            _useCase.Tick();

            Assert.That(_character.Published, Has.Count.EqualTo(1));
            Assert.That(_character.Published[0].Sequence, Is.EqualTo(42));
            Assert.That(Vector3.Distance(_character.Published[0].WorldPosition, _movement.Transform.position), Is.LessThan(1e-4f));
        }

        [Test]
        public void Server_ResetCharacter_BumpsTeleportEpoch()
        {
            _character.PublishesAuthoritativeState = true;
            _useCase.ResetCharacter(_character, new Vector3(5f, 0f, 5f), Quaternion.identity);

            _useCase.Tick();

            Assert.That(_character.Published[0].TeleportEpoch, Is.EqualTo(1));
        }
    }
}
