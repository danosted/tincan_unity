#nullable enable
namespace TinCan.Features.Voyage
{
    public static class VoyagePhaseExtensions
    {
        /// <summary>Arrived or Lost: the end screen shows and the world holds still until Restart.</summary>
        public static bool IsOver(this VoyagePhase phase) => phase is VoyagePhase.Arrived or VoyagePhase.Lost;
    }
}
