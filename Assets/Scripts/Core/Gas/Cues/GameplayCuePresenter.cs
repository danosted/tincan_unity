#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
using TinCan.Core.Domain.Hud;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// Infrastructure for cue actions, on every peer. Prefab instances are pooled per prefab and parented to the cue's
    /// target while in use (ship-local); free ones wait inactive under a pool root. An instance destroyed with its target
    /// is simply dropped. HUD text needs the UI installer and is skipped without it.
    /// </summary>
    public sealed class GameplayCuePresenter : IGameplayCuePresenter, ITickable, IDisposable
    {
        private sealed class Live
        {
            public GameObject Instance = null!;
            public GameObject Prefab = null!;
            public float ExpiresAt; // < 0: held until released
        }

        private readonly ITimeService _time;
        private readonly IHudValues? _hud;
        private readonly Dictionary<GameObject, Stack<GameObject>> _free = new();
        private readonly Dictionary<GameplayCueInstanceKey, Live> _held = new();
        private readonly List<Live> _timed = new();
        private readonly Dictionary<string, float> _hudExpiry = new(StringComparer.Ordinal);
        private readonly List<string> _expiredHud = new();
        private readonly List<GameplayCueInstanceKey> _releasing = new();
        private Transform? _poolRoot;
        private float _now;

        // The HUD is a core service (TinCan.UI's installer always loads); tests may pass none.
        [Inject]
        public GameplayCuePresenter(ITimeService time, IHudValues? hud = null)
        {
            _time = time;
            _hud = hud;
        }

        public int HeldCount => _held.Count;
        public int TimedCount => _timed.Count;

        public void SpawnTimed(GameObject prefab, Transform parent, Vector3 localOffset, float seconds)
        {
            var live = Take(prefab, parent, localOffset);
            live.ExpiresAt = _now + Mathf.Max(0f, seconds);
            _timed.Add(live);
        }

        public void SpawnHeld(GameplayCueInstanceKey key, GameObject prefab, Transform parent, Vector3 localOffset)
        {
            Release(key);
            var live = Take(prefab, parent, localOffset);
            live.ExpiresAt = -1f;
            _held[key] = live;
        }

        public void Release(GameplayCueInstanceKey key)
        {
            if (!_held.Remove(key, out var live)) return;
            Return(live);
        }

        public void ReleaseActor(Guid actorId)
        {
            _releasing.Clear();
            foreach (var key in _held.Keys)
            {
                if (key.ActorId == actorId) _releasing.Add(key);
            }
            foreach (var key in _releasing) Release(key);
        }

        public void PlaySound(AudioClip clip, Vector3 position, float volume) => AudioSource.PlayClipAtPoint(clip, position, volume);

        public void ShowHudText(string key, string text, float seconds)
        {
            if (_hud == null) return;
            _hud.Set(key, text);
            _hudExpiry[key] = _now + seconds;
        }

        public void Tick()
        {
            _now += _time.DeltaTime;

            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                if (_timed[i].ExpiresAt > _now) continue;
                Return(_timed[i]);
                _timed.RemoveAt(i);
            }

            if (_hud == null || _hudExpiry.Count == 0) return;
            _expiredHud.Clear();
            foreach (var pair in _hudExpiry)
            {
                if (pair.Value <= _now) _expiredHud.Add(pair.Key);
            }
            foreach (var key in _expiredHud)
            {
                _hudExpiry.Remove(key);
                _hud.Remove(key);
            }
        }

        public void Dispose()
        {
            foreach (var live in _held.Values) Destroy(live.Instance);
            foreach (var live in _timed) Destroy(live.Instance);
            foreach (var stack in _free.Values)
            {
                foreach (var instance in stack) Destroy(instance);
            }
            _held.Clear();
            _timed.Clear();
            _free.Clear();
            if (_poolRoot != null) Destroy(_poolRoot.gameObject);
        }

        private Live Take(GameObject prefab, Transform parent, Vector3 localOffset)
        {
            GameObject? instance = null;
            if (_free.TryGetValue(prefab, out var stack))
            {
                while (instance == null && stack.Count > 0) instance = stack.Pop(); // skip ones destroyed with the pool root
            }
            if (instance == null) instance = Object.Instantiate(prefab); // Unity null: a destroyed pooled instance counts

            instance.transform.SetParent(parent, worldPositionStays: false);
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(true); // playOnAwake particles and audio restart on enable
            return new Live { Instance = instance, Prefab = prefab };
        }

        private void Return(Live live)
        {
            if (live.Instance == null) return; // destroyed with its target

            live.Instance.SetActive(false);
            live.Instance.transform.SetParent(PoolRoot(), worldPositionStays: false);
            if (!_free.TryGetValue(live.Prefab, out var stack)) _free[live.Prefab] = stack = new Stack<GameObject>();
            stack.Push(live.Instance);
        }

        private Transform PoolRoot()
        {
            if (_poolRoot != null) return _poolRoot;

            var root = new GameObject("GameplayCuePool");
            root.SetActive(false);
            _poolRoot = root.transform;
            return _poolRoot;
        }

        private static void Destroy(GameObject? instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }
}
