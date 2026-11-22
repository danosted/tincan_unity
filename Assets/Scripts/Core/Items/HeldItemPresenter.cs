#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Local player: a HUD line naming what they hold ("Holding: Fuel crystal"), from their replicated equipment. Gone
    /// while their hands are empty. Presentation only.
    /// </summary>
    public sealed class HeldItemPresenter : ITickable
    {
        public const string Key = "Holding";

        private readonly IActorRegistry _actors;
        private readonly IHudValues? _hud; // null where no HUD is loaded: nothing to show
        private ItemDefinition? _shown;

        public HeldItemPresenter(IActorRegistry actors, IHudValues? hud)
        {
            _actors = actors;
            _hud = hud;
        }

        [Inject]
        public HeldItemPresenter(IActorRegistry actors, IObjectResolver resolver)
            : this(actors, resolver.TryResolve<IHudValues>(out var hud) ? hud : null)
        {
        }

        public void Tick() => Step(EquipmentLocator.Resolve(_actors.GetLocalPlayerActor<IActor>())?.Held);

        /// <summary>One frame: what the local player holds, or null.</summary>
        public void Step(ItemDefinition? held)
        {
            if (held == _shown) return;

            _shown = held;
            if (held == null) _hud?.Remove(Key);
            else _hud?.Set(Key, held.DisplayName);
        }
    }
}
