#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>
    /// Every peer: the current voyage's boon gates (<see cref="BoonGateLayoutProcessor"/>), from the replicated
    /// <see cref="ISessionLayout"/>, rebuilt when a new voyage begins. None before a voyage. The chart reads it on every
    /// peer; the server spawns a networked gate at each.
    /// </summary>
    public class BoonGateMap
    {
        private readonly IActorRegistry _actors;
        private readonly BoonGateLayoutProcessor _layout;
        private readonly BoonsConfig _config;
        private readonly List<BoonGatePlacement> _gates = new();
        private (int Seed, Vector3 Origin, Vector3 Destination)? _key;

        public BoonGateMap(IActorRegistry actors, BoonGateLayoutProcessor layout, BoonsConfig config)
        {
            _actors = actors;
            _layout = layout;
            _config = config;
        }

        public IReadOnlyList<BoonGatePlacement> Gates
        {
            get
            {
                Refresh();
                return _gates;
            }
        }

        /// <summary>The seed of the voyage the gates were laid for; 0 before any voyage.</summary>
        public int Seed => _key?.Seed ?? 0;

        private void Refresh()
        {
            (int Seed, Vector3 Origin, Vector3 Destination)? key = null;
            foreach (var session in _actors.GetActors<ISessionLayout>())
            {
                if (session.LayoutSeed == 0) continue;
                key = (session.LayoutSeed, session.Origin, session.Destination);
                break;
            }

            if (key == _key) return;
            _key = key;
            _gates.Clear();
            if (key is { } layout) _layout.Gates(layout.Seed, layout.Origin, layout.Destination, _config.Rules, _gates);
        }
    }
}
