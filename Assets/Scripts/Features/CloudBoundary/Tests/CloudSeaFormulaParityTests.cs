#nullable enable
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Tests
{
    /// <summary>
    /// The game and the drawn sea read one formula (plan .docs/plans/cloud-sea.md): runs the shader's HLSL on the GPU
    /// (CloudSeaFormulaParity.compute) and the C# <see cref="CloudSeaFormula"/> on the same points, and fails when they
    /// drift. Skipped where the Editor has no compute shaders (a -nographics run).
    /// </summary>
    public class CloudSeaFormulaParityTests
    {
        private const string ComputePath = "Assets/Scripts/Features/CloudBoundary/Tests/CloudSeaFormulaParity.compute";

        [Test]
        public void ShaderAndGame_AgreeOnTheSwellTheHolesAndTheWeb()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("No compute shaders in this Editor.");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            Assert.That(compute, Is.Not.Null, ComputePath);

            var swell = new CloudSwell(20f, 900f, 3f, CloudSwell.OffsetOf(12345));
            const float time = 1234.5f;
            var hole = new CloudHoleShape(new Vector2(150f, -80f), 60f, 25f, 0.8f, new Vector2(-120f, 90f), 0.7f); // a ragged rift: the round hole is its special case
            // A small web, so the points cross many rifts, open and shut ones.
            var web = new CloudWeb(120f, 30f, 12f, 0.6f, 0.6f, 6f, new Vector2(4321f, 1234f));
            var points = new Vector2[1024];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2((i % 32) * 23.7f - 40f, (i / 32) * -19.3f + 30f);

            var input = new ComputeBuffer(points.Length, sizeof(float) * 2);
            var output = new ComputeBuffer(points.Length, sizeof(float) * 3);
            try
            {
                input.SetData(points);
                int kernel = compute.FindKernel("Evaluate");
                compute.SetBuffer(kernel, "Points", input);
                compute.SetBuffer(kernel, "Results", output);
                compute.SetInt("PointCount", points.Length);
                compute.SetVector("SwellParams", new Vector4(swell.Amplitude, swell.Wavelength, swell.Speed, time));
                compute.SetVector("SwellOffset", new Vector4(swell.Offset.x, swell.Offset.y, 0f, 0f));
                compute.SetVector("Hole", new Vector4(hole.Centre.x, hole.Centre.y, hole.Radius, hole.Rim));
                compute.SetFloat("HoleOpenness", hole.Openness);
                compute.SetVector("HoleReach", new Vector4(hole.Reach.x, hole.Reach.y, hole.Ragged, 0f));
                compute.SetVector("WebShape", new Vector4(web.Cell, web.Width, web.Rim, web.Ragged));
                compute.SetVector("WebMotion", new Vector4(web.OpenShare, web.Drift, web.Offset.x, web.Offset.y));
                compute.Dispatch(kernel, (points.Length + 63) / 64, 1, 1);

                var results = new Vector3[points.Length];
                output.GetData(results);
                var holes = new[] { hole };
                int inRift = 0, onCloud = 0, misses = 0;
                float worstMiss = 0f;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 p = points[i];
                    Assert.That(results[i].x, Is.EqualTo(CloudSeaFormula.Swell(p.x, p.y, time, swell)).Within(0.02f), $"swell at {p}");
                    Assert.That(results[i].y, Is.EqualTo(CloudSeaFormula.HoleCut(p.x, p.y, holes)).Within(1e-3f), $"hole cut at {p}");
                    // The GPU rounds its hashes and sines a little differently: on a rift's rim, where the cut changes
                    // fast, that moves the edge by centimetres, and a border opening or closing runs a moment ahead or
                    // behind. So the shader's cut must be one the game has within half a metre and two seconds.
                    float webCut = CloudSeaFormula.WebCut(p.x, p.y, time, web);
                    float low = webCut, high = webCut;
                    foreach (Vector3 nudge in new[] { new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, -2f) })
                    {
                        float near = CloudSeaFormula.WebCut(p.x + nudge.x, p.y + nudge.y, time + nudge.z, web);
                        low = Mathf.Min(low, near);
                        high = Mathf.Max(high, near);
                    }

                    // Where three cells meet the cut can switch within that, so a few points may still differ, but
                    // never by much and never many.
                    float miss = Mathf.Max(low - results[i].z, results[i].z - high, 0f);
                    if (miss > 0.005f) misses++;
                    worstMiss = Mathf.Max(worstMiss, miss);
                    if (webCut > 0.99f) inRift++;
                    if (webCut < 0.01f) onCloud++;
                }

                Assert.That(misses, Is.LessThanOrEqualTo(points.Length / 100), $"{misses} points where the shader's web differs (worst {worstMiss:0.000})");
                Assert.That(worstMiss, Is.LessThan(0.1f), "the shader's web is far from the game's somewhere");
                Assert.That(inRift, Is.GreaterThan(0), "no point fell in a rift: the web case tests nothing");
                Assert.That(onCloud, Is.GreaterThan(0), "every point in a rift");
            }
            finally
            {
                input.Release();
                output.Release();
            }
        }
    }
}
