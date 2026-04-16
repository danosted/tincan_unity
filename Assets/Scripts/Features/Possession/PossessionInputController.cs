using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Controller Layer: Listens for user input to trigger possession changes.
    /// Decouples input from the stateful UseCase.
    /// </summary>
    public class PossessionInputController : ITickable, IInitializable
    {
        private readonly IInputService _inputService;
        private readonly PossessionUseCase _possessionUseCase;

        public PossessionInputController(
            IInputService inputService,
            PossessionUseCase possessionUseCase)
        {
            _inputService = inputService;
            _possessionUseCase = possessionUseCase;
        }

        public void Initialize()
        {
            Debug.Log("[PossessionInputController] Initializing...");
        }

        public void Tick()
        {
            // Manual Switch Input
            if (!_inputService.WasActionTriggered(ActionNames.ToggleControl)) return;

            _possessionUseCase.SwitchToNext();
        }
    }
}
