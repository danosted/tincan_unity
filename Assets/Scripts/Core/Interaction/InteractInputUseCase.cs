#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Inputs;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Core.Targeting;
using UnityEngine;
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

        /// <summary>Interact considers only interaction targets: the server's press and the owner's prompt use the same filter.</summary>
        public static readonly System.Func<TinCan.Core.Domain.Targeting.ITargetable, bool> OnlyInteractable = target => target is IInteractionTarget;
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
    /// An interaction with <see cref="InteractionDefinition.HoldSeconds"/> above 0 is work: the press starts it, and it
    /// is done only after Interact has been held that long with the same target still acquired every tick; letting go,
    /// looking away or walking out of reach cancels it.
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
        private readonly ITimeService _time;
        private readonly IInteractOverride? _override; // Null without a feature that overrides Interact (stations)
        private readonly Dictionary<Guid, bool> _wasPressed = new();
        private readonly Dictionary<Guid, (IInteractionTarget Target, float Held)> _working = new();

        public InteractInputUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITargetingService targeting,
            IInteractionOrchestrator orchestrator,
            InteractionTargetingSettings settings,
            IEventPublisher events,
            ITimeService time,
            IInteractOverride? interactOverride = null)
        {
            _network = network;
            _actors = actors;
            _targeting = targeting;
            _orchestrator = orchestrator;
            _settings = settings;
            _events = events;
            _time = time;
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
            ITimeService time,
            IObjectResolver resolver)
            : this(network, actors, targeting, orchestrator, settings, events, time,
                resolver.TryResolve<IInteractOverride>(out var interactOverride) ? interactOverride : null)
        {
        }

        /// <summary>Server: how far a player's held work is, in [0, 1); false when they are not working on anything.</summary>
        public bool TryGetWork(Guid playerId, out IInteractionTarget target, out float progress)
        {
            target = null!;
            progress = 0f;
            if (!_working.TryGetValue(playerId, out var work)) return false;

            target = work.Target;
            progress = work.Held / Mathf.Max(work.Target.Definition.HoldSeconds, 1e-4f);
            return true;
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

                if (!pressed)
                {
                    if (_working.Remove(player.Id)) _events.LogInfo(LogSource, "Work stopped: Interact let go.");
                    continue;
                }

                if (!wasPressed) Interact(player);
                else if (_working.TryGetValue(player.Id, out var work)) KeepWorking(player, work.Target, work.Held);
            }
        }

        private void Interact(IHumanoidCharacterView player)
        {
            if (_override != null && _override.TryHandleInteract(player)) return;

            if (!TryAcquire(player, out var target))
            {
                _events.LogInfo(LogSource, "Interact pressed with nothing in reach.");
                return;
            }

            if (target.Definition != null && target.Definition.HoldSeconds > 0f)
            {
                _working[player.Id] = (target, 0f);
                return;
            }

            Perform(player, target);
        }

        // One more tick of held work: the same target must still be acquired, or the work stops.
        private void KeepWorking(IHumanoidCharacterView player, IInteractionTarget target, float held)
        {
            if (!TryAcquire(player, out var current) || !ReferenceEquals(current, target))
            {
                _working.Remove(player.Id);
                _events.LogInfo(LogSource, "Work stopped: the target is out of reach or out of sight.");
                return;
            }

            held += _time.DeltaTime;
            if (held + 1e-4f < target.Definition.HoldSeconds)
            {
                _working[player.Id] = (target, held);
                return;
            }

            _working.Remove(player.Id);
            Perform(player, target);
        }

        private bool TryAcquire(IHumanoidCharacterView player, out IInteractionTarget target)
        {
            target = null!;
            if (!_targeting.TryAcquire(new HumanoidTargeter(player), _settings.Targeting, InteractionTargetingSettings.OnlyInteractable, out var result) ||
                result.Target is not IInteractionTarget acquired)
            {
                return false;
            }

            target = acquired;
            return true;
        }

        private void Perform(IHumanoidCharacterView player, IInteractionTarget target)
        {
            _events.Publish(new InteractionPerformedEvent(player.Id, Describe(target)));
            _orchestrator.HandleInteraction(player, target);
        }

        private static string Describe(IInteractionTarget target) =>
            target is UnityEngine.Object unityObject && unityObject != null ? unityObject.name : target.GetType().Name;
    }
}
