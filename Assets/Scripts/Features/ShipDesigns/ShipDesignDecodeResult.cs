#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>A decoded design, or why the text or bytes could not be read. A design is never half-read.</summary>
    public sealed class ShipDesignDecodeResult
    {
        private ShipDesignDecodeResult(ShipDesign? design, string? error)
        {
            Design = design;
            Error = error;
        }

        public ShipDesign? Design { get; }
        public string? Error { get; }
        public bool Succeeded => Design != null;

        public static ShipDesignDecodeResult Read(ShipDesign design) => new(design, null);

        public static ShipDesignDecodeResult Failed(string error) => new(null, error);

        public override string ToString() => Succeeded ? $"read {Design}" : $"failed: {Error}";
    }
}
