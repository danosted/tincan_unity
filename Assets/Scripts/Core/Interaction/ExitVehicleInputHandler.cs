#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Interaction
{
    /// <summary>Leaves the vehicle; declines (so Cancel falls through) when the player is already in their body.</summary>
    public sealed class ExitVehicleInputHandler : InputCommandHandler<ExitVehicleCommand>
    {
        private readonly IVehicleBoardingUseCase _boarding;

        public ExitVehicleInputHandler(IVehicleBoardingUseCase boarding)
        {
            _boarding = boarding;
        }

        protected override bool Handle(ExitVehicleCommand command) => _boarding.ExitVehicle();
    }
}
