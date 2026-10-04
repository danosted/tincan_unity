#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>What <see cref="ShipDesignValidator"/> found. A design with no blocking problem can be flown.</summary>
    public sealed class ShipDesignValidation
    {
        public ShipDesignValidation(IReadOnlyList<ShipDesignProblem> problems) => Problems = problems;

        public IReadOnlyList<ShipDesignProblem> Problems { get; }

        public bool IsValid => Problems.All(p => !p.IsBlocking);

        public bool Has(ShipDesignProblemKind kind) => Problems.Any(p => p.Kind == kind);

        public override string ToString() =>
            Problems.Count == 0 ? "valid" : string.Join("; ", Problems.Select(p => p.ToString()));
    }
}
