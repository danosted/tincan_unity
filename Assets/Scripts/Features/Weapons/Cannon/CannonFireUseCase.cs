#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Features.Stations;
using TinCan.Core.Targeting;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>Server: a cannon's beam fired, after its charge.</summary>
    public readonly struct CannonFiredEvent
    {
        public readonly Guid CannonId;
        public readonly int ShotId;

        public CannonFiredEvent(Guid cannonId, int shotId)
        {
            CannonId = cannonId;
            ShotId = shotId;
        }
    }

    /// <summary>
    /// Server: a beam ended on a target (<see cref="Target"/> set) or on something solid. Not published for a beam that
    /// hit nothing. <see cref="OwnShip"/>: it hit the ship its cannon is mounted on (a misfire).
    /// </summary>
    public readonly struct CannonHitEvent
    {
        public readonly int ShotId;
        public readonly string Target;
        public readonly bool Damaged;
        public readonly bool OwnShip;

        public CannonHitEvent(int shotId, string target, bool damaged, bool ownShip = false)
        {
            ShotId = shotId;
            Target = target;
            Damaged = damaged;
            OwnShip = ownShip;
        }
    }

    /// <summary>
    /// Application Layer, server only, after humanoid movement. For each manned cannon it turns the occupant's station
    /// aim (from the input simulated this tick, steered by the Gunner context; see <see cref="GunnerAimUseCase"/>) into
    /// barrel angles, and on the tick their fire input is first pressed it activates the fire ability, whose cooldown
    /// is the time between shots. That starts the charge; <see cref="CannonConfig.ChargeSeconds"/> later the beam fires
    /// along the barrel's aim then (the gunner can still turn it while it charges): one sweep from the muzzle out to
    /// <see cref="CannonConfig.BeamRange"/> through targeting (<see cref="ITargetingService.TryAcquireSegment"/>),
    /// passing the cannon that fired it and players. A target answers the beam in its own way (R2): a collider target
    /// through the hit effect on its controller (a hazard's health; it breaks), and a target that is no collider
    /// through its <see cref="ISpiritBeamTargets"/> source, when it is nearer than anything solid (the storm's heart
    /// targets). A hit on the cannon's own ship is a misfire and applies the hit effect to the ship. Every peer hears of the shot twice (charging, fired with where it went) and draws it
    /// itself (<see cref="CannonShotPresenter"/>). Plan: .docs/plans/ray-cannons.md.
    /// </summary>
    public class CannonFireUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Cannon";

        private sealed class Charge
        {
            public int Id;
            public int FireTick;
            public ICannon Cannon = null!;
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly ITargetingService _targeting;
        private readonly IStationOccupancy _occupancy;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private readonly IReadOnlyList<ISpiritBeamTargets> _beamTargets;
        private readonly List<Charge> _charges = new();
        private readonly List<ICannon> _cannons = new();
        private readonly Dictionary<Guid, bool> _wasPressed = new();
        private readonly Func<Collider, bool> _ignore;
        private Transform? _sweepingCannon;
        private int _nextShotId;

        public CannonFireUseCase(
            INetworkService network,
            IActorRegistry actors,
            ITimeService time,
            IEventPublisher events,
            AbilitySystemUseCase abilities,
            ITargetingService targeting,
            IStationOccupancy occupancy,
            CannonConfig config,
            CannonAimProcessor aim,
            IReadOnlyList<ISpiritBeamTargets> beamTargets)
        {
            _beamTargets = beamTargets;
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _targeting = targeting;
            _occupancy = occupancy;
            _config = config;
            _aim = aim;
            _ignore = Ignores;
        }

        /// <summary>Shots charging, for tests and scenario probes.</summary>
        public int ShotsCharging => _charges.Count;

        public void Tick()
        {
            if (!_network.IsServer) return;

            _cannons.Clear();
            _cannons.AddRange(_actors.GetActors<ICannon>());
            foreach (var cannon in _cannons) TickCannon(cannon);

            FireCharged();
        }

        private void TickCannon(ICannon cannon)
        {
            if (cannon.Base == null || cannon.Muzzle == null) return;

            var occupant = cannon is IStation station ? _occupancy.OccupantOf(station) : null;
            if (occupant == null)
            {
                _wasPressed.Remove(cannon.Id);
                return;
            }

            // The occupant's station aim from the input simulated this tick, clamped again here: the server trusts no aim.
            var (yaw, elevation) = _aim.ClampAim(occupant.InputState.StationAim, _config.AimLimits);
            cannon.ServerSetAim(yaw, elevation);
            cannon.ApplyAim(yaw, elevation);

            if (JustPressedFire(cannon.Id, occupant) && !IsCharging(cannon) && _config.FireAbility != null &&
                _abilities.TryActivateAbility(occupant, _config.FireAbility))
            {
                var charge = new Charge
                {
                    Id = ++_nextShotId,
                    FireTick = _time.Tick + Mathf.CeilToInt(_config.ChargeSeconds * _time.TickRate),
                    Cannon = cannon,
                };
                _charges.Add(charge);
                cannon.ServerShotCharging(charge.Id);
            }
        }

        private bool IsCharging(ICannon cannon)
        {
            foreach (var charge in _charges)
            {
                if (ReferenceEquals(charge.Cannon, cannon)) return true;
            }
            return false;
        }

        private bool JustPressedFire(Guid cannonId, IHumanoidCharacterView occupant)
        {
            int bit = _config.FireInput != null ? _config.FireInput.BitIndex : -1;
            if (bit < 0) return false;

            bool pressed = (occupant.InputState.ActiveInputMask & (1UL << bit)) != 0;
            bool wasPressed = _wasPressed.TryGetValue(cannonId, out var previous) && previous;
            _wasPressed[cannonId] = pressed;
            return pressed && !wasPressed;
        }

        private void FireCharged()
        {
            for (int i = _charges.Count - 1; i >= 0; i--)
            {
                var charge = _charges[i];
                if (_time.Tick < charge.FireTick) continue;

                _charges.RemoveAt(i);
                // A cannon gone while it charged (its ship despawned) fires nothing.
                if (charge.Cannon is Component component && component == null) continue;
                if (charge.Cannon.Base == null || charge.Cannon.Muzzle == null) continue;
                Fire(charge);
            }

            _sweepingCannon = null;
        }

        private void Fire(Charge charge)
        {
            var cannon = charge.Cannon;
            Vector3 origin = cannon.Muzzle!.position;
            Vector3 direction = _aim.MuzzleDirection(cannon.Base!.rotation, cannon.Yaw, cannon.Elevation);
            Vector3 end = origin + direction * _config.BeamRange;

            _sweepingCannon = cannon.Base;
            SegmentHit hit = default;
            bool solid = _config.Sweep != null && _targeting.TryAcquireSegment(origin, end, _config.Sweep, _ignore, out hit);
            float length = solid ? Vector3.Distance(origin, hit.Point) : _config.BeamRange;

            // Targets that are not colliders answer the beam themselves, if they are nearer than anything solid.
            ISpiritBeamTargets? answering = null;
            foreach (var source in _beamTargets)
            {
                if (source.FirstHit(origin, direction, length) is not { } distance || distance >= length) continue;
                length = distance;
                answering = source;
            }

            if (answering != null)
            {
                end = origin + direction * length;
                answering.Answer(origin, direction, length);
                _events.Publish(new CannonHitEvent(charge.Id, answering.GetType().Name, damaged: false));
                _events.LogInfo(LogSource, $"Shot {charge.Id} hit a {answering.GetType().Name} target.");
            }
            else if (solid)
            {
                end = hit.Point;
                Hit(charge, hit);
            }

            cannon.ServerShotFired(charge.Id, origin, end);
            _events.Publish(new CannonFiredEvent(cannon.Id, charge.Id));
            _events.LogInfo(LogSource, $"Shot {charge.Id} fired, {(end - origin).magnitude:0} m.");
        }

        private void Hit(Charge charge, SegmentHit hit)
        {
            var ship = charge.Cannon.ShipRoot;
            if (ship != null && hit.Collider != null && hit.Collider.transform.IsChildOf(ship))
            {
                Misfire(charge);
                return;
            }

            var controller = hit.Target?.Controller;
            bool damaged = controller != null && _config.HitEffect != null;
            if (damaged) _abilities.ApplyEffect(controller!, _config.HitEffect!);

            string target = hit.Target is Component component && component != null ? component.name : hit.Target != null ? hit.Target.GetType().Name : "terrain";
            _events.Publish(new CannonHitEvent(charge.Id, target, damaged));
            _events.LogInfo(LogSource, $"Shot {charge.Id} hit {target}.");
        }

        // The beam hit the ship its cannon stands on. The hull is not a target, so the hit effect goes to the ship's
        // controller directly.
        private void Misfire(Charge charge)
        {
            var ship = charge.Cannon.ShipController;
            bool damaged = ship != null && _config.HitEffect != null;
            if (damaged) _abilities.ApplyEffect(ship!, _config.HitEffect!);

            _events.Publish(new CannonHitEvent(charge.Id, "own ship", damaged, ownShip: true));
            _events.LogInfo(LogSource, $"Shot {charge.Id} hit its own ship.");
        }

        // A beam passes the cannon that fired it (it starts inside it) and players. Its own ship it hits: a misfire.
        private bool Ignores(Collider collider) =>
            (_sweepingCannon != null && collider.transform.IsChildOf(_sweepingCannon)) ||
            collider.GetComponentInParent<IHumanoidCharacterView>() != null;
    }
}
