#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// Presentation, every peer, after the frame's updates: the one owner of the scene's air and light
    /// (<see cref="IAtmosphereOwner"/>). Each frame it folds the local view's cloud submersion (<see cref="IViewSubmersion"/>,
    /// from CloudBoundary when the scene has it) into the look and applies it: the banded fog the TinCan shaders read
    /// (Shaders/Include/TinCanAtmosphere.hlsl), a matching plain fog for every other shader, the three-colour ambient,
    /// and the sun's colour, intensity and angle. A global volume carries the look's post effects, and the local camera
    /// gets post-processing turned on. The look's shading and surface style go to the shaders too
    /// (<see cref="LookShaderGlobals"/>), every frame, so editing the profile or the style asset in Play shows at once.
    /// Leaving restores the shaders' plain fog and the materials' own shading. Plans: .docs/plans/look-slice.md (L2),
    /// .docs/plans/artstyle-pipeline-breakdown.md.
    /// </summary>
    public sealed class AtmospherePresenter : IAtmosphereOwner, IStartable, ILateTickable, IDisposable
    {
        private const int VolumePriority = 10;


        private readonly LookProfile _profile;
        private readonly ISurfaceStyle _surface;
        private readonly AtmosphereProcessor _processor;
        private readonly ILocalViewCamera _view;
        private readonly IObjectResolver _resolver;
        private IViewSubmersion? _submersion;
        private GameObject? _volume;
        private Light? _sun;
        private Camera? _postCamera;

        public AtmospherePresenter(LookProfile profile, ISurfaceStyle surface, AtmosphereProcessor processor, ILocalViewCamera view, IObjectResolver resolver)
        {
            _profile = profile;
            _surface = surface;
            _processor = processor;
            _view = view;
            _resolver = resolver;
        }

        /// <summary>The frame applied last.</summary>
        public AtmosphereFrame Current { get; private set; }

        public void Start()
        {
            // Resolved here, not in the constructor: CloudBoundary's view asks for the owner while it is being injected.
            _resolver.TryResolve(out _submersion);
            // The scene's sun is the one its lighting settings name (Window > Rendering > Lighting > Sun Source).
            _sun = RenderSettings.sun;
            if (_sun == null)
            {
                Debug.LogWarning("[AtmospherePresenter] The scene names no sun (Lighting > Sun Source); the look leaves the light alone.");
            }
            else
            {
                var angles = _profile.Settings.SunAngles;
                _sun.transform.rotation = Quaternion.Euler(angles.x, angles.y, 0f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fogMode = FogMode.Exponential;
            AtmosphereShaderGlobals.SetEnabled(true);
            LookShaderGlobals.SetEnabled(true);

            if (_profile.Post != null)
            {
                _volume = new GameObject("Look Volume");
                var volume = _volume.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = VolumePriority;
                volume.sharedProfile = _profile.Post;
            }
        }

        public void LateTick()
        {
            var look = _profile.Settings;
            var frame = _processor.Evaluate(look, _submersion?.Whiteout ?? 0f, _submersion?.LightMultiplier ?? 1f);
            Current = frame;

            AtmosphereShaderGlobals.Apply(frame, look);
            LookShaderGlobals.Apply(_profile.Shading, _surface.Evaluate());


            var camera = _view.Camera;
            float viewHeight = camera != null ? camera.transform.position.y : look.FogBaseHeight;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(frame.AwayFog, frame.SunwardFog, 0.35f);
            RenderSettings.fogDensity = frame.UniformDensityAt(viewHeight, look.FogBaseHeight);

            RenderSettings.ambientSkyColor = frame.AmbientSky;
            RenderSettings.ambientEquatorColor = frame.AmbientHorizon;
            RenderSettings.ambientGroundColor = frame.AmbientGround;

            if (_sun != null)
            {
                _sun.color = frame.SunColour;
                _sun.intensity = frame.SunIntensity;
            }

            if (camera != null && camera != _postCamera)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                _postCamera = camera;
            }
        }

        public void Dispose()
        {
            AtmosphereShaderGlobals.SetEnabled(false);
            LookShaderGlobals.SetEnabled(false);
            if (_volume != null) Object.Destroy(_volume);
        }
    }
}
