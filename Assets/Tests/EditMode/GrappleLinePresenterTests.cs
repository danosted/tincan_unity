#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.GrappleHook;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class GrappleLinePresenterTests
    {
        private GrappleHookConfig _config = null!;
        private FakeHooks _hooks = null!;
        private FakeActorRegistry _actors = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _player = null!;
        private GrappleLinePresenter? _presenter;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GrappleHookConfig>();
            _hooks = new FakeHooks();
            _actors = new FakeActorRegistry();
            _movement = new FakeHumanoidMovementView("Player");
            _player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            _movement.Destroy();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Server_DrawsEachHookedPlayersLine_FromTheHookTipToTheHookPoint()
        {
            var tip = new GameObject(_config.LineStartName).transform;
            tip.SetParent(_movement.Transform, false);
            tip.localPosition = new Vector3(0.3f, 0.2f, 0.5f);
            _hooks.Points[_player.Id] = new Vector3(0f, 10f, 30f);
            _presenter = new GrappleLinePresenter(_config, _hooks, _actors, new FakeNetworkService());

            _presenter.Tick();

            var line = _presenter.LineOf(_player.Id);
            Assert.That(line, Is.Not.Null);
            Assert.That(Vector3.Distance(line!.GetPosition(0), tip.position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(line.GetPosition(1), new Vector3(0f, 10f, 30f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void ALineThatLetGo_IsHidden()
        {
            _hooks.Points[_player.Id] = new Vector3(0f, 10f, 30f);
            _presenter = new GrappleLinePresenter(_config, _hooks, _actors, new FakeNetworkService());
            _presenter.Tick();

            _hooks.Points.Clear();
            _presenter.Tick();

            Assert.That(_presenter.LineOf(_player.Id), Is.Null);
        }

        [Test]
        public void Client_DrawsOtherPlayersLines_FromTheReplicatedState()
        {
            var state = new FakeLineState { Point = new Vector3(4f, 5f, 6f) };
            _actors.Register(state);
            _hooks.Points[_player.Id] = new Vector3(99f, 99f, 99f);
            _presenter = new GrappleLinePresenter(_config, _hooks, _actors, new FakeSessionNetworkService { IsServer = false, IsClient = true });

            _presenter.Tick();

            var line = _presenter.LineOf(_player.Id);
            Assert.That(line, Is.Not.Null);
            Assert.That(Vector3.Distance(line!.GetPosition(1), state.Point), Is.LessThan(1e-4f), "A remote player's line is the replicated one.");
        }

        [Test]
        public void LineStart_WithoutATip_IsBelowTheEyes()
        {
            var body = new GameObject("Body").transform;
            body.position = new Vector3(1f, 2f, 3f);

            Vector3 start = GrappleLinePresenter.LineStart(null, body, eyeHeight: 1f);

            Assert.That(Vector3.Distance(start, new Vector3(1f, 2.7f, 3f)), Is.LessThan(1e-4f));
            Object.DestroyImmediate(body.gameObject);
        }

        private sealed class FakeHooks : IGrappleHooks
        {
            public Dictionary<Guid, Vector3> Points { get; } = new();
            public bool TryGetHookPoint(Guid playerId, out Vector3 worldPoint) => Points.TryGetValue(playerId, out worldPoint);
        }

        private sealed class FakeLineState : IGrappleLineState
        {
            public Vector3 Point;
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public void ServerSetLines(IReadOnlyList<GrappleLineSnapshot> lines) { }

            public bool TryGetHookPoint(IActor player, out Vector3 worldPoint)
            {
                worldPoint = Point;
                return true;
            }
        }
    }
}
