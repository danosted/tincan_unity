using TinCan.Core.Domain;

namespace TinCan.Features.Interaction
{
    public interface IMaintenanceUseCase
    {
        void RepairModule(IActor interactor, IRepairable target);
    }
}
