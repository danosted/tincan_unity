#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// An edit's outcome: the new design and the edit that undoes it, or the unchanged design and why the edit was
    /// refused.
    /// </summary>
    public sealed class ShipDesignEditResult
    {
        private ShipDesignEditResult(ShipDesign design, ShipDesignEdit? undo, string? refusal, int addedInstanceId)
        {
            Design = design;
            Undo = undo;
            Refusal = refusal;
            AddedInstanceId = addedInstanceId;
        }

        public ShipDesign Design { get; }
        public ShipDesignEdit? Undo { get; }
        public string? Refusal { get; }
        public bool Applied => Refusal == null;

        /// <summary>The part a Place or Restore added (0 for a Remove or a refusal).</summary>
        public int AddedInstanceId { get; }

        public static ShipDesignEditResult Done(ShipDesign design, ShipDesignEdit undo, int addedInstanceId = 0) =>
            new(design, undo, null, addedInstanceId);

        public static ShipDesignEditResult Refused(ShipDesign design, string refusal) => new(design, null, refusal, 0);

        public override string ToString() => Applied ? $"applied (undo: {Undo})" : $"refused: {Refusal}";
    }
}
