#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>Marks an island's object, so a physics query can tell island rock from anything else it overlaps.</summary>
    public sealed class SkyIslandBody : MonoBehaviour
    {
        public SkyIslandId Id { get; set; }
    }
}
