#nullable enable
namespace TinCan.DevTools
{
    /// <summary>
    /// What a bot or scenario means to press, named for the situation it applies to. <see cref="ScriptedActionMap"/>
    /// turns each into a context's action (and a value for axes), so scripts stay readable and never name keys; an
    /// intent whose context is not live does nothing, exactly like the real key.
    /// </summary>
    public enum ScriptedAction
    {
        // On foot (Humanoid context)
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Jump,
        Sprint,
        Interact,
        Primary,
        Secondary,

        // At the helm (Airship context)
        ShipThrottleUp,
        ShipThrottleDown,
        ShipTurnLeft,
        ShipTurnRight,
        ShipPitchUp,
        ShipPitchDown,

        // At a cannon (Gunner context)
        GunnerFire,
        GunnerLeave,
        /// <summary>Swings the barrel right while held (a steady mouse sweep).</summary>
        GunnerAimRight,
        GunnerAimLeft,
        GunnerAimUp,
        GunnerAimDown,

        // Anywhere (Global context)
        Cancel,
        SwitchPossession,
    }
}
