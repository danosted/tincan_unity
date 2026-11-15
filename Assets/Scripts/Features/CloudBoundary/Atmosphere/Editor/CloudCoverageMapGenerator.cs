#nullable enable
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Atmosphere.Editor
{
    /// <summary>
    /// Procedurally generates the cloud coverage/type map consumed by VolumetricClouds.cloudMap:
    /// R = coverage (how much cloud at this XZ position), G = rain/storm amount, B = cloud type
    /// (selects a column in the type LUT, blending between the built-in presets), A = max cloud
    /// height (1 unless a height range is given). No paint tool available, so this is generated instead of
    /// hand-authored; re-run with a different seed/scale to get a different layout.
    /// The noise repeats exactly across the map (each scale is rounded to whole cells per map), so the map tiles with no
    /// seam: the clouds repeat it across the world, and a seam shows as a straight line through the sky.
    /// </summary>
    public static class CloudCoverageMapGenerator
    {
        private const string DefaultOutputPath = "Assets/Settings/CloudCoverageMap.png";
        private const string SkyOutputPath = "Assets/Settings/CloudSkyCoverageMap.png";

        [MenuItem("TinCan/Features/Generate Cloud Coverage Map")]
        private static void GenerateFromMenu()
        {
            Texture2D map = Generate(DefaultOutputPath);
            EditorUtility.DisplayDialog("Cloud Coverage Map", $"Generated {map.width}x{map.height} map at {DefaultOutputPath}.", "OK");
            EditorGUIUtility.PingObject(map);
        }

        /// <summary>The map for the high sky layer (plan .docs/plans/cloud-sea.md): patches of cloud with open sky
        /// between them, so the sky is not one even sheet.</summary>
        [MenuItem("TinCan/Features/Generate Sky Coverage Map")]
        public static void GenerateSky()
        {
            // Coverage over a wide range (thin regions give small scattered puffs, thick ones big merged heaps), a height
            // limit that changes by region (flat clouds beside towers), and cloud types that differ region to region.
            Texture2D map = Generate(SkyOutputPath, coverageScale: 4f, coverageThreshold: 0.3f, coveragePower: 1f, typeScale: 3f,
                seed: 271, contrast: 2.2f, heightScale: 6f, heightRange: new Vector2(0.3f, 1f));
            Debug.Log($"[CloudCoverageMapGenerator] Sky coverage map at {SkyOutputPath} ({map.width}x{map.height}).");
        }

        /// <summary>
        /// Generates and saves the map as a PNG asset, returning the imported Texture2D.
        /// </summary>
        /// <param name="coverageScale">Noise cells across the map for coverage patches (rounded to a whole number so the map tiles); higher gives more, smaller patches.</param>
        /// <param name="typeScale">Noise cells across the map for cloud type; kept low relative to coverageScale so type transitions gradually across large regions.</param>
        /// <param name="rainScale">Noise cells across the map for the rain/storm channel.</param>
        /// <param name="rainWeight">0-1 cap on how strong the rain channel gets.</param>
        /// <param name="coverageThreshold">Raw fractal noise below this becomes open sky (0 coverage). Layered noise clusters around ~0.5, so values near 0.5 leave about half the sky open.</param>
        /// <param name="contrast">Spreads the coverage and type noise away from 0.5 (1 leaves it as it is), so regions differ more.</param>
        /// <param name="heightScale">Noise cells across the map for the height limit (alpha).</param>
        /// <param name="heightRange">The height limit's range, as a fraction of the layer (alpha). Default (1, 1): every cloud may fill the layer.</param>
        /// <param name="coveragePower">Applied to coverage after the threshold remap (values &lt;1 push it up). The shader multiplies final density by coverage*coverage, so a coverage of e.g. 0.4 becomes only ~16% effective density - this compensates so "covered" regions actually read as covered instead of nearly invisible.</param>
        public static Texture2D Generate(
            string outputPath,
            int resolution = 512,
            int seed = 12345,
            float coverageScale = 6f,
            float coverageThreshold = 0.15f,
            float coveragePower = 0.6f,
            float typeScale = 2f,
            float rainScale = 8f,
            float rainWeight = 0.25f,
            float contrast = 1f,
            float heightScale = 4f,
            Vector2? heightRange = null)
        {
            int coverageCells = Mathf.Max(1, Mathf.RoundToInt(coverageScale));
            int typeCells = Mathf.Max(1, Mathf.RoundToInt(typeScale));
            int rainCells = Mathf.Max(1, Mathf.RoundToInt(rainScale));
            int heightCells = Mathf.Max(1, Mathf.RoundToInt(heightScale));
            Vector2 height = heightRange ?? Vector2.one;
            // Layered noise gathers around 0.5; contrast spreads it back over 0..1.
            float Spread(float n) => Mathf.Clamp01((n - 0.5f) * contrast + 0.5f);

            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / resolution;
                    float v = (float)y / resolution;

                    float coverage = Spread(FractalNoise(u, v, coverageCells, seed, octaves: 4));
                    coverage = Mathf.Clamp01(Mathf.InverseLerp(coverageThreshold, 1f, coverage));
                    coverage = Mathf.Pow(coverage, coveragePower);

                    float cloudType = Spread(FractalNoise(u, v, typeCells, seed + 1, octaves: 1));

                    float rain = Mathf.Clamp01(FractalNoise(u, v, rainCells, seed + 2, octaves: 2) * rainWeight);

                    float maxHeight = Mathf.Lerp(height.x, height.y, Spread(FractalNoise(u, v, heightCells, seed + 3, octaves: 2)));

                    pixels[y * resolution + x] = new Color(coverage, rain, cloudType, maxHeight);
                }
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(outputPath);
            if (AssetImporter.GetAtPath(outputPath) is TextureImporter importer)
            {
                // Data texture (coverage/rain/type/height), not a color image - must not be gamma-corrected.
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }

        // Octaves of value noise over u, v in 0..1 with a whole number of cells across, doubling each octave, so the
        // sum repeats exactly at u, v = 1.
        private static float FractalNoise(float u, float v, int cells, int seed, int octaves)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                int period = cells << i;
                value += PeriodicNoise(u * period, v * period, period, seed + i * 101) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
            }

            return value / maxValue;
        }

        // Smooth value noise on a lattice that wraps every period cells.
        private static float PeriodicNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            fy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float a = Lattice(x0, y0, period, seed), b = Lattice(x0 + 1, y0, period, seed);
            float c = Lattice(x0, y0 + 1, period, seed), d = Lattice(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Lattice(int x, int y, int period, int seed)
        {
            uint ix = (uint)(((x % period) + period) % period);
            uint iy = (uint)(((y % period) + period) % period);
            uint h = (uint)seed * 0x9E3779B1u ^ ix * 0x85EBCA77u ^ iy * 0xC2B2AE3Du;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }
    }
}
