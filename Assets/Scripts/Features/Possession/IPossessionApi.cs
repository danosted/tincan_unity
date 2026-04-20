using System;

namespace TinCan.Features.Possession
{
    public interface IPossessionApi
    {
        event Action<IPossessable, ulong> OnPossessionReceived;
        event Action<IPossessable, ulong> OnPossessionLost;
        void RequestPossession(PossessionRequest.Request request);
    }
}
