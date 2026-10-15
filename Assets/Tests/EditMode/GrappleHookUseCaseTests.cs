#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Gas.Inputs;
using TinCan.Core.Humanoid;
using TinCan.Features.GrappleHook;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class GrappleHookUseCaseTests
    {
        private const int FireBit = 3;
        private const float DeltaTime = 1f / 30f;

        private GrappleHookConfig _config = null!;
        private GameplayTag _carrying = null!;
        private PrimaryInput _fire = null!;
        private FakeQuery _query = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _character = null!;
        private GameObject _rock = null!;

        [SetUp]
        public void SetUp()
        {
            _carrying = ScriptableObject.CreateInstance<GameplayTag>();
            _carrying.name = "State.Carrying.GrappleHook";
            _fire = ScriptableObject.CreateInstance<PrimaryInput>();
            _fire.BitIndex = FireBit;
            _config = ScriptableObject.CreateInstance<GrappleHookConfig>();
            _config.CarryingTag = _carrying;
            _config.FireInput = _fire;

            _rock = new GameObject("Rock");
            _rock.transform.position = new Vector3(0f, 0f, 20f);
            _query = new FakeQuery { Frame = _rock.transform, Point = new Vector3(0f, 0.7f, 19.5f) };

            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _movement = new FakeHumanoidMovementView("Holder");
            _character = new FakeHumanoidCharacterView(_movement);
            _character.HeldTags.Add(_carrying);
            _actors.Register(_character);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            if (_rock != null) Object.DestroyImmediate(_rock);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_carrying);
            Object.DestroyImmediate(_fire);
        }

        [Test]
        public void Press_WhileHoldingTheHook_CatchesWhereTheLookPoints_AndReelsTowardIt()
        {
            var useCase = UseCase();

            var step = Contribute(useCase, Input(1, fire: true));

            Assert.That(_query.Calls, Is.EqualTo(1));
            Assert.That(Vector3.Distance(_query.From, new Vector3(0f, 0.7f, 0f)), Is.LessThan(1e-4f), "Fired from the eye.");
            Assert.That(Vector3.Distance(_query.Direction, Vector3.forward), Is.LessThan(1e-4f), "Along the input's look.");
            Assert.That(step.Velocity.z, Is.GreaterThan(0f), "Pulled toward the hook point.");
            Assert.That(useCase.TryGetHookPoint(_character.Id, out var point), Is.True);
            Assert.That(Vector3.Distance(point, _query.Point), Is.LessThan(1e-4f));
            Assert.That(_events.Count<GrappleHookAttachedEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void Press_WithoutTheHook_DoesNothing()
        {
            _character.HeldTags.Clear();
            var useCase = UseCase();

            var step = Contribute(useCase, Input(1, fire: true));

            Assert.That(_query.Calls, Is.EqualTo(0));
            Assert.That(step.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);
        }

        [Test]
        public void Press_ThatCatchesNothing_DoesNotPull()
        {
            _query.Frame = null;
            var useCase = UseCase();

            var step = Contribute(useCase, Input(1, fire: true));

            Assert.That(step.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);
        }

        [Test]
        public void HoldingTheInput_KeepsReeling_ReleasingItLetsGo()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(1, fire: true));

            var held = Contribute(useCase, Input(2, fire: true), new Vector3(0f, 0f, 2f));
            Assert.That(held.Velocity.z, Is.GreaterThan(2f), "Still reeling while held.");
            Assert.That(_query.Calls, Is.EqualTo(1), "Holding does not fire again.");

            var released = Contribute(useCase, Input(3, fire: false), new Vector3(0f, -1f, 4f));
            Assert.That(released.Velocity, Is.EqualTo(new Vector3(0f, -1f, 4f)), "Released: the body keeps its own motion.");
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);
            Assert.That(_events.Count<GrappleHookReleasedEvent>(), Is.EqualTo(1));
        }

        [Test]
        public void PuttingTheHookAway_LetsGo()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(1, fire: true));

            _character.HeldTags.Clear();
            Contribute(useCase, Input(2, fire: true));

            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);
        }

        [Test]
        public void Arriving_LetsGo_AndHoldingOnDoesNotFireAgain()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(1, fire: true));

            _movement.Transform.position = _query.Point - new Vector3(0f, 0f, 1f);
            var arrival = Contribute(useCase, Input(2, fire: true), new Vector3(0f, 0f, 18f));

            Assert.That(arrival.Velocity.y, Is.EqualTo(_config.ArriveHopSpeed).Within(1e-4f), "The arrival hop.");
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);

            Contribute(useCase, Input(3, fire: true));
            Assert.That(_query.Calls, Is.EqualTo(1), "Only a new press fires.");
        }

        [Test]
        public void TheHookPoint_MovesWithWhatItCaughtOn()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(1, fire: true));

            _rock.transform.position += new Vector3(5f, 0f, 0f);

            Assert.That(useCase.TryGetHookPoint(_character.Id, out var point), Is.True);
            Assert.That(point.x, Is.EqualTo(5f).Within(1e-4f), "A point on a flying ship moves with the ship.");
        }

        [Test]
        public void LosingWhatItCaughtOn_LetsGo()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(1, fire: true));

            Object.DestroyImmediate(_rock);
            var step = Contribute(useCase, Input(2, fire: true));

            Assert.That(step.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False);
        }

        [Test]
        public void Replay_PullsForTheInputsTheAnchorCovered_AndChangesNothing()
        {
            var useCase = UseCase();
            Contribute(useCase, Input(10, fire: true));
            Contribute(useCase, Input(11, fire: true));
            Contribute(useCase, Input(15, fire: false));

            Assert.That(Contribute(useCase, Input(9, fire: true), replay: true).Velocity, Is.EqualTo(Vector3.zero), "Before it fired.");
            Assert.That(Contribute(useCase, Input(12, fire: true), replay: true).Velocity.z, Is.GreaterThan(0f), "While it held.");
            Assert.That(Contribute(useCase, Input(15, fire: true), replay: true).Velocity, Is.EqualTo(Vector3.zero), "From its release on.");

            Contribute(useCase, Input(20, fire: true), replay: true);
            Assert.That(_query.Calls, Is.EqualTo(1), "A replayed press never fires.");
            Assert.That(useCase.TryGetHookPoint(_character.Id, out _), Is.False, "Replay changed no state.");
        }

        [Test]
        public void Server_PublishesTheLines_OnlyWhenTheyChange()
        {
            var state = new FakeLineState();
            _actors.Register(state);
            var useCase = UseCase();

            useCase.Tick();
            Assert.That(state.Published.Count, Is.EqualTo(1), "First publish to a new state fixture.");
            Assert.That(state.Published[0], Is.Empty);

            Contribute(useCase, Input(1, fire: true));
            useCase.Tick();
            useCase.Tick();
            Assert.That(state.Published.Count, Is.EqualTo(2), "Published once for the catch.");
            Assert.That(state.Published[1].Count, Is.EqualTo(1));
            Assert.That(state.Published[1][0].Player, Is.SameAs(_character));
            Assert.That(state.Published[1][0].Frame, Is.SameAs(_rock.transform));

            Contribute(useCase, Input(2, fire: false));
            useCase.Tick();
            Assert.That(state.Published.Count, Is.EqualTo(3));
            Assert.That(state.Published[2], Is.Empty, "Released: no line.");
        }

        [Test]
        public void Client_PredictsThePull_ButPublishesNothing()
        {
            var state = new FakeLineState();
            _actors.Register(state);
            var useCase = UseCase(new FakeSessionNetworkService { IsServer = false, IsClient = true });

            var step = Contribute(useCase, Input(1, fire: true));
            useCase.Tick();

            Assert.That(step.Velocity.z, Is.GreaterThan(0f), "The owner predicts its own pull.");
            Assert.That(state.Published, Is.Empty);
            Assert.That(_events.Total, Is.EqualTo(0), "Domain events are the server's.");
        }

        [Test]
        public void InTheMovementSimulation_ReelsTheBodyTowardTheHook()
        {
            var time = new FakeTimeService { DeltaTime = DeltaTime };
            var network = new FakeNetworkService();
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, time, new FakeEventPublisher());
            var hooks = UseCase(network);
            var movement = new HumanoidMovementUseCase(new FakeInputReader(), FakeInputContexts.Humanoid(), network,
                new HumanoidMovementProcessor(), abilities, _actors, time, FakeInputContexts.NoContributors,
                new IHumanoidMotionContributor[] { hooks });

            _movement.Deceleration = 200f; // the player prefab's braking (NetworkPlayer.prefab): far stronger than the reel's acceleration, so the pull must not start from the braked velocity
            _character.InputState = Input(1, fire: true);
            for (int i = 0; i < 10; i++) movement.Tick();

            Assert.That(_movement.Transform.position.z, Is.GreaterThan(2f), "Ten ticks of reeling moved the body toward the hook.");
            Assert.That(hooks.TryGetHookPoint(_character.Id, out _), Is.True);
        }

        private GrappleHookUseCase UseCase(INetworkService? network = null) =>
            new(_config, new GrappleHookProcessor(), _query, network ?? new FakeNetworkService(), _actors, _events);

        private HumanoidMotionStep Contribute(GrappleHookUseCase useCase, HumanoidInputState input, Vector3 velocity = default, bool replay = false)
        {
            var step = new HumanoidMotionStep(velocity, velocity, DeltaTime, isGrounded: false, isReplay: replay);
            useCase.Contribute(_character, input, ref step);
            return step;
        }

        private static HumanoidInputState Input(uint sequence, bool fire) => new()
        {
            Sequence = sequence,
            LookRotation = Quaternion.identity,
            ActiveInputMask = fire ? 1UL << FireBit : 0UL
        };

        private sealed class FakeQuery : IGrappleHookQuery
        {
            public Transform? Frame;
            public Vector3 Point;
            public int Calls;
            public Vector3 From;
            public Vector3 Direction;

            public bool TryCatch(Vector3 from, Vector3 direction, float range, LayerMask layers, Transform holder,
                out Transform frame, out Vector3 point)
            {
                Calls++;
                From = from;
                Direction = direction;
                frame = Frame!;
                point = Point;
                return Frame != null;
            }
        }

        private sealed class FakeLineState : IGrappleLineState
        {
            public System.Guid Id { get; } = System.Guid.NewGuid();
            public bool IsSimulating => true;
            public List<List<GrappleLineSnapshot>> Published { get; } = new();

            public void ServerSetLines(IReadOnlyList<GrappleLineSnapshot> lines) => Published.Add(new List<GrappleLineSnapshot>(lines));

            public bool TryGetHookPoint(IActor player, out Vector3 worldPoint)
            {
                worldPoint = default;
                return false;
            }
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            private readonly List<object> _events = new();
            public int Total => _events.Count;
            public int Count<T>() => _events.FindAll(e => e is T).Count;
            public void Publish<TEvent>(TEvent evt) => _events.Add(evt!);
        }
    }
}
