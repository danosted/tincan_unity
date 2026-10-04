#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using VContainer;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Application Layer, server only, after airship movement: island rock is solid to the ship. A ship found inside an
    /// island (<see cref="ISkyIslandContactQuery"/>) is pushed out at once (<see cref="IAirshipCollisionResponse"/>: its
    /// velocity into the rock goes, with a small bounce) and, at most once per <see cref="SkyIslandConfig.ImpactCooldown"/>,
    /// takes <see cref="SkyIslandConfig.ImpactEffect"/> on its health. Scraping along an island hurts again every cooldown.
    /// Plan: .docs/plans/sky-islands.md (S2).
    /// </summary>
    public class SkyIslandImpactUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "SkyIslands";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly ISkyIslandContactQuery _contact;
        private readonly IAirshipCollisionResponse _response;
        private readonly AbilitySystemUseCase _abilities;
        private readonly SkyIslandConfig _config;
        private readonly Dictionary<Guid, float> _hurtAgainAt = new();
        private float _elapsed;

        [Inject]
        public SkyIslandImpactUseCase(INetworkService network, IActorRegistry actors, ITimeService time, IEventPublisher events,
            ISkyIslandContactQuery contact, IAirshipCollisionResponse response, AbilitySystemUseCase abilities, SkyIslandConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _contact = contact;
            _response = response;
            _abilities = abilities;
            _config = config;
        }

        /// <summary>Hits that hurt the ship, since the server started.</summary>
        public int Hits { get; private set; }

        public void Tick()
        {
            if (!_network.IsServer) return;
            _elapsed += _time.DeltaTime;

            foreach (var ship in _actors.GetActors<IAirshipView>())
            {
                if (!ship.IsSimulating || ship.Transform == null) continue;
                if (!_contact.TryPushOut(ship, out var push, out var island)) continue;

                _response.Push(ship, push);
                if (_hurtAgainAt.TryGetValue(ship.Id, out var at) && _elapsed < at) continue;

                _hurtAgainAt[ship.Id] = _elapsed + _config.ImpactCooldown;
                Hurt(ship, island);
            }
        }

        private void Hurt(IAirshipView ship, SkyIslandId island)
        {
            Hits++;
            var controller = (ship as IShipState)?.Controller;
            if (controller != null && _config.ImpactEffect != null) _abilities.ApplyEffect(controller, _config.ImpactEffect);

            _events.Publish(new SkyIslandHitShipEvent(ship.Id, island, Hits));
            _events.LogInfo(LogSource, $"The ship hit island {island} ({Hits} so far).");
        }
    }
}
