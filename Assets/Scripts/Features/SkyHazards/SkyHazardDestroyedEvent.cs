#nullable enable
namespace TinCan.Features.SkyHazards
{
    /// <summary>Server: a hazard was shot down.</summary>
    public readonly struct SkyHazardDestroyedEvent
    {
        public readonly string Hazard;
        public readonly int Destroyed;

        public SkyHazardDestroyedEvent(string hazard, int destroyed)
        {
            Hazard = hazard;
            Destroyed = destroyed;
        }
    }
}
