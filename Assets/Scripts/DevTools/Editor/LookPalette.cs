#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// The art direction's palette, read from its one source, <see cref="AssetPath"/>: each role's id (kebab-case, also
    /// the swatch file name), display name, hex colour and what it is used for. The doc swatches
    /// (<see cref="LookPaletteDocs"/>) and the library materials take their colours from here. Doc:
    /// .docs/ART_DIRECTION.md, "Colour"; plan: .docs/plans/look-slice.md (L0).
    /// </summary>
    public sealed class LookPalette
    {
        public const string AssetPath = "Assets/Art/Palette/palette.json";

        [Serializable]
        public sealed class Role
        {
            public string id = string.Empty;
            public string name = string.Empty;
            public string hex = string.Empty;
            public string use = string.Empty;

            public Color Colour => ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.magenta;
        }

        [Serializable]
        private sealed class PaletteFile
        {
            public List<Role> roles = new();
        }

        private LookPalette(IReadOnlyList<Role> roles) => Roles = roles;

        public IReadOnlyList<Role> Roles { get; }

        /// <summary>The palette in the project, read from disk (not through the AssetDatabase, so it works before import).</summary>
        public static LookPalette Load() => Parse(File.ReadAllText(Path.Combine(ProjectRoot, AssetPath)));

        public static LookPalette Parse(string json) => new(JsonUtility.FromJson<PaletteFile>(json)?.roles ?? new List<Role>());

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        public Color this[string id] => Find(id)?.Colour ?? throw new KeyNotFoundException($"no palette role '{id}' in {AssetPath}");

        public Role? Find(string id) => Roles.FirstOrDefault(r => r.id == id);

        /// <summary>What is wrong with the palette: empty, blank or repeated ids, ids that are not kebab-case, or hex
        /// colours that are not #RRGGBB. Empty when it is sound.</summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (Roles.Count == 0) problems.Add("the palette has no roles");
            foreach (var role in Roles)
            {
                if (!IsKebab(role.id)) problems.Add($"role id '{role.id}' is not kebab-case");
                if (string.IsNullOrWhiteSpace(role.name)) problems.Add($"role '{role.id}' has no name");
                if (!IsHex(role.hex)) problems.Add($"role '{role.id}' hex '{role.hex}' is not #RRGGBB");
            }

            foreach (var repeated in Roles.GroupBy(r => r.id).Where(g => g.Count() > 1))
                problems.Add($"role id '{repeated.Key}' appears {repeated.Count()} times");
            return problems;
        }

        private static bool IsKebab(string id) =>
            id.Length > 0 && id[0] != '-' && id[^1] != '-' && !id.Contains("--") && id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

        private static bool IsHex(string hex) =>
            hex.Length == 7 && hex[0] == '#' && hex.Skip(1).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    }
}
