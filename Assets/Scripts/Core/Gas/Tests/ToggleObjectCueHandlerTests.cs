#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Cues;
using TinCan.Core.Gas.Cues.Handlers;
using UnityEngine;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Core.Gas.Tests
{
    public class ToggleObjectCueHandlerTests
    {
        [Test]
        public void ShowsWhileActive_HidesWhenRemoved_IgnoresBursts()
        {
            var marker = new GameObject("Marker");
            marker.SetActive(false);
            var handler = marker.AddComponent<ToggleObjectCueHandler>();
            var cueEvent = new GameplayCueEvent(null!, null, new FakeAbilityController(), GameplayCuePeerRole.Proxy);

            handler.OnExecute(cueEvent);
            Assert.That(marker.activeSelf, Is.False);
            handler.OnActive(cueEvent);
            Assert.That(marker.activeSelf, Is.True);
            handler.OnRemoved(cueEvent);
            Assert.That(marker.activeSelf, Is.False);

            Object.DestroyImmediate(marker);
        }
    }
}
