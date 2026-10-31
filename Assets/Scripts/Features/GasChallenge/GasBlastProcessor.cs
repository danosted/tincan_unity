#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Domain, pure: which of a ship's healthy damage points a gas blast breaks. Those within reach of the blast's
    /// centre, fittings before hull spots, nearest first, at most <c>maxBreaks</c>.
    /// </summary>
    public sealed class GasBlastProcessor
    {
        public IReadOnlyList<int> Pick(Vector3 centre, IEnumerable<BlastTarget> healthy, float reach, int maxBreaks) =>
            healthy
                .Select(target => (target, distance: Vector3.Distance(centre, target.Position)))
                .Where(candidate => candidate.distance <= reach)
                .OrderByDescending(candidate => candidate.target.IsFitting)
                .ThenBy(candidate => candidate.distance)
                .ThenBy(candidate => candidate.target.Index)
                .Take(Mathf.Max(maxBreaks, 0))
                .Select(candidate => candidate.target.Index)
                .ToList();
    }
}
