using System;
using VContainer.Unity;
using TinCan.Core.Domain.Networking;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Marker interface for any actor that is simulated locally.
    /// Used by systems like GAS to skip global ticks for actors that are explicitly ticked by a movement simulation.
    /// </summary>
    public interface ISimulatedActor : IActor { }

    /// <summary>
    /// Domain Layer: Interface for any actor that is simulated locally based on a networked input state.
    /// </summary>
    /// <typeparam name="TInput">The type of input state used for simulation.</typeparam>
    public interface ISimulatedActor<TInput> : ISimulatedActor
    {
        /// <summary>
        /// The current input state (either gathered locally or received from the network).
        /// </summary>
        TInput InputState { get; set; }
    }

    /// <summary>
    /// Domain/Application Layer: Generic base class for systems that simulate actors based on networked input.
    /// Handles the loop of identifying actors, gathering input, and triggering simulation.
    /// </summary>
    public abstract class SimulationUseCase<TView, TInput> : ITickable
        where TView : ISimulatedActor<TInput>
    {
        protected readonly IInputService InputService;
        protected readonly INetworkService NetworkService;
        protected readonly IActorRegistry Registry;
        protected readonly ITimeService TimeService;

        protected SimulationUseCase(
            IInputService inputService,
            INetworkService networkService,
            IActorRegistry registry,
            ITimeService timeService)
        {
            InputService = inputService;
            NetworkService = networkService;
            Registry = registry;
            TimeService = timeService;
        }

        public virtual void Tick()
        {
            var actors = Registry.GetActors<TView>();

            foreach (var actor in actors)
            {
                bool isCaptured = IsCapturedByLocalClient(actor);

                if (isCaptured)
                {
                    actor.InputState = GatherLocalInput(actor);
                }

                if (!actor.IsSimulating)
                {
                    continue;
                }

                ProcessSimulation(actor, actor.InputState, isCaptured);
            }
        }

        private bool IsCapturedByLocalClient(TView actor)
        {
            if (actor is IPossessable possessable)
            {
                return possessable.IsCapturedBy(NetworkService.LocalClientId);
            }

            return false;
        }

        /// <summary>
        /// Gather input from the local InputService for the captured actor.
        /// </summary>
        protected abstract TInput GatherLocalInput(TView actor);

        /// <summary>
        /// Perform the actual movement/logic calculation for the actor.
        /// </summary>
        protected abstract void ProcessSimulation(TView actor, TInput input, bool isCaptured);
    }
}
