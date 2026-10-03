#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Look;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ViewRigSelection"/>: the first registered camera rig wins; none or several are allowed but warned about.</summary>
    public class ViewRigSelectionTests
    {
        private sealed class FakeRig : IViewRig
        {
            public void Take(Camera camera, System.Collections.Generic.IReadOnlyList<Renderer> body) { }
            public void Release(Camera camera, System.Collections.Generic.IReadOnlyList<Renderer> body) { }
            public (Vector3 Position, Quaternion Rotation) Place(in ViewPose pose) => (pose.VisualPosition, Quaternion.identity);
            public float AimHeight(float eyeHeight) => eyeHeight;
        }

        [Test]
        public void OneRig_IsUsed_WithoutWarning()
        {
            var rig = new FakeRig();

            var selected = ViewRigSelection.Select(new IViewRig[] { rig }, out var warning);

            Assert.That(selected, Is.SameAs(rig));
            Assert.That(warning, Is.Null);
        }

        [Test]
        public void SeveralRigs_TheFirstWins_WithAWarning()
        {
            var first = new FakeRig();

            var selected = ViewRigSelection.Select(new IViewRig[] { first, new FakeRig() }, out var warning);

            Assert.That(selected, Is.SameAs(first));
            Assert.That(warning, Does.Contain("2 camera rigs"));
        }

        [Test]
        public void NoRig_SelectsNothing_WithAWarning()
        {
            Assert.That(ViewRigSelection.Select(Array.Empty<IViewRig>(), out var warning), Is.Null);
            Assert.That(warning, Does.Contain("No camera rig"));
            Assert.That(ViewRigSelection.Select(null, out _), Is.Null);
        }
    }
}
