#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Ship;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Server-side control over ship damage, for other features (repair) and dev tooling (scenarios).</summary>
    public interface IShipBreakage
    {
        /// <summary>Random breakage on/off; starts from <see cref="ShipDamageConfig.AutoBreak"/>.</summary>
        bool AutoBreak { get; set; }
        int BrokenCount { get; }
        bool TryBreak(int pointIndex);
        bool TryRestore(int pointIndex);
    }

    /// <summary>
    /// Application Layer, server only, after airship movement. Breaks a random healthy part on a timer by applying the
    /// break effect to that part's own GAS controller. Every tick it reconciles each part's health with the effects it
    /// holds: a broken part keeps one hull breach on the ship (fuel leak, State.Ship.Damaged) and one broken mark on
    /// itself (State.Damaged); a fully repaired part releases both. Reconciling rather than reacting keeps breaks and
    /// repairs from any source (timer, repair tool, other damage effects, scenarios) consistent.
    /// </summary>
    public class ShipBreakageUseCase : ISimulationTickable, IShipBreakage
    {
        public SimulationPhase Phase => SimulationPhase.AfterAirship;

        private const string LogSource = "ShipDamage";

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly ShipBreakageProcessor _processor;
        private readonly ShipDamageConfig _config;
        private readonly Random _random;
        // Per broken part: the hull breach it holds on the ship, and the broken mark on the part itself.
        private readonly Dictionary<IShipDamagePoint, (ActiveGameplayEffect? Ship, ActiveGameplayEffect? Part)> _breaches = new();
        private readonly List<bool> _brokenFlags = new();

        private float _elapsed;
        private float _nextBreakAt = -1f;

        public ShipBreakageUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITimeService time,
            IEventPublisher events,
            AbilitySystemUseCase abilities,
            ShipBreakageProcessor processor,
            ShipDamageConfig config)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _processor = processor;
            _config = config;
            _random = config.Seed != 0 ? new Random(config.Seed) : new Random();
            AutoBreak = config.AutoBreak;
        }

        public bool AutoBreak { get; set; }
        public int BrokenCount => _breaches.Count;

        public void Tick()
        {
            if (!_network.IsServer || !TryResolveShip(out var airship, out var points)) return;

            _elapsed += _time.DeltaTime;
            if (_nextBreakAt < 0f) _nextBreakAt = _elapsed + _config.FirstBreakDelay;

            if (AutoBreak && _elapsed >= _nextBreakAt)
            {
                BreakRandom(points);
                _nextBreakAt = _elapsed + _processor.NextInterval(_random, _config.MinInterval, _config.MaxInterval);
            }

            Reconcile(airship, points);
        }

        public bool TryBreak(int pointIndex) => ApplyToPoint(pointIndex, _config.BreakEffect);

        public bool TryRestore(int pointIndex) => ApplyToPoint(pointIndex, _config.RestoreEffect);

        private bool ApplyToPoint(int pointIndex, GameplayEffectDefinition? effect)
        {
            if (!_network.IsServer || effect == null || !TryResolveShip(out var airship, out var points)) return false;

            var point = points.FirstOrDefault(candidate => candidate.Index == pointIndex);
            if (point?.Controller == null) return false;

            _abilities.ApplyEffect(point.Controller, effect);
            Reconcile(airship, points);
            return true;
        }

        private void BreakRandom(IReadOnlyList<IShipDamagePoint> points)
        {
            _brokenFlags.Clear();
            foreach (var point in points) _brokenFlags.Add(point.IsBroken);
            if (!_processor.CanBreak(_brokenFlags, _config.MaxBroken)) return;

            int index = _processor.PickHealthy(_random, _brokenFlags);
            if (index >= 0 && _config.BreakEffect != null && points[index].Controller is { } controller)
            {
                _abilities.ApplyEffect(controller, _config.BreakEffect);
            }
        }

        private void Reconcile(IAirshipView airship, IReadOnlyList<IShipDamagePoint> points)
        {
            var controller = (airship as IShipState)?.Controller;

            foreach (var point in points)
            {
                bool tracked = _breaches.TryGetValue(point, out var breach);
                switch (broken: point.IsBroken, tracked)
                {
                    case (broken: true, tracked: false):
                        _breaches[point] = (Apply(controller, _config.HullBreachEffect), Apply(point.Controller, _config.PartBrokenEffect));
                        _events.Publish(new ShipPartBrokenEvent(airship.Id, point.Index, _breaches.Count));
                        _events.LogInfo(LogSource, $"Part {point.Index} broke ({_breaches.Count} broken).");
                        break;
                    case (broken: false, tracked: true):
                        _breaches.Remove(point);
                        Remove(controller, breach.Ship);
                        Remove(point.Controller, breach.Part);
                        _events.Publish(new ShipPartRepairedEvent(airship.Id, point.Index, _breaches.Count));
                        _events.LogInfo(LogSource, $"Part {point.Index} repaired ({_breaches.Count} broken).");
                        break;
                }
            }

            // Points that despawned (fixture removed) take their breach with them.
            foreach (var gone in _breaches.Keys.Where(point => !points.Contains(point)).ToArray())
            {
                Remove(controller, _breaches[gone].Ship);
                _breaches.Remove(gone);
            }
        }

        private ActiveGameplayEffect? Apply(IAbilityControllerBase? target, GameplayEffectDefinition? effect) =>
            target != null && effect != null ? _abilities.ApplyEffect(target, effect) : null;

        private void Remove(IAbilityControllerBase? target, ActiveGameplayEffect? effect)
        {
            if (target != null && effect != null) _abilities.RemoveEffect(target, effect);
        }

        private bool TryResolveShip(out IAirshipView airship, out IReadOnlyList<IShipDamagePoint> points)
        {
            foreach (var candidate in _actors.GetActors<IAirshipView>())
            {
                if (!candidate.IsSimulating) continue;
                var found = ShipDamageLocator.FindPoints(candidate);
                if (found.Count == 0) continue;

                airship = candidate;
                points = found;
                return true;
            }

            airship = null!;
            points = Array.Empty<IShipDamagePoint>();
            return false;
        }
    }
}
