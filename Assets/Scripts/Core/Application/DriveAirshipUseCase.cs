using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Core.Application
{
    /// <summary>
    /// Application service (Use Case) that coordinates between input and domain logic.
    /// </summary>
    public class DriveAirshipUseCase
    {
        private readonly IInputService _inputService;
        private readonly AirshipEngine _engine;

        public DriveAirshipUseCase(IInputService inputService, AirshipEngine engine)
        {
            _inputService = inputService;
            _engine = engine;
        }

        public void Execute(float deltaTime)
        {
            float throttleInput = _inputService.GetAxis(ActionNames.MoveForward, ActionNames.MoveBackward);
            _engine.ApplyThrottle(throttleInput, deltaTime);
            
            // Logic for pitch/yaw can be added here
        }

        public float GetCurrentThrottle() => _engine.CurrentThrottle;
    }
}
