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
    /// local occupant a straight line along the barrel. Draws each cannon's beam while it is on: for the local gunner
    /// from the moment they hold Fire (<see cref="GunnerAimUseCase.LocalFiring"/>, so it fires at once on a client too),
    /// for everyone else from the replicated <see cref="ICannon.BeamOn"/>. The beam runs along this peer's barrel, as long
    /// as the server's last pulse reached, and throbs on this peer's own pulse clock (<see cref="CannonConfig.PulseSeconds"/>)
    /// with a thrum and a bloom where it hits something. The muzzle glows from cyan to hot as the heat rises, flickers while
    /// it vents, and hisses when it overheats. Plan: .docs/plans/ray-cannons.md.
    /// </summary>
    public sealed class CannonShotPresenter : ITickable, IDisposable
    {
        private sealed class BeamView
        {
            public ICannon Cannon = null!;
            public float Yaw;
            public float Elevation;
            public bool AimInitialized;

            public bool Firing;
            public float Shown;
            public float Clock;
            public int LastPulse = -1;
            public float Length;
            public float SinceServerPulse = float.MaxValue;
            public bool EndsOnSomething;
            public bool WasOverheated;

            public LineRenderer? Line;
            public Transform? Bloom;
            public Transform? Glow;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly IActorRegistry _actors;
        private readonly INetworkService _network;
        private readonly ITimeService _time;
        private readonly IAudioPlayer _audio;
        private readonly CannonConfig _config;
        private readonly CannonAimProcessor _aim;
        private readonly GunnerAimUseCase _gunner;
        private readonly Dictionary<Guid, BeamView> _views = new();
        private readonly List<ICannon> _cannons = new();
        private readonly List<Guid> _gone = new();
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

        /// <summary>Server pulses received on this peer (scenario probe: the client saw the beam).</summary>
        public int BeamsShown { get; private set; }

        public void Tick()
        {
            float deltaTime = _time.DeltaTime;
            _cannons.Clear();
            _cannons.AddRange(_actors.GetActors<ICannon>());

            foreach (var cannon in _cannons)
            {
                if (cannon.Base == null || cannon.Muzzle == null) continue;
                if (!_views.TryGetValue(cannon.Id, out var view))
                {
                    view = new BeamView { Cannon = cannon, Length = _config.BeamRange };
                    _views[cannon.Id] = view;
                }

                bool local = IsLocallyManned(cannon);
                PresentAim(cannon, view, local, deltaTime);
                while (cannon.TryTakePulse(out var pulse)) Receive(view, pulse);
                PresentBeam(cannon, view, local, deltaTime);
            }

            // Cannons gone (their ship despawned) take their beam with them.
            _gone.Clear();
            foreach (var (id, view) in _views)
            {
                if (view.Cannon is Component component && component == null || view.Cannon.Muzzle == null) _gone.Add(id);
            }
            foreach (var id in _gone)
            {
                Release(_views[id]);
                _views.Remove(id);
            }
        }

        private void PresentAim(ICannon cannon, BeamView view, bool local, float deltaTime)
        {
            if (local && _gunner.LocalAim is { } gunning && gunning.Cannon == cannon.Id)
            {
                // The gunner's live aim (steered every frame from the mouse), not the replicated one, which only moves in
                // simulation ticks: the gunner's camera rides the barrel, so steps would show.
                (view.Yaw, view.Elevation) = _aim.ClampAim(gunning.Aim, _config.AimLimits);
            }
            else if (!view.AimInitialized)
            {
                (view.Yaw, view.Elevation) = (cannon.Yaw, cannon.Elevation);
            }
            else
            {
                float blend = 1f - Mathf.Exp(-_config.ProxyAimSharpness * deltaTime);
                view.Yaw = Mathf.LerpAngle(view.Yaw, cannon.Yaw, blend);
                view.Elevation = Mathf.Lerp(view.Elevation, cannon.Elevation, blend);
            }
            view.AimInitialized = true;

            cannon.ApplyAim(view.Yaw, view.Elevation);

            if (!local)
            {
                cannon.ShowAimPreview(null, 0);
                return;
            }

            _preview[0] = cannon.Muzzle!.position;
            _preview[1] = _preview[0] + Direction(cannon, view) * _config.BeamRange;
            cannon.ShowAimPreview(_preview, 2);
        }

        private bool IsLocallyManned(ICannon cannon) =>
            cannon is IStation { OccupantClientId: { } occupant } && occupant == _network.LocalClientId &&
            _actors.GetLocalPlayerActor<IHumanoidCharacterView>() != null;

        // The server's pulse says how far the beam reached; this peer draws it along its own barrel.
        private void Receive(BeamView view, CannonPulseEvent pulse)
        {
            view.Length = Vector3.Distance(pulse.Origin, pulse.End);
            view.EndsOnSomething = view.Length < _config.BeamRange - 0.5f;
            view.SinceServerPulse = 0f;
            BeamsShown++;
        }

        private void PresentBeam(ICannon cannon, BeamView view, bool local, float deltaTime)
        {
            bool firing = local ? _gunner.LocalFiring && !cannon.Overheated : cannon.BeamOn;
            Vector3 muzzle = cannon.Muzzle!.position;
            view.SinceServerPulse += deltaTime;

            if (firing && !view.Firing)
            {
                view.Clock = 0f;
                view.LastPulse = -1;
            }
            view.Firing = firing;

            float throb = 0f;
            if (firing)
            {
                view.Shown = 1f;
                int pulse = Mathf.FloorToInt(view.Clock / _config.PulseSeconds);
                if (pulse != view.LastPulse)
                {
                    view.LastPulse = pulse;
                    if (_config.PulseSound != null) _audio.PlayOneShot(_config.PulseSound, AudioChannel.World, muzzle, _config.PulseVolume, 0.05f);
                }
                float phase = view.Clock / _config.PulseSeconds - pulse;
                throb = Mathf.Exp(-phase * 5f);
                view.Clock += deltaTime;
            }
            else
            {
                view.Shown = Mathf.Max(0f, view.Shown - deltaTime / _config.FadeSeconds);
            }

            if (cannon.Overheated && !view.WasOverheated && _config.VentSound != null)
                _audio.PlayOneShot(_config.VentSound, AudioChannel.World, muzzle, _config.VentVolume, 0.03f);
            view.WasOverheated = cannon.Overheated;

            DrawBeam(cannon, view, muzzle, throb);
            DrawGlow(cannon, view, muzzle, throb);
        }

        private void DrawBeam(ICannon cannon, BeamView view, Vector3 muzzle, float throb)
        {
            if (view.Shown <= 0f)
            {
                if (view.Line != null) view.Line.gameObject.SetActive(false);
                if (view.Bloom != null) view.Bloom.gameObject.SetActive(false);
                return;
            }

            // Until a pulse of this burst reports back, the beam reaches its range; then as far as the server's last pulse.
            float length = view.SinceServerPulse <= _config.PulseSeconds * 3f ? view.Length : _config.BeamRange;
            bool endsOnSomething = view.SinceServerPulse <= _config.PulseSeconds * 3f && view.EndsOnSomething;
            Vector3 end = muzzle + Direction(cannon, view) * length;

            var line = view.Line ??= MakeLine();
            line.gameObject.SetActive(true);
            line.SetPosition(0, muzzle);
            line.SetPosition(1, end);
            line.widthMultiplier = _config.BeamWidth * view.Shown * (1f - _config.PulseThrob + _config.PulseThrob * throb);

            var bloom = view.Bloom ??= MakeGlow();
            bloom.gameObject.SetActive(endsOnSomething);
            if (!endsOnSomething) return;
            bloom.position = end;
            bloom.localScale = Vector3.one * (_config.GlowDiameter * view.Shown * (0.5f + 0.7f * throb));
            SetColor(bloom, _config.EndColor);
        }

        private void DrawGlow(ICannon cannon, BeamView view, Vector3 muzzle, float throb)
        {
            float heat = cannon.Heat;
            float size = Mathf.Max(view.Shown * (0.45f + 0.4f * throb), heat * 0.7f);
            if (cannon.Overheated) size *= 0.75f + 0.25f * Mathf.Sin(_time.Time * 40f); // venting: it flickers
            var glow = view.Glow ??= MakeGlow();
            glow.gameObject.SetActive(size > 0.01f);
            if (size <= 0.01f) return;

            glow.position = muzzle;
            glow.localScale = Vector3.one * (_config.GlowDiameter * size);
            SetColor(glow, Color.Lerp(_config.MuzzleColor, _config.HotColor, heat * heat));
        }

        private Vector3 Direction(ICannon cannon, BeamView view) => _aim.MuzzleDirection(cannon.Base!.rotation, view.Yaw, view.Elevation);

        private Transform Root => _root ??= new GameObject("CannonShots").transform;

        private Transform MakeGlow()
        {
            var view = new GameObject("BeamGlow", typeof(MeshFilter), typeof(MeshRenderer));
            view.transform.SetParent(Root, false);
            view.GetComponent<MeshFilter>().sharedMesh = GlowMesh();
            var renderer = view.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_config.BeamMaterial != null) renderer.sharedMaterial = _config.BeamMaterial;
            view.SetActive(false);
            return view.transform;
        }

        private void SetColor(Transform glow, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            glow.GetComponent<MeshRenderer>().SetPropertyBlock(_block);
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

        private LineRenderer MakeLine()
        {
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

        private static void Release(BeamView view)
        {
            if (view.Line != null) Object.Destroy(view.Line.gameObject);
            if (view.Bloom != null) Object.Destroy(view.Bloom.gameObject);
            if (view.Glow != null) Object.Destroy(view.Glow.gameObject);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            if (_glowMesh != null) Object.Destroy(_glowMesh);
            _views.Clear();
        }
    }
}
