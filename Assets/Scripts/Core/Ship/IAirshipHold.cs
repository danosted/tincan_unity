#nullable enable
namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: something a feature holds a ship with (the anchor; plan .docs/plans/peaceful-mode.md, P1). While any
    /// registered source holds a ship, <see cref="AirshipMovementUseCase"/> keeps it still where it is: no engine, no
    /// flight forces, no momentum, and it starts from rest when let go. The pilot's input is still read and kept on the
    /// ship. Register with <c>.As&lt;IAirshipHold&gt;()</c>; with none, ships are never held.
    /// </summary>
    public interface IAirshipHold
    {
        bool Holds(IAirshipView ship);
    }
}
