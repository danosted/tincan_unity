#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Features.Stations;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// A ship's chart table: the station a navigator plots the route from (it is an <see cref="IStation"/>). Its
    /// <see cref="Chart"/> is the table top every peer draws the chart on.
    /// </summary>
    public interface IChartTable : IStation, IActor
    {
        /// <summary>The ship whose route this table edits (the one it is fixed to); null before it is parented to one.</summary>
        IAirshipView? Ship { get; }

        /// <summary>The chart's centre on the table top: +y up out of the table, +z toward the table's top edge.</summary>
        Transform? Chart { get; }
    }
}
