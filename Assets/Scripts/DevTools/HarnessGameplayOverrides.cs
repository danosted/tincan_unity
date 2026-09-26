#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.CloudBoundary;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Gameplay rules the harness suspends while a bot route runs, so measurements see steady play rather than
    /// resets. Today that is the cloud-boundary character reset: the piloted ship can sink below the reset depth,
    /// and the respawn teleport would then fire every tick.
    /// </summary>
    public sealed class HarnessGameplayOverrides : IInitializable
    {
        private readonly HarnessOptions _options;
        private readonly CloudBoundaryUseCase _cloudBoundary;
        private readonly IEventPublisher _events;

        public HarnessGameplayOverrides(HarnessOptions options, CloudBoundaryUseCase cloudBoundary, IEventPublisher events)
        {
            _options = options;
            _cloudBoundary = cloudBoundary;
            _events = events;
        }

        public void Initialize()
        {
            if (_options.BotRoute == null) return;

            _cloudBoundary.CharacterResetEnabled = false;
            _events.LogInfo("NetHarness", "Cloud-boundary character reset disabled for the bot run.");
        }
    }
}
