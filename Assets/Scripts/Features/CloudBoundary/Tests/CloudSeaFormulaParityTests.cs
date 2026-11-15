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
        public void ShaderAndGame_AgreeOnTheSwellAndTheHoles()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("No compute shaders in this Editor.");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            Assert.That(compute, Is.Not.Null, ComputePath);

            var swell = new CloudSwell(20f, 900f, 3f, CloudSwell.OffsetOf(12345));
            const float time = 1234.5f;
            var hole = new CloudHoleShape(new Vector2(150f, -80f), 60f, 25f, 0.8f, new Vector2(-120f, 90f), 0.7f); // a ragged rift: the round hole is its special case
            var points = new Vector2[256];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2((i % 16) * 23.7f - 40f, (i / 16) * -19.3f + 30f);

            var input = new ComputeBuffer(points.Length, sizeof(float) * 2);
            var output = new ComputeBuffer(points.Length, sizeof(float) * 2);
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
                compute.Dispatch(kernel, (points.Length + 63) / 64, 1, 1);

                var results = new Vector2[points.Length];
                output.GetData(results);
                var holes = new[] { hole };
                for (int i = 0; i < points.Length; i++)
                {
                    Vector2 p = points[i];
                    Assert.That(results[i].x, Is.EqualTo(CloudSeaFormula.Swell(p.x, p.y, time, swell)).Within(0.02f), $"swell at {p}");
                    Assert.That(results[i].y, Is.EqualTo(CloudSeaFormula.HoleCut(p.x, p.y, holes)).Within(1e-3f), $"hole cut at {p}");
                }
            }
            finally
            {
                input.Release();
                output.Release();
            }
        }
    }
}
