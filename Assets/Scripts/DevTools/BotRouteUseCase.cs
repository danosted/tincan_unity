#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Airship;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Possession;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Plays the <c>-bot</c> route through <see cref="IScriptedInput"/>, so gameplay reads it exactly like a keyboard.
    /// The clock starts once the local player exists. Ship commands take or release the helm through the server's
    /// possession authority, so they only work on the host.
    /// </summary>
    public sealed class BotRouteUseCase : ITickable
    {
        private const string LogSource = "NetHarness";

        private readonly HarnessOptions _options;
        private readonly HarnessSession _session;
        private readonly IScriptedInput _input;
        private readonly IActorRegistry _registry;
        private readonly INetworkService _network;
        private readonly IPossessionAuthority _possession;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly List<BotStep> _entered = new();
        private readonly HashSet<string> _held = new();

        private BotRoute? _route;
        private BotRouteCursor? _cursor;
        private IHumanoidCharacterView? _player;
        private float _elapsed;
        private bool _hasHelm;

        public BotRouteUseCase(
            HarnessOptions options,
            HarnessSession session,
            IScriptedInput input,
            IActorRegistry registry,
            INetworkService network,
            IPossessionAuthority possession,
            ITimeService time,
            IEventPublisher events)
        {
            _options = options;
            _session = session;
            _input = input;
            _registry = registry;
            _network = network;
            _possession = possession;
            _time = time;
            _events = events;
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
                    var airship = _registry.GetActors<IAirshipView>().FirstOrDefault();
                    _hasHelm = airship != null && _possession.TryAcquirePossession(_player.Id, airship);
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

            _possession.TryReleasePossession(_player.Id);
            _hasHelm = false;
            _events.LogInfo(LogSource, "Bot released the helm.");
        }

        private void Finish()
        {
            foreach (var action in _held) _input.Release(action);
            _held.Clear();
            ReleaseHelm();

            _events.LogInfo(LogSource, $"Bot route '{_route?.Name}' complete.");
            _session.CompleteRoute();
        }
    }
}
