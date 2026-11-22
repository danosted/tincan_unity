#nullable enable
using UnityEngine;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Where the anchor's rope ladder is, in world space: the line a climber's body follows, from <see cref="Top"/> (the
    /// deck's height, outside the rail) down to <see cref="Foot"/>, and the spot on deck a climber is set down on.
    /// </summary>
    public readonly struct AnchorLadder
    {
        public readonly Vector3 Top;
        public readonly Vector3 Foot;
        public readonly Vector3 DeckPosition;
        public readonly Quaternion DeckRotation;

        public AnchorLadder(Vector3 top, Vector3 foot, Vector3 deckPosition, Quaternion deckRotation)
        {
            Top = top;
            Foot = foot;
            DeckPosition = deckPosition;
            DeckRotation = deckRotation;
        }
    }
}
