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
    /// <summary>Server: one pulse of a cannon's beam fired.</summary>
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
    /// Server: a pulse ended on a target (<see cref="Target"/> set) or on something solid. Not published for a pulse that
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

    /// <summary>Server: a cannon overheated; it vents until it is cold.</summary>
    public readonly struct CannonOverheatedEvent
    {
        public readonly Guid CannonId;

        public CannonOverheatedEvent(Guid cannonId) => CannonId = cannonId;
    }

    /// <summary>
    /// Application Layer, server only, after humanoid movement. For each manned cannon it turns the occupant's station
    /// aim (from the input simulated this tick, steered by the Gunner context; see <see cref="GunnerAimUseCase"/>) into
    /// barrel angles. While their fire input is held the beam is on and pulses: the first pulse on the tick it is
    /// pressed, then one every <see cref="CannonConfig.PulseSeconds"/>, each activating the fire ability (who may fire).
    /// Each pulse adds heat; at full heat the cannon overheats and the beam stops until it has cooled all the way down
    /// (<see cref="CannonConfig.SecondsToCool"/>). It cools whenever it is not firing, manned or not.
    /// A pulse is one sweep along the barrel's aim from the muzzle out to <see cref="CannonConfig.BeamRange"/> through
    /// targeting (<see cref="ITargetingService.TryAcquireSegment"/>), passing the cannon that fired it and players. A
    /// target answers each pulse in its own way: a collider target through the hit effect on its controller (a hazard's
    /// health; it breaks), and a target that is no collider through its <see cref="ISpiritBeamTargets"/> source, when it
    /// is nearer than anything solid (the storm's heart targets). A hit on the cannon's own ship is a misfire and applies
    /// the hit effect to the ship. Every peer reads the beam's state from the cannon, hears of each pulse with where it
    /// went, and draws it itself (<see cref="CannonShotPresenter"/>). Plan: .docs/plans/ray-cannons.md.
    /// </summary>
    public class CannonFireUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Cannon";

        private sealed class BeamState
        {
            public float Heat;
            public bool Overheated;
            public int NextPulseTick;
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
        private readonly List<ICannon> _cannons = new();
        private readonly Dictionary<Guid, BeamState> _beams = new();
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

        public void Tick()
        {
            if (!_network.IsServer) return;

            _cannons.Clear();
            _cannons.AddRange(_actors.GetActors<ICannon>());
            foreach (var cannon in _cannons) TickCannon(cannon);
            _sweepingCannon = null;
        }

        private void TickCannon(ICannon cannon)
        {
            if (cannon.Base == null || cannon.Muzzle == null) return;
            if (!_beams.TryGetValue(cannon.Id, out var beam))
            {
                beam = new BeamState();
                _beams[cannon.Id] = beam;
            }

            var occupant = cannon is IStation station ? _occupancy.OccupantOf(station) : null;
            if (occupant != null)
            {
                // The occupant's station aim from the input simulated this tick, clamped again here: the server trusts no aim.
                var (yaw, elevation) = _aim.ClampAim(occupant.InputState.StationAim, _config.AimLimits);
                cannon.ServerSetAim(yaw, elevation);
                cannon.ApplyAim(yaw, elevation);
            }

            bool on = occupant != null && IsFireHeld(occupant) && !beam.Overheated;
            if (on && _time.Tick >= beam.NextPulseTick)
            {
                if (_config.FireAbility != null && _abilities.TryActivateAbility(occupant!, _config.FireAbility))
                {
                    beam.NextPulseTick = _time.Tick + Mathf.Max(1, Mathf.RoundToInt(_config.PulseSeconds * _time.TickRate));
                    Pulse(cannon);
                    beam.Heat = Mathf.Min(1f, beam.Heat + _config.HeatPerPulse);
                    if (beam.Heat >= 1f - 1e-4f)
                    {
                        beam.Heat = 1f;
                        beam.Overheated = true;
                        on = false;
                        _events.Publish(new CannonOverheatedEvent(cannon.Id));
                        _events.LogInfo(LogSource, "A cannon overheated; it vents until cold.");
                    }
                }
                else
                {
                    on = false;
                }
            }

            if (!on)
            {
                beam.Heat = Mathf.Max(0f, beam.Heat - 1f / (_config.SecondsToCool * _time.TickRate));
                if (beam.Heat <= 0f) beam.Overheated = false;
            }

            cannon.ServerSetBeam(on, beam.Heat, beam.Overheated);
        }

        private bool IsFireHeld(IHumanoidCharacterView occupant)
        {
            int bit = _config.FireInput != null ? _config.FireInput.BitIndex : -1;
            return bit >= 0 && (occupant.InputState.ActiveInputMask & (1UL << bit)) != 0;
        }

        private void Pulse(ICannon cannon)
        {
            int shotId = ++_nextShotId;
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
                _events.Publish(new CannonHitEvent(shotId, answering.GetType().Name, damaged: false));
                _events.LogInfo(LogSource, $"Pulse {shotId} hit a {answering.GetType().Name} target.");
            }
            else if (solid)
            {
                end = hit.Point;
                Hit(cannon, shotId, hit);
            }

            cannon.ServerPulse(shotId, origin, end);
            _events.Publish(new CannonFiredEvent(cannon.Id, shotId));
        }

        private void Hit(ICannon cannon, int shotId, SegmentHit hit)
        {
            var ship = cannon.ShipRoot;
            if (ship != null && hit.Collider != null && hit.Collider.transform.IsChildOf(ship))
            {
                Misfire(cannon, shotId);
                return;
            }

            var controller = hit.Target?.Controller;
            bool damaged = controller != null && _config.HitEffect != null;
            if (damaged) _abilities.ApplyEffect(controller!, _config.HitEffect!);

            string target = hit.Target is Component component && component != null ? component.name : hit.Target != null ? hit.Target.GetType().Name : "terrain";
            _events.Publish(new CannonHitEvent(shotId, target, damaged));
        }

        // The beam hit the ship its cannon stands on. The hull is not a target, so the hit effect goes to the ship's
        // controller directly.
        private void Misfire(ICannon cannon, int shotId)
        {
            var ship = cannon.ShipController;
            bool damaged = ship != null && _config.HitEffect != null;
            if (damaged) _abilities.ApplyEffect(ship!, _config.HitEffect!);

            _events.Publish(new CannonHitEvent(shotId, "own ship", damaged, ownShip: true));
        }

        // A beam passes the cannon that fired it (it starts inside it) and players. Its own ship it hits: a misfire.
        private bool Ignores(Collider collider) =>
            (_sweepingCannon != null && collider.transform.IsChildOf(_sweepingCannon)) ||
            collider.GetComponentInParent<IHumanoidCharacterView>() != null;
    }
}
