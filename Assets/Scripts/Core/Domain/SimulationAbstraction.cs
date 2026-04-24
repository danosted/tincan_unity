using System;
using VContainer.Unity;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: Interface for any actor that is simulated locally based on a networked input state.
    /// </summary>
    /// <typeparam name="TInput">The type of input state used for simulation.</typeparam>
    public interface ISimulatedActor<TInput> : IActor
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
                if (!actor.IsSimulating)
                {
                    continue;
                }

                HandleGenericSimulation(actor);
            }
        }

        protected void HandleGenericSimulation(TView actor)
        {
            bool isCaptured = false;
            if (actor is IPossessable possessable)
            {
                isCaptured = possessable.IsCapturedBy(NetworkService.LocalClientId);
            }

            if (isCaptured)
            {
                // 1. Gather local input
                actor.InputState = GatherLocalInput(actor);
            }
            // 2. If not captured, InputState is assumed to be synced via the Network Mediator

            // 3. Process Domain Logic (Simulate)
            ProcessSimulation(actor, actor.InputState, isCaptured);
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
