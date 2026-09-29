#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using TinCan.Core.Gas.Cues;
using TinCan.Core.Gas.Cues.Actions;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Each action asks the presenter for the right thing, and does nothing without its data or a target.</summary>
    public class GameplayCueActionTests
    {
        private GameObject _target = null!;
        private GameObject _prefab = null!;
        private GameplayTag _cue = null!;
        private FakeAbilityController _controller = null!;
        private RecordingCuePresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject("Target");
            _prefab = new GameObject("Sparks");
            _cue = ScriptableObject.CreateInstance<GameplayTag>();
            _controller = new FakeAbilityController();
            _presenter = new RecordingCuePresenter();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_target);
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_cue);
        }

        [Test]
        public void SpawnPrefab_InABurst_IsTimed()
        {
            var action = new SpawnPrefabCueAction { Prefab = _prefab, Lifetime = 1.5f };

            action.Run(Event(_target.transform), Context(action, GameplayCueEventKind.Execute));

            Assert.That(_presenter.Calls, Is.EqualTo(new[] { "timed Sparks 1.5" }));
        }

        [Test]
        public void SpawnPrefab_WhileActive_IsHeldUnderItsKey_AndStopReleasesIt()
        {
            var action = new SpawnPrefabCueAction { Prefab = _prefab };
            var context = Context(action, GameplayCueEventKind.Active);

            action.Run(Event(_target.transform), context);
            action.Stop(Event(_target.transform), context);

            Assert.That(_presenter.Held, Is.EqualTo(new[] { context.Key }));
            Assert.That(_presenter.Released, Is.EqualTo(new[] { context.Key }));
        }

        [Test]
        public void SpawnPrefab_WithoutPrefabOrTarget_DoesNothing()
        {
            var empty = new SpawnPrefabCueAction();
            var action = new SpawnPrefabCueAction { Prefab = _prefab };

            empty.Run(Event(_target.transform), Context(empty, GameplayCueEventKind.Execute));
            action.Run(Event(null), Context(action, GameplayCueEventKind.Execute));

            Assert.That(_presenter.Calls, Is.Empty);
        }

        [Test]
        public void PlaySound_PlaysTheClipAtTheTarget()
        {
            var clip = AudioClip.Create("Bang", 10, 1, 44100, false);
            var action = new PlaySoundCueAction { Clip = clip, Volume = 0.5f };

            action.Run(Event(_target.transform), Context(action, GameplayCueEventKind.Execute));

            Assert.That(_presenter.Calls, Is.EqualTo(new[] { "sound Bang 0.5" }));
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void HudToast_ShowsItsTextForItsSeconds_AndSkipsBlankText()
        {
            var toast = new HudToastCueAction { Text = "Part repaired", Seconds = 2f };
            var blank = new HudToastCueAction { Text = " " };

            toast.Run(Event(_target.transform), Context(toast, GameplayCueEventKind.Removed));
            blank.Run(Event(_target.transform), Context(blank, GameplayCueEventKind.Removed));

            Assert.That(_presenter.Calls, Is.EqualTo(new[] { "hud Part repaired 2" }));
        }

        private GameplayCueEvent Event(Transform? target) => new(_cue, target, _controller, GameplayCuePeerRole.Server);

        private GameplayCueActionContext Context(GameplayCueAction action, GameplayCueEventKind phase) =>
            new(_presenter, phase, new GameplayCueInstanceKey(action, _controller.Id));
    }
}
