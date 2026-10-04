#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Ship;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Server, per frame: gives every ship whose design state has no design yet the selected design (see
    /// <see cref="IShipDesigns"/>), after validating it. A design with blocking problems is refused and the ship stays
    /// bare, with the problems in the log. Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    public sealed class ShipDesignUseCase : ITickable, IShipDesigns
    {
        private const string LogSource = "ShipDesigns";
        public const string LaunchArgument = "-shipDesign";

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly IShipPartCatalog _catalog;
        private readonly IShipDesignStore _store;
        private readonly ShipDesignValidator _validator;
        private readonly ShipStatsProcessor _stats;
        private readonly ShipDesignsConfig _config;
        private readonly IEventPublisher _events;
        private readonly IReadOnlyList<string> _launchArguments;
        private readonly HashSet<System.Guid> _refused = new();
        private ShipDesign? _selected;
        private bool _loaded;

        [Inject]
        public ShipDesignUseCase(IActorRegistry actors, INetworkService network, IShipPartCatalog catalog, IShipDesignStore store,
            ShipDesignValidator validator, ShipDesignsConfig config, IEventPublisher events, ShipStatsProcessor stats)
            : this(actors, network, catalog, store, validator, config, events, stats, null)
        {
        }

        /// <param name="launchArguments">The command line; null reads it on first use (never in a static initializer).</param>
        public ShipDesignUseCase(IActorRegistry actors, INetworkService network, IShipPartCatalog catalog, IShipDesignStore store,
            ShipDesignValidator validator, ShipDesignsConfig config, IEventPublisher events, ShipStatsProcessor stats,
            IReadOnlyList<string>? launchArguments)
        {
            _actors = actors;
            _network = network;
            _catalog = catalog;
            _store = store;
            _validator = validator;
            _stats = stats;
            _config = config;
            _events = events;
            _launchArguments = launchArguments ?? new LazyLaunchArguments();
        }

        public void Select(ShipDesign design)
        {
            _selected = design;
            _loaded = true;
            _refused.Clear();
        }

        public void Tick()
        {
            if (!_network.IsServer) return;

            foreach (var state in _actors.GetActors<IShipDesignState>().ToList())
            {
                if (state.DesignHash != 0 || state.Ship == null || _refused.Contains(state.Id)) continue;

                var design = Selected();
                if (design == null || !TryApply(state, design, out _)) _refused.Add(state.Id);
            }
        }

        public bool TryApply(IShipDesignState state, ShipDesign design, out string? error)
        {
            error = null;
            if (!_network.IsServer) error = "Only the server gives ships their design.";

            var validation = _validator.Validate(design, _catalog, _config.Limits);
            if (error == null && !validation.IsValid) error = $"\"{design.Name}\" cannot fly: {validation}";
            if (error != null)
            {
                _events.LogError(LogSource, error);
                return false;
            }

            var bytes = ShipDesignBinaryCodec.Encode(design);
            ulong hash = ShipDesignHash.Compute(design);
            state.ServerSetDesign(hash, bytes);
            var flight = _stats.Compute(design, _catalog, _config.Flight);
            var tuning = state.Ship as IAirshipTuning ?? (state.Ship as UnityEngine.Component)?.GetComponent<IAirshipTuning>();
            tuning?.ServerSetBaseStats(flight.MaxSpeed, flight.TurnSpeed, flight.MaxHealth);

            var shipId = state.Ship?.Id ?? state.Id;
            if (validation.Problems.Count > 0) _events.LogWarning(LogSource, $"\"{design.Name}\": {validation}");
            _events.Publish(new ShipDesignAppliedEvent(shipId, design.Name, hash, design.Parts.Count));
            _events.LogInfo(LogSource, $"Ship {shipId} flies \"{design.Name}\" ({design.Parts.Count} parts, {bytes.Length} bytes): {flight}.");
            return true;
        }

        private ShipDesign? Selected()
        {
            if (_loaded) return _selected;
            _loaded = true;

            string key = ArgumentValue(LaunchArgument) ?? _config.DefaultDesign;
            var result = _store.Load(key);
            if (result.Succeeded) _selected = result.Design;
            else _events.LogError(LogSource, $"Design \"{key}\" could not be loaded: {result.Error}");
            return _selected;
        }

        private string? ArgumentValue(string flag)
        {
            for (int i = 0; i < _launchArguments.Count - 1; i++)
            {
                if (string.Equals(_launchArguments[i], flag, System.StringComparison.OrdinalIgnoreCase)) return _launchArguments[i + 1];
            }

            return null;
        }

        /// <summary>The real command line, read when first asked (LaunchArguments reads MPPM tags, which throws outside Play).</summary>
        private sealed class LazyLaunchArguments : IReadOnlyList<string>
        {
            private IReadOnlyList<string> Arguments => LaunchArguments.Current;
            public int Count => Arguments.Count;
            public string this[int index] => Arguments[index];
            public IEnumerator<string> GetEnumerator() => Arguments.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
