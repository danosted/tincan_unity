using System;

namespace TinCan.Features.Possession
{
    public interface IPossessionApi
    {
        event Action<IPossessable, ulong> OnPossessionChanged;
        void RequestPossession(PossessionRequest.Request request);
    }
}
