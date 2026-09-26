#nullable enable
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Runs the network test harness (see .docs/NETWORK_TEST_HARNESS.md) under Multiplayer Play Mode: Player 1 hosts
    /// and pilots, Player 2 joins and walks the deck. The tags exist only for the run. They are applied when a run
    /// starts, removed when Play mode ends, and a run interrupted by an Editor restart is cleaned up on the next load.
    /// So the tags never linger in anyone's normal Play or leak into ProjectSettings/VirtualProjectsConfig.json.
    /// MPPM has no public API for tags, so this goes through its internal
    /// <c>Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode</c> type and fails with a clear message if that moves.
    /// </summary>
    [InitializeOnLoad]
    public static class NetHarnessPlayerTagsMenu
    {
        private const string Root = "TinCan/Dev/Net Harness/";
        private const string MppmType = "Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode, UnityEditor.MultiplayerModule";
        private const string PendingKey = "TinCan.NetHarness.TagsPending";      // survives Editor restarts
        private const string SessionKey = "TinCan.NetHarness.RunThisSession";   // survives domain reloads only

        public static readonly string[] HostTags = { "autohost", "netsim:Lag100", "bot:Pilot" };
        public static readonly string[] ClientTags = { "autojoin", "netsim:Lag100", "bot:DeckWalk" };

        static NetHarnessPlayerTagsMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Tags still pending from a previous Editor session (crash or restart mid-run): clean them up.
            if (EditorPrefs.GetBool(PendingKey) && !SessionState.GetBool(SessionKey, false) &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () => Clear("leftover from an interrupted run");
            }
        }

        [MenuItem(Root + "Run Host + Client (Lag100)")]
        public static void RunLag100() => Run(HostTags, ClientTags);

        [MenuItem(Root + "Run Host + Client (no latency)")]
        public static void RunNoLatency() => Run(
            HostTags.Where(tag => !tag.StartsWith("netsim")).ToArray(),
            ClientTags.Where(tag => !tag.StartsWith("netsim")).ToArray());

        [MenuItem(Root + "Clear Tags")]
        public static void ClearMenu() => Clear("cleared by hand");

        private static void Run(string[] hostTags, string[] clientTags)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NetHarness] Already in Play mode; stop it before starting a run.");
                return;
            }

            if (!Apply(hostTags, clientTags)) return;

            EditorPrefs.SetBool(PendingKey, true);
            SessionState.SetBool(SessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !EditorPrefs.GetBool(PendingKey)) return;

            Clear("run finished");
        }

        private static void Clear(string reason)
        {
            var allTags = HostTags.Concat(ClientTags).Distinct().ToArray();
            if (!TryGetMppm(out var projectTags, out var playerOne, out var playerTwo)) return;

            SetPlayerTags(playerOne!, Array.Empty<string>());
            SetPlayerTags(playerTwo!, Array.Empty<string>());
            foreach (var tag in allTags) RemoveProjectTag(projectTags!, tag);

            EditorPrefs.DeleteKey(PendingKey);
            SessionState.EraseBool(SessionKey);
            Debug.Log($"[NetHarness] Player tags cleared ({reason}).");
        }

        private static bool Apply(string[] hostTags, string[] clientTags)
        {
            if (!TryGetMppm(out var projectTags, out var playerOne, out var playerTwo)) return false;

            foreach (var tag in hostTags.Concat(clientTags).Distinct())
            {
                if (!(bool)Invoke(projectTags!, "Contains", tag)!) InvokeWithError(projectTags!, "Add", tag);
            }

            SetPlayerTags(playerOne!, hostTags);
            SetPlayerTags(playerTwo!, clientTags);
            if (clientTags.Length > 0) EnsureLaunched(playerTwo!);

            Debug.Log($"[NetHarness] Run starting. Player 1: [{string.Join(", ", hostTags)}]  Player 2: [{string.Join(", ", clientTags)}]. Tags clear when Play mode ends.");
            return true;
        }

        private static bool TryGetMppm(out object? projectTags, out object? playerOne, out object? playerTwo)
        {
            projectTags = playerOne = playerTwo = null;
            var mppm = Type.GetType(MppmType);
            if (mppm != null)
            {
                projectTags = GetStatic(mppm, "PlayerTags");
                playerOne = GetStatic(mppm, "PlayerOne");
                playerTwo = GetStatic(mppm, "PlayerTwo");
            }

            if (projectTags != null && playerOne != null && playerTwo != null) return true;

            Debug.LogError("[NetHarness] Multiplayer Play Mode internals not found or changed; set the tags by hand (see NETWORK_TEST_HARNESS.md).");
            return false;
        }

        private static void SetPlayerTags(object player, string[] tags)
        {
            InvokeWithError(player, "ClearTags");
            foreach (var tag in tags) InvokeWithError(player, "AddTag", tag);
        }

        /// <summary>UnityPlayerTags.Remove(string, out PlayerIdentifier[], out TagError); a tag that is not there is fine.</summary>
        private static void RemoveProjectTag(object projectTags, string tag)
        {
            if (!(bool)Invoke(projectTags, "Contains", tag)!) return;

            var remove = projectTags.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (remove == null || remove.GetParameters().Length != 3)
            {
                Debug.LogWarning($"[NetHarness] Could not remove project tag '{tag}'; remove it in Project Settings > Multiplayer > Playmode.");
                return;
            }

            var parameters = new object?[] { tag, null, null };
            if (!(bool)remove.Invoke(projectTags, parameters)!) Debug.LogWarning($"[NetHarness] Removing tag '{tag}' failed: {parameters[2]}.");
        }

        /// <summary>
        /// MPPM remembers which players are active but only launches their Editor instances when activated in this
        /// session, so a fresh Editor has Player 2 marked active yet not running. The first launch takes a while.
        /// </summary>
        private static void EnsureLaunched(object player)
        {
            var state = player.GetType().GetProperty("PlayerState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player);
            if (state?.ToString() is "Launched" or "Launching") return;

            var activate = player.GetType().GetMethod("Activate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (activate == null || activate.GetParameters().Length != 2)
            {
                Debug.LogWarning("[NetHarness] Could not launch Player 2; activate it in Window > Multiplayer > Multiplayer Play Mode.");
                return;
            }

            var parameters = new object?[] { null, new System.Collections.Generic.List<string>() };
            bool ok = (bool)activate.Invoke(player, parameters)!;
            Debug.Log(ok ? "[NetHarness] Launching Player 2." : $"[NetHarness] Player 2 did not launch: {parameters[0]}.");
        }

        private static object? GetStatic(Type type, string property) =>
            type.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

        private static object? Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, args);

        /// <summary>Calls a <c>bool Method(..., out TagError)</c> and logs the error when it returns false.</summary>
        private static void InvokeWithError(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null || info.GetParameters().Length != args.Length + 1)
            {
                Debug.LogError($"[NetHarness] {target.GetType().Name}.{method} not found or changed signature.");
                return;
            }

            var parameters = new object?[args.Length + 1];
            Array.Copy(args, parameters, args.Length);
            bool ok = (bool)info.Invoke(target, parameters)!;
            if (!ok) Debug.LogWarning($"[NetHarness] {method}({string.Join(", ", args)}) failed: {parameters[^1]}.");
        }
    }
}
