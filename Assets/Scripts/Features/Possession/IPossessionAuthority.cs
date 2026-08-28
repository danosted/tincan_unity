using System;
using TinCan.Core.Domain;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Server-side authority for granting possession to an authenticated player actor.
    /// </summary>
    public interface IPossessionAuthority
    {
        bool TryAcquirePossession(Guid requesterActorId, IPossessable target);
        bool TryReleasePossession(Guid requesterActorId);
    }
}
