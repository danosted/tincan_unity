#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.UI;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Core.UI.Tests
{
    /// <summary>
    /// <see cref="CursorPolicyUseCase"/> and <see cref="CursorPolicyProcessor"/>: one owner of the cursor, deciding from the
    /// menus, the asks and possession, and writing only on a change.
    /// </summary>
    public class CursorPolicyUseCaseTests
    {
        private MenuUseCase _menus = null!;
        private FakeSessionNetworkService _network = null!;
        private FakePossessionState _possession = null!;
        private FakeCursorDevice _device = null!;
        private CursorPolicyUseCase _cursor = null!;
        private MenuDefinition _menu = null!;

        [SetUp]
        public void SetUp()
        {
            _menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            _network = new FakeSessionNetworkService { IsActive = false };
            _possession = new FakePossessionState();
            _device = new FakeCursorDevice();
            _cursor = new CursorPolicyUseCase(_network, _possession, _device);
            new MenuCursorPresenter(_menus, _cursor).Initialize();
            _menu = MenuDefinition.Create("main", "Main");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_menu);

        [Test]
        public void Decide_MenuFirst_ThenTheNewestAsk_ThenPossessionInASession()
        {
            Assert.That(CursorPolicyProcessor.Decide(true, CursorState.Locked, true, true), Is.EqualTo(CursorState.Free));
            Assert.That(CursorPolicyProcessor.Decide(false, CursorState.Free, true, true), Is.EqualTo(CursorState.Free));
            Assert.That(CursorPolicyProcessor.Decide(false, null, true, true), Is.EqualTo(CursorState.Locked));
            Assert.That(CursorPolicyProcessor.Decide(false, null, true, false), Is.EqualTo(CursorState.Free));
            Assert.That(CursorPolicyProcessor.Decide(false, null, false, true), Is.EqualTo(CursorState.Free), "offline");
        }

        [Test]
        public void InASession_ControllingSomething_LocksTheCursor_AndAMenuFreesIt_UntilItCloses()
        {
            _network.IsActive = true;
            _possession.CurrentPossession = new FakePossessable();
            _cursor.Tick();
            Assert.That(_device.Last, Is.EqualTo(CursorState.Locked));

            _menus.Open(_menu);
            Assert.That(_device.Last, Is.EqualTo(CursorState.Free), "at once, through MenuCursorPresenter");

            _cursor.Request("shipyard", CursorState.Locked);
            Assert.That(_device.Last, Is.EqualTo(CursorState.Free), "a menu beats every ask");

            _cursor.Release("shipyard");
            _menus.CloseAll();
            Assert.That(_device.Last, Is.EqualTo(CursorState.Locked));
        }

        [Test]
        public void TheNewestAsk_Wins_AndReleasingItFallsBack()
        {
            _cursor.Request("shipyard", CursorState.Locked);
            _cursor.Request("freecam", CursorState.Free);
            Assert.That(_cursor.Mode, Is.EqualTo(CursorState.Free));

            _cursor.Request("shipyard", CursorState.Free);
            _cursor.Request("shipyard", CursorState.Locked);
            Assert.That(_cursor.Mode, Is.EqualTo(CursorState.Locked), "a changed ask is new again");

            _cursor.Release("shipyard");
            Assert.That(_cursor.Mode, Is.EqualTo(CursorState.Free));
        }

        [Test]
        public void TheDevice_IsWrittenOnlyWhenTheModeChanges()
        {
            _cursor.Tick();
            _cursor.Tick();
            _cursor.Request("shipyard", CursorState.Free);

            Assert.That(_device.Applied, Is.EqualTo(new[] { CursorState.Free }));
        }
    }
}
