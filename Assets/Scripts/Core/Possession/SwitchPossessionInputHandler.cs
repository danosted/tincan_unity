#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Possession
{
    /// <summary>Asks the server to hand this player the next possessable (<see cref="PossessionUseCase.SwitchToNext"/>).</summary>
    public sealed class SwitchPossessionInputHandler : InputCommandHandler<SwitchPossessionCommand>
    {
        private readonly PossessionUseCase _possession;

        public SwitchPossessionInputHandler(PossessionUseCase possession)
        {
            _possession = possession;
        }

        protected override bool Handle(SwitchPossessionCommand command)
        {
            _possession.SwitchToNext();
            return true;
        }
    }
}
