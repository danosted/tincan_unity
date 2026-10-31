#nullable enable
namespace TinCan.Core.Domain.Navigation
{
    /// <summary>
    /// What an obstacle on the chart is: solid rock to fly around, over or under, a zone of danger, or a gas pocket
    /// (it goes off on the hull and breaks what is near).
    /// </summary>
    public enum ChartObstacleKind
    {
        Rock,
        Hazard,
        Gas
    }
}
