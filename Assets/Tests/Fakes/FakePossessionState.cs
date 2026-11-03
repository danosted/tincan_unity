#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Possession;

namespace TinCan.Tests.Fakes
{
    public class FakePossessionState : IPossessionState
    {
        public IPossessable? CurrentPossession { get; set; }
        public IPossessable? PlayerActor { get; set; }
    }
}
