#nullable enable
namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Says when only the server moves a humanoid: while any rule holds, its owner stops predicting its own movement and
    /// follows the server's state (snapped, faded on screen), still sending input the server may use. It is for moments the
    /// owner cannot predict well, such as hanging on a leash behind a ship that each peer sees at a different pose. Ask it
    /// with state every peer shares (a replicated tag). Register with <c>.As&lt;IServerDrivenHumanoidRule&gt;()</c>.
    /// </summary>
    public interface IServerDrivenHumanoidRule
    {
        bool IsServerDriven(IHumanoidCharacterView character);
    }
}
