#nullable enable
using UnityEngine.UIElements;

namespace TinCan.DevTools.UI
{
    /// <summary>
    /// A block of live developer text in the top-left corner (DevReadout.uss, contributed by the harness installer). It
    /// never takes the pointer.
    /// </summary>
    [UxmlElement]
    public partial class DevReadout : Label
    {
        public static readonly string UssClassName = "tc-dev-readout";

        public DevReadout()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
        }
    }
}
