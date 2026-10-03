#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Features.Helm;
using TinCan.Features.Stations;
using VContainer;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Plays the <c>-bot</c> route through scripted input (<see cref="ScriptedActionDriver"/>), so gameplay reads it exactly like a keyboard.
    /// The clock starts once the local player exists. Ship commands take or leave the helm station through the server's
    /// station occupancy, so they only work on the host (and only with the Helm and Stations features loaded).
    /// </summary>
    public sealed class BotRouteUseCase : ITickable
    {
        private const string LogSource = "NetHarness";

        private readonly HarnessOptions _options;
        private readonly HarnessSession _session;
        private readonly ScriptedActionDriver _input;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;
        private readonly IObjectResolver? _resolver;
        private IStationOccupancy? _occupancy;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly List<BotStep> _entered = new();
        private readonly HashSet<ScriptedAction> _held = new();

        private BotRoute? _route;
        private BotRouteCursor? _cursor;
        private IHumanoidCharacterView? _player;
        private float _elapsed;
        private bool _hasHelm;
        private int _laps;

        public BotRouteUseCase(
            HarnessOptions options,
            HarnessSession session,
            ScriptedActionDriver input,
            IActorRegistry registry,
            INetworkService network,
            IStationOccupancy? occupancy,
            ITimeService time,
            IEventPublisher events)
        {
            _options = options;
            _session = session;
            _input = input;
            _registry = registry;
            _network = network;
            _occupancy = occupancy;
            _time = time;
            _events = events;
        }

        // Stations is a feature a profile may leave out, so occupancy is looked up optionally, on first use.
        [Inject]
        public BotRouteUseCase(
            HarnessOptions options,
            HarnessSession session,
            ScriptedActionDriver input,
            IActorRegistry registry,
            INetworkService network,
            IObjectResolver resolver,
            ITimeService time,
            IEventPublisher events)
            : this(options, session, input, registry, network, (IStationOccupancy?)null, time, events)
        {
            _resolver = resolver;
        }

        private IStationOccupancy? Occupancy()
        {
            if (_occupancy == null && _resolver != null && _resolver.TryResolve<IStationOccupancy>(out var occupancy)) _occupancy = occupancy;
            return _occupancy;
        }

        public void Tick()
        {
            if (_options.BotRoute == null || _session.IsRouteComplete) return;

            if (_cursor == null && !TryStart()) return;

            _elapsed += _time.DeltaTime;
            foreach (var step in _cursor!.Advance(_elapsed, _entered))
            {
                Enter(step);
            }

            if (_cursor.IsComplete) Finish();
        }

        private bool TryStart()
        {
            if (!_network.IsActive) return false;

            _player = _registry.GetLocalPlayerActor<IHumanoidCharacterView>();
            if (_player == null) return false;

            if (!BotRoutes.TryGet(_options.BotRoute, out var route))
            {
                _events.LogWarning(LogSource, $"Unknown bot route '{_options.BotRoute}'. Known: {BotRoutes.Names}. Running Idle.");
            }

            _route = route;
            _cursor = new BotRouteCursor(route);
            _elapsed = 0f;
            _session.StartRoute(route.Name);
            _events.LogInfo(LogSource, $"Bot route '{route.Name}' started ({route.TotalDuration:0.#} s).");
            return true;
        }

        private void Enter(BotStep step)
        {
            _session.EnterStep(step.Label);

            foreach (var action in _held.Where(action => !step.Held.Contains(action)).ToList())
            {
                _input.Release(action);
                _held.Remove(action);
            }

            foreach (var action in step.Held)
            {
                if (_held.Add(action)) _input.Press(action);
            }

            foreach (var action in step.Taps)
            {
                _input.Tap(action);
            }

            RunShipCommand(step.Ship);
        }

        private void RunShipCommand(BotShipCommand command)
        {
            if (command == BotShipCommand.None || _player == null) return;

            if (!_network.IsServer)
            {
                _events.LogWarning(LogSource, $"Ship command {command} skipped: only the host can take the helm.");
                return;
            }

            switch (command)
            {
                case BotShipCommand.Take:
                    var helm = _registry.GetActors<IHelm>().OfType<IStation>().FirstOrDefault();
                    _hasHelm = helm != null && Occupancy()?.TryOccupy(_player, helm) == true;
                    _events.LogInfo(LogSource, _hasHelm ? "Bot took the helm." : "Bot could not take the helm.");
                    break;
                case BotShipCommand.Release:
                    ReleaseHelm();
                    break;
            }
        }

        private void ReleaseHelm()
        {
            if (!_hasHelm || _player == null) return;

            Occupancy()?.Leave(_player.Id);
            _hasHelm = false;
            _events.LogInfo(LogSource, "Bot released the helm.");
        }

        private void Finish()
        {
            foreach (var action in _held) _input.Release(action);
            _held.Clear();
            ReleaseHelm();

            if (_options.BotLoop)
            {
                // -botloop: start over on the next tick, so load lasts as long as the run; the route never completes.
                _laps++;
                _cursor = null;
                _events.LogInfo(LogSource, $"Bot route '{_route?.Name}' lap {_laps} done; looping.");
                return;
            }

            _events.LogInfo(LogSource, $"Bot route '{_route?.Name}' complete.");
            _session.CompleteRoute();
        }
    }
}
