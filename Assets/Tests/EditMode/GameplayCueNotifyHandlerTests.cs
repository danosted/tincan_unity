#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using TinCan.Features.Abilities.Cues;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>A notify runs the right list for each moment, and removal ends what OnActive started first.</summary>
    public class GameplayCueNotifyHandlerTests
    {
        private sealed class RecordingAction : GameplayCueAction
        {
            public readonly string Name;
            public readonly List<string> Log;

            public RecordingAction(string name, List<string> log)
            {
                Name = name;
                Log = log;
            }

            public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context) => Log.Add($"{Name} run {context.Phase}");
            public override void Stop(in GameplayCueEvent cueEvent, in GameplayCueActionContext context) => Log.Add($"{Name} stop");
        }

        private readonly List<string> _log = new();
        private GameplayCueNotify _notify = null!;
        private GameplayTag _cue = null!;
        private GameplayCueNotifyHandler _handler = null!;
        private GameplayCueEvent _event;

        [SetUp]
        public void SetUp()
        {
            _cue = ScriptableObject.CreateInstance<GameplayTag>();
            _notify = ScriptableObject.CreateInstance<GameplayCueNotify>();
            Set("_cue", _cue);
            Set("_onExecute", new List<GameplayCueAction?> { new RecordingAction("bang", _log), null });
            Set("_onActive", new List<GameplayCueAction?> { new RecordingAction("sparks", _log) });
            Set("_onRemoved", new List<GameplayCueAction?> { new RecordingAction("ding", _log) });
            _handler = new GameplayCueNotifyHandler(_notify, new RecordingCuePresenter());
            _event = new GameplayCueEvent(_cue, null, new FakeAbilityController(), GameplayCuePeerRole.Server);
            _log.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_notify);
            Object.DestroyImmediate(_cue);
        }

        [Test]
        public void Cue_IsTheNotifysTag() => Assert.That(_handler.Cue, Is.SameAs(_cue));

        [Test]
        public void Execute_RunsOnExecute_SkippingEmptyEntries()
        {
            _handler.OnExecute(_event);

            Assert.That(_log, Is.EqualTo(new[] { "bang run Execute" }));
        }

        [Test]
        public void Active_RunsOnActive()
        {
            _handler.OnActive(_event);

            Assert.That(_log, Is.EqualTo(new[] { "sparks run Active" }));
        }

        [Test]
        public void Removed_StopsOnActive_ThenRunsOnRemoved()
        {
            _handler.OnRemoved(_event);

            Assert.That(_log, Is.EqualTo(new[] { "sparks stop", "ding run Removed" }));
        }

        private void Set(string field, object value) =>
            typeof(GameplayCueNotify).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_notify, value);
    }
}
