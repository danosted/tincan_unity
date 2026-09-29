#nullable enable
using NUnit.Framework;
using TinCan.Core.UI;

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

        [Test]
        public void SetMeter_ClampsTheFill_AndRedrawsOnlyOnVisibleChange()
        {
            var hud = new HudUseCase();
            int changed = 0;
            hud.Changed += () => changed++;

            hud.SetMeter("Hull", 1.5f);
            hud.SetMeter("Hull", 1f);
            hud.SetMeter("Hull", 0.9999f);
            hud.SetMeter("Hull", 0.95f);

            Assert.That(hud.Meters["Hull"], Is.EqualTo(0.95f));
            Assert.That(changed, Is.EqualTo(2), "set once at 1 (clamped), then 0.95; the tiny step does not redraw");
        }

        [Test]
        public void RemoveMeter_DropsIt_AndIgnoresMissingKeys()
        {
            var hud = new HudUseCase();
            hud.SetMeter("Hull", 0.5f);
            int changed = 0;
            hud.Changed += () => changed++;

            hud.RemoveMeter("Hull");
            hud.RemoveMeter("Hull");

            Assert.That(hud.Meters.ContainsKey("Hull"), Is.False);
            Assert.That(changed, Is.EqualTo(1));
        }
    }
}
