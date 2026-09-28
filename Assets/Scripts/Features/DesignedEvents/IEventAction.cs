#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Data for one thing a designed event does (break a part, announce a line). Immutable, no behaviour: the feature
    /// that owns the mechanic registers an <see cref="IEventActionHandler"/> for the type.
    /// </summary>
    public interface IEventAction
    {
    }
}
