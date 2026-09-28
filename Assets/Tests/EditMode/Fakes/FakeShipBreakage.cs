#nullable enable
using System.Collections.Generic;
using TinCan.Features.Airship.Damage;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>IShipBreakage over a set of part indices; parts outside 0..PartCount-1 do not exist.</summary>
    public sealed class FakeShipBreakage : IShipBreakage
    {
        private readonly HashSet<int> _broken = new();

        public int PartCount { get; set; } = 5;
        public bool AutoBreak { get; set; }
        public int BrokenCount => _broken.Count;
        public IReadOnlyCollection<int> Broken => _broken;

        public bool TryBreak(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= PartCount) return false;
            _broken.Add(pointIndex);
            return true;
        }

        public bool TryRestore(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= PartCount) return false;
            _broken.Remove(pointIndex);
            return true;
        }
    }
}
