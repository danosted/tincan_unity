#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Networking;

namespace TinCan.Tests.Fakes
{
    /// <summary>LAN discovery whose games the test sets; records whether something browses.</summary>
    public sealed class FakeLanDiscovery : ILanDiscovery
    {
        private readonly List<DiscoveredGame> _games = new();

        public IReadOnlyList<DiscoveredGame> Games => _games;
        public bool Browsing { get; private set; }
        public event Action? Changed;

        public void Browse(bool browsing) => Browsing = browsing;

        public void SetGames(params DiscoveredGame[] games)
        {
            _games.Clear();
            _games.AddRange(games);
            Changed?.Invoke();
        }
    }
}
