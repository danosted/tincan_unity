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
    /// When the scene runs a day (an <see cref="ISunSource"/>, the DayCycle feature) and the look has day keys, the sun
    /// follows it and the look's light, the air and the painted sky (<see cref="LookProfile.Sky"/>,
    /// <see cref="SkyShaderGlobals"/>) follow the sun's height (<see cref="DayLightProcessor"/>).
    /// A weather (<see cref="IAtmosphereWeather"/>, the storm) then blends the light and the air toward its own look by
    /// its weight (<see cref="WeatherLookProcessor"/>), before a cloud's whiteout.
    /// Leaving restores the shaders' plain fog and the materials' own shading. Plans: .docs/plans/look-slice.md (L2),
    /// .docs/plans/artstyle-pipeline-breakdown.md, .docs/plans/day-cycle.md (D2).
    /// </summary>
    public sealed class AtmospherePresenter : IAtmosphereOwner, IStartable, ILateTickable, IDisposable
    {
        private const int VolumePriority = 10;


        private readonly LookProfile _profile;
        private readonly ISurfaceStyle _surface;
        private readonly AtmosphereProcessor _processor;
        private readonly DayLightProcessor _dayLight;
        private readonly ILocalViewCamera _view;
        private readonly IObjectResolver _resolver;
        private IViewSubmersion? _submersion;
        private ISunSource? _day;
        private IAtmosphereWeather? _weather;
        private GameObject? _volume;
        private Light? _sun;
        private Camera? _postCamera;

        public AtmospherePresenter(LookProfile profile, ISurfaceStyle surface, AtmosphereProcessor processor, DayLightProcessor dayLight,
            ILocalViewCamera view, IObjectResolver resolver)
        {
            _profile = profile;
            _surface = surface;
            _processor = processor;
            _dayLight = dayLight;
            _view = view;
            _resolver = resolver;
        }

        /// <summary>The frame applied last.</summary>
        public AtmosphereFrame Current { get; private set; }

        public void Start()
        {
            // Resolved here, not in the constructor: CloudBoundary's view asks for the owner while it is being injected.
            _resolver.TryResolve(out _submersion);
            // The moving day (DayCycle), when the scene loads it; otherwise the look's fixed sun.
            _resolver.TryResolve(out _day);
            // A weather (the Storm feature), when the scene loads one: it blends the look after the day.
            _resolver.TryResolve(out _weather);
            if (_profile.Sky != null) RenderSettings.skybox = _profile.Sky;
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
            bool day = _day != null && _profile.Day.Length > 0;
            var light = day ? _dayLight.Evaluate(_profile.Day, _day!.Elevation) : default;
            if (day) look = _dayLight.Apply(look, light);
            if (_weather != null) look = WeatherLookProcessor.Apply(look, _weather.Look, _weather.Weight, _weather.Flash);
            var frame = _processor.Evaluate(look, _submersion?.Whiteout ?? 0f, _submersion?.LightMultiplier ?? 1f);
            Current = frame;

            AtmosphereShaderGlobals.Apply(frame, look);
            AtmosphereShaderGlobals.ApplyWhiteout(
                AtmosphereProcessor.SkyWhiteout(_submersion?.Whiteout ?? 0f, look.WhiteoutDensity, look.MaxFog));
            AtmosphereShaderGlobals.ApplyWeatherShade(_weather?.Look.CloudShade ?? Color.white, _weather?.Weight ?? 0f);
            SkyShaderGlobals.SetEnabled(day);
            if (day) SkyShaderGlobals.Apply(light);
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
                if (_day != null) _sun.transform.rotation = SunRotation(_day.ToSun);
            }

            if (camera != null && camera != _postCamera)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                _postCamera = camera;
            }
        }

        /// <summary>A directional light's rotation shining from <paramref name="toSun"/> (its forward is the light's way).</summary>
        public static Quaternion SunRotation(Vector3 toSun) =>
            Quaternion.LookRotation(-toSun, Mathf.Abs(toSun.y) > 0.999f ? Vector3.forward : Vector3.up);

        public void Dispose()
        {
            AtmosphereShaderGlobals.SetEnabled(false);
            AtmosphereShaderGlobals.ApplyWeatherShade(Color.white, 0f);
            LookShaderGlobals.SetEnabled(false);
            SkyShaderGlobals.SetEnabled(false);
            if (_volume != null) Object.Destroy(_volume);
        }
    }
}
