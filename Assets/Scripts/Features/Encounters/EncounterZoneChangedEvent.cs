#nullable enable

namespace TinCan.Features.Encounters
{
    /// <summary>Server: an encounter zone moved on in its round (a ship crossed in, turned back, the event started, ...).</summary>
    public readonly struct EncounterZoneChangedEvent
    {
        public readonly string Zone;
        public readonly EncounterZoneState From;
        public readonly EncounterZoneState To;

        public EncounterZoneChangedEvent(string zone, EncounterZoneState from, EncounterZoneState to)
        {
            Zone = zone;
            From = from;
            To = to;
        }

        public override string ToString() => $"{Zone}: {From} -> {To}";
    }
}
