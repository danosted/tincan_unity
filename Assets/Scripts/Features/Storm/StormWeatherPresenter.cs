#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Atmosphere;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Every peer with a view, presentation only: the storm as the atmosphere's weather (<see cref="IAtmosphereWeather"/>).
    /// Each frame it reads the storm's depth and strength (<see cref="IStormIntensity"/>), weighs the config's look by
    /// them, and runs the lightning on the shared clock (<see cref="StormProcessor"/>): a strike flashes the air and asks
    /// <see cref="StormSoundPresenter"/> for its thunder, later the farther it is.
    /// </summary>
    public sealed class StormWeatherPresenter : IAtmosphereWeather, ITickable
    {
        private readonly StormConfig _config;
        private readonly IStormIntensity _intensity;
        private readonly ITimeService _time;
        private readonly StormSoundPresenter _sound;
        private int _lastSlot = int.MinValue;
        private float _strikeAt = float.NegativeInfinity;

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

        public void Tick() => Step(_time.Time);

        /// <summary>One frame at the shared clock's <paramref name="now"/>.</summary>
        public void Step(float now)
        {
            Weight = StormProcessor.Weight(_intensity.Depth, _intensity.Strength);

            int slot = Mathf.FloorToInt(now / StormProcessor.SlotSeconds);
            if (slot != _lastSlot)
            {
                _lastSlot = slot;
                float chance = StormProcessor.StrikeChance(Weight, _config.StrikesPerMinute, _config.LightningMinWeight);
                if (StormProcessor.StrikeIn(slot, _config.Seed, chance) is { } offset)
                {
                    _strikeAt = slot * StormProcessor.SlotSeconds + offset;
                    _sound.Thunder(_strikeAt + StormProcessor.ThunderDelay(Weight, _config.ThunderDelay, _config.Seed, slot), slot);
                }
            }

            Flash = StormProcessor.Flash(now - _strikeAt, _config.FlashSeconds);
            _sound.Step(now, _intensity.Depth, _intensity.Strength, Weight);
        }
    }
}
