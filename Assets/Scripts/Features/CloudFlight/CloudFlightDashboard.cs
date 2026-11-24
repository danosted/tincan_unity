#nullable enable
using System;
using System.Globalization;
using System.Text;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace TinCan.Features.CloudFlight
{
    /// <summary>
    /// Developer readout, top right on the Dev layer, while the config's Show Dashboard is on: the cloud flight's numbers
    /// for the first ship (speed = engine + momentum, whether it is charging or spending, height above the
    /// rest height, the zone, stall, the vertical push (the cloud's lift and the fall), and how high the last launch went). The numbers live on the
    /// server, so it shows on the host only.
    /// </summary>
    public sealed class CloudFlightDashboard : ITickable, IDisposable
    {
        public const string UssClassName = "tc-cloud-flight-dashboard";
        private const int BarWidth = 30;

        private readonly CloudFlightUseCase _flight;
        private readonly CloudFlightConfig _config;
        private readonly IUiLayers _layers;
        private readonly StringBuilder _text = new();
        private Label? _label;
        private float _peak = float.NegativeInfinity;
        private float _lastPeak = float.NaN;

        public CloudFlightDashboard(CloudFlightUseCase flight, CloudFlightConfig config, IUiLayers layers)
        {
            _flight = flight;
            _config = config;
            _layers = layers;
        }

        public void Tick()
        {
            CloudFlightReading reading = default;
            bool shown = _config.ShowDashboard && First(out reading);
            if (_label != null) _label.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;

            Track(reading);
            (_label ??= Create()).text = Describe(reading);
        }

        public void Dispose() => _label?.RemoveFromHierarchy();

        private bool First(out CloudFlightReading first)
        {
            foreach (var (ship, reading) in _flight.Readings.Values)
            {
                if (ship.Transform == null) continue;
                first = reading;
                return true;
            }

            first = default;
            return false;
        }

        // The highest point of the flight out of the cloud; kept as the last launch once the ship is back in.
        private void Track(CloudFlightReading reading)
        {
            if (reading.InCloud < 0.5f)
            {
                _peak = Mathf.Max(_peak, reading.Height);
                return;
            }

            if (_peak > _config.CloudBlend) _lastPeak = _peak;
            _peak = float.NegativeInfinity;
        }

        private string Describe(CloudFlightReading r)
        {
            var c = CultureInfo.InvariantCulture;
            float nose = Mathf.Asin(Mathf.Clamp(r.Slope, -1f, 1f)) * Mathf.Rad2Deg;
            string charge = r.Charge > 0.3f ? "CHARGING" : r.Charge < -0.3f ? "SPENDING" : "steady";

            _text.Clear();
            _text.Append("CLOUD FLIGHT   ").Append(Zone(r)).Append('\n');
            _text.Append(string.Format(c, "speed     {0,5:0.0} m/s  (engine {1:0.0} + momentum {2:+0.0;-0.0})\n", r.Speed, r.Engine, r.Momentum));
            _text.Append("          ").Append(Bar(r.Speed / _config.MaxSpeed, _config.StallSpeed / _config.MaxSpeed)).Append('\n');
            _text.Append(string.Format(c, "momentum  {0,5:+0.0;-0.0} m/s²  {1}\n", r.Charge, charge));
            _text.Append(string.Format(c, "nose      {0,5:+0;-0}°   (sea {1:+0;-0;0}°, free climb up to {2:0}°)\n", nose, -r.Force.Level, _config.FreeClimb));
            _text.Append(string.Format(c, "height    {0,5:+0.0;-0.0} m above rest  (in cloud {1:0}%)\n", r.Height, r.InCloud * 100f));
            _text.Append(string.Format(c, "vertical  {0,5:+0.0;-0.0} m/s  (cloud lift {1:+0.0;-0.0}, fall {2:0.0})  stall {3:0}%\n", r.Force.Vertical, r.Heave, r.Fall, r.Stall * 100f));
            _text.Append("launch    ");
            if (_peak > float.NegativeInfinity) _text.Append(string.Format(c, "{0:0} m high so far", _peak));
            else if (!float.IsNaN(_lastPeak)) _text.Append(string.Format(c, "last went {0:0} m high", _lastPeak));
            else _text.Append("none yet");
            return _text.ToString();
        }

        private static string Zone(CloudFlightReading r)
        {
            string zone = r.InCloud >= 0.99f ? "IN THE CLOUD" : r.InCloud > 0f ? "ON THE TOPS" : r.Thin > 0f ? "THIN AIR" : "OPEN AIR";
            return r.Stall > 0.5f ? zone + "  -  STALLING" : zone;
        }

        // A text bar of the speed, with the stall speed marked.
        private static string Bar(float fill, float stall)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(fill) * BarWidth);
            int mark = Mathf.RoundToInt(Mathf.Clamp01(stall) * BarWidth);
            var bar = new StringBuilder("[");
            for (int i = 0; i < BarWidth; i++) bar.Append(i == mark ? '|' : i < filled ? '#' : '.');
            return bar.Append(']').ToString();
        }

        private Label Create()
        {
            var label = new Label { name = "cloud-flight-dashboard", pickingMode = PickingMode.Ignore };
            label.AddToClassList(UssClassName);
            _layers.Root(UiLayer.Dev).Add(label);
            return label;
        }
    }
}
