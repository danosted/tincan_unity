#nullable enable
using NUnit.Framework;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipyardAimProcessor"/>: a face part goes on the plank floor the ray comes down onto, snapped to the frame
    /// step; a node part on the node under the cursor; Remove takes the part the ray hits.
    /// </summary>
    public class ShipyardAimProcessorTests
    {
        private readonly ShipyardAimProcessor _aim = new();
        private readonly ShipFrameShape _deck = ShipFrameShape.From(ShipFrameTestDesigns.DeckFrame());

        [Test]
        public void AFacePart_GoesOnTheFloorUnderTheCursor_SnappedToTheStep()
        {
            var aim = _aim.Aim(new ShipyardRay(new Vector3(0.62f, 10f, 0.81f), Vector3.down), _deck, ShipPartAttach.Face, 5, 0.35f);

            Assert.That(aim.IsValid, Is.True);
            Assert.That(aim.Anchor, Is.EqualTo(ShipPartAnchor.OnFace(9, 5, 10)), "half-metre steps");
        }

        [Test]
        public void NearTheFloorsEdge_TheSnappedPointIsOffIt_SoTheHitItselfIsUsed()
        {
            // 1.97 m snaps to 2 m, the edge: the hit (1.97 m) is on the floor.
            var aim = _aim.Aim(new ShipyardRay(new Vector3(1.97f, 10f, 0f), Vector3.down), _deck, ShipPartAttach.Face, 5, 0.35f);

            Assert.That(aim.Anchor.X, Is.EqualTo(20).Or.EqualTo(19));
            Assert.That(ShipPartPose.TryGetAnchorPoint(aim.Anchor, _deck, out _), Is.True);
        }

        [Test]
        public void OffTheFloor_OrLookingUp_NothingIsAimedAt()
        {
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(8f, 10f, 8f), Vector3.down), _deck, ShipPartAttach.Face, 5, 0.35f).IsValid, Is.False);
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, -5f, 0f), Vector3.up), _deck, ShipPartAttach.Face, 5, 0.35f).IsValid, Is.False);
            Assert.That(_aim.Aim(new ShipyardRay(Vector3.zero, Vector3.up), ShipFrameShape.From(ShipFrame.Empty), ShipPartAttach.Face, 5, 0.35f).IsValid,
                Is.False, "no frame, nowhere");
        }

        [Test]
        public void ANodePart_GoesOnTheNodeNearestTheCamera_WithinTheRadius()
        {
            // Node 1 at (-2, -0.5, -2); node 4 at (2, -0.5, -2) is behind it along this ray.
            var along = new ShipyardRay(new Vector3(-6f, -0.5f, -2f), Vector3.right);
            var near = new ShipyardRay(new Vector3(-2.2f, 10f, -2f), Vector3.down);
            var far = new ShipyardRay(new Vector3(-1f, 10f, -1f), Vector3.down);

            Assert.That(_aim.Aim(along, _deck, ShipPartAttach.Node, 5, 0.35f).Anchor, Is.EqualTo(ShipPartAnchor.OnNode(1)));
            Assert.That(_aim.Aim(near, _deck, ShipPartAttach.Node, 5, 0.35f).Anchor, Is.EqualTo(ShipPartAnchor.OnNode(1)));
            Assert.That(_aim.Aim(far, _deck, ShipPartAttach.Node, 5, 0.35f).IsValid, Is.False);
        }

        [Test]
        public void ThePartUnderTheCursor_IsJustBehindTheSurfaceTheRayHits()
        {
            var onTop = new ShipyardRay(new Vector3(2f, 5f, 3f), Vector3.down, true, new Vector3(2.2f, 0.5f, 2.9f), Vector3.up);

            var aim = _aim.Aim(onTop, _deck, ShipPartAttach.Face, 5, 0.35f);

            Assert.That(Vector3.Distance(aim.PartPoint!.Value, new Vector3(2.2f, 0.4f, 2.9f)), Is.LessThan(1e-4f));
        }
    }
}
