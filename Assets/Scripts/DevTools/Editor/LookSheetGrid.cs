#nullable enable
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// A sheet of equal tiles rendered by one camera: the HDR target (so glows exceed 1 and the look's bloom picks them
    /// up), the double render (the first render after a change can come out blank), tiles left to right and top row
    /// first, an optional label per tile, and the PNG in <see cref="LookContactSheet.OutputFolder"/>. Used by the look
    /// sweeps; the other sheets move onto it later (plan .docs/plans/artstyle-pipeline-breakdown.md, P1).
    /// </summary>
    public sealed class LookSheetGrid : IDisposable
    {
        private const int LabelScale = 3;
        private static readonly Color LabelInk = new(0.98f, 0.96f, 0.92f);
        private static readonly Color LabelBox = new(0.11f, 0.09f, 0.1f);

        private readonly Texture2D _sheet;

        public LookSheetGrid(int columns, int rows, int tileWidth, int tileHeight)
        {
            Columns = columns;
            Rows = rows;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            _sheet = new Texture2D(tileWidth * columns, tileHeight * rows, TextureFormat.RGB24, false);
            Target = new RenderTexture(tileWidth, tileHeight, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 4 };
        }

        public int Columns { get; }
        public int Rows { get; }
        public int TileWidth { get; }
        public int TileHeight { get; }

        /// <summary>The camera renders into this.</summary>
        public RenderTexture Target { get; }

        /// <summary>Renders the camera into the tile at (column, row), with a label in its top-left corner.</summary>
        public void Capture(Camera camera, int column, int row, string? label = null)
        {
            camera.targetTexture = Target;
            camera.Render();
            camera.Render();
            RenderTexture.active = Target;
            // Texture rows start at the bottom.
            int y = (Rows - 1 - row) * TileHeight;
            _sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), column * TileWidth, y);
            RenderTexture.active = null;
            if (!string.IsNullOrEmpty(label)) LookSheetText.Draw(_sheet, label!, column * TileWidth, y + TileHeight, LabelScale, LabelInk, LabelBox);
        }

        /// <summary>Writes the sheet and returns its full path.</summary>
        public string Write(string fileName)
        {
            _sheet.Apply();
            var folder = Path.Combine(LookPalette.ProjectRoot, LookContactSheet.OutputFolder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, _sheet.EncodeToPNG());
            return path;
        }

        public void Dispose()
        {
            if (RenderTexture.active == Target) RenderTexture.active = null;
            Object.DestroyImmediate(Target);
            Object.DestroyImmediate(_sheet);
        }
    }
}
