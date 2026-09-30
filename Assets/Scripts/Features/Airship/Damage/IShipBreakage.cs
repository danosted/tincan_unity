#nullable enable
namespace TinCan.Features.Airship.Damage
{
    /// <summary>Server-side control over ship damage, for other features (repair) and dev tooling (scenarios).</summary>
    public interface IShipBreakage
    {
        /// <summary>Random breakage on/off; starts from <see cref="ShipDamageConfig.AutoBreak"/>.</summary>
        bool AutoBreak { get; set; }
        int BrokenCount { get; }
        bool TryBreak(int pointIndex);
        bool TryRestore(int pointIndex);
    }
}
