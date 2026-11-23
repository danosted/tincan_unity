#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Audio;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Humanoid;
using TinCan.Features.Stations;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Presentation, every peer, every frame. Turns each cannon's barrel: the local occupant's own aim at once
    /// (<see cref="GunnerAimUseCase.LocalAim"/>, local and predicted), everyone else's replicated aim smoothed. Shows the
    /// local occupant a straight line along the barrel. Draws each shot from its two events: on the charge, a glow that
    /// grows at this peer's muzzle and the fire sound; on the fire, a beam from this peer's muzzle to the server's end
    /// point that thins out over <see cref="CannonConfig.BeamSeconds"/>, with a bloom where it hit something. The muzzle
    /// is this peer's own, because a client sees its ship slightly behind the server's. Plan: .docs/plans/ray-cannons.md.
    /// </summary>
    public sealed class CannonShotPresenter : ITickable, IDisposable
    {
        private sealed class ChargeGlow
        {
            public int ShotId;
            public ICannon Cannon = null!;
            public float Age;
            public Transform View = null!;
        }

        private sealed class Beam
        {
            public ICannon Cannon = null!;
            public Vector3 End;
            public float Age;
            public LineRenderer Line = null!;
            public Transform? Bloom;
        }

        private sealed class AimState
        {
            public float Yaw;
            public float Elevation;
            public bool Initialized;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly IAudioPlayer _audio;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private readonly GunnerAimUseCase _gunner;
        private readonly List<ChargeGlow> _charges = new();
        private readonly List<Beam> _beams = new();
        private readonly Stack<Transform> _glowPool = new();
        private readonly Stack<LineRenderer> _linePool = new();
        private readonly Dictionary<Guid, AimState> _aims = new();
        private readonly List<ICannon> _cannons = new();
        private readonly Vector3[] _preview = new Vector3[2];
        private MaterialPropertyBlock? _block;
        private Mesh? _glowMesh;
        private Transform? _root;

        public CannonShotPresenter(IActorRegistry actors, INetworkService network, ITimeService time, IAudioPlayer audio, CannonConfig config,
            CannonAimProcessor aim, GunnerAimUseCase gunner)
        {
            _actors = actors;
            _network = network;
            _time = time;
            _audio = audio;
            _config = config;
            _aim = aim;
            _gunner = gunner;
        }

        /// <summary>Beams drawn so far on this peer (scenario probe: the client saw the shot).</summary>
        public int BeamsShown { get; private set; }

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

            AdvanceCharges(deltaTime);
            AdvanceBeams(deltaTime);
        }

        private void PresentAim(ICannon cannon, float deltaTime)
        {
            if (cannon.Base == null || cannon.Muzzle == null) return;
            if (!_aims.TryGetValue(cannon.Id, out var state))
            {
                state = new AimState();
                _aims[cannon.Id] = state;
            }

            bool local = IsLocallyManned(cannon);
            if (local && _gunner.LocalAim is { } gunning && gunning.Cannon == cannon.Id)
            {
                // The gunner's live aim (steered every frame from the mouse), not the replicated one, which only moves in
                // simulation ticks: the gunner's camera rides the barrel, so steps would show.
                (state.Yaw, state.Elevation) = _aim.ClampAim(gunning.Aim, _config.AimLimits);
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
            state.Initialized = true;

            cannon.ApplyAim(state.Yaw, state.Elevation);

            if (!local)
            {
                cannon.ShowAimPreview(null, 0);
                return;
            }

            _preview[0] = cannon.Muzzle.position;
            _preview[1] = _preview[0] + _aim.MuzzleDirection(cannon.Base.rotation, state.Yaw, state.Elevation) * _config.BeamRange;
            cannon.ShowAimPreview(_preview, 2);
        }

        private bool IsLocallyManned(ICannon cannon) =>
            cannon is IStation { OccupantClientId: { } occupant } && occupant == _network.LocalClientId &&
            _actors.GetLocalPlayerActor<IHumanoidCharacterView>() != null;

        private void Receive(ICannon cannon, CannonShotEvent shotEvent)
        {
            if (!shotEvent.HasFired)
            {
                var glow = TakeGlow(_config.MuzzleColor);
                glow.localScale = Vector3.zero;
                _charges.Add(new ChargeGlow { ShotId = shotEvent.ShotId, Cannon = cannon, View = glow });
                if (_config.FireSound != null && cannon.Muzzle != null)
                    _audio.PlayOneShot(_config.FireSound, AudioChannel.World, cannon.Muzzle.position, _config.FireVolume, 0.04f);
                return;
            }

            int index = _charges.FindIndex(charge => charge.ShotId == shotEvent.ShotId && ReferenceEquals(charge.Cannon, cannon));
            if (index >= 0) ReleaseCharge(index);

            var beam = new Beam { Cannon = cannon, End = shotEvent.End, Line = TakeLine() };
            // A beam that ran out its range hit nothing: no bloom.
            if ((shotEvent.End - shotEvent.Origin).sqrMagnitude < (_config.BeamRange - 0.5f) * (_config.BeamRange - 0.5f))
            {
                beam.Bloom = TakeGlow(_config.EndColor);
                beam.Bloom.position = shotEvent.End;
            }
            _beams.Add(beam);
            BeamsShown++;
            Draw(beam, shotEvent.Origin);
        }

        private void AdvanceCharges(float deltaTime)
        {
            for (int i = _charges.Count - 1; i >= 0; i--)
            {
                var charge = _charges[i];
                charge.Age += deltaTime;

                // A lost "fired" message must not leave a glow hanging forever.
                var muzzle = charge.Cannon.Muzzle;
                if (muzzle == null || charge.Age > _config.ChargeSeconds + 1f)
                {
                    ReleaseCharge(i);
                    continue;
                }

                float share = _config.ChargeSeconds > 0f ? Mathf.Clamp01(charge.Age / _config.ChargeSeconds) : 1f;
                charge.View.position = muzzle.position;
                charge.View.localScale = Vector3.one * (_config.GlowDiameter * share * share);
            }
        }

        private void AdvanceBeams(float deltaTime)
        {
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                var beam = _beams[i];
                beam.Age += deltaTime;
                if (beam.Age >= _config.BeamSeconds)
                {
                    ReleaseBeam(i);
                    continue;
                }

                Draw(beam, null);
            }
        }

        // From this peer's muzzle (or the server's origin when the cannon is gone) to the server's end, thinning out.
        private void Draw(Beam beam, Vector3? fallbackOrigin)
        {
            float left = 1f - Mathf.Clamp01(beam.Age / _config.BeamSeconds);
            var muzzle = beam.Cannon.Muzzle;
            Vector3 origin = muzzle != null ? muzzle.position : fallbackOrigin ?? beam.Line.GetPosition(0);
            beam.Line.SetPosition(0, origin);
            beam.Line.SetPosition(1, beam.End);
            beam.Line.widthMultiplier = _config.BeamWidth * left;
            if (beam.Bloom != null) beam.Bloom.localScale = Vector3.one * (_config.GlowDiameter * (0.6f + 0.8f * (1f - left)) * left);
        }

        private Transform Root => _root ??= new GameObject("CannonShots").transform;

        private Transform TakeGlow(Color color)
        {
            Transform glow;
            if (_glowPool.Count > 0)
            {
                glow = _glowPool.Pop();
            }
            else
            {
                var view = new GameObject("BeamGlow", typeof(MeshFilter), typeof(MeshRenderer));
                view.transform.SetParent(Root, false);
                view.GetComponent<MeshFilter>().sharedMesh = GlowMesh();
                var renderer = view.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (_config.BeamMaterial != null) renderer.sharedMaterial = _config.BeamMaterial;
                glow = view.transform;
            }

            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            glow.GetComponent<MeshRenderer>().SetPropertyBlock(_block);
            glow.gameObject.SetActive(true);
            return glow;
        }

        // The built-in sphere with white vertex colours: the beam's particle shader multiplies by them.
        private Mesh GlowMesh()
        {
            if (_glowMesh != null) return _glowMesh;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _glowMesh = Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(sphere);
            _glowMesh.name = "BeamGlow";
            var colors = new Color[_glowMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            _glowMesh.colors = colors;
            return _glowMesh;
        }

        private LineRenderer TakeLine()
        {
            if (_linePool.Count > 0)
            {
                var pooled = _linePool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var line = new GameObject("Beam").AddComponent<LineRenderer>();
            line.transform.SetParent(Root, false);
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.startColor = _config.MuzzleColor;
            line.endColor = _config.EndColor;
            if (_config.BeamMaterial != null) line.sharedMaterial = _config.BeamMaterial;
            return line;
        }

        private void ReleaseCharge(int index)
        {
            var charge = _charges[index];
            _charges.RemoveAt(index);
            ReleaseGlow(charge.View);
        }

        private void ReleaseBeam(int index)
        {
            var beam = _beams[index];
            _beams.RemoveAt(index);
            if (beam.Bloom != null) ReleaseGlow(beam.Bloom);
            if (beam.Line == null) return;
            beam.Line.gameObject.SetActive(false);
            _linePool.Push(beam.Line);
        }

        private void ReleaseGlow(Transform? glow)
        {
            if (glow == null) return;
            glow.gameObject.SetActive(false);
            _glowPool.Push(glow);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            if (_glowMesh != null) Object.Destroy(_glowMesh);
            _charges.Clear();
            _beams.Clear();
            _glowPool.Clear();
            _linePool.Clear();
        }
    }
}
