#nullable enable
namespace TinCan.Features.Shipyard
{
    /// <summary>One row of the shipyard's controls panel: the keys on the left, what they do on the right.</summary>
    public readonly struct ShipyardControlRow
    {
        public ShipyardControlRow(string keys, string action)
        {
            Keys = keys;
            Action = action;
        }

        public string Keys { get; }
        public string Action { get; }

        public override string ToString() => $"{Keys}: {Action}";
    }
}
