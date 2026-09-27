#nullable enable
using System;
using NUnit.Framework;
using TinCan.Features.Abilities.Cues;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Pooled cue instances follow their target, return on time or on release, and are reused.</summary>
    public class GameplayCuePresenterTests
    {
        private FakeTimeService _time = null!;
        private FakeHudValues _hud = null!;
        private GameplayCuePresenter _presenter = null!;
        private GameObject _target = null!;
        private GameObject _prefab = null!;
        private readonly Guid _actor = Guid.NewGuid();

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { DeltaTime = 1f };
            _hud = new FakeHudValues();
            _presenter = new GameplayCuePresenter(_time, _hud);
            _target = new GameObject("Target");
            _prefab = new GameObject("Sparks");
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            if (_target != null) Object.DestroyImmediate(_target);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Timed_SitsUnderTheTarget_ThenReturnsToThePoolAndIsReused()
        {
            _presenter.SpawnTimed(_prefab, _target.transform, Vector3.up, 1.5f);
            var instance = _target.transform.GetChild(0);
            Assert.That(instance.localPosition, Is.EqualTo(Vector3.up));
            Assert.That(instance.gameObject.activeSelf, Is.True);

            _presenter.Tick();
            Assert.That(_presenter.TimedCount, Is.EqualTo(1), "1 s of 1.5");
            _presenter.Tick();
            Assert.That(_presenter.TimedCount, Is.EqualTo(0));
            Assert.That(_target.transform.childCount, Is.EqualTo(0));
            Assert.That(instance.gameObject.activeSelf, Is.False);

            _presenter.SpawnTimed(_prefab, _target.transform, Vector3.zero, 1f);
            Assert.That(_target.transform.GetChild(0), Is.SameAs(instance), "pooled instance reused");
        }

        [Test]
        public void Held_StaysUntilReleased_AndReplacesItsOwnKey()
        {
            var key = new GameplayCueInstanceKey(this, _actor);

            _presenter.SpawnHeld(key, _prefab, _target.transform, Vector3.zero);
            _presenter.SpawnHeld(key, _prefab, _target.transform, Vector3.zero);
            _presenter.Tick();
            Assert.That(_presenter.HeldCount, Is.EqualTo(1));
            Assert.That(_target.transform.childCount, Is.EqualTo(1));

            _presenter.Release(key);
            Assert.That(_presenter.HeldCount, Is.EqualTo(0));
            Assert.That(_target.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void ReleaseActor_ReturnsOnlyThatActorsInstances()
        {
            var other = Guid.NewGuid();
            _presenter.SpawnHeld(new GameplayCueInstanceKey(this, _actor), _prefab, _target.transform, Vector3.zero);
            _presenter.SpawnHeld(new GameplayCueInstanceKey(this, other), _prefab, _target.transform, Vector3.zero);

            _presenter.ReleaseActor(_actor);

            Assert.That(_presenter.HeldCount, Is.EqualTo(1));
        }

        [Test]
        public void InstanceDestroyedWithItsTarget_IsDropped_AndThePoolRecovers()
        {
            var key = new GameplayCueInstanceKey(this, _actor);
            _presenter.SpawnHeld(key, _prefab, _target.transform, Vector3.zero);
            Object.DestroyImmediate(_target);

            Assert.DoesNotThrow(() => _presenter.Release(key));

            _target = new GameObject("Target2");
            _presenter.SpawnTimed(_prefab, _target.transform, Vector3.zero, 1f);
            Assert.That(_target.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void HudText_ShowsForItsSeconds_AndRepeatingRestartsTheTime()
        {
            _presenter.ShowHudText("Part repaired", string.Empty, 2f);
            _presenter.Tick();
            _presenter.ShowHudText("Part repaired", string.Empty, 2f);
            _presenter.Tick();
            Assert.That(_hud.All.ContainsKey("Part repaired"), Is.True, "restarted at 1 s, so still showing at 2 s");

            _presenter.Tick();
            Assert.That(_hud.All.ContainsKey("Part repaired"), Is.False);
        }

        [Test]
        public void HudText_WithoutHud_IsSkipped()
        {
            var headless = new GameplayCuePresenter(_time);

            Assert.DoesNotThrow(() => headless.ShowHudText("Part repaired", string.Empty, 1f));
            Assert.DoesNotThrow(headless.Tick);
        }
    }
}
