#nullable enable
using NUnit.Framework;
using TinCan.DevTools.Editor;

namespace TinCan.DevTools.Editor.Tests
{
    /// <summary>The palette source is sound and its docs (swatches, sheet, ART_DIRECTION table) agree with it.
    /// Plan: .docs/plans/look-slice.md (L0).</summary>
    public class LookPaletteTests
    {
        [Test]
        public void TheProjectPalette_HasNoProblems()
        {
            Assert.That(LookPalette.Load().Problems(), Is.Empty);
        }

        [Test]
        public void ThePaletteDocs_AreCurrent_RunWritePaletteDocsWhenThisFails()
        {
            Assert.That(LookPaletteDocs.StaleFiles(LookPalette.Load()), Is.Empty,
                "Run TinCan > Dev > Look > Write Palette Docs after changing Assets/Art/Palette/palette.json.");
        }

        [Test]
        public void Problems_NameRepeatedIds_BadIds_AndBadHex()
        {
            var palette = LookPalette.Parse(
                "{\"roles\":[{\"id\":\"ink\",\"name\":\"Ink\",\"hex\":\"#1C1719\"},{\"id\":\"ink\",\"name\":\"Ink\",\"hex\":\"#1c1719\"}," +
                "{\"id\":\"Timber Dark\",\"name\":\"Timber\",\"hex\":\"#5A3A24\"}]}");

            var problems = palette.Problems();

            Assert.That(problems, Has.Some.Contains("'ink' appears 2 times"));
            Assert.That(problems, Has.Some.Contains("'#1c1719' is not #RRGGBB"));
            Assert.That(problems, Has.Some.Contains("'Timber Dark' is not kebab-case"));
        }

        [Test]
        public void WithTable_ReplacesOnlyTheLinesBetweenTheMarkers()
        {
            var palette = LookPalette.Parse("{\"roles\":[{\"id\":\"ink\",\"name\":\"Ink\",\"hex\":\"#1C1719\",\"use\":\"Darks\"}]}");
            const string doc = "before\n<!-- palette:begin note -->\nold row\n<!-- palette:end -->\nafter\n";

            var written = LookPaletteDocs.WithTable(doc, palette);

            Assert.That(written, Is.EqualTo("before\n<!-- palette:begin note -->\n| | Role | Hex | Used for |\n|---|---|---|---|\n" +
                "| ![](art/palette/ink.svg) | Ink | `#1C1719` | Darks |\n<!-- palette:end -->\nafter\n"));
        }

        [Test]
        public void WithTable_LeavesADocWithoutMarkersAlone()
        {
            var palette = LookPalette.Parse("{\"roles\":[{\"id\":\"ink\",\"name\":\"Ink\",\"hex\":\"#1C1719\"}]}");

            Assert.That(LookPaletteDocs.WithTable("no table here\n", palette), Is.EqualTo("no table here\n"));
        }
    }
}
