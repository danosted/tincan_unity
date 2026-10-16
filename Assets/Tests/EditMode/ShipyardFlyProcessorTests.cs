#nullable enable
using NUnit.Framework;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardFlyProcessor"/>: look, fly along the view, rise straight up, push, and frame a ship.</summary>
    public class ShipyardFlyProcessorTests
    {
        private readonly ShipyardFlyProcessor _fly = new();

        [Test]
        public void Looking_TurnsAndTilts_ButNeverPastStraightDown()
        {
            var pose = _fly.Look(new ShipyardCameraPose(Vector3.zero, 0f, 0f), new Vector2(30f, -200f), 89f);

            Assert.That(pose.Yaw, Is.EqualTo(30f));
            Assert.That(pose.Pitch, Is.EqualTo(89f), "mouse down tilts the view down, clamped");
        }

        [Test]
        public void Flying_GoesAlongTheView_RisingGoesStraightUp_AndPushingIgnoresTime()
        {
            var level = new ShipyardCameraPose(Vector3.zero, 90f, 0f);
            var down = new ShipyardCameraPose(Vector3.zero, 0f, 90f);

            var forward = _fly.Fly(level, new Vector2(0f, 1f), 0f, 0f, 4f, 0.5f);
            var strafe = _fly.Fly(level, new Vector2(1f, 0f), 0f, 0f, 4f, 0.5f);
            var rise = _fly.Fly(down, Vector2.zero, 1f, 0f, 4f, 0.5f);
            var push = _fly.Fly(down, Vector2.zero, 0f, 3f, 4f, 0f);

            Assert.That(Vector3.Distance(forward.Position, new Vector3(2f, 0f, 0f)), Is.LessThan(1e-4f), "yaw 90 looks along +X");
            Assert.That(Vector3.Distance(strafe.Position, new Vector3(0f, 0f, -2f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(rise.Position, new Vector3(0f, 2f, 0f)), Is.LessThan(1e-4f), "up is up, whatever the view");
            Assert.That(Vector3.Distance(push.Position, new Vector3(0f, -3f, 0f)), Is.LessThan(1e-4f), "pushed along a view looking down");
        }

        [Test]
        public void Framing_LooksAtTheCentre_FromFarEnoughToSeeItAll()
        {
            var bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(30f, 10f, 120f));

            var pose = _fly.Frame(bounds, 60f, 35f, 30f, 18f);

            float distance = Vector3.Distance(pose.Position, bounds.center);
            Assert.That(distance, Is.EqualTo(bounds.extents.magnitude / Mathf.Sin(30f * Mathf.Deg2Rad)).Within(1e-3f));
            Assert.That(Vector3.Dot(pose.Forward, (bounds.center - pose.Position).normalized), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Distance(_fly.Frame(new Bounds(Vector3.zero, Vector3.one), 60f, 0f, 30f, 18f).Position, Vector3.zero), Is.EqualTo(18f).Within(1e-3f),
                "never closer than the start distance");
        }
    }
}
