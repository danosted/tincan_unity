#nullable enable
using System.Collections.Generic;
using TinCan.Features.Airship;

namespace TinCan.Features.GasChallenge
{
    /// <summary>The live gas pockets a ship touches this tick (a physics query, so nothing scans the scene).</summary>
    public interface IGasPocketQuery
    {
        void Touching(IAirshipView ship, List<GasPocketVolume> results);
    }
}
