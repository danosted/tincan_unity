#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TinCan.Core.Domain.Networking;

namespace TinCan.Core.UI.Commands
{
    /// <summary>
    /// Joins a game at the "address" and "port" values of the menu the command lives in. While it waits, the menu's
    /// "status" note reads "Connecting to ..."; if the host turns it away (another version, a full game) or cannot be
    /// reached, the note says why.
    /// </summary>
    public class JoinGameMenuCommand : IMenuCommand, IDisposable
    {
        public const string Id = "JoinGame";
        public const string AddressItemId = "address";
        public const string PortItemId = "port";
        public const string StatusItemId = "status";
        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        public static string ConnectingMessage(JoinTarget target) => $"Connecting to {target}...";

        private readonly ISessionService _session;
        private readonly CancellationTokenSource _lifetime = new();

        public JoinGameMenuCommand(ISessionService session)
        {
            _session = session;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_session.IsActive) return;

            var address = context.GetValue(AddressItemId);
            if (string.IsNullOrWhiteSpace(address)) address = DefaultAddress;

            if (!ushort.TryParse(context.GetValue(PortItemId), out var port) || port == 0) port = DefaultPort;

            Join(context.Menus, new JoinTarget(address.Trim(), port));
        }

        /// <summary>Joins <paramref name="target"/>, saying so on the menu's status line (also used by the list of LAN games).</summary>
        public void Join(IMenuSystem menus, JoinTarget target)
        {
            if (_session.IsActive) return;

            menus.SetValue(StatusItemId, ConnectingMessage(target));
            JoinAsync(menus, target).Forget();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTaskVoid JoinAsync(IMenuSystem menus, JoinTarget target)
        {
            var start = await _session.JoinAsync(target, _lifetime.Token);
            menus.SetValue(StatusItemId, start.IsStarted ? string.Empty : start.Message);
        }
    }
}
