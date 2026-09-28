#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Data for something a designed event waits for (ship repaired). Immutable, no behaviour: the feature that owns
    /// the state registers an <see cref="IEventConditionHandler"/> for the type.
    /// </summary>
    public interface IEventCondition
    {
    }
}
