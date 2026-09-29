#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Gas.Cues;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records what cue actions asked the presenter for, as readable lines.</summary>
    public sealed class RecordingCuePresenter : IGameplayCuePresenter
    {
        public List<string> Calls { get; } = new();
        public List<GameplayCueInstanceKey> Held { get; } = new();
        public List<GameplayCueInstanceKey> Released { get; } = new();

        public void SpawnTimed(GameObject prefab, Transform parent, Vector3 localOffset, float seconds) => Calls.Add($"timed {prefab.name} {seconds}");

        public void SpawnHeld(GameplayCueInstanceKey key, GameObject prefab, Transform parent, Vector3 localOffset)
        {
            Held.Add(key);
            Calls.Add($"held {prefab.name}");
        }

        public void Release(GameplayCueInstanceKey key)
        {
            Released.Add(key);
            Calls.Add("release");
        }

        public void ReleaseActor(Guid actorId) => Calls.Add("release actor");

        public void PlaySound(AudioClip clip, Vector3 position, float volume) => Calls.Add($"sound {clip.name} {volume}");

        public void ShowHudText(string key, string text, float seconds) => Calls.Add($"hud {key} {seconds}");
    }
}
