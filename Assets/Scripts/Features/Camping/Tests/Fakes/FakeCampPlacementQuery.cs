#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping.Tests.Fakes
{
    /// <summary>The world as the test sets it: ground (or none) wherever the aim goes, and room or not.</summary>
    public sealed class FakeCampPlacementQuery : ICampPlacementQuery
    {
        public CampGround? Ground { get; set; } = new CampGround(new Vector3(0f, 0f, 2f), Vector3.up, onShip: false);
        public bool Room { get; set; } = true;
        public int RoomAsked { get; private set; }
        public float LastRadius { get; private set; }

        public bool TryAim(Vector3 eye, Vector3 direction, float reach, Transform ignore, out CampGround ground)
        {
            ground = Ground ?? default;
            return Ground != null;
        }

        public bool HasRoom(Vector3 point, float radius, float height, Transform ignore)
        {
            RoomAsked++;
            LastRadius = radius;
            return Room;
        }
    }
}
