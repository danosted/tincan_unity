#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipPartBlocks"/> and <see cref="ShipPartPose"/>: a part's 1 m blocks stand on its anchor point, turned
    /// with its yaw; blocks closer than a block on every axis overlap, side by side do not; a point is in the block that
    /// holds it.
    /// </summary>
    public class ShipPartBlocksTests
    {
        [Test]
        public void Blocks_OverlapWhenCloserThanABlock_ButNotSideBySide()
        {
            var space = new ShipPartBlocks();
            space.Add(new Vector3(0f, 0.5f, 0f), 1);

            Assert.That(space.TryGetOverlap(new Vector3(0.9f, 0.5f, -0.9f), 2, out var owner), Is.True);
            Assert.That(owner, Is.EqualTo(1));
            Assert.That(space.TryGetOverlap(new Vector3(1f, 0.5f, 0f), 2, out _), Is.False, "a block apart: side by side");
            Assert.That(space.TryGetOverlap(new Vector3(0f, 0.5f, 0f), 1, out _), Is.False, "a part never overlaps itself");
        }

        [Test]
        public void APoint_IsInTheBlockThatHoldsIt()
        {
            var space = new ShipPartBlocks();
            space.Add(new Vector3(0f, 0.5f, 0f), 1);
            space.Add(new Vector3(1f, 0.5f, 0f), 2);

            Assert.That(space.TryGetOwnerAt(new Vector3(1.2f, 0.9f, -0.4f), out var owner) ? owner : 0, Is.EqualTo(2));
            Assert.That(space.TryGetOwnerAt(new Vector3(0f, 1.2f, 0f), out _), Is.False, "above both");
            Assert.That(space.All.Select(b => b.Owner), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void APartsBlocks_StandOnItsAnchorPoint_TurnedWithItsYaw()
        {
            using var parts = new ShipDesignTestParts();
            var beam = new ShipPartPlacement(10, ShipDesignTestParts.Beam, ShipPartAnchor.OnFace(9, 0, 0), 1);
            var anchor = new Vector3(2f, -0.5f, 3f);

            var blocks = ShipPartPose.Blocks(beam, parts[ShipDesignTestParts.Beam], anchor);

            Assert.That(blocks, Is.EqualTo(new[] { new Vector3(2f, 0f, 3f), new Vector3(2f, 0f, 2f), new Vector3(2f, 0f, 1f) }),
                "a beam along +X turned a quarter runs along -Z, each block's underside on the floor");
            Assert.That(parts[ShipDesignTestParts.Post].Attach, Is.EqualTo(ShipPartAttach.Node));
        }

        [Test]
        public void AFacePart_StandsAtTheFloorsHeightUnderIt_AndANodePart_AtItsNode()
        {
            using var parts = new ShipDesignTestParts();
            var deck = ShipFrameShape.From(ShipFrameTestDesigns.DeckFrame());
            var block = new ShipPartPlacement(10, ShipDesignTestParts.Block, ShipPartAnchor.OnFace(9, 15, 15), 0);
            var post = new ShipPartPlacement(11, ShipDesignTestParts.Post, ShipPartAnchor.OnNode(3), 0);

            Assert.That(ShipPartPose.TryGetPose(block, parts[ShipDesignTestParts.Block], deck, out var at, out _), Is.True);
            Assert.That(at.y, Is.GreaterThan(-0.5f), "near corner 3, drifted up 0.2 m");
            Assert.That(ShipPartPose.TryGetPose(post, parts[ShipDesignTestParts.Post], deck, out var onNode, out _), Is.True);
            Assert.That(Vector3.Distance(onNode, new Vector3(2f, -0.3f, 2f)), Is.LessThan(1e-4f));
            Assert.That(ShipPartPose.TryGetPose(post, parts[ShipDesignTestParts.Block], deck, out _, out _), Is.False, "a block does not sit on a node");
        }
    }
}
