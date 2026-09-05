#nullable enable
using System;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using VContainer.Unity;

namespace TinCan.Features.UI
{
    public enum SessionRequestKind
    {
        None,
        Host,
        Join
    }

    public readonly struct SessionRequest
    {
        public readonly SessionRequestKind Kind;
        public readonly string Address;
        public readonly ushort Port;

        public SessionRequest(SessionRequestKind kind, string address, ushort port)
        {
            Kind = kind;
            Address = address;
            Port = port;
        }
    }

    /// <summary>
    /// Starts a session from command-line arguments so builds can be used as unattended test clients:
    /// <c>-autohost</c> or <c>-autojoin [address[:port]]</c>. Runs after the menu bootstrap; the menu closes itself
    /// once the session is up.
    /// </summary>
    public class CommandLineSessionBootstrap : IStartable
    {
        public const string HostFlag = "-autohost";
        public const string JoinFlag = "-autojoin";

        private readonly INetworkService _networkService;
        private readonly IEventPublisher _eventPublisher;

        public CommandLineSessionBootstrap(INetworkService networkService, IEventPublisher eventPublisher)
        {
            _networkService = networkService;
            _eventPublisher = eventPublisher;
        }

        public void Start()
        {
            if (!TryParse(System.Environment.GetCommandLineArgs(), out var request) || _networkService.IsActive) return;

            switch (request.Kind)
            {
                case SessionRequestKind.Host:
                    _eventPublisher.LogInfo("Session", "Auto-hosting from command line.");
                    _networkService.StartHost();
                    break;
                case SessionRequestKind.Join:
                    _eventPublisher.LogInfo("Session", $"Auto-joining {request.Address}:{request.Port} from command line.");
                    _networkService.SetConnection(request.Address, request.Port);
                    _networkService.StartClient();
                    break;
            }
        }

        public static bool TryParse(string[] args, out SessionRequest request)
        {
            request = new SessionRequest(SessionRequestKind.None, string.Empty, 0);
            if (args == null) return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], HostFlag, StringComparison.OrdinalIgnoreCase))
                {
                    request = new SessionRequest(SessionRequestKind.Host, string.Empty, 0);
                    return true;
                }

                if (!string.Equals(args[i], JoinFlag, StringComparison.OrdinalIgnoreCase)) continue;

                string endpoint = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : string.Empty;
                request = new SessionRequest(SessionRequestKind.Join, ParseAddress(endpoint), ParsePort(endpoint));
                return true;
            }

            return false;
        }

        private static string ParseAddress(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return JoinGameMenuCommandDefaults.Address;
            int colon = endpoint.LastIndexOf(':');
            return colon > 0 ? endpoint.Substring(0, colon) : endpoint;
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
