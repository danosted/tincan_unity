#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipFrameMeshProcessor"/>: plank slabs whose top is where the nodes are, facing out on every side; beams
    /// under the floor's top; UVs in metres; the same frame always gives the same meshes, and a story's fingerprint
    /// changes only with its own data.
    /// </summary>
    public class ShipFrameMeshProcessorTests
    {
        private readonly ShipFrameMeshProcessor _processor = new();
        private readonly ShipFrameTuning _tuning = ShipFrameTuning.Default;

        private static Vector3 Normal(ShipFrameMesh mesh, int triangle)
        {
            var v = mesh.Vertices;
            int i = triangle * 3;
            return Vector3.Cross(v[mesh.Triangles[i + 1]] - v[mesh.Triangles[i]], v[mesh.Triangles[i + 2]] - v[mesh.Triangles[i]]).normalized;
        }

        [Test]
        public void ThePlankDeck_IsASlab_ItsTopWhereTheNodesAre()
        {
            var story = _processor.Build(ShipFrameTestDesigns.DeckFrame(), _tuning).Single();
            var planks = story.Planks;

            Assert.That(planks.Vertices, Has.Count.EqualTo(4 + 4 + 4 * 4), "top, bottom, four sides");
            Assert.That(planks.Triangles, Has.Count.EqualTo(3 * (2 + 2 + 4 * 2)));
            Assert.That(planks.Vertices.Max(v => v.y), Is.EqualTo(-0.3f).Within(1e-4f), "the drifted corner");
            Assert.That(planks.Vertices.Min(v => v.y), Is.EqualTo(-0.5f - _tuning.PlankThickness).Within(1e-4f));
            Assert.That(Normal(planks, 0).y, Is.GreaterThan(0.99f), "the top faces up");
            Assert.That(Normal(planks, 1).y, Is.LessThan(-0.99f), "the bottom faces down");
        }

        [Test]
        public void EverySideOfASlab_FacesOut_WhicheverWayTheLoopRuns()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var reversed = frame.With(faces: new[] { new ShipFrameFace(9, new[] { 4, 3, 2, 1 }, ShipFrameFill.Planks) });

            foreach (var planks in new[] { frame, reversed }.Select(f => _processor.Build(f, _tuning).Single().Planks))
            {
                Assert.That(Normal(planks, 0).y, Is.GreaterThan(0.99f));
                for (int triangle = 4; triangle < planks.Triangles.Count / 3; triangle++)
                {
                    var centre = planks.Vertices[planks.Triangles[triangle * 3]];
                    var outward = new Vector3(centre.x, 0f, centre.z);
                    Assert.That(Vector3.Dot(Normal(planks, triangle), outward), Is.GreaterThan(0f), $"side triangle {triangle}");
                }
            }
        }

        [Test]
        public void Beams_HangUnderTheFloor_WithABlockAtEachNode()
        {
            var deck = ShipFrameTestDesigns.DeckFrame();
            var unrailed = deck.With(edges: deck.Edges.Select(e => new ShipFrameEdge(e.Id, e.A, e.B)));
            var beams = _processor.Build(unrailed, _tuning).Single().Beams;

            Assert.That(beams.Vertices, Has.Count.EqualTo((4 + 4) * 24), "four beams and four node blocks, 24 corners a box");
            // Every beam and block top sits 1 cm under the nodes it hangs from; the highest node is the drifted one at -0.3 m.
            Assert.That(beams.Vertices.Max(v => v.y), Is.EqualTo(-0.31f).Within(1e-3f));
            Assert.That(beams.Vertices.Where(v => v.x < -1.5f).Max(v => v.y), Is.EqualTo(-0.51f).Within(1e-3f), "the level -X side");
        }

        [Test]
        public void AnOpenFace_HasNoPlanks_ButKeepsItsBeams()
        {
            var story = _processor.Build(ShipFrameTestDesigns.DeckFrame(ShipFrameFill.Open), _tuning).Single();

            Assert.That(story.Planks.IsEmpty, Is.True);
            Assert.That(story.Beams.IsEmpty, Is.False);
        }

        [Test]
        public void PlankUvs_RunInMetresAlongTheLongestSide()
        {
            var planks = _processor.Build(ShipFrameTestDesigns.DeckFrame(), _tuning).Single().Planks;

            // Corners 1 and 2 are 4 m apart: 4 / TextureMetres repeats apart.
            float repeats = Vector2.Distance(planks.Uvs[0], planks.Uvs[1]);
            Assert.That(repeats, Is.EqualTo(4f / _tuning.TextureMetres).Within(0.02f));
        }

        [Test]
        public void TheSameFrame_GivesTheSameMeshes_AndAStoryChangesOnlyWithItsOwnData()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var twoStories = frame.With(storyHeights: new[] { 30, 30 },
                nodes: frame.Nodes.Concat(new[] { new ShipFrameNode(11, 1, -20, 0, -20), new ShipFrameNode(12, 1, 20, 0, -20), new ShipFrameNode(13, 1, 0, 0, 20) }),
                edges: frame.Edges.Concat(new[] { new ShipFrameEdge(14, 11, 12), new ShipFrameEdge(15, 12, 13), new ShipFrameEdge(16, 13, 11) }));
            var movedUpstairs = twoStories.With(nodes: twoStories.Nodes.Select(n => n.Id == 13 ? n.MovedTo(0, 0, 10) : n));

            var first = _processor.Build(twoStories, _tuning);
            var again = _processor.Build(twoStories, _tuning);
            var moved = _processor.Build(movedUpstairs, _tuning);

            Assert.That(again.Select(s => s.Fingerprint), Is.EqualTo(first.Select(s => s.Fingerprint)));
            Assert.That(again[0].Planks.Vertices, Is.EqualTo(first[0].Planks.Vertices));
            Assert.That(again[0].Planks.Triangles, Is.EqualTo(first[0].Planks.Triangles));
            Assert.That(moved[0].Fingerprint, Is.EqualTo(first[0].Fingerprint), "story 0 did not change");
            Assert.That(moved[1].Fingerprint, Is.Not.EqualTo(first[1].Fingerprint));
            Assert.That(first[1].Planks.IsEmpty, Is.True, "no face upstairs");
            Assert.That(first[1].Beams.Vertices.Min(v => v.y), Is.GreaterThan(2f), "story 1's floor is 3 m up");
        }

        [Test]
        public void Stairs_AreStepsToSee_AndOneRampToWalk_InTheStoryBelowTheirLanding()
        {
            var tuning = ShipFrameTuning.Default;
            var stories = new ShipFrameMeshProcessor().Build(ShipFrameTestDesigns.StairWell(), tuning);

            var lower = stories[0];
            Assert.That(lower.Steps.Triangles.Count, Is.EqualTo(15 * 12 * 3), "3 m at 0.2 m a step: 15 blocks of 12 triangles");
            Assert.That(lower.Ramps.Triangles.Count, Is.EqualTo(12 * 3), "one ramp");
            Assert.That(stories[1].Steps.IsEmpty && stories[1].Ramps.IsEmpty, Is.True);
            Assert.That(lower.Ramps.Vertices.Max(v => v.y), Is.EqualTo(2.5f).Within(0.06f), "up to the landing at 2.5 m");
            Assert.That(lower.Ramps.Vertices.Max(v => v.z), Is.EqualTo(5f).Within(0.1f), "down to the floor 5 m forward of the landing");
        }

        [Test]
        public void AGate_IsTwoPosts_ARailingHasRailsToo()
        {
            var tuning = ShipFrameTuning.Default;
            var deck = ShipFrameTestDesigns.DeckFrame();
            int Beams(ShipFrameRailing rail) =>
                new ShipFrameMeshProcessor().Build(deck.With(edges: deck.Edges.Select(e => e.Id == 6 ? e.WithRail(rail) : e)), tuning)[0].Beams.Triangles.Count;

            int none = Beams(ShipFrameRailing.None);
            Assert.That(Beams(ShipFrameRailing.Gate) - none, Is.EqualTo(2 * 12 * 3), "two posts");
            Assert.That(Beams(ShipFrameRailing.Rail), Is.GreaterThan(Beams(ShipFrameRailing.Gate)));
        }
    }
}
