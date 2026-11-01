#nullable enable
using System.Collections.Generic;
using TinCan.Core.UI;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Records every mode written to the cursor.</summary>
    public sealed class FakeCursorDevice : ICursorDevice
    {
        public List<CursorState> Applied { get; } = new();

        public CursorState? Last => Applied.Count == 0 ? null : Applied[^1];

        public void Apply(CursorState mode) => Applied.Add(mode);
    }
}
