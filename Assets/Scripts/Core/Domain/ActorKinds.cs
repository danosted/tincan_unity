#nullable enable

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Marks a player's humanoid body, so systems below the humanoid (GAS's ability grants) can recognise one without
    /// referencing it. Extended by <c>IHumanoidCharacterView</c>.
    /// </summary>
    public interface IHumanoidActor : IActor { }

    /// <summary>
    /// Marks the airship, so systems below the ship can recognise it without referencing it. Extended by
    /// <c>IAirshipView</c>.
    /// </summary>
    public interface IShipActor : IActor { }
}
