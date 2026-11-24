#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Atmosphere;
using TinCan.Features.CloudBoundary.Atmosphere;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Every peer with a view, presentation only: the storm as the atmosphere's weather (<see cref="IAtmosphereWeather"/>)
    /// and the volumetric clouds' (<see cref="ICloudWeather"/>). Each frame it reads the storm's depth, strength and region
    /// (<see cref="IStormIntensity"/>), weighs the config's look by them, gives the shaders the region
    /// (<see cref="StormShaderGlobals"/>), and runs the lightning on the shared clock (<see cref="StormProcessor"/>): a
    /// strike at a spot in the storm (the same on every peer) lights the cloud round it, so the storm flickers from afar,
    /// flashes the air round you as deep as you are in it, and asks <see cref="StormSoundPresenter"/> for its thunder,
    /// later, fainter and more muffled the farther it is.
    /// </summary>
    public sealed class StormWeatherPresenter : IAtmosphereWeather, ICloudWeather, ITickable, System.IDisposable
    {
        private readonly StormConfig _config;
        private readonly IStormIntensity _intensity;
        private readonly ITimeService _time;
        private readonly StormSoundPresenter _sound;
        private int _lastSlot = int.MinValue;
        private float _strikeAt = float.NegativeInfinity;
        private Vector3 _strikeSpot;

        public StormWeatherPresenter(StormConfig config, IStormIntensity intensity, ITimeService time, StormSoundPresenter sound)
        {
            _config = config;
            _intensity = intensity;
            _time = time;
            _sound = sound;
        }

        public float Weight { get; private set; }
        public WeatherLook Look => _config.Look;
        public float Flash { get; private set; }
        public CloudWeatherTarget Clouds => _config.Clouds;

        public void Tick() => Step(_time.Time);

        public void Dispose() => StormShaderGlobals.Clear();

        /// <summary>One frame at the shared clock's <paramref name="now"/>.</summary>
        public void Step(float now)
        {
            Weight = StormProcessor.Weight(_intensity.Depth, _intensity.Strength);
            var region = _intensity.Region;

            int slot = Mathf.FloorToInt(now / StormProcessor.SlotSeconds);
            if (slot != _lastSlot)
            {
                _lastSlot = slot;
                // In the world the storm strikes as hard as it rages, wherever you watch from; in the lab's slider mode
                // it has no place, so it strikes as deep as you are in it.
                float presence = region != null ? _intensity.Strength : Weight;
                float chance = StormProcessor.StrikeChance(presence, _config.StrikesPerMinute, _config.LightningMinWeight);
                if (StormProcessor.StrikeIn(slot, _config.Seed, chance) is { } offset)
                {
                    _strikeAt = slot * StormProcessor.SlotSeconds + offset;
                    if (region is { } place)
                    {
                        _strikeSpot = StormProcessor.StrikeSpot(place.Centre, place.Radius, _config.Seed, slot);
                        if (_intensity.Camera is { } camera)
                        {
                            float distance = Vector3.Distance(camera, _strikeSpot);
                            _sound.Thunder(_strikeAt + distance / StormProcessor.SpeedOfSound, slot,
                                StormProcessor.ThunderShare(distance), StormProcessor.ThunderCutoff(distance));
                        }
                    }
                    else
                    {
                        _sound.Thunder(_strikeAt + StormProcessor.ThunderDelay(Weight, _config.ThunderDelay, _config.Seed, slot), slot,
                            Mathf.Clamp01(0.4f + Weight), StormProcessor.ThunderCutoff(0f));
                    }
                }
            }

            // The strike lights the cloud round it (seen from anywhere) and the air round you as deep as you are in it.
            float flash = StormProcessor.Flash(now - _strikeAt, _config.FlashSeconds);
            Flash = flash * Weight;
            _sound.Step(now, _intensity.Depth, _intensity.Strength, Weight);

            if (region is { } shown)
            {
                StormShaderGlobals.Apply(shown.Centre, shown.Radius, _intensity.Strength);
                StormShaderGlobals.ApplyFlash(_strikeSpot, _config.Look.FlashColour, flash);
            }
            else StormShaderGlobals.Clear();
        }
    }
}
