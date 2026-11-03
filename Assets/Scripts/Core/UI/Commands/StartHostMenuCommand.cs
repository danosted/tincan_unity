#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;

namespace TinCan.Core.UI.Commands
{
    public class StartHostMenuCommand : IMenuCommand, IDisposable
    {
        public const string Id = "StartHost";

        private readonly ISessionService _session;
        private readonly IEventPublisher _events;
        private readonly CancellationTokenSource _lifetime = new();

        public StartHostMenuCommand(ISessionService session, IEventPublisher events)
        {
            _session = session;
            _events = events;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
            if (_session.IsActive) return;
            HostAsync().Forget();
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async UniTaskVoid HostAsync()
        {
            var start = await _session.HostAsync(HostOptions.Default, _lifetime.Token);
            if (!start.IsStarted) _events.LogWarning("Session", start.Message);
        }
    }
}
