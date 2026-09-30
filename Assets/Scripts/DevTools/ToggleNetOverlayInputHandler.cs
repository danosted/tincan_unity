#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.DevTools
{
    /// <summary>F3 while the harness runs: shows or hides its readout.</summary>
    public sealed class ToggleNetOverlayInputHandler : InputCommandHandler<ToggleNetOverlayCommand>
    {
        private readonly MovementTelemetryUseCase _telemetry;

        public ToggleNetOverlayInputHandler(MovementTelemetryUseCase telemetry)
        {
            _telemetry = telemetry;
        }

        protected override bool Handle(ToggleNetOverlayCommand command) => _telemetry.ToggleOverlay();
    }
}
