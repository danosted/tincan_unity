#nullable enable
namespace TinCan.Core.Domain.Navigation
{
    /// <summary>What an obstacle on the chart is: solid rock to fly around, over or under, or a zone of danger.</summary>
    public enum ChartObstacleKind
    {
        Rock,
        Hazard
    }
}
