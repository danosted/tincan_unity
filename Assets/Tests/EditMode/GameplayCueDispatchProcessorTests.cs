#nullable enable
using NUnit.Framework;
using TinCan.Core.Gas.Cues;

namespace TinCan.Tests.EditMode
{
    /// <summary>The dispatch table from .docs/plans/gameplay-cues.md: every peer plays a burst exactly once.</summary>
    public class GameplayCueDispatchProcessorTests
    {
        private readonly GameplayCueDispatchProcessor _processor = new();

        [Test]
        public void Host_NotPredicted_PlaysAndSendsToAll()
        {
            var dispatch = _processor.Decide(isServer: true, isClient: true, isOwner: false, GameplayEffectContext.Default);

            Assert.That(dispatch.PlayLocally, Is.True);
            Assert.That(dispatch.SendToClients, Is.True);
            Assert.That(dispatch.ExcludeOwner, Is.False);
        }

        [Test]
        public void Host_Predicted_PlaysAndSendsToAllButTheOwner()
        {
            var dispatch = _processor.Decide(isServer: true, isClient: true, isOwner: false, GameplayEffectContext.Predicted);

            Assert.That(dispatch.PlayLocally, Is.True);
            Assert.That(dispatch.SendToClients, Is.True);
            Assert.That(dispatch.ExcludeOwner, Is.True);
        }

        [Test]
        public void DedicatedServer_SendsWithoutPlaying()
        {
            var dispatch = _processor.Decide(isServer: true, isClient: false, isOwner: false, GameplayEffectContext.Default);

            Assert.That(dispatch.PlayLocally, Is.False);
            Assert.That(dispatch.SendToClients, Is.True);
        }

        [Test]
        public void OwnerClient_Predicted_PlaysAtOnce_SendsNothing()
        {
            var dispatch = _processor.Decide(isServer: false, isClient: true, isOwner: true, GameplayEffectContext.Predicted);

            Assert.That(dispatch.PlayLocally, Is.True);
            Assert.That(dispatch.SendToClients, Is.False);
        }

        [Test]
        public void OwnerClient_NotPredicted_Drops_BecauseTheServerSendsIt()
        {
            // The equipment binder applies effects on the owner outside prediction: the server's copy is the one that plays.
            var dispatch = _processor.Decide(isServer: false, isClient: true, isOwner: true, GameplayEffectContext.Default);

            Assert.That(dispatch.PlayLocally || dispatch.SendToClients, Is.False);
        }

        [Test]
        public void ProxyClient_Drops()
        {
            var dispatch = _processor.Decide(isServer: false, isClient: true, isOwner: false, GameplayEffectContext.Predicted);

            Assert.That(dispatch.PlayLocally || dispatch.SendToClients, Is.False);
        }
    }
}
