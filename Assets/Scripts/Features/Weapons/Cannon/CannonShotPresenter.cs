#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Features.Stations;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Presentation, every peer, every frame. Turns each cannon's barrel: the local occupant's own aim at once (their
    /// look is local and predicted), everyone else's replicated aim smoothed. Shows the local occupant the arc a shot
    /// would fly. Draws the balls: a ball is placed on its arc by the time since this peer heard of it (ticks are not
    /// comparable across peers), starting at this peer's own muzzle and blending onto the server's path, because a
    /// client sees its ship slightly behind the server's.
    /// </summary>
    public sealed class CannonShotPresenter : ITickable, IDisposable
    {
        private sealed class Ball
        {
            public int ShotId;
            public ICannon Cannon = null!;
            public BallisticArc Arc;
            public Vector3 MuzzleOffset;
            public float Age;
            public Transform View = null!;
        }

        private sealed class AimState
        {
            public float Yaw;
            public float Elevation;
            public Vector3 LastBasePosition;
            public Quaternion LastBaseRotation;
            public Vector3 MuzzleVelocity;
            public bool Initialized;
        }

        // Frame-to-frame motion of an interpolated ship is uneven; smooth the inherited velocity so the aiming arc holds still.
        private const float InheritedVelocitySharpness = 8f;

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private readonly List<Ball> _balls = new();
        private readonly Stack<Transform> _pool = new();
        private readonly Dictionary<Guid, AimState> _aims = new();
        private readonly List<ICannon> _cannons = new();
        private Vector3[] _preview = Array.Empty<Vector3>();
        private Transform? _root;

        public CannonShotPresenter(IActorRegistry actors, INetworkService network, ITimeService time, CannonConfig config, CannonAimProcessor aim)
        {
            _actors = actors;
            _network = network;
            _time = time;
            _config = config;
            _aim = aim;
        }

        /// <summary>Balls drawn so far on this peer (scenario probe: the client saw the shot).</summary>
        public int BallsShown { get; private set; }

        public int BallsInFlight => _balls.Count;

        public void Tick()
        {
            float deltaTime = _time.DeltaTime;
            _cannons.Clear();
            _cannons.AddRange(_actors.GetActors<ICannon>());

            foreach (var cannon in _cannons)
            {
                PresentAim(cannon, deltaTime);
                while (cannon.TryTakeShotEvent(out var shotEvent)) Receive(cannon, shotEvent);
            }

            AdvanceBalls(deltaTime);
        }

        private void PresentAim(ICannon cannon, float deltaTime)
        {
            if (cannon.Base == null || cannon.Muzzle == null) return;
            if (!_aims.TryGetValue(cannon.Id, out var state))
            {
                state = new AimState();
                _aims[cannon.Id] = state;
            }

            var local = LocalOccupant(cannon);
            if (local != null)
            {
                // The live look (the orbital camera's yaw and pitch, updated every frame from the mouse), not the body's
                // facing, which only turns in simulation ticks: the gunner's camera rides the barrel, so steps would show.
                var movement = local.Movement;
                (state.Yaw, state.Elevation) = _aim.BarrelAngles(cannon.Base.rotation, movement.LookRotation * Vector3.forward, movement.LookPitch, _config.AimLimits);
            }
            else if (!state.Initialized)
            {
                (state.Yaw, state.Elevation) = (cannon.Yaw, cannon.Elevation);
            }
            else
            {
                float blend = 1f - Mathf.Exp(-_config.ProxyAimSharpness * deltaTime);
                state.Yaw = Mathf.LerpAngle(state.Yaw, cannon.Yaw, blend);
                state.Elevation = Mathf.Lerp(state.Elevation, cannon.Elevation, blend);
            }

            cannon.ApplyAim(state.Yaw, state.Elevation);

            // The ship's motion at the muzzle, without the barrel's swing (which made the arc jump while aiming).
            var basePose = cannon.Base;
            if (state.Initialized && deltaTime > 0f)
            {
                Vector3 velocity = _aim.BaseVelocityAt(cannon.Muzzle.position, state.LastBasePosition, state.LastBaseRotation,
                    basePose.position, basePose.rotation, deltaTime);
                state.MuzzleVelocity = Vector3.Lerp(state.MuzzleVelocity, velocity, 1f - Mathf.Exp(-InheritedVelocitySharpness * deltaTime));
            }
            state.LastBasePosition = basePose.position;
            state.LastBaseRotation = basePose.rotation;
            state.Initialized = true;

            if (local != null && _config.AimPreviewSeconds > 0f) ShowPreview(cannon, state);
            else cannon.ShowAimPreview(null, 0);
        }

        private IHumanoidCharacterView? LocalOccupant(ICannon cannon)
        {
            if (cannon is not IStation { OccupantClientId: { } occupant } || occupant != _network.LocalClientId) return null;
            return _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
        }

        private void ShowPreview(ICannon cannon, AimState state)
        {
            int count = _config.AimPreviewPoints;
            if (_preview.Length != count) _preview = new Vector3[count];

            var arc = BallisticArc.FromMuzzle(cannon.Muzzle!.position, _aim.MuzzleDirection(cannon.Base!.rotation, state.Yaw, state.Elevation),
                _config.MuzzleSpeed, state.MuzzleVelocity, _config.GravityVector, 0);
            for (int i = 0; i < count; i++) _preview[i] = arc.PositionAt(_config.AimPreviewSeconds * i / (count - 1));

            cannon.ShowAimPreview(_preview, count);
        }

        private void Receive(ICannon cannon, CannonShotEvent shotEvent)
        {
            if (shotEvent.Ended)
            {
                int index = _balls.FindIndex(ball => ball.ShotId == shotEvent.ShotId && ReferenceEquals(ball.Cannon, cannon));
                if (index >= 0) Release(index);
                return;
            }

            var view = Take();
            var arc = new BallisticArc(shotEvent.Origin, shotEvent.Velocity, _config.GravityVector, 0);
            Vector3 localMuzzle = cannon.Muzzle != null ? cannon.Muzzle.position : shotEvent.Origin;
            _balls.Add(new Ball { ShotId = shotEvent.ShotId, Cannon = cannon, Arc = arc, MuzzleOffset = localMuzzle - shotEvent.Origin, View = view });
            view.position = localMuzzle;
            view.gameObject.SetActive(true);
            BallsShown++;
        }

        private void AdvanceBalls(float deltaTime)
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                var ball = _balls[i];
                ball.Age += deltaTime;

                // A lost "ended" message must not leave a ball hanging forever.
                if (ball.Age > _config.MaxLifetime + 1f)
                {
                    Release(i);
                    continue;
                }

                float blend = _config.MuzzleBlendSeconds > 0f ? 1f - Mathf.Clamp01(ball.Age / _config.MuzzleBlendSeconds) : 0f;
                ball.View.position = ball.Arc.PositionAt(ball.Age) + ball.MuzzleOffset * blend;
            }
        }

        private Transform Take()
        {
            if (_pool.Count > 0) return _pool.Pop();

            _root ??= new GameObject("CannonShots").transform;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Cannonball";
            Object.Destroy(ball.GetComponent<Collider>()); // drawn only; the server's sweep decides hits
            ball.transform.SetParent(_root, false);
            ball.transform.localScale = Vector3.one * _config.BallDiameter;
            return ball.transform;
        }

        private void Release(int index)
        {
            var ball = _balls[index];
            _balls.RemoveAt(index);
            if (ball.View == null) return;
            ball.View.gameObject.SetActive(false);
            _pool.Push(ball.View);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _balls.Clear();
            _pool.Clear();
        }
    }
}
