#nullable enable
namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Adds to the local player's predicted input after the body's own controls are read, for a system that owns part
    /// of the input state (a station writes <see cref="HumanoidInputState.StationAim"/>). Owner only, once per gather;
    /// register with <c>.As&lt;IHumanoidInputContributor&gt;()</c>.
    /// </summary>
    public interface IHumanoidInputContributor
    {
        void Contribute(IHumanoidCharacterView character, ref HumanoidInputState input);
    }
}
