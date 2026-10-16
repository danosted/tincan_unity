#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardFramePickProcessor"/>: nodes before edges before faces, nothing behind a face, the floor plane, and the dragged node left out.</summary>
    public class ShipyardFramePickProcessorTests
    {
        private readonly ShipyardFramePickProcessor _pick = new();
        private readonly ShipFrameShape _deck = ShipFrameShape.From(ShipFrameTestDesigns.DeckFrame());

        private ShipyardFramePick Down(float x, float z, int ignore = 0) =>
            _pick.Pick(_deck, new ShipyardRay(new Vector3(x, 10f, z), Vector3.down), -0.5f, 0.35f, ignore);

        [Test]
        public void ANodeWins_ThenAnEdge_ThenAFace()
        {
            Assert.That((Down(-2f, -2f).Kind, Down(-2f, -2f).Id), Is.EqualTo((ShipyardFramePickKind.Node, 1)));
            Assert.That((Down(-2f, 0.5f).Kind, Down(-2f, 0.5f).Id), Is.EqualTo((ShipyardFramePickKind.Edge, 5)));
            Assert.That((Down(0f, 0f).Kind, Down(0f, 0f).Id), Is.EqualTo((ShipyardFramePickKind.Face, 9)));
            Assert.That(Down(8f, 8f).Kind, Is.EqualTo(ShipyardFramePickKind.None));
        }

        [Test]
        public void AWall_HidesTheEdgeBehindIt()
        {
            var box = ShipFrameShape.From(ShipFrameTestDesigns.TwoStoryBox());

            // From outside the -X wall (23), down through it onto the far floor edge (11, the +X side).
            var origin = new Vector3(-6f, 1.5f, 0f);
            var pick = _pick.Pick(box, new ShipyardRay(origin, new Vector3(2f, -0.5f, 0f) - origin), -0.5f, 0.35f);

            Assert.That((pick.Kind, pick.Id), Is.EqualTo((ShipyardFramePickKind.Face, 23)));
        }

        [Test]
        public void TheFloorPlane_IsFoundUnderTheCursor_EvenOverNothing()
        {
            var pick = Down(8f, 8f);

            Assert.That(pick.OnPlane, Is.True);
            Assert.That(Vector3.Distance(pick.PlanePoint, new Vector3(8f, -0.5f, 8f)), Is.LessThan(1e-4f));
            Assert.That(_pick.Pick(_deck, new ShipyardRay(new Vector3(0f, 10f, 0f), Vector3.up), -0.5f, 0.35f).OnPlane, Is.False, "looking away");
        }

        [Test]
        public void TheDraggedNode_AndItsEdges_AreLeftOut()
        {
            var pick = Down(-2f, -2f, ignore: 1);

            Assert.That((pick.Kind, pick.Id), Is.EqualTo((ShipyardFramePickKind.Face, 9)), "not node 1 nor its edges: only the face's corner is left under it");
        }
    }
}
