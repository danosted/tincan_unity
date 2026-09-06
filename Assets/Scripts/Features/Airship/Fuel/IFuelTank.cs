#nullable enable
namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Server-owned fuel store of an airship. Reads work everywhere (replicated attribute); writes only apply on the server.
    /// </summary>
    public interface IFuelTank
    {
        float Level { get; }
        float Capacity { get; }
        bool IsEmpty { get; }
        FuelConfig? Config { get; }

        void Consume(float amount);

        /// <summary>Adds fuel up to capacity and returns how much was actually accepted.</summary>
        float Refill(float amount);
    }

    /// <summary>A ship fixture where fuel is poured in; resolves the tank it feeds.</summary>
    public interface IFuelFillPort
    {
        IFuelTank? Tank { get; }
    }
}
