#nullable enable
namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: who steers a ship, as a source of its input. The helm station is one (a player at the wheel); an AI
    /// helmsman would be another. <see cref="AirshipMovementUseCase"/> asks each registered source in turn and takes the
    /// first answer; with none the ship gets zero input and coasts down. Register with <c>.As&lt;IAirshipPilotInput&gt;()</c>.
    /// </summary>
    public interface IAirshipPilotInput
    {
        bool TryGetInput(IAirshipView ship, out AirshipInputState input);
    }
}
