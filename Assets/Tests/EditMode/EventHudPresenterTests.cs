#nullable enable
using NUnit.Framework;
using TinCan.Features.DesignedEvents;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>EventHudPresenter: every peer shows the replicated announcement; without the state it leaves the HUD alone.</summary>
    public class EventHudPresenterTests
    {
        [Test]
        public void ShowsTheReplicatedAnnouncement_AndRemovesItWhenCleared()
        {
            var actors = new FakeActorRegistry();
            var hud = new FakeHudValues();
            var state = new FakeEventState { Announcement = "Gas ahead" };
            actors.Register(state);
            var presenter = new EventHudPresenter(actors, hud);

            presenter.Tick();
            Assert.That(hud.All[EventHudPresenter.HudKey], Is.EqualTo("Gas ahead"));

            state.Announcement = string.Empty;
            presenter.Tick();
            Assert.That(hud.All.ContainsKey(EventHudPresenter.HudKey), Is.False);
        }

        [Test]
        public void WritesOnlyWhenTheAnnouncementChanges()
        {
            var actors = new FakeActorRegistry();
            var hud = new FakeHudValues();
            actors.Register(new FakeEventState { Announcement = "x" });
            var presenter = new EventHudPresenter(actors, hud);

            presenter.Tick();
            presenter.Tick();
            Assert.That(hud.SetCalls, Is.EqualTo(1));
        }

        [Test]
        public void WithoutEventState_LeavesTheHudLineAlone()
        {
            var hud = new FakeHudValues();
            hud.Set(EventHudPresenter.HudKey, "host-only line");
            new EventHudPresenter(new FakeActorRegistry(), hud).Tick();
            Assert.That(hud.All[EventHudPresenter.HudKey], Is.EqualTo("host-only line"));
        }
    }
}
