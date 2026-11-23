#nullable enable
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Server: targets a feature puts in the beam's way that are not colliders (the storm's heart targets). When a cannon
    /// fires, <see cref="CannonFireUseCase"/> asks every registered source how far along the beam its first target is;
    /// the nearest answer, if nearer than anything solid, ends the beam there and that source answers the hit in its own
    /// way. So a new use of the beam is a new kind of target, never a change to the cannon (plan:
    /// .docs/plans/ray-cannons.md, R2). Register with <c>.As&lt;ISpiritBeamTargets&gt;()</c>.
    /// </summary>
    public interface ISpiritBeamTargets
    {
        /// <summary>Metres along the beam to the first of this source's targets it passes, or null for none.</summary>
        /// <param name="direction">Normalized.</param>
        /// <param name="length">How far the beam reaches (to its range, or to the first solid thing).</param>
        float? FirstHit(Vector3 origin, Vector3 direction, float length);

        /// <summary>The beam ended on this source's target <paramref name="distance"/> metres along it: answer the hit.</summary>
        void Answer(Vector3 origin, Vector3 direction, float distance);
    }
}
