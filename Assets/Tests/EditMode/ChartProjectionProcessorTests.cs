#nullable enable
using NUnit.Framework;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ChartProjectionProcessor"/>: heading-up, ship-centred chart points. Plan: navigation-compass.md.</summary>
    public class ChartProjectionProcessorTests
    {
        private readonly ChartProjectionProcessor _projection = new();

        [Test]
        public void StraightAhead_IsUpTheChart_WhateverTheHeading()
        {
            var ship = new Vector3(100f, 50f, -20f);
            var heading = 70f;
            var ahead = ship + Quaternion.Euler(0f, heading, 0f) * Vector3.forward * 500f;

            var chart = _projection.ToChart(ahead, ship, heading, 1000f);

            Assert.That(chart.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(chart.y, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Starboard_IsRightOnTheChart()
        {
            var chart = _projection.ToChart(new Vector3(0f, 0f, -300f), Vector3.zero, 90f, 1000f);

            Assert.That(chart.x, Is.GreaterThan(0.29f), "heading east, south is to starboard");
            Assert.That(Mathf.Abs(chart.y), Is.LessThan(1e-4f));
        }

        [Test]
        public void ToWorld_UndoesToChart_AndSetsTheAltitude()
        {
            var ship = new Vector3(10f, 30f, 20f);
            var world = new Vector3(-400f, 99f, 900f);

            var chart = _projection.ToChart(world, ship, 33f, 2500f);
            var back = _projection.ToWorld(chart, 120f, ship, 33f, 2500f);

            Assert.That(back.x, Is.EqualTo(world.x).Within(0.01f));
            Assert.That(back.z, Is.EqualTo(world.z).Within(0.01f));
            Assert.That(back.y, Is.EqualTo(120f));
        }

        [Test]
        public void ClampToChart_PullsFarPointsOntoTheRim()
        {
            Assert.That(_projection.ClampToChart(new Vector2(3f, 4f)), Is.EqualTo(new Vector2(0.6f, 0.8f)));
            Assert.That(_projection.ClampToChart(new Vector2(0.2f, 0.1f)), Is.EqualTo(new Vector2(0.2f, 0.1f)));
        }

        [Test]
        public void Heading_IgnoresPitchAndBank()
        {
            Assert.That(_projection.Heading(Quaternion.Euler(-20f, 120f, 10f)), Is.EqualTo(120f).Within(0.5f));
        }
    }
}
