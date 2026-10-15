#nullable enable
namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// The loadout file: <c>&lt;design key&gt;.fittings.json</c> beside the design's <c>&lt;design key&gt;.ship.json</c>. The
    /// shared key is the link; the design file never names it. Every file carries <c>formatVersion</c>; a change to the
    /// format raises <see cref="CurrentVersion"/> and teaches <see cref="ShipFittingLoadoutJsonCodec"/> the old one.
    /// </summary>
    public static class ShipFittingLoadoutFormat
    {
        public const int CurrentVersion = 1;
        public const string FileExtension = ".fittings.json";
        public const int MaxTextLength = 256 * 1024;
        public const int MaxFittings = 1024;

        public const string FormatVersionField = "formatVersion";
        public const string FittingsField = "fittings";
        public const string PartField = "part";
        public const string SocketField = "socket";
        public const string FittingField = "fitting";
    }
}
