#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Launch options from the command line and, in the Editor, from Multiplayer Play Mode player tags.
    /// A tag is read as a flag with an optional value: <c>autohost</c> becomes <c>-autohost</c> and
    /// <c>bot:DeckWalk</c> (or <c>bot=DeckWalk</c>) becomes <c>-bot DeckWalk</c>, so builds and MPPM
    /// instances share one vocabulary.
    /// </summary>
    public static class LaunchArguments
    {
        private static IReadOnlyList<string>? _current;

        /// <summary>Command-line arguments followed by the current MPPM player's tags; cached per domain.</summary>
        public static IReadOnlyList<string> Current => _current ??= Merge(Environment.GetCommandLineArgs(), ReadPlayerTags());

        public static IReadOnlyList<string> Merge(string[]? commandLine, IReadOnlyList<string>? tags)
        {
            var merged = new List<string>(commandLine ?? Array.Empty<string>());
            foreach (var tag in tags ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;

                string trimmed = tag.Trim();
                int separator = trimmed.IndexOfAny(new[] { ':', '=' });
                if (separator < 0)
                {
                    merged.Add(AsFlag(trimmed));
                    continue;
                }

                merged.Add(AsFlag(trimmed.Substring(0, separator)));
                merged.Add(trimmed.Substring(separator + 1));
            }

            return merged;
        }

        public static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>The argument after <paramref name="flag"/>, if present and not itself a flag.</summary>
        public static bool TryGetValue(IReadOnlyList<string> args, string flag, out string value)
        {
            value = string.Empty;
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) continue;
                if (args[i + 1].StartsWith("-")) return false;

                value = args[i + 1];
                return true;
            }

            return false;
        }

        private static string AsFlag(string name) => name.StartsWith("-") ? name : "-" + name;

        private static IReadOnlyList<string> ReadPlayerTags()
        {
#if UNITY_EDITOR
            return Unity.Multiplayer.PlayMode.CurrentPlayer.Tags ?? Array.Empty<string>();
#else
            return Array.Empty<string>();
#endif
        }
    }
}
