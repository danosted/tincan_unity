#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A design as text: the file format (<see cref="ShipDesignJsonCodec"/>). Gameplay code only sees
    /// <see cref="ShipDesign"/>, so the library behind it can change in one class.
    /// </summary>
    public interface IShipDesignCodec
    {
        string Encode(ShipDesign design);

        ShipDesignDecodeResult Decode(string text);
    }
}
