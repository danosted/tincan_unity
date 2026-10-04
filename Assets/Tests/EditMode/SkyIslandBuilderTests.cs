#nullable enable
using NUnit.Framework;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="SkyIslandBuilder"/>: a built island is solid where the layout put it, from above and from the side.</summary>
    public class SkyIslandBuilderTests
    {
        private SkyIslandConfig _config = null!;
        private SkyIslandBuilder _builder = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkyIslandConfig>();
            _builder = new SkyIslandBuilder(_config, new SkyIslandMeshProcessor());
        }

        [TearDown]
        public void TearDown()
        {
            _builder.Clear();
            foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (root != null && root.parent == null && root.name == "SkyIslands") Object.DestroyImmediate(root.gameObject);
            }
            Object.DestroyImmediate(_config);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ABuiltIsland_StopsARayFromAboveAndFromTheSide(int index)
        {
            var island = new SkyIslandSpec(new SkyIslandId(3, 2, -1, index), new Vector3(1000f, 30f, -400f),
                index == 0 ? 60f : 10f, index == 0 ? 80f : 15f, 99u);

            _builder.Build(island);
            Physics.SyncTransforms();

            Assert.That(Physics.Raycast(island.Top + Vector3.up * 200f, Vector3.down, out var down, 400f), Is.True, "from above");
            Assert.That(down.collider.name, Is.EqualTo(SkyIslandBuilder.NameOf(island.Id)));
            Assert.That(down.point.y, Is.InRange(island.Top.y - 2f, island.Top.y + island.Radius * 0.2f));

            var side = island.Top + Vector3.down * island.Depth * 0.2f;
            Assert.That(Physics.Raycast(side + Vector3.left * 300f, Vector3.right, out _, 300f), Is.True, "from the side");
        }

        [Test]
        public void AnyNear_KnowsWhereTheStandingIslandsAre()
        {
            var island = new SkyIslandSpec(new SkyIslandId(3, 0, 0, 0), new Vector3(500f, 30f, 0f), 40f, 60f, 5u);
            Assert.That(_builder.AnyNear(island.Top, 1f), Is.False, "nothing built yet");

            var built = _builder.Build(island);

            Assert.That(_builder.AnyNear(island.Top + Vector3.down * 30f, 1f), Is.True, "inside its rock");
            Assert.That(_builder.AnyNear(island.Top + Vector3.right * 60f, 30f), Is.True, "20 m off its edge, reaching 30");
            Assert.That(_builder.AnyNear(island.Top + Vector3.right * 60f, 10f), Is.False, "20 m off its edge, reaching 10");
            Assert.That(_builder.AnyNear(Vector3.zero, 100f), Is.False, "far away");

            _builder.Remove(built);
            Assert.That(_builder.AnyNear(island.Top, 1f), Is.False, "removed");
        }

        [Test]
        public void EveryShape_GivesTheColliderAMesh()
        {
            for (uint seed = 0; seed < 40; seed++)
            {
                var island = new SkyIslandSpec(new SkyIslandId(3, (int)seed, 0, (int)(seed % 2)), new Vector3(seed * 500f, 30f, 0f),
                    seed % 2 == 0 ? 40f : 8f, seed % 2 == 0 ? 50f : 12f, seed);
                _builder.Build(island);
                var collider = GameObject.Find(SkyIslandBuilder.NameOf(island.Id))!.GetComponent<MeshCollider>();
                Assert.That(collider.sharedMesh != null, Is.True, $"shape seed {seed}");
            }
        }

        [Test]
        public void ARemovedIsland_IsNoLongerSolid_AndItsObjectIsReused()
        {
            var island = new SkyIslandSpec(new SkyIslandId(3, 0, 0, 0), new Vector3(0f, 30f, 0f), 40f, 50f, 7u);
            var built = _builder.Build(island);

            _builder.Remove(built);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(island.Top + Vector3.up * 200f, Vector3.down, 400f), Is.False);

            var next = new SkyIslandSpec(new SkyIslandId(3, 5, 5, 0), new Vector3(2000f, 30f, 0f), 40f, 50f, 8u);
            var again = _builder.Build(next);
            Assert.That(again, Is.SameAs(built), "the pooled object");

            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(next.Top + Vector3.up * 200f, Vector3.down, out var hit, 400f), Is.True, "the reused object is solid");
            Assert.That(hit.collider.name, Is.EqualTo(SkyIslandBuilder.NameOf(next.Id)));
        }
    }
}
