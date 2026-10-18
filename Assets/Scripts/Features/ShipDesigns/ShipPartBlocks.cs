#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The 1 m blocks placed parts fill (<see cref="ShipPartPose.Blocks"/>), each with its owner's instance id. Parts only
    /// turn in quarter yaws, so blocks stay square to the ship: two overlap when their centres are less than a block apart
    /// on every axis (a hair of slack, so blocks side by side do not), and a point is in a block within half a block of its
    /// centre. Ships carry tens of parts, not thousands, so a list is enough.
    /// </summary>
    public sealed class ShipPartBlocks
    {
        private const float Slack = 0.01f;
        private readonly List<(Vector3 Centre, int Owner)> _blocks = new();

        public int Count => _blocks.Count;

        public IReadOnlyList<(Vector3 Centre, int Owner)> All => _blocks;

        public void Add(Vector3 centre, int owner) => _blocks.Add((centre, owner));

        /// <summary>The first block of another owner that overlaps one centred here, if any.</summary>
        public bool TryGetOverlap(Vector3 centre, int except, out int owner)
        {
            const float reach = ShipPartPose.BlockSize - Slack;
            foreach (var (other, id) in _blocks)
            {
                if (id == except) continue;
                var d = centre - other;
                if (Mathf.Abs(d.x) >= reach || Mathf.Abs(d.y) >= reach || Mathf.Abs(d.z) >= reach) continue;
                owner = id;
                return true;
            }

            owner = 0;
            return false;
        }

        /// <summary>The block a point lies in, if any.</summary>
        public bool TryGetOwnerAt(Vector3 point, out int owner)
        {
            const float half = ShipPartPose.BlockSize * 0.5f;
            foreach (var (centre, id) in _blocks)
            {
                var d = point - centre;
                if (Mathf.Abs(d.x) > half || Mathf.Abs(d.y) > half || Mathf.Abs(d.z) > half) continue;
                owner = id;
                return true;
            }

            owner = 0;
            return false;
        }
    }
}
