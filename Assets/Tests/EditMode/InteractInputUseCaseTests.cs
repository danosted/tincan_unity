#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Targeting;
using TinCan.Features.Abilities.Inputs;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Interaction;
using TinCan.Features.Targeting;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
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
        }

        private sealed class RecordingOrchestrator : IInteractionOrchestrator
        {
            public List<(IActor Requester, IInteractionTarget Target)> Interactions { get; } = new();
            public void HandleInteraction(InteractionRequest request) { }
            public void HandleInteraction(IActor requester, IInteractionTarget target) => Interactions.Add((requester, target));
            public void HandleExit() { }
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
            _useCase = new InteractInputUseCase(new FakeNetworkService(), _actors, _targeting, _orchestrator,
                new InteractionTargetingSettings(_input, _definition), new FakeEventPublisher());
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            Object.DestroyImmediate(_input);
            Object.DestroyImmediate(_definition);
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
