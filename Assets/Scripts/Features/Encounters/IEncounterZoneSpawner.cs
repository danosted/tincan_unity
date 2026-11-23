#nullable enable
using UnityEngine;

namespace TinCan.Features.Encounters
{
    /// <summary>Server: puts an encounter zone into the world (a networked spawn; faked in tests).</summary>
    public interface IEncounterZoneSpawner
    {
        void Spawn(GameObject prefab, Vector3 position);
    }
}
