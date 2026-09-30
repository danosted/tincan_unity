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
    /// <summary>Server: a cannon fired a shot.</summary>
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

    /// <summary>Server: a cannonball ended its flight, on a target (<see cref="Target"/> set) or on something solid.</summary>
    public readonly struct CannonballHitEvent
    {
        public readonly int ShotId;
        public readonly string Target;
        public readonly bool Damaged;

        public CannonballHitEvent(int shotId, string target, bool damaged)
        {
            ShotId = shotId;
            Target = target;
            Damaged = damaged;
        }
    }

    /// <summary>
    /// Application Layer, server only, after humanoid movement. For each manned cannon it turns the occupant's station
    /// aim (from the input simulated this tick, steered by the Gunner context; see <see cref="GunnerAimUseCase"/>) into
    /// barrel angles, and on the tick their fire input is first pressed it activates the fire ability; the ability's
    /// cooldown is the reload. A shot is an arc,
    /// not an object: each tick its newest piece is swept through targeting (<see cref="ITargetingService.TryAcquireSegment"/>),
    /// passing the cannon's own ship and players. A hit applies the hit effect to the target's controller. Every peer
    /// hears of the shot twice (fired, ended) and draws it itself (<see cref="CannonShotPresenter"/>).
    /// </summary>
    public class CannonFireUseCase : ISimulationTickable
    {
        public SimulationPhase Phase => SimulationPhase.AfterHumanoid;

        private const string LogSource = "Cannon";

        private sealed class Shot
        {
            public int Id;
            public BallisticArc Arc;
            public ICannon Cannon = null!;
            public Transform? ShipRoot;
        }

        private readonly INetworkService _network;
        private readonly IActorRegistry _actors;
        private readonly ITimeService _time;
        private readonly IEventPublisher _events;
        private readonly AbilitySystemUseCase _abilities;
        private readonly ITargetingService _targeting;
        private readonly IStationOccupancy _occupancy;
        private readonly CannonConfig _config;
        private readonly CannonballProcessor _balls;
        private readonly CannonAimProcessor _aim;
        private readonly List<Shot> _shots = new();
        private readonly List<ICannon> _cannons = new();
        private readonly Dictionary<Guid, bool> _wasPressed = new();
        private readonly Dictionary<Guid, (Vector3 Position, Quaternion Rotation)> _lastBase = new();
        private readonly Func<Collider, bool> _ignore;
        private Transform? _sweepingShip;
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
            CannonballProcessor balls,
            CannonAimProcessor aim)
        {
            _network = network;
            _actors = actors;
            _time = time;
            _events = events;
            _abilities = abilities;
            _targeting = targeting;
            _occupancy = occupancy;
            _config = config;
            _balls = balls;
            _aim = aim;
            _ignore = Ignores;
        }

        /// <summary>Shots in flight, for tests and scenario probes.</summary>
        public int ShotsInFlight => _shots.Count;

        public void Tick()
        {
            if (!_network.IsServer) return;

            _cannons.Clear();
            _cannons.AddRange(_actors.GetActors<ICannon>());
            foreach (var cannon in _cannons) TickCannon(cannon);

            SweepShots();
        }

        private void TickCannon(ICannon cannon)
        {
            if (cannon.Base == null || cannon.Muzzle == null) return;

            var basePose = (cannon.Base.position, cannon.Base.rotation);
            bool hadLast = _lastBase.TryGetValue(cannon.Id, out var last);
            _lastBase[cannon.Id] = basePose;

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

            if (JustPressedFire(cannon.Id, occupant) && _config.FireAbility != null &&
                _abilities.TryActivateAbility(occupant, _config.FireAbility))
            {
                // The ship's motion at the muzzle, not the barrel's swing (see CannonAimProcessor.BaseVelocityAt).
                Vector3 muzzle = cannon.Muzzle.position;
                Vector3 inherited = hadLast
                    ? _aim.BaseVelocityAt(muzzle, last.Position, last.Rotation, basePose.position, basePose.rotation, _time.DeltaTime)
                    : Vector3.zero;
                Fire(cannon, muzzle, _aim.MuzzleDirection(cannon.Base.rotation, yaw, elevation), inherited);
            }
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

        private void Fire(ICannon cannon, Vector3 origin, Vector3 direction, Vector3 inherited)
        {
            var arc = BallisticArc.FromMuzzle(origin, direction, _config.MuzzleSpeed, inherited, _config.GravityVector, _time.Tick);
            var shot = new Shot { Id = ++_nextShotId, Arc = arc, Cannon = cannon, ShipRoot = cannon.ShipRoot };
            _shots.Add(shot);

            cannon.ServerShotFired(shot.Id, arc.Origin, arc.Velocity);
            _events.Publish(new CannonFiredEvent(cannon.Id, shot.Id));
            _events.LogInfo(LogSource, $"Shot {shot.Id} fired at {arc.Velocity.magnitude:0} m/s.");
        }

        private void SweepShots()
        {
            int tick = _time.Tick;
            int tickRate = _time.TickRate;

            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var shot = _shots[i];
                var (from, to) = _balls.SegmentAt(shot.Arc, tick, tickRate);

                _sweepingShip = shot.ShipRoot;
                if (_config.Sweep != null && _targeting.TryAcquireSegment(from, to, _config.Sweep, _ignore, out var hit))
                {
                    Hit(shot, hit);
                    End(i, hit.Point);
                }
                else if (_balls.IsSpent(shot.Arc, tick, tickRate, _config.BallLimits))
                {
                    End(i, to);
                }
            }

            _sweepingShip = null;
        }

        private void Hit(Shot shot, SegmentHit hit)
        {
            var controller = hit.Target?.Controller;
            bool damaged = controller != null && _config.HitEffect != null;
            if (damaged) _abilities.ApplyEffect(controller!, _config.HitEffect!);

            string target = hit.Target is Component component && component != null ? component.name : hit.Target != null ? hit.Target.GetType().Name : "terrain";
            _events.Publish(new CannonballHitEvent(shot.Id, target, damaged));
            _events.LogInfo(LogSource, $"Shot {shot.Id} hit {target}.");
        }

        private void End(int index, Vector3 point)
        {
            var shot = _shots[index];
            _shots.RemoveAt(index);
            if (shot.Cannon is not Component component || component != null) shot.Cannon.ServerShotEnded(shot.Id, point);
        }

        // A shot passes its own ship and players (no friendly fire).
        private bool Ignores(Collider collider) =>
            (_sweepingShip != null && collider.transform.IsChildOf(_sweepingShip)) ||
            collider.GetComponentInParent<IHumanoidCharacterView>() != null;
    }
}
