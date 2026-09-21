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
    /// height (left at 1 for now). No paint tool available, so this is generated instead of
    /// hand-authored; re-run with a different seed/scale to get a different layout.
    /// </summary>
    public static class CloudCoverageMapGenerator
    {
        private const string DefaultOutputPath = "Assets/Settings/CloudCoverageMap.png";

        [MenuItem("TinCan/Features/Generate Cloud Coverage Map")]
        private static void GenerateFromMenu()
        {
            Texture2D map = Generate(DefaultOutputPath);
            EditorUtility.DisplayDialog("Cloud Coverage Map", $"Generated {map.width}x{map.height} map at {DefaultOutputPath}.", "OK");
            EditorGUIUtility.PingObject(map);
        }

        /// <summary>
        /// Generates and saves the map as a PNG asset, returning the imported Texture2D.
        /// </summary>
        /// <param name="coverageScale">Noise frequency for coverage patches; higher gives more, smaller patches.</param>
        /// <param name="coverageThreshold">0-1. Higher leaves more open sky (gaps) between patches.</param>
        /// <param name="typeScale">Noise frequency for cloud type; kept low relative to coverageScale so type transitions gradually across large regions.</param>
        /// <param name="rainScale">Noise frequency for the rain/storm channel.</param>
        /// <param name="rainWeight">0-1 cap on how strong the rain channel gets.</param>
        /// <param name="coverageThreshold">Raw fractal noise below this becomes open sky (0 coverage). Layered Perlin noise clusters tightly around ~0.5, so keep this well below 0.5 - a value like 0.45 leaves only a thin sliver of the noise's range contributing any coverage at all.</param>
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
            float rainWeight = 0.25f)
        {
            var random = new System.Random(seed);
            Vector2 coverageOffset = RandomOffset(random);
            Vector2 typeOffset = RandomOffset(random);
            Vector2 rainOffset = RandomOffset(random);

            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / resolution;
                    float v = (float)y / resolution;

                    float coverage = FractalNoise(u * coverageScale + coverageOffset.x, v * coverageScale + coverageOffset.y, octaves: 4);
                    coverage = Mathf.Clamp01(Mathf.InverseLerp(coverageThreshold, 1f, coverage));
                    coverage = Mathf.Pow(coverage, coveragePower);

                    float cloudType = Mathf.PerlinNoise(u * typeScale + typeOffset.x, v * typeScale + typeOffset.y);

                    float rain = Mathf.Clamp01(FractalNoise(u * rainScale + rainOffset.x, v * rainScale + rainOffset.y, octaves: 2) * rainWeight);

                    pixels[y * resolution + x] = new Color(coverage, rain, cloudType, 1f);
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

        private static Vector2 RandomOffset(System.Random random) => new((float)random.NextDouble() * 1000f, (float)random.NextDouble() * 1000f);

        private static float FractalNoise(float x, float y, int octaves)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float frequency = 1f;
            float maxValue = 0f;
            for (int i = 0; i < octaves; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return value / maxValue;
        }
    }
}
