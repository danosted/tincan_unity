using System;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class SimulationUseCaseTests
    {
        private class TestActor : ISimulatedActor<int>
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; } = true;
            public int InputState { get; set; }
        }

        private class TestPossessableActor : TestActor, IPossessable
        {
            public ulong? PossessorId { get; private set; }
            public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
            public bool CanPossess(ulong playerId) => true;
        }

        private class TestSimulationUseCase : SimulationUseCase<ISimulatedActor<int>, int>
        {
            public int GatherLocalInputCallCount;
            public int ProcessSimulationCallCount;
            public bool LastIsCaptured;

            public TestSimulationUseCase(IInputService inputService, INetworkService networkService, IActorRegistry registry, ITimeService timeService)
                : base(inputService, networkService, registry, timeService) { }

            protected override int GatherLocalInput(ISimulatedActor<int> actor)
            {
                GatherLocalInputCallCount++;
                return 42;
            }

            protected override void ProcessSimulation(ISimulatedActor<int> actor, int input, bool isCaptured)
            {
                ProcessSimulationCallCount++;
                LastIsCaptured = isCaptured;
            }
        }

        private FakeActorRegistry _registry;
        private FakeNetworkService _networkService;
        private TestSimulationUseCase _useCase;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _networkService = new FakeNetworkService { LocalClientId = 5 };
            _useCase = new TestSimulationUseCase(new FakeInputService(), _networkService, _registry, new FakeTimeService());
        }

        [Test]
        public void NonPossessableActor_IsNeverCapturedOrGatheredFrom()
        {
            var actor = new TestActor();
            _registry.Register(actor);

            _useCase.Tick();

            Assert.That(_useCase.GatherLocalInputCallCount, Is.EqualTo(0));
            Assert.That(_useCase.ProcessSimulationCallCount, Is.EqualTo(1));
            Assert.That(_useCase.LastIsCaptured, Is.False);
        }

        [Test]
        public void PossessableActor_CapturedByLocalClient_GathersInputAndProcessesAsCaptured()
        {
            var actor = new TestPossessableActor();
            actor.AuthoritativeSetPossessor(_networkService.LocalClientId);
            _registry.Register(actor);

            _useCase.Tick();

            Assert.That(_useCase.GatherLocalInputCallCount, Is.EqualTo(1));
            Assert.That(actor.InputState, Is.EqualTo(42));
            Assert.That(_useCase.ProcessSimulationCallCount, Is.EqualTo(1));
            Assert.That(_useCase.LastIsCaptured, Is.True);
        }

        [Test]
        public void PossessableActor_CapturedByRemoteClient_DoesNotGatherLocalInput()
        {
            var actor = new TestPossessableActor();
            actor.AuthoritativeSetPossessor(999);
            _registry.Register(actor);

            _useCase.Tick();

            Assert.That(_useCase.GatherLocalInputCallCount, Is.EqualTo(0));
            Assert.That(_useCase.ProcessSimulationCallCount, Is.EqualTo(1));
            Assert.That(_useCase.LastIsCaptured, Is.False);
        }

        [Test]
        public void NonSimulatingActor_IsSkippedEntirely()
        {
            var actor = new TestActor { IsSimulating = false };
            _registry.Register(actor);

            _useCase.Tick();

            Assert.That(_useCase.ProcessSimulationCallCount, Is.EqualTo(0));
        }
    }
}
