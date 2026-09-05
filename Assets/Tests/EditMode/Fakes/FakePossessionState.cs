#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Possession;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakePossessionState : IPossessionState
    {
        public IPossessable? CurrentPossession { get; set; }
        public IPossessable? PlayerActor { get; set; }
    }
}
