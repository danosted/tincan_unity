#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Encounters
{
    /// <summary>
    /// Local view, presentation only: the HUD line for the zone the camera is in: "Storm ahead: 6 s" during the cue, "In
    /// the storm" while it runs, "The storm is spent" while it rests. Gone outside every zone.
    /// </summary>
    public sealed class EncounterHudPresenter : ITickable
    {
        public const string Key = "Encounter";

        private readonly IEncounterZones _zones;
        private readonly ILocalViewCamera _view;
        private readonly ITimeService _time;
        private readonly IHudValues? _hud;
        private string _shown = string.Empty;

        public EncounterHudPresenter(IEncounterZones zones, ILocalViewCamera view, ITimeService time, IHudValues? hud)
        {
            _zones = zones;
            _view = view;
            _time = time;
            _hud = hud;
        }

        [Inject]
        public EncounterHudPresenter(IEncounterZones zones, ILocalViewCamera view, ITimeService time, IObjectResolver resolver)
            : this(zones, view, time, resolver.TryResolve<IHudValues>(out var hud) ? hud : null)
        {
        }

        public void Tick()
        {
            var camera = _view.Camera;
            Show(camera != null ? Describe(_zones.ZoneAt(camera.transform.position), _time.Time) : string.Empty);
        }

        /// <summary>The line for being in <paramref name="inZone"/> at <paramref name="now"/>; empty for none.</summary>
        public static string Describe((IEncounterZone Zone, float Depth)? inZone, double now)
        {
            if (inZone is not { } at) return string.Empty;
            var zone = at.Zone;
            string name = zone.DisplayName;
            return zone.State switch
            {
                EncounterZoneState.Warning => $"{name} ahead: {Mathf.CeilToInt((float)(zone.StateEndsAt - now))} s",
                EncounterZoneState.Running => $"In the {name.ToLowerInvariant()}",
                EncounterZoneState.Resting or EncounterZoneState.Gone => $"The {name.ToLowerInvariant()} is spent",
                _ => string.Empty
            };
        }

        private void Show(string text)
        {
            if (text == _shown) return;
            _shown = text;
            if (text.Length == 0) _hud?.Remove(Key);
            else _hud?.Set(Key, text);
        }
    }
}
