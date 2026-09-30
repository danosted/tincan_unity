#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Starts a session from command-line arguments so builds can be used as unattended test clients or servers:
    /// <c>-autohost</c>, <c>-server [address][:port]</c> (a dedicated server with no local player, listening on
    /// <c>0.0.0.0:7777</c> unless told otherwise) or <c>-autojoin [address[:port]]</c>, optionally with
    /// <c>-joindelay &lt;seconds&gt;</c> so a client joins a session that is already running (late-join tests). In the
    /// Editor the same flags can come from
    /// Multiplayer Play Mode player tags (see <see cref="LaunchArguments"/>). Runs after the menu bootstrap; the menu
    /// closes itself once the session is up.
    /// </summary>
    public class CommandLineSessionBootstrap : IStartable, ITickable
    {
        public const string HostFlag = "-autohost";
        public const string ServerFlag = "-server";
        public const string DefaultListenAddress = "0.0.0.0";
        public const string JoinFlag = "-autojoin";
        public const string JoinDelayFlag = "-joindelay";

        private readonly INetworkService _networkService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ITimeService _time;

        private SessionRequest? _pendingJoin;
        private float _joinDelayRemaining;

        [Inject]
        public CommandLineSessionBootstrap(INetworkService networkService, IEventPublisher eventPublisher, ITimeService time)
        {
            _networkService = networkService;
            _eventPublisher = eventPublisher;
            _time = time;
        }

        public void Start() => Begin(LaunchArguments.Current);

        /// <summary>Acts on <paramref name="args"/>: hosts or joins now, or schedules a delayed join for <see cref="Tick"/>.</summary>
        public void Begin(IReadOnlyList<string>? args)
        {
            if (!TryParse(args, out var request) || _networkService.IsActive) return;

            switch (request.Kind)
            {
                case SessionRequestKind.Host:
                    _eventPublisher.LogInfo("Session", "Auto-hosting from command line.");
                    _networkService.StartHost();
                    break;
                case SessionRequestKind.Server:
                    _eventPublisher.LogInfo("Session", $"Starting a dedicated server on {request.Address}:{request.Port} from command line.");
                    _networkService.SetListenEndpoint(request.Address, request.Port);
                    _networkService.StartServer();
                    break;
                case SessionRequestKind.Join when request.DelaySeconds > 0f:
                    _eventPublisher.LogInfo("Session", $"Auto-joining {request.Address}:{request.Port} in {request.DelaySeconds:0.#} s (late join).");
                    _pendingJoin = request;
                    _joinDelayRemaining = request.DelaySeconds;
                    break;
                case SessionRequestKind.Join:
                    Join(request);
                    break;
            }
        }

        public void Tick()
        {
            if (_pendingJoin is not { } request) return;

            _joinDelayRemaining -= _time.DeltaTime;
            if (_joinDelayRemaining > 0f) return;

            _pendingJoin = null;
            if (!_networkService.IsActive) Join(request);
        }

        private void Join(SessionRequest request)
        {
            _eventPublisher.LogInfo("Session", $"Auto-joining {request.Address}:{request.Port} from command line.");
            _networkService.SetConnection(request.Address, request.Port);
            _networkService.StartClient();
        }

        public static bool TryParse(IReadOnlyList<string>? args, out SessionRequest request)
        {
            request = new SessionRequest(SessionRequestKind.None, string.Empty, 0);
            if (args == null) return false;

            for (int i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], HostFlag, StringComparison.OrdinalIgnoreCase))
                {
                    request = new SessionRequest(SessionRequestKind.Host, string.Empty, 0);
                    return true;
                }

                if (string.Equals(args[i], ServerFlag, StringComparison.OrdinalIgnoreCase))
                {
                    string listen = EndpointAfter(args, i);
                    request = new SessionRequest(SessionRequestKind.Server, ParseAddress(listen, DefaultListenAddress), ParsePort(listen));
                    return true;
                }

                if (!string.Equals(args[i], JoinFlag, StringComparison.OrdinalIgnoreCase)) continue;

                string endpoint = EndpointAfter(args, i);
                request = new SessionRequest(SessionRequestKind.Join, ParseAddress(endpoint, JoinGameMenuCommandDefaults.Address), ParsePort(endpoint), ParseDelay(args));
                return true;
            }

            return false;
        }

        private static string EndpointAfter(IReadOnlyList<string> args, int flag) =>
            flag + 1 < args.Count && !args[flag + 1].StartsWith("-") ? args[flag + 1] : string.Empty;

        private static float ParseDelay(IReadOnlyList<string> args) =>
            LaunchArguments.TryGetValue(args, JoinDelayFlag, out var value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds > 0f
                ? seconds
                : 0f;

        /// <summary>The address part of <c>address[:port]</c>; <paramref name="fallback"/> when there is none (<c>""</c>, <c>:9000</c>).</summary>
        private static string ParseAddress(string endpoint, string fallback)
        {
            int colon = endpoint.LastIndexOf(':');
            string address = colon >= 0 ? endpoint.Substring(0, colon) : endpoint;
            return string.IsNullOrWhiteSpace(address) ? fallback : address;
        }

        private static ushort ParsePort(string endpoint)
        {
            int colon = endpoint.LastIndexOf(':');
            if (colon < 0 || !ushort.TryParse(endpoint.Substring(colon + 1), out var port) || port == 0) return JoinGameMenuCommandDefaults.Port;
            return port;
        }

        private static class JoinGameMenuCommandDefaults
        {
            public const string Address = Commands.JoinGameMenuCommand.DefaultAddress;
            public const ushort Port = Commands.JoinGameMenuCommand.DefaultPort;
        }
    }
}
