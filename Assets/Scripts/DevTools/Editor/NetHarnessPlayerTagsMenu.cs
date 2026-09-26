#nullable enable
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// One-click Multiplayer Play Mode setup for the network test harness (see .docs/NETWORK_TEST_HARNESS.md):
    /// Player 1 hosts and pilots, Player 2 joins and walks the deck, both under the same latency preset.
    /// MPPM has no public API for assigning tags, so this goes through its internal
    /// <c>Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode</c> type and fails with a clear message if that moves.
    /// </summary>
    public static class NetHarnessPlayerTagsMenu
    {
        private const string Root = "TinCan/Dev/Net Harness/";
        private const string MppmType = "Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode, UnityEditor.MultiplayerModule";

        public static readonly string[] HostTags = { "autohost", "netsim:Lag100", "bot:Pilot" };
        public static readonly string[] ClientTags = { "autojoin", "netsim:Lag100", "bot:DeckWalk" };

        [MenuItem(Root + "Apply Host + Client (Lag100)")]
        public static void ApplyLag100() => Apply(HostTags, ClientTags);

        [MenuItem(Root + "Apply Host + Client (no latency)")]
        public static void ApplyNoLatency() => Apply(
            HostTags.Where(tag => !tag.StartsWith("netsim")).ToArray(),
            ClientTags.Where(tag => !tag.StartsWith("netsim")).ToArray());

        [MenuItem(Root + "Clear Tags")]
        public static void Clear() => Apply(Array.Empty<string>(), Array.Empty<string>());

        private static void Apply(string[] hostTags, string[] clientTags)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[NetHarness] Exit Play mode before changing player tags.");
                return;
            }

            var mppm = Type.GetType(MppmType);
            if (mppm == null)
            {
                Debug.LogError("[NetHarness] Multiplayer Play Mode internals not found; set the tags by hand (see NETWORK_TEST_HARNESS.md).");
                return;
            }

            object? projectTags = GetStatic(mppm, "PlayerTags");
            object? playerOne = GetStatic(mppm, "PlayerOne");
            object? playerTwo = GetStatic(mppm, "PlayerTwo");
            if (projectTags == null || playerOne == null || playerTwo == null)
            {
                Debug.LogError("[NetHarness] Multiplayer Play Mode player API changed; set the tags by hand (see NETWORK_TEST_HARNESS.md).");
                return;
            }

            foreach (var tag in hostTags.Concat(clientTags).Distinct())
            {
                if (!(bool)Invoke(projectTags, "Contains", tag)!) InvokeWithError(projectTags, "Add", tag);
            }

            SetPlayerTags(playerOne, hostTags);
            SetPlayerTags(playerTwo, clientTags);
            if (clientTags.Length > 0) EnsureLaunched(playerTwo);

            Debug.Log($"[NetHarness] Player 1: [{string.Join(", ", hostTags)}]  Player 2: [{string.Join(", ", clientTags)}].");
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

        private static void SetPlayerTags(object player, string[] tags)
        {
            InvokeWithError(player, "ClearTags");
            foreach (var tag in tags) InvokeWithError(player, "AddTag", tag);
        }

        private static object? GetStatic(Type type, string property) =>
            type.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);

        private static object? Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, args);

        /// <summary>Calls a <c>bool Method(..., out TagError)</c> and logs the error when it returns false.</summary>
        private static void InvokeWithError(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null)
            {
                Debug.LogError($"[NetHarness] {target.GetType().Name}.{method} not found.");
                return;
            }

            var parameters = new object?[args.Length + 1];
            Array.Copy(args, parameters, args.Length);
            if (info.GetParameters().Length != parameters.Length)
            {
                Debug.LogError($"[NetHarness] {target.GetType().Name}.{method} has an unexpected signature.");
                return;
            }

            bool ok = (bool)info.Invoke(target, parameters)!;
            if (!ok) Debug.LogWarning($"[NetHarness] {method}({string.Join(", ", args)}) failed: {parameters[^1]}.");
        }
    }
}
