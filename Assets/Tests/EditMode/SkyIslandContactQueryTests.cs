#nullable enable
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="PhysicsSkyIslandContactQuery"/> against a real built island: a ship whose hull is inside the rock gets a
    /// push out of it (pushing it by that much clears it), one beside the rock gets none, and triggers never count.
    /// </summary>
    public class SkyIslandContactQueryTests
    {
        private static readonly SkyIslandSpec Island =
            new(new SkyIslandId(5, 0, 0, 0), new Vector3(0f, 30f, 0f), 50f, 70f, 12u);

        private SkyIslandConfig _config = null!;
        private SkyIslandBuilder _builder = null!;
        private FakeAirshipView _ship = null!;
        private PhysicsSkyIslandContactQuery _query = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkyIslandConfig>();
            _builder = new SkyIslandBuilder(_config, new SkyIslandMeshProcessor());
            _builder.Build(Island);
            _query = new PhysicsSkyIslandContactQuery(_builder);

            _ship = new FakeAirshipView();
            var hull = new GameObject("Hull");
            hull.transform.SetParent(_ship.Transform, false);
            hull.AddComponent<BoxCollider>().size = new Vector3(10f, 6f, 30f);
            var trigger = new GameObject("InteractVolume");
            trigger.transform.SetParent(_ship.Transform, false);
            var volume = trigger.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(200f, 200f, 200f);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            _builder.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AHullInTheRock_IsPushedOut()
        {
            // Find the island's side 12 m below the top (clear of the rim), then poke the hull's bow 4 m into it.
            Physics.SyncTransforms();
            Assume.That(Physics.Raycast(new Vector3(-200f, 18f, 0f), Vector3.right, out var side, 200f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Place(new Vector3(side.point.x - 15f + 4f, 18f, 0f), Quaternion.Euler(0f, 90f, 0f));

            Assert.That(_query.TryPushOut(_ship, out var push, out var island), Is.True);
            Assert.That(island, Is.EqualTo(Island.Id));
            Assert.That(push.x, Is.LessThan(0f), "back out the way it came");
            Assert.That(push.magnitude, Is.InRange(0.5f, 9f), $"the 4 m it went in, or a little more out under the overhang: {push}");

            _ship.Transform.position += push * 1.01f;
            Physics.SyncTransforms();
            Assert.That(_query.TryPushOut(_ship, out _, out _), Is.False, "pushed clear");
        }

        [Test]
        public void AHullBesideTheRock_IsNotPushed()
        {
            Physics.SyncTransforms();
            Assume.That(Physics.Raycast(new Vector3(-200f, 18f, 0f), Vector3.right, out var side, 200f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Place(new Vector3(side.point.x - 15f - 20f, 18f, 0f), Quaternion.Euler(0f, 90f, 0f));

            Assert.That(_query.TryPushOut(_ship, out _, out _), Is.False, "20 m away; the big trigger around the ship does not count");
        }

        [Test]
        public void AHullDeepInTheRock_IsStillPushed()
        {
            Place(new Vector3(0f, 30f - Island.Depth * 0.3f, 0f), Quaternion.identity);

            Assert.That(_query.TryPushOut(_ship, out var push, out _), Is.True);
            Assert.That(push.magnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void AsAnObstacle_TheRockBlocks_AndTheSkyBesideItDoesNot()
        {
            _ship.Transform.position = new Vector3(0f, 500f, 0f); // its trigger out of the way
            Physics.SyncTransforms();
            var obstacles = new PhysicsSkyIslandObstacleQuery(_builder);

            Assert.That(obstacles.IsBlocked(Island.Top + Vector3.down * Island.Depth * 0.3f, 3f), Is.True, "inside the rock");
            Assert.That(obstacles.IsBlocked(Island.Top + Vector3.right * (Island.Radius + 20f), 3f), Is.False, "beside it");
            Assert.That(obstacles.IsBlocked(Island.Top + Vector3.up * 30f, 3f), Is.False, "above it");
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            _ship.Transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }
    }
}
