#nullable enable
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.Helm
{
    /// <summary>
    /// Application Layer, the helmsman's own peer, once per input gather: while the Helmsman context is live, its
    /// throttle, yaw and pitch go into the predicted input as <see cref="HumanoidInputState.StationAxes"/> (x throttle,
    /// y yaw, z pitch), so steering is sent, buffered and replayed with the rest of the player's input.
    /// </summary>
    public sealed class HelmInputUseCase : IHumanoidInputContributor
    {
        private readonly IInputReader _input;
        private readonly HelmsmanInputContext _controls;
        private readonly IInputContexts _contexts;

        public HelmInputUseCase(IInputReader input, HelmsmanInputContext controls, IInputContexts contexts)
        {
            _input = input;
            _controls = controls;
            _contexts = contexts;
        }

        public void Contribute(IHumanoidCharacterView character, ref HumanoidInputState input)
        {
            if (!_contexts.IsActive(_controls)) return;

            input.StationAxes = new Vector3(
                _input.ReadAxis(_controls.Throttle),
                _input.ReadAxis(_controls.Yaw),
                _input.ReadAxis(_controls.Pitch));
        }
    }
}
