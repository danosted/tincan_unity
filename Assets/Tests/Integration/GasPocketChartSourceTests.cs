#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Navigation;
using TinCan.Features.GasChallenge;
using TinCan.Features.Navigation;
using UnityEngine;
using TinCan.Features.GasChallenge.Tests;
using TinCan.Tests.Fakes;

namespace TinCan.Tests.Integration
{
    /// <summary>GasPocketChartSource (live pockets as gas obstacles) and ChartContents' filters.</summary>
    public class GasPocketChartSourceTests
    {
        private sealed class HideGas : IChartFilter
        {
            public bool ShowsObstacle(in ChartObstacle obstacle) => obstacle.Kind != ChartObstacleKind.Gas;
            public bool ShowsPoint(in PointOfInterest point) => point.Id != 2;
        }

        private FakeActorRegistry _actors = null!;

        [SetUp]
        public void SetUp() => _actors = new FakeActorRegistry();

        [Test]
        public void LivePocketsInRange_AreGasObstacles_SpanningTheirHeight()
        {
            _actors.Register(new FakeGasPocket(new Vector3(100f, 50f, 0f), 16f));
            _actors.Register(new FakeGasPocket(new Vector3(0f, 0f, 200f)) { Detonated = true });
            _actors.Register(new FakeGasPocket(new Vector3(5000f, 0f, 0f)));

            var obstacles = new List<ChartObstacle>();
            new GasPocketChartSource(_actors).CollectObstacles(Vector3.zero, 1000f, obstacles);

            Assert.That(obstacles.Count, Is.EqualTo(1), "gone off, or off the chart: left out");
            Assert.That(obstacles[0].Kind, Is.EqualTo(ChartObstacleKind.Gas));
            Assert.That((obstacles[0].Bottom, obstacles[0].Top, obstacles[0].Radius), Is.EqualTo((34f, 66f, 16f)));
        }

        [Test]
        public void ChartContents_DropsWhatAFilterHides_AndKeepsTheRest()
        {
            _actors.Register(new FakeGasPocket(Vector3.zero));
            var source = new FakeChartSource();
            source.Points.Add(new PointOfInterest(2, "Hidden", Vector3.zero, startsKnown: true));
            source.Points.Add(new PointOfInterest(3, "Shown", Vector3.zero, startsKnown: true));
            var sources = new IChartSource[] { new GasPocketChartSource(_actors), source };

            Assert.That(new ChartContents(sources).Obstacles(Vector3.zero, 100f).Count, Is.EqualTo(1), "no filter: everything");

            var filtered = new ChartContents(sources, new IChartFilter[] { new HideGas() });
            Assert.That(filtered.Obstacles(Vector3.zero, 100f), Is.Empty);
            Assert.That(filtered.PointsOfInterest().Count, Is.EqualTo(1));
            Assert.That(filtered.TryGetPointOfInterest(2, out _), Is.False);
        }
    }
}
