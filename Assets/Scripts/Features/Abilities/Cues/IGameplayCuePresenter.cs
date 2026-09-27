#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// The Unity side of cue actions: pooled prefab instances, one-shot sounds and timed HUD text. Actions stay thin
    /// data objects that call this, so they can be tested with a fake.
    /// </summary>
    public interface IGameplayCuePresenter
    {
        /// <summary>A pooled instance under <paramref name="parent"/> (ship-local) that returns to the pool after <paramref name="seconds"/>.</summary>
        void SpawnTimed(GameObject prefab, Transform parent, Vector3 localOffset, float seconds);

        /// <summary>A pooled instance held until <see cref="Release"/> for the same key; spawning an existing key replaces it.</summary>
        void SpawnHeld(GameplayCueInstanceKey key, GameObject prefab, Transform parent, Vector3 localOffset);

        void Release(GameplayCueInstanceKey key);

        /// <summary>Returns everything held for an actor to the pool (the actor is despawning).</summary>
        void ReleaseActor(Guid actorId);

        void PlaySound(AudioClip clip, Vector3 position, float volume);

        /// <summary>Shows a HUD line under <paramref name="key"/> for <paramref name="seconds"/>; showing it again restarts the time.</summary>
        void ShowHudText(string key, string text, float seconds);
    }
}
