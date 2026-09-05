#nullable enable
using NUnit.Framework;
using TinCan.Features.UI;

namespace TinCan.Tests.EditMode
{
    public class HudUseCaseTests
    {
        [Test]
        public void Set_AddsValueAndRaisesChangedOncePerRealChange()
        {
            var hud = new HudUseCase();
            int changed = 0;
            hud.Changed += () => changed++;

            hud.Set("Fuel", "100");
            hud.Set("Fuel", "100");
            hud.Set("Fuel", "87");

            Assert.That(hud.All["Fuel"], Is.EqualTo("87"));
            Assert.That(changed, Is.EqualTo(2));
        }

        [Test]
        public void Remove_DropsValueAndIgnoresMissingKeys()
        {
            var hud = new HudUseCase();
            hud.Set("Fuel", "100");
            int changed = 0;
            hud.Changed += () => changed++;

            hud.Remove("Fuel");
            hud.Remove("Fuel");

            Assert.That(hud.All.ContainsKey("Fuel"), Is.False);
            Assert.That(changed, Is.EqualTo(1));
        }
    }
}
