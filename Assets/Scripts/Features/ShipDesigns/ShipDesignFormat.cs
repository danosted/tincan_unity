#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The design file format. Every file carries <c>formatVersion</c>. A change to the format raises
    /// <see cref="CurrentVersion"/>, adds one <see cref="IShipDesignMigration"/> from the old version, and commits a sample of
    /// the old format under <c>Assets/Tests/EditMode/ShipDesigns/</c> that must keep loading.
    /// Version 2 (.docs/plans/ship-frames.md): the frame, and <c>nextId</c> (shared by parts and frame elements) in place of
    /// <c>nextPartInstanceId</c>.
    /// Version 3: a part's <c>pos</c> (frame units, 10 cm) in place of its grid <c>cell</c>, so parts sit anywhere on a frame.
    /// Version 4 (.docs/plans/frame-only-ships.md): a part hangs on a frame element, <c>on</c> {face, x, z} or {node}, in
    /// place of <c>pos</c>; <c>rot</c> is a quarter yaw; edges' <c>rail</c> names a kind; the frame has <c>stairs</c>;
    /// parts a migration could not anchor are kept in <c>dropped</c>.
    /// </summary>
    public static class ShipDesignFormat
    {
        public const int CurrentVersion = 4;
        public const string FileExtension = ".ship.json";

        public const string FormatVersionField = "formatVersion";
        public const string NameField = "name";
        public const string AuthorField = "author";
        public const string NextIdField = "nextId";
        public const string PartsField = "parts";
        public const string FrameField = "frame";

        /// <summary>Version 1's name for <see cref="NextIdField"/>.</summary>
        public const string V1NextPartInstanceIdField = "nextPartInstanceId";

        public const string PartInstanceIdField = "id";
        public const string PartIdField = "part";
        public const string PartAnchorField = "on";
        public const string PartOrientationField = "rot";
        public const string AnchorFaceField = "face";
        public const string AnchorNodeField = "node";
        public const string AnchorXField = "x";
        public const string AnchorZField = "z";

        /// <summary>Parts a migration could not hang on the frame: kept, never built.</summary>
        public const string DroppedField = "dropped";

        /// <summary>Version 3's part position in frame units (whole metres times ten in version 2's files).</summary>
        public const string V3PartPosField = "pos";

        /// <summary>Version 2's grid cell, whole metres; <see cref="V3PartPosField"/> is ten times it.</summary>
        public const string V2PartCellField = "cell";

        public const string FrameBaseField = "base";
        public const string FrameStoriesField = "stories";
        public const string FrameNodesField = "nodes";
        public const string FrameEdgesField = "edges";
        public const string FrameFacesField = "faces";
        public const string FrameStairsField = "stairs";

        public const string ElementIdField = "id";
        public const string NodeStoryField = "story";
        public const string NodePositionField = "pos";
        public const string EdgeAField = "a";
        public const string EdgeBField = "b";
        public const string EdgeRailingField = "rail";
        public const string FaceNodesField = "nodes";
        public const string FaceFillField = "fill";
        public const string StairEdgeField = "edge";
        public const string StairDirField = "dir";

        public const string FillOpen = "open";
        public const string FillPlanks = "planks";

        public const string RailRail = "rail";
        public const string RailGate = "gate";
    }
}
