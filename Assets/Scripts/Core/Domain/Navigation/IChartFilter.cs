#nullable enable
namespace TinCan.Core.Domain.Navigation
{
    /// <summary>
    /// Every peer: a feature that hides things on the navigator's chart, after every <see cref="IChartSource"/> has added
    /// them (for example: gas pockets only while a gas detector is mounted, rocks only once surveyed). Something is shown
    /// when every registered filter shows it; with none registered, everything is. Register an implementation with
    /// <c>.As&lt;IChartFilter&gt;()</c>. Decide from replicated or seeded state, so crewmates see the same chart.
    /// Plan: .docs/plans/gas-squall-event.md (G6).
    /// </summary>
    public interface IChartFilter
    {
        bool ShowsObstacle(in ChartObstacle obstacle);

        bool ShowsPoint(in PointOfInterest point);
    }
}
