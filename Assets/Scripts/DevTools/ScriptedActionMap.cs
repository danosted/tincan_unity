#nullable enable
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Features.Weapons.Cannon;
using UnityEngine;
using VContainer;

namespace TinCan.DevTools
{
    /// <summary>
    /// The one place a <see cref="ScriptedAction"/> becomes an action id and value, read from the loaded contexts (a
    /// context whose feature is not loaded maps to nothing).
    /// </summary>
    public sealed class ScriptedActionMap
    {
        /// <summary>Mouse counts per frame of a scripted aim sweep.</summary>
        private const float AimSweep = 20f;

        private readonly IObjectResolver _resolver;

        public ScriptedActionMap(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public bool TryResolve(ScriptedAction action, out InputActionId id, out Vector2 value)
        {
            (InputActionId? found, Vector2 amount) = action switch
            {
                ScriptedAction.MoveForward => (Get<HumanoidInputContext>()?.Move, Vector2.up),
                ScriptedAction.MoveBackward => (Get<HumanoidInputContext>()?.Move, Vector2.down),
                ScriptedAction.MoveLeft => (Get<HumanoidInputContext>()?.Move, Vector2.left),
                ScriptedAction.MoveRight => (Get<HumanoidInputContext>()?.Move, Vector2.right),
                ScriptedAction.Jump => (Get<HumanoidInputContext>()?.Jump, Vector2.right),
                ScriptedAction.Sprint => (Get<HumanoidInputContext>()?.Sprint, Vector2.right),
                ScriptedAction.Interact => (Get<HumanoidInputContext>()?.Interact, Vector2.right),
                ScriptedAction.Primary => (Get<HumanoidInputContext>()?.Primary, Vector2.right),
                ScriptedAction.Secondary => (Get<HumanoidInputContext>()?.Secondary, Vector2.right),
                ScriptedAction.ShipThrottleUp => (Get<AirshipInputContext>()?.Throttle, Vector2.right),
                ScriptedAction.ShipThrottleDown => (Get<AirshipInputContext>()?.Throttle, Vector2.left),
                ScriptedAction.ShipTurnLeft => (Get<AirshipInputContext>()?.Yaw, Vector2.left),
                ScriptedAction.ShipTurnRight => (Get<AirshipInputContext>()?.Yaw, Vector2.right),
                ScriptedAction.ShipPitchUp => (Get<AirshipInputContext>()?.Pitch, Vector2.left),
                ScriptedAction.ShipPitchDown => (Get<AirshipInputContext>()?.Pitch, Vector2.right),
                ScriptedAction.GunnerFire => (Get<GunnerInputContext>()?.Fire, Vector2.right),
                ScriptedAction.GunnerLeave => (Get<GunnerInputContext>()?.Leave, Vector2.right),
                ScriptedAction.GunnerAimRight => (Get<GunnerInputContext>()?.Aim, new Vector2(AimSweep, 0f)),
                ScriptedAction.GunnerAimLeft => (Get<GunnerInputContext>()?.Aim, new Vector2(-AimSweep, 0f)),
                ScriptedAction.GunnerAimUp => (Get<GunnerInputContext>()?.Aim, new Vector2(0f, AimSweep)),
                ScriptedAction.GunnerAimDown => (Get<GunnerInputContext>()?.Aim, new Vector2(0f, -AimSweep)),
                ScriptedAction.Cancel => (Get<GlobalInputContext>()?.Cancel, Vector2.right),
                ScriptedAction.SwitchPossession => (Get<GlobalInputContext>()?.SwitchPossession, Vector2.right),
                _ => (null, Vector2.zero),
            };

            id = found!;
            value = amount;
            return found != null;
        }

        private T? Get<T>() where T : class => _resolver.ResolveOrDefault<T>();
    }
}
