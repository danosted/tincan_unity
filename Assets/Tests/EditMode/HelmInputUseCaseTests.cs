#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Features.Helm;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="HelmInputUseCase"/>: at the helm the Helmsman context's axes travel in the predicted input; elsewhere they stay zero.</summary>
    public class HelmInputUseCaseTests
    {
        private sealed class SwitchableContexts : IInputContexts
        {
            public readonly List<InputContext> Live = new();
            public IReadOnlyList<InputContext> Active => Live;
            public bool IsActive(InputContext? context) => context != null && Live.Contains(context);
            public event Action? Changed { add { } remove { } }
        }

        private HelmsmanInputContext _controls = null!;
        private FakeInputReader _input = null!;
        private SwitchableContexts _contexts = null!;
        private HelmInputUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _controls = FakeInputContexts.Helmsman();
            _input = new FakeInputReader();
            _input.Axes[_controls.Throttle!] = 1f;
            _input.Axes[_controls.Yaw!] = -1f;
            _input.Axes[_controls.Pitch!] = 0.5f;
            _contexts = new SwitchableContexts();
            _useCase = new HelmInputUseCase(_input, _controls, _contexts);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_controls);

        [Test]
        public void AtTheHelm_TheAxesGoIntoTheInput()
        {
            _contexts.Live.Add(_controls);
            var input = new HumanoidInputState();

            _useCase.Contribute(null!, ref input);

            Assert.That(input.StationAxes, Is.EqualTo(new Vector3(1f, -1f, 0.5f)));
        }

        [Test]
        public void AwayFromTheHelm_TheAxesStayZero_EvenWithTheKeysHeld()
        {
            var input = new HumanoidInputState();

            _useCase.Contribute(null!, ref input);

            Assert.That(input.StationAxes, Is.EqualTo(Vector3.zero));
        }
    }
}
