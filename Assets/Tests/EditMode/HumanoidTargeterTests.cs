#nullable enable
using NUnit.Framework;
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="HumanoidTargeter.Facing"/>: targeting faces the replicated look, in the platform's yaw frame, not the turning body.</summary>
    public class HumanoidTargeterTests
    {
        [Test]
        public void Facing_IsTheLooksYaw_NotTheBodys()
        {
            var facing = HumanoidTargeter.Facing(Quaternion.Euler(0f, 40f, 0f), null, Quaternion.Euler(0f, 10f, 0f));

            Assert.That(facing.eulerAngles.y, Is.EqualTo(40f).Within(0.01f));
        }

        [Test]
        public void Facing_OnAPlatform_AddsThePlatformsYaw()
        {
            var platform = new GameObject("Deck");
            try
            {
                platform.transform.rotation = Quaternion.Euler(5f, 90f, 3f); // the deck banks; only its yaw counts

                var facing = HumanoidTargeter.Facing(Quaternion.Euler(0f, 40f, 0f), platform.transform, Quaternion.identity);

                Assert.That(facing.eulerAngles.y, Is.EqualTo(130f).Within(0.01f));
                Assert.That(facing.eulerAngles.x, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(platform);
            }
        }

        [Test]
        public void Facing_BeforeTheFirstInput_IsTheBodys()
        {
            var body = Quaternion.Euler(0f, 25f, 0f);

            Assert.That(HumanoidTargeter.Facing(default, null, body), Is.EqualTo(body));
        }
    }
}
