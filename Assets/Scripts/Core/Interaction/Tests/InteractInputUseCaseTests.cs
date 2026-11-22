#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Gas.Inputs;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Targeting;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Core.Interaction.Tests
{
    public class InteractInputUseCaseTests
    {
        private sealed class FakeTarget : IInteractionTarget
        {
            public InteractionDefinition Definition => null!;
        }

        private sealed class FakeTargeting : ITargetingService
        {
            public ITargetable? Answer { get; set; }
            public int Queries { get; private set; }

            public bool TryAcquire(ITargeter targeter, TargetingDefinition definition, out TargetResult result)
            {
                Queries++;
                result = Answer != null ? new TargetResult(Answer, 1f, 0f) : default;
                return Answer != null;
            }

            public bool TryAcquireSegment(Vector3 from, Vector3 to, TargetingDefinition definition, System.Func<Collider, bool>? ignore, out SegmentHit hit)
            {
                hit = default;
                return false;
            }
        }

        private sealed class RecordingOrchestrator : IInteractionOrchestrator
        {
            public List<(IActor Requester, IInteractionTarget Target)> Interactions { get; } = new();
            public void HandleInteraction(InteractionRequest request) { }
            public void HandleInteraction(IActor requester, IInteractionTarget target) => Interactions.Add((requester, target));
        }

        private InteractInput _input = null!;
        private TargetingDefinition _definition = null!;
        private FakeTargeting _targeting = null!;
        private RecordingOrchestrator _orchestrator = null!;
        private FakeActorRegistry _actors = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeNetHumanoidView _player = null!;
        private FakeTarget _rack = null!;
        private InteractInputUseCase _useCase = null!;
        private FakeTimeService _time = null!;

        private sealed class WorkTarget : IInteractionTarget
        {
            public WorkTarget(InteractionDefinition definition) => Definition = definition;
            public InteractionDefinition Definition { get; }
        }

        /// <summary>A target whose interaction takes <paramref name="seconds"/> of held work.</summary>
        private WorkTarget Work(float seconds)
        {
            var definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            definition.HoldSeconds = seconds;
            _created.Add(definition);
            return new WorkTarget(definition);
        }

        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _input = ScriptableObject.CreateInstance<InteractInput>();
            _input.BitIndex = 3;
            _definition = ScriptableObject.CreateInstance<TargetingDefinition>();
            _rack = new FakeTarget();
            _targeting = new FakeTargeting { Answer = _rack };
            _orchestrator = new RecordingOrchestrator();
            _actors = new FakeActorRegistry();
            _movement = new FakeHumanoidMovementView("Player");
            _player = new FakeNetHumanoidView(_movement);
            _actors.Register(_player);
            _time = new FakeTimeService { DeltaTime = 0.1f };
            _useCase = new InteractInputUseCase(new FakeNetworkService(), _actors, _targeting, _orchestrator,
                new InteractionTargetingSettings(_input, _definition), new FakeEventPublisher(), _time);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            Object.DestroyImmediate(_input);
            Object.DestroyImmediate(_definition);
            foreach (var created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void HeldWork_IsDoneOnlyAfterItsTime()
        {
            var vein = Work(0.5f);
            _targeting.Answer = vein;
            Press(true);

            _useCase.Tick(); // the press starts the work
            for (int i = 0; i < 4; i++) _useCase.Tick();
            Assert.That(_orchestrator.Interactions, Is.Empty, "0.4 s of 0.5 s");
            Assert.That(_useCase.TryGetWork(_player.Id, out _, out float progress), Is.True);
            Assert.That(progress, Is.EqualTo(0.8f).Within(1e-3f));

            _useCase.Tick();
            Assert.That(_orchestrator.Interactions, Has.Count.EqualTo(1));
            Assert.That(_orchestrator.Interactions[0].Target, Is.SameAs(vein));
            Assert.That(_useCase.TryGetWork(_player.Id, out _, out _), Is.False);

            for (int i = 0; i < 10; i++) _useCase.Tick();
            Assert.That(_orchestrator.Interactions, Has.Count.EqualTo(1), "still holding: done once, not again");
        }

        [Test]
        public void LettingGo_CancelsTheWork()
        {
            _targeting.Answer = Work(0.5f);
            Press(true);
            _useCase.Tick();
            _useCase.Tick();
            _useCase.Tick();

            Press(false);
            _useCase.Tick();
            Press(true);
            for (int i = 0; i < 3; i++) _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Is.Empty, "the work restarted from zero on the new press");
        }

        [Test]
        public void LookingAway_CancelsTheWork()
        {
            _targeting.Answer = Work(0.3f);
            Press(true);
            _useCase.Tick();
            _useCase.Tick();

            _targeting.Answer = Work(0.3f); // another target
            for (int i = 0; i < 5; i++) _useCase.Tick();
            _targeting.Answer = null;
            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Is.Empty);
            Assert.That(_useCase.TryGetWork(_player.Id, out _, out _), Is.False);
        }

        private void Press(bool pressed) =>
            _player.InputState = new HumanoidInputState { ActiveInputMask = pressed ? 1UL << 3 : 0UL };

        [Test]
        public void Press_InteractsOnceWithTheServersTarget()
        {
            Press(true);
            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Has.Count.EqualTo(1), "Holding the key interacts once, on the press.");
            Assert.That(_orchestrator.Interactions[0].Target, Is.SameAs(_rack));
            Assert.That(_orchestrator.Interactions[0].Requester, Is.SameAs(_player));
        }

        [Test]
        public void ReleaseAndPressAgain_InteractsAgain()
        {
            Press(true);
            _useCase.Tick();
            Press(false);
            _useCase.Tick();
            Press(true);
            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Has.Count.EqualTo(2));
        }

        [Test]
        public void NothingInReach_DoesNothing()
        {
            _targeting.Answer = null;
            Press(true);

            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Is.Empty);
            Assert.That(_targeting.Queries, Is.EqualTo(1));
        }

        [Test]
        public void TargetThatIsNotAnInteraction_IsIgnored()
        {
            _targeting.Answer = new FakeShipDamageTargetable();
            Press(true);

            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Is.Empty);
        }

        [Test]
        public void NotPressed_NeverQueriesTargeting()
        {
            Press(false);

            _useCase.Tick();

            Assert.That(_targeting.Queries, Is.EqualTo(0));
        }

        [Test]
        public void UnassignedBit_DoesNothing()
        {
            _input.BitIndex = -1;
            Press(true);

            _useCase.Tick();

            Assert.That(_orchestrator.Interactions, Is.Empty);
        }

        private sealed class FakeShipDamageTargetable : ITargetable
        {
            public Vector3 AimPoint => Vector3.zero;
            public bool IsTargetable => true;
            public TinCan.Core.Domain.Abilities.IAbilityControllerBase? Controller => null;
        }
    }
}
