#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Networking;
using TinCan.Core.UI.Commands;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The Join menu's games on this network: one row per game discovery found ("Ada's game  2/8"), browsing only
    /// while the list is on screen. Picking a game joins it like Connect does. A game of another version or a full one
    /// shows, but cannot be picked.
    /// </summary>
    public sealed class LanGamesRowSource : IMenuRowSource, IDisposable
    {
        public const string Id = "lan-games";
        public const string LookingLabel = "Looking for games on this network...";

        private readonly ILanDiscovery _discovery;
        private readonly JoinGameMenuCommand _join;
        private readonly List<MenuSourceRow> _rows = new();
        private readonly Dictionary<string, JoinTarget> _targets = new();

        public LanGamesRowSource(ILanDiscovery discovery, JoinGameMenuCommand join)
        {
            _discovery = discovery;
            _join = join;
            _discovery.Changed += OnDiscoveryChanged;
        }

        public string SourceId => Id;
        public IReadOnlyList<MenuSourceRow> Rows => _rows;
        public string EmptyLabel => LookingLabel;
        public event Action? Changed;

        public static string Label(DiscoveredGame game)
        {
            string text = $"{game.Name}  {game.Players}/{game.MaxPlayers}";
            if (!game.SameVersion) return $"{text}  (version {game.Version})";
            return game.IsFull ? $"{text}  (full)" : text;
        }

        public void SetShown(bool shown) => _discovery.Browse(shown);

        public void Invoke(string key, MenuContext context)
        {
            if (_targets.TryGetValue(key, out var target)) _join.Join(context.Menus, target);
        }

        public void Dispose() => _discovery.Changed -= OnDiscoveryChanged;

        private void OnDiscoveryChanged()
        {
            _rows.Clear();
            _targets.Clear();
            foreach (var game in _discovery.Games)
            {
                _rows.Add(new MenuSourceRow(game.Key, Label(game), game.SameVersion && !game.IsFull));
                _targets[game.Key] = game.Target;
            }
            Changed?.Invoke();
        }
    }
}
