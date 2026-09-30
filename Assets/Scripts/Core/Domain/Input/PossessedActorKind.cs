#nullable enable
namespace TinCan.Core.Domain.Input
{
    /// <summary>The kinds of actor a context can follow through possession (<see cref="InputContextActivation.WhilePossessing"/>).</summary>
    public enum PossessedActorKind
    {
        /// <summary>A player's body (<see cref="IHumanoidActor"/>).</summary>
        Humanoid = 0,
        /// <summary>The airship (<see cref="IShipActor"/>).</summary>
        Ship = 1,
        /// <summary>Anything else that can be possessed, such as the free camera.</summary>
        Other = 2,
    }
}
