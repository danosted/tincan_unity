#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// Application Layer, every peer: runs cue handlers.
    /// <list type="bullet">
    /// <item>State cues: each frame it reads every GAS actor's cue tags (the catalog's cues and those of the actor's own
    /// handlers) and turns them into Active / Removed edges (<see cref="GameplayCueStateTracker"/>). A late joiner's
    /// first frame sees cues already active as Active; a despawning actor is forgotten without Removed.</item>
    /// <item>Burst cues: <see cref="Play"/>, from the dispatcher or the network relay, runs Execute.</item>
    /// </list>
    /// Handlers are the components implementing <see cref="IGameplayCueHandler"/> in the actor's own hierarchy (not in a
    /// nested actor's), found once when the actor is first seen.
    /// </summary>
    public sealed class GameplayCueUseCase : ITickable, IInitializable, IDisposable, IGameplayCuePlayer, IGameplayCueFeed
    {
        private static readonly IGameplayCueHandler[] NoHandlers = Array.Empty<IGameplayCueHandler>();

        private readonly IAbilityRegistry _abilities;
        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly GameplayCueCatalog _catalog;
        private readonly GameplayCueStateTracker _tracker;
        private readonly Dictionary<Guid, IGameplayCueHandler[]> _handlers = new();
        private readonly HashSet<Guid> _seenThisTick = new();

        public event Action<GameplayCueEventKind, GameplayCueEvent>? CueHandled;

        public GameplayCueUseCase(IAbilityRegistry abilities, IActorRegistry actors, INetworkService network, GameplayCueCatalog catalog, GameplayCueStateTracker tracker)
        {
            _abilities = abilities;
            _actors = actors;
            _network = network;
            _catalog = catalog;
            _tracker = tracker;
        }

        public void Initialize()
        {
            _actors.OnActorUnregistered += Forget;
            foreach (var problem in _catalog.Problems) Debug.LogWarning($"[GameplayCues] {problem}");
        }

        public void Dispose() => _actors.OnActorUnregistered -= Forget;

        public void Tick()
        {
            // A player registers twice (HumanoidPlayer and its ability mediator share one Id): observe each actor once.
            _seenThisTick.Clear();
            foreach (var controller in _abilities.AllControllers)
            {
                if (!_seenThisTick.Add(controller.Id)) continue;
                var handlers = HandlersOf(controller);
                foreach (var cue in _catalog.Cues) Observe(controller, cue, handlers);
                foreach (var handler in handlers)
                {
                    if (handler.Cue != null && _catalog.For(handler.Cue).Count == 0) Observe(controller, handler.Cue, handlers);
                }
            }
        }

        public void Play(GameplayTag cue, IAbilityControllerBase target) => Handle(GameplayCueEventKind.Execute, cue, target, HandlersOf(target));

        private void Observe(IAbilityControllerBase controller, GameplayTag cue, IGameplayCueHandler[] handlers)
        {
            switch (_tracker.Observe(controller.Id, cue, controller.HasTag(cue)))
            {
                case GameplayCueStateEdge.Active:
                    Handle(GameplayCueEventKind.Active, cue, controller, handlers);
                    break;
                case GameplayCueStateEdge.Removed:
                    Handle(GameplayCueEventKind.Removed, cue, controller, handlers);
                    break;
            }
        }

        private void Handle(GameplayCueEventKind kind, GameplayTag cue, IAbilityControllerBase controller, IGameplayCueHandler[] handlers)
        {
            var cueEvent = new GameplayCueEvent(cue, TransformOf(controller), controller, RoleFor(controller));

            foreach (var handler in handlers)
            {
                if (handler.Cue == cue) Run(handler, kind, cueEvent);
            }

            CueHandled?.Invoke(kind, cueEvent);
        }

        // Presentation must never stop the frame: one broken handler is logged and the others still run.
        private static void Run(IGameplayCueHandler handler, GameplayCueEventKind kind, in GameplayCueEvent cueEvent)
        {
            try
            {
                switch (kind)
                {
                    case GameplayCueEventKind.Execute:
                        handler.OnExecute(cueEvent);
                        break;
                    case GameplayCueEventKind.Active:
                        handler.OnActive(cueEvent);
                        break;
                    case GameplayCueEventKind.Removed:
                        handler.OnRemoved(cueEvent);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, handler as UnityEngine.Object);
            }
        }

        private GameplayCuePeerRole RoleFor(IAbilityControllerBase controller) =>
            _network.IsServer || !_network.IsActive ? GameplayCuePeerRole.Server
            : controller is IGameplayCueRelay { IsOwnedLocally: true } ? GameplayCuePeerRole.Owner
            : GameplayCuePeerRole.Proxy;

        private IGameplayCueHandler[] HandlersOf(IAbilityControllerBase controller)
        {
            if (_handlers.TryGetValue(controller.Id, out var cached)) return cached;

            var root = ActorRoot(controller);
            var found = root == null
                ? NoHandlers
                : root.GetComponentsInChildren<IGameplayCueHandler>(includeInactive: true)
                    .Where(handler => handler is Component component && component.GetComponentInParent<IAbilityControllerBase>(true)?.Id == controller.Id)
                    .ToArray();
            _handlers[controller.Id] = found;
            return found;
        }

        // The highest transform that still belongs to this actor: a player's mediator may sit below the component that
        // shares its Id, and handlers anywhere under the actor count.
        private static Transform? ActorRoot(IAbilityControllerBase controller)
        {
            if (controller is not Component component || component == null) return null;

            var root = component.transform;
            while (root.parent != null && root.parent.GetComponentInParent<IAbilityControllerBase>(true)?.Id == controller.Id)
            {
                root = root.parent;
            }
            return root;
        }

        private static Transform? TransformOf(IAbilityControllerBase controller) =>
            controller is Component component && component != null ? component.transform : null;

        private void Forget(IActor actor)
        {
            _tracker.Forget(actor.Id);
            _handlers.Remove(actor.Id);
        }
    }
}
