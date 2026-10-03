#nullable enable
using NUnit.Framework;
using TinCan.DevTools.Perf;

namespace TinCan.Tests.EditMode
{
    public class PerfOptionsTests
    {
        [Test]
        public void Parse_NoFlag_IsDisabled()
        {
            var options = PerfOptions.Parse(new[] { "game.exe", "-autojoin", "127.0.0.1" });

            Assert.That(options.Enabled, Is.False);
        }

        [Test]
        public void Parse_ReadsEveryOption()
        {
            var options = PerfOptions.Parse(new[]
            {
                "-perf", "-perfrole", "bot2", "-perfwarmup", "15", "-perfduration", "120.5", "-perfout", "/perf",
                "-perflabel", "CrewLoad", "-perfquit", "-perfprofile", "300"
            });

            Assert.That(options.Enabled, Is.True);
            Assert.That(options.Role, Is.EqualTo("bot2"));
            Assert.That(options.WarmupSeconds, Is.EqualTo(15f));
            Assert.That(options.DurationSeconds, Is.EqualTo(120.5f));
            Assert.That(options.OutputDirectory, Is.EqualTo("/perf"));
            Assert.That(options.Label, Is.EqualTo("CrewLoad"));
            Assert.That(options.QuitWhenDone, Is.True);
            Assert.That(options.ProfileFrames, Is.EqualTo(300));
        }

        [Test]
        public void Parse_BadNumbers_FallBackToDefaults()
        {
            var options = PerfOptions.Parse(new[] { "-perf", "-perfwarmup", "soon", "-perfduration", "-5" });

            Assert.That(options.WarmupSeconds, Is.EqualTo(PerfOptions.DefaultWarmupSeconds));
            Assert.That(options.DurationSeconds, Is.EqualTo(PerfOptions.DefaultDurationSeconds));
            Assert.That(options.Label, Is.EqualTo("none"));
        }
    }

    public class PerfSeriesTests
    {
        private static PerfSeries SeriesOf(params float[] values)
        {
            var series = new PerfSeries("frame_ms", "ms");
            foreach (var value in values) series.Add(value);
            return series;
        }

        [Test]
        public void Summarise_NearestRankPercentiles()
        {
            var series = new PerfSeries("frame_ms", "ms");
            for (int i = 1; i <= 100; i++) series.Add(i);

            var summary = series.Summarise();

            Assert.That(summary.Count, Is.EqualTo(100));
            Assert.That(summary.P50, Is.EqualTo(50f));
            Assert.That(summary.P95, Is.EqualTo(95f));
            Assert.That(summary.P99, Is.EqualTo(99f));
            Assert.That(summary.Max, Is.EqualTo(100f));
            Assert.That(summary.Mean, Is.EqualTo(50.5f).Within(0.001f));
            Assert.That(summary.Total, Is.EqualTo(5050f));
        }

        [Test]
        public void Summarise_CountsHitchesAboveTheThreshold()
        {
            var summary = SeriesOf(16f, 17f, 40f, 16f, 33.4f, 33.3f).Summarise(hitchThreshold: 33.3f);

            Assert.That(summary.Hitches, Is.EqualTo(2));
        }

        [Test]
        public void Summarise_Empty_IsZero()
        {
            var summary = new PerfSeries("tick_ms", "ms").Summarise();

            Assert.That(summary.Count, Is.EqualTo(0));
            Assert.That(summary.P95, Is.EqualTo(0f));
        }

        [Test]
        public void Slope_FlatSeries_IsZero_GrowingSeries_IsItsRate()
        {
            Assert.That(PerfSeries.Slope(SeriesOf(100f, 100f, 100f, 100f).Samples), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(PerfSeries.Slope(SeriesOf(100f, 102f, 104f, 106f).Samples), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void Slope_SkipsTheWarmUp()
        {
            var samples = SeriesOf(10f, 50f, 100f, 100f, 100f).Samples;

            Assert.That(PerfSeries.Slope(samples, skip: 2), Is.EqualTo(0f).Within(0.0001f));
        }
    }

    public class PerfReportTests
    {
        [Test]
        public void ToJson_HoldsInfoMetricsMessagesAndUnavailable()
        {
            var report = new PerfReport("server") { WindowSeconds = 90f, Frames = 2700 };
            report.AddInfo("label", "CrewLoad");
            var series = new PerfSeries("tick_ms", "ms/tick");
            series.Add(1.5f);
            series.Add(2.5f);
            report.AddMetric(series.Summarise());
            report.AddMessages("RpcMessage", 12, 0);
            report.AddMessages("RpcMessage", 0, 7);
            report.AddUnavailable("GPU Frame Time");

            string json = report.ToJson();

            StringAssert.Contains("\"role\": \"server\"", json);
            StringAssert.Contains("\"label\": \"CrewLoad\"", json);
            StringAssert.Contains("\"windowS\": 90", json);
            StringAssert.Contains("\"tick_ms\": { \"unit\": \"ms/tick\", \"n\": 2", json);
            StringAssert.Contains("\"p95\": 2.5", json);
            StringAssert.Contains("\"messagesSent\": { \"RpcMessage\": 12 }", json);
            StringAssert.Contains("\"messagesReceived\": { \"RpcMessage\": 7 }", json);
            StringAssert.Contains("\"unavailable\": [\"GPU Frame Time\"]", json);
        }

        [Test]
        public void Number_IsInvariantAndRounded()
        {
            Assert.That(PerfReport.Number(1234.56789f), Is.EqualTo("1234.568"));
            Assert.That(PerfReport.Number(float.NaN), Is.EqualTo("0"));
        }
    }
}
