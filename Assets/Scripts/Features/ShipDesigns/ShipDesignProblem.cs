#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>One thing wrong with a design. <see cref="InstanceId"/> is the part it concerns, or 0 for the whole design.</summary>
    public readonly struct ShipDesignProblem
    {
        public ShipDesignProblem(ShipDesignProblemKind kind, int instanceId, string message)
        {
            Kind = kind;
            InstanceId = instanceId;
            Message = message;
        }

        public ShipDesignProblemKind Kind { get; }
        public int InstanceId { get; }
        public string Message { get; }

        /// <summary>A blocking problem stops the design from being flown; an unknown part only stops that part being built.</summary>
        public bool IsBlocking => Kind != ShipDesignProblemKind.UnknownPart;

        public override string ToString() => $"{Kind}: {Message}";
    }
}
