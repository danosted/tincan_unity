#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Targeting;
using VContainer;

namespace TinCan.Core.Interaction
{
    /// <summary>What interacting means: which input bit is the Interact key, and how its target is acquired.</summary>
    public sealed class InteractionTargetingSettings
    {
        public InteractionTargetingSettings(GameplayInput input, TargetingDefinition targeting)
        {
            Input = input;
            Targeting = targeting;
        }

        public GameplayInput Input { get; }
        public TargetingDefinition Targeting { get; }
    }

    /// <summary>
    /// Server: a feature that gives Interact another meaning for some players (occupying a station: Interact leaves it).
    /// Asked first on every press; returning true consumes the press before any target is acquired.
    /// </summary>
    public interface IInteractOverride
    {
        bool TryHandleInteract(IHumanoidCharacterView player);
    }

    /// <summary>Server: a player pressed Interact and the server acquired this target for them.</summary>
    public readonly struct InteractionPerformedEvent
    {
        public readonly Guid RequesterId;
        public readonly string Target;

        public InteractionPerformedEvent(Guid requesterId, string target)
        {
            RequesterId = requesterId;
            Target = target;
        }
    }

    /// <summary>
    /// Application Layer, server only, after humanoid movement. Interact is a predicted input bit: on the tick a player's
    /// simulated input first has it pressed, this acquires the target with <see cref="InteractionTargetingSettings.Targeting"/>
    /// (TD_Interact) from that tick's pose and hands it to the orchestrator. The owner's prompt ran the same query on
    /// the same input, so both agree without the client ever naming a target (the old RPC trusted the client's choice).
    /// </summary>
    public class InteractInputUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Interaction";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITargetingService _targeting;
        private readonly IInteractionOrchestrator _orchestrator;
        private readonly InteractionTargetingSettings _settings;
        private readonly IEventPublisher _events;
        private readonly IInteractOverride? _override; // Null without a feature that overrides Interact (stations)
        private readonly Dictionary<Guid, bool> _wasPressed = new();

        public InteractInputUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITargetingService targeting,
            IInteractionOrchestrator orchestrator,
            InteractionTargetingSettings settings,
            IEventPublisher events,
            IInteractOverride? interactOverride = null)
        {
            _network = network;
            _actors = actors;
            _targeting = targeting;
            _orchestrator = orchestrator;
            _settings = settings;
            _events = events;
            _override = interactOverride;
        }

        [Inject]
        public InteractInputUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITargetingService targeting,
            IInteractionOrchestrator orchestrator,
            InteractionTargetingSettings settings,
            IEventPublisher events,
            IObjectResolver resolver)
            : this(network, actors, targeting, orchestrator, settings, events,
                resolver.TryResolve<IInteractOverride>(out var interactOverride) ? interactOverride : null)
        {
        }

        public void Tick()
        {
            int bit = _settings.Input.BitIndex;
            if (!_network.IsServer || bit < 0) return;

            foreach (var player in _actors.GetActors<IHumanoidCharacterView>())
            {
                bool pressed = (player.InputState.ActiveInputMask & (1UL << bit)) != 0;
                bool wasPressed = _wasPressed.TryGetValue(player.Id, out var previous) && previous;
                _wasPressed[player.Id] = pressed;
                if (!pressed || wasPressed) continue;

                Interact(player);
            }
        }

        private void Interact(IHumanoidCharacterView player)
        {
            if (_override != null && _override.TryHandleInteract(player)) return;

            if (!_targeting.TryAcquire(new HumanoidTargeter(player), _settings.Targeting, out var result) ||
                result.Target is not IInteractionTarget target)
            {
                _events.LogInfo(LogSource, "Interact pressed with nothing in reach.");
                return;
            }

            _events.Publish(new InteractionPerformedEvent(player.Id, Describe(target)));
            _orchestrator.HandleInteraction(player, target);
        }

        private static string Describe(IInteractionTarget target) =>
            target is UnityEngine.Object unityObject && unityObject != null ? unityObject.name : target.GetType().Name;
    }
}
