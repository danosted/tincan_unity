#nullable enable
using TinCan.Features.Encounters;
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// A storm zone's heart, as every peer sees it (replicated by <see cref="StormHeartNetworkMediator"/>, which sits on
    /// the encounter zone's object beside its mediator): how strong the storm still is, the height the heart floats at,
    /// and which target slot was hit last. Written by the server (<see cref="StormCureUseCase"/>).
    /// </summary>
    public interface IStormHeart
    {
        /// <summary>1 raging, falling with each hit to 0 cured.</summary>
        float Strength { get; }

        /// <summary>World height of the heart: a little above the ship that sailed in (StormConfig.HeartAboveShip).</summary>
        float Altitude { get; }

        /// <summary>The slot whose target was hit (spent for the rest of its slot), or -1.</summary>
        long HitSlot { get; }

        /// <summary>Server: a round begins: raging, nothing hit, the heart at this height.</summary>
        void ServerReset(float altitude);

        /// <summary>Server: the target of <paramref name="slot"/> was hit; the storm is down to <paramref name="strength"/>.</summary>
        void ServerHit(long slot, float strength);
    }

    /// <summary>Finds a zone's heart: the component beside its mediator, or none (a zone that is not a storm, a test fake).</summary>
    public static class StormHearts
    {
        public static IStormHeart? Of(IEncounterZone? zone) =>
            zone is Component component && component != null && component.TryGetComponent(out IStormHeart heart) ? heart : null;
    }
}
