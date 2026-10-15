#nullable enable
using NUnit.Framework;
using TinCan.Features.FirstPersonCamera;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="HeldItemSway"/>: held items hang from the eyes and trail the look, never by more than the cap.</summary>
    public class HeldItemSwayTests
    {
        [Test]
        public void Hold_AtALevelLook_IsTheBodysVisualRoot()
        {
            var look = Quaternion.Euler(0f, 40f, 0f);

            var (position, rotation) = HeldItemSway.Hold(new Vector3(1f, 2.7f, 3f), look, eyeHeight: 0.7f);

            Assert.That(Vector3.Distance(position, new Vector3(1f, 2f, 3f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(rotation, look), Is.LessThan(1e-3f));
        }

        [Test]
        public void Hold_LookingDown_SwingsTheItemsAboutTheEyes()
        {
            var eyes = new Vector3(0f, 1f, 0f);
            var look = Quaternion.Euler(90f, 0f, 0f);

            var (position, rotation) = HeldItemSway.Hold(eyes, look, eyeHeight: 0.7f);

            // Straight down: what hung below the eyes now sits behind them, still 0.7 from them, turned with the look.
            Assert.That(Vector3.Distance(position, eyes), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(Vector3.Distance(position, new Vector3(0f, 1f, -0.7f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(rotation, look), Is.LessThan(1e-3f));
        }

        [Test]
        public void Trail_EasesTowardTheLook()
        {
            var look = Quaternion.Euler(0f, 4f, 0f);

            var trailing = HeldItemSway.Trail(Quaternion.identity, look, sharpness: 10f, maxAngle: 20f, deltaTime: 0.02f);

            float angle = Quaternion.Angle(trailing, look);
            Assert.That(angle, Is.GreaterThan(0.1f));
            Assert.That(angle, Is.LessThan(4f));
        }

        [Test]
        public void Trail_NeverLagsMoreThanTheCap()
        {
            var look = Quaternion.Euler(0f, 90f, 0f);

            var trailing = HeldItemSway.Trail(Quaternion.identity, look, sharpness: 1f, maxAngle: 5f, deltaTime: 0.01f);

            Assert.That(Quaternion.Angle(trailing, look), Is.EqualTo(5f).Within(1e-2f));
        }

        [Test]
        public void Trail_SettlesOnTheLook()
        {
            var look = Quaternion.Euler(10f, 30f, 0f);
            var trailing = Quaternion.identity;

            for (int i = 0; i < 120; i++) trailing = HeldItemSway.Trail(trailing, look, sharpness: 16f, maxAngle: 5f, deltaTime: 1f / 60f);

            Assert.That(Quaternion.Angle(trailing, look), Is.LessThan(0.01f));
        }
    }
}
