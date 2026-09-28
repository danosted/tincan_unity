#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Damage;
using TinCan.Features.CloudBoundary;
using VContainer;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Gameplay rules the harness suspends while a bot route or scenario runs, so measurements see steady play rather than
    /// resets or random events. The cloud-boundary character reset: the piloted ship can sink below the reset depth, and
    /// the respawn teleport would then fire every tick. Random ship breakage (when that feature is on): scenarios break
    /// parts on purpose, and a random break would change what they measure.
    /// </summary>
    public sealed class HarnessGameplayOverrides : IInitializable
    {
        private readonly HarnessOptions _options;
        private readonly CloudBoundaryUseCase? _cloudBoundary; // null when the cloud boundary installer is off
        private readonly IEventPublisher _events;
        private readonly IShipBreakage? _breakage;

        public HarnessGameplayOverrides(HarnessOptions options, IEventPublisher events, IObjectResolver resolver)
        {
            _options = options;
            _cloudBoundary = resolver.TryResolve<CloudBoundaryUseCase>(out var cloudBoundary) ? cloudBoundary : null;
            _events = events;
            _breakage = resolver.TryResolve<IShipBreakage>(out var breakage) ? breakage : null;
        }

        public void Initialize()
        {
            if (!_options.IsScripted) return;

            if (_cloudBoundary != null)
            {
                _cloudBoundary.CharacterResetEnabled = false;
                _events.LogInfo("NetHarness", "Cloud-boundary character reset disabled for the scripted run.");
            }

            if (_breakage == null) return;
            _breakage.AutoBreak = false;
            _events.LogInfo("NetHarness", "Random ship breakage disabled for the scripted run.");
        }
    }
}
