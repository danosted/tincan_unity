#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Look > Write Palette Docs: writes the palette's documentation from <see cref="LookPalette"/>: one
    /// swatch SVG per role and the whole set (<c>palette.svg</c>) in <see cref="SwatchFolder"/>, and the role table in
    /// ART_DIRECTION.md between its palette markers. <see cref="StaleFiles"/> lists what disagrees with the palette, so a
    /// test keeps the docs honest. Plan: .docs/plans/look-slice.md (L0).
    /// </summary>
    public static class LookPaletteDocs
    {
        public const string SwatchFolder = ".docs/art/palette/";
        public const string ArtDirectionPath = ".docs/ART_DIRECTION.md";
        public const string SheetFile = "palette.svg";
        private const string TableBegin = "<!-- palette:begin";
        private const string TableEnd = "<!-- palette:end -->";
        private const int SheetCell = 60;

        [MenuItem("TinCan/Dev/Look/Write Palette Docs")]
        public static void Write()
        {
            var written = 0;
            foreach (var (path, text) in Expected(LookPalette.Load()))
            {
                var full = Path.Combine(LookPalette.ProjectRoot, path);
                if (File.Exists(full) && File.ReadAllText(full) == text) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
                written++;
            }

            Debug.Log($"[Look] palette docs: {written} file(s) written");
        }

        /// <summary>The project-relative paths whose content disagrees with what the palette would write (or that are
        /// missing). Empty when the docs are current.</summary>
        public static IReadOnlyList<string> StaleFiles(LookPalette palette) =>
            Expected(palette)
                .Where(e =>
                {
                    var full = Path.Combine(LookPalette.ProjectRoot, e.Path);
                    return !File.Exists(full) || File.ReadAllText(full) != e.Text;
                })
                .Select(e => e.Path)
                .ToList();

        /// <summary>Every file the palette writes, with its whole content: the swatches, the sheet, and ART_DIRECTION.md
        /// with its table replaced (left as it is when its markers are missing).</summary>
        public static IEnumerable<(string Path, string Text)> Expected(LookPalette palette)
        {
            foreach (var role in palette.Roles) yield return (SwatchFolder + role.id + ".svg", Swatch(role.hex));
            yield return (SwatchFolder + SheetFile, Sheet(palette));

            var doc = Path.Combine(LookPalette.ProjectRoot, ArtDirectionPath);
            if (File.Exists(doc)) yield return (ArtDirectionPath, WithTable(File.ReadAllText(doc), palette));
        }

        public static string Swatch(string hex) =>
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"48\" height=\"24\"><rect x=\"0.5\" y=\"0.5\" width=\"47\" height=\"23\" rx=\"4\" fill=\"{hex}\" stroke=\"#00000033\"/></svg>";

        public static string Sheet(LookPalette palette)
        {
            var svg = new StringBuilder();
            svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{palette.Roles.Count * SheetCell}\" height=\"80\">");
            svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");
            for (int i = 0; i < palette.Roles.Count; i++)
            {
                var role = palette.Roles[i];
                int x = i * SheetCell;
                svg.Append($"<rect x=\"{x}\" y=\"0\" width=\"{SheetCell}\" height=\"{SheetCell}\" fill=\"{role.hex}\"/>");
                svg.Append($"<text x=\"{x + 4}\" y=\"74\" font-family=\"sans-serif\" font-size=\"8\" fill=\"#666\">{role.id}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        /// <summary>The markdown table of roles, one row per role, header included, each line ending in a newline.</summary>
        public static string Table(LookPalette palette)
        {
            var table = new StringBuilder();
            table.Append("| | Role | Hex | Used for |\n|---|---|---|---|\n");
            foreach (var role in palette.Roles)
                table.Append($"| ![](art/palette/{role.id}.svg) | {role.name} | `{role.hex}` | {role.use} |\n");
            return table.ToString();
        }

        /// <summary>The document with the lines between its palette markers replaced by <see cref="Table"/>.</summary>
        public static string WithTable(string doc, LookPalette palette)
        {
            int begin = doc.IndexOf(TableBegin, System.StringComparison.Ordinal);
            if (begin < 0) return doc;
            int afterBegin = doc.IndexOf('\n', begin);
            int end = doc.IndexOf(TableEnd, afterBegin < 0 ? begin : afterBegin, System.StringComparison.Ordinal);
            if (afterBegin < 0 || end < 0) return doc;
            return doc[..(afterBegin + 1)] + Table(palette) + doc[end..];
        }
    }
}
