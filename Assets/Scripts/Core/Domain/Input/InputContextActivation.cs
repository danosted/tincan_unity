#nullable enable
namespace TinCan.Core.Domain.Input
{
    /// <summary>When an <see cref="InputContext"/> is active. Evaluated every frame; nothing pushes or pops contexts.</summary>
    public enum InputContextActivation
    {
        /// <summary>Always on (Global, Camera, DevTools).</summary>
        Always = 0,
        /// <summary>The local player possesses an actor of <see cref="InputContext.PossessedKind"/>.</summary>
        WhilePossessing = 1,
        /// <summary>What the local player possesses carries <see cref="InputContext.Tag"/> (a station's occupy effect).</summary>
        WhilePossessedHasTag = 2,
        /// <summary>A menu is open.</summary>
        WhileMenuOpen = 3,
        /// <summary>The Controls menu is waiting for a key.</summary>
        WhileRebinding = 4,
        /// <summary>The system that owns the context has opened it (<see cref="IInputContextSwitch"/>): a mode such as the shipyard.</summary>
        WhileOpened = 5,
    }
}
