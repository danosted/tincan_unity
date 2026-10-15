#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.GrappleHook;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class GrappleAimPresenterTests
    {
        private GrappleHookConfig _config = null!;
        private GameplayTag _carrying = null!;
        private FakeQuery _query = null!;
        private FakeHooks _hooks = null!;
        private FakeActorRegistry _actors = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _player = null!;
        private GameObject _rock = null!;
        private GrappleAimPresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _carrying = ScriptableObject.CreateInstance<GameplayTag>();
            _config = ScriptableObject.CreateInstance<GrappleHookConfig>();
            _config.CarryingTag = _carrying;
            _config.MaxRange = 50f;
            _config.MarkerSightFactor = 2f;

            _rock = new GameObject("Rock");
            _query = new FakeQuery { Frame = _rock.transform };
            _hooks = new FakeHooks();
            _actors = new FakeActorRegistry();
            _movement = new FakeHumanoidMovementView("Player");
            _player = new FakeHumanoidCharacterView(_movement);
            _player.HeldTags.Add(_carrying);
            _actors.Register(_player);
            _actors.LocalPlayer = _player;
            _presenter = new GrappleAimPresenter(_config, _query, _hooks, _actors);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _movement.Destroy();
            Object.DestroyImmediate(_rock);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_carrying);
        }

        [Test]
        public void ASurfaceWithinRange_IsInRange_AtTheCatchPoint()
        {
            _query.Point = new Vector3(0f, 0.7f, 30f);

            _presenter.Tick();

            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.InRange));
            Assert.That(_presenter.MarkerPoint, Is.EqualTo(_query.Point));
        }

        [Test]
        public void ASurfaceBeyondRange_IsOutOfRange_AndTheMarkerLooksFurtherThanTheHookFlies()
        {
            _query.Point = new Vector3(0f, 0.7f, 70f);

            _presenter.Tick();

            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.OutOfRange));
            Assert.That(_query.Range, Is.EqualTo(100f), "Twice the hook's range.");
        }

        [Test]
        public void NothingInSight_ShowsNothing()
        {
            _query.Frame = null;

            _presenter.Tick();

            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.None));
        }

        [Test]
        public void ItAimsFromTheEyeAlongTheLiveLook()
        {
            _query.Point = new Vector3(30f, 0.7f, 0f);
            _movement.LookRotation = Quaternion.Euler(0f, 90f, 0f);
            _movement.LookPitch = 30f;

            _presenter.Tick();

            Assert.That(Vector3.Distance(_query.From, new Vector3(0f, 0.7f, 0f)), Is.LessThan(1e-4f));
            Assert.That(_query.Direction.x, Is.GreaterThan(0.8f), "Yaw 90 looks along +x.");
            Assert.That(_query.Direction.y, Is.EqualTo(-0.5f).Within(1e-4f), "Pitch 30 looks down.");
        }

        [Test]
        public void WithoutTheHook_OrWhileHooked_ShowsNothing()
        {
            _query.Point = new Vector3(0f, 0.7f, 30f);
            _player.HeldTags.Clear();
            _presenter.Tick();
            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.None), "Not holding the hook.");

            _player.HeldTags.Add(_carrying);
            _hooks.Points[_player.Id] = Vector3.zero;
            _presenter.Tick();
            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.None), "Already hooked: the line shows where it holds.");
        }

        [Test]
        public void OnlyTheLocalPlayerAims()
        {
            _query.Point = new Vector3(0f, 0.7f, 30f);
            _actors.LocalPlayer = null;

            _presenter.Tick();

            Assert.That(_presenter.Reach, Is.EqualTo(GrappleAimReach.None));
            Assert.That(_query.Calls, Is.EqualTo(0));
        }

        private sealed class FakeQuery : IGrappleHookQuery
        {
            public Transform? Frame;
            public Vector3 Point;
            public int Calls;
            public Vector3 From;
            public Vector3 Direction;
            public float Range;

            public bool TryCatch(Vector3 from, Vector3 direction, float range, LayerMask layers, Transform holder,
                out Transform frame, out Vector3 point)
            {
                Calls++;
                From = from;
                Direction = direction;
                Range = range;
                frame = Frame!;
                point = Point;
                return Frame != null;
            }
        }

        private sealed class FakeHooks : IGrappleHooks
        {
            public Dictionary<Guid, Vector3> Points { get; } = new();
            public bool TryGetHookPoint(Guid playerId, out Vector3 worldPoint) => Points.TryGetValue(playerId, out worldPoint);
        }
    }
}
