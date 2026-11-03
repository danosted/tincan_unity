#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Writes short labels onto a sheet's pixels in a 3 by 5 block font, so a tile carries its letter and values in the
    /// picture itself (agents and people read the same PNG). Upper case letters, digits and . - : , only; anything else
    /// is a space.
    /// </summary>
    public static class LookSheetText
    {
        private const int GlyphWidth = 3;
        private const int GlyphHeight = 5;

        // Rows top to bottom, '#' lit.
        private static readonly Dictionary<char, string[]> Glyphs = new()
        {
            ['A'] = new[] { ".#.", "#.#", "###", "#.#", "#.#" },
            ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
            ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" },
            ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
            ['F'] = new[] { "###", "#..", "##.", "#..", "#.." },
            ['G'] = new[] { ".##", "#..", "#.#", "#.#", ".##" },
            ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" },
            ['J'] = new[] { "..#", "..#", "..#", "#.#", ".#." },
            ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" },
            ['L'] = new[] { "#..", "#..", "#..", "#..", "###" },
            ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" },
            ['N'] = new[] { "##.", "#.#", "#.#", "#.#", "#.#" },
            ['O'] = new[] { ".#.", "#.#", "#.#", "#.#", ".#." },
            ['P'] = new[] { "##.", "#.#", "##.", "#..", "#.." },
            ['Q'] = new[] { ".#.", "#.#", "#.#", "##.", ".##" },
            ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
            ['S'] = new[] { ".##", "#..", ".#.", "..#", "##." },
            ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
            ['V'] = new[] { "#.#", "#.#", "#.#", "#.#", ".#." },
            ['W'] = new[] { "#.#", "#.#", "###", "###", "#.#" },
            ['X'] = new[] { "#.#", "#.#", ".#.", "#.#", "#.#" },
            ['Y'] = new[] { "#.#", "#.#", ".#.", ".#.", ".#." },
            ['Z'] = new[] { "###", "..#", ".#.", "#..", "###" },
            ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
            ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { "##.", "..#", ".#.", "#..", "###" },
            ['3'] = new[] { "##.", "..#", ".#.", "..#", "##." },
            ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
            ['5'] = new[] { "###", "#..", "##.", "..#", "##." },
            ['6'] = new[] { ".##", "#..", "###", "#.#", "###" },
            ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
            ['8'] = new[] { "###", "#.#", "###", "#.#", "###" },
            ['9'] = new[] { "###", "#.#", "###", "..#", "##." },
            ['.'] = new[] { "...", "...", "...", "...", ".#." },
            ['-'] = new[] { "...", "...", "###", "...", "..." },
            [':'] = new[] { "...", ".#.", "...", ".#.", "..." },
            [','] = new[] { "...", "...", "...", ".#.", "#.." },
        };

        /// <summary>Draws <paramref name="text"/> on a dark box whose top-left corner is at (x, top), in sheet pixels
        /// (texture rows start at the bottom). Call before <see cref="Texture2D.Apply()"/>.</summary>
        public static void Draw(Texture2D sheet, string text, int x, int top, int scale, Color ink, Color box)
        {
            text = text.ToUpperInvariant();
            int cell = scale;
            int advance = (GlyphWidth + 1) * cell;
            int padding = 2 * cell;
            int width = Mathf.Min(text.Length * advance + padding * 2, sheet.width - x);
            int height = GlyphHeight * cell + padding * 2;
            Fill(sheet, x, top - height, width, height, box);

            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out var rows)) continue;
                int left = x + padding + i * advance;
                if (left + GlyphWidth * cell > sheet.width) break;
                for (int row = 0; row < GlyphHeight; row++)
                for (int column = 0; column < GlyphWidth; column++)
                {
                    if (rows[row][column] != '#') continue;
                    Fill(sheet, left + column * cell, top - padding - (row + 1) * cell, cell, cell, ink);
                }
            }
        }

        private static void Fill(Texture2D sheet, int x, int y, int width, int height, Color colour)
        {
            x = Mathf.Clamp(x, 0, sheet.width);
            y = Mathf.Clamp(y, 0, sheet.height);
            width = Mathf.Min(width, sheet.width - x);
            height = Mathf.Min(height, sheet.height - y);
            if (width <= 0 || height <= 0) return;
            var block = new Color[width * height];
            for (int i = 0; i < block.Length; i++) block[i] = colour;
            sheet.SetPixels(x, y, width, height, block);
        }
    }
}
