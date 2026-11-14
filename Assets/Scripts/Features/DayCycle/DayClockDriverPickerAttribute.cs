#nullable enable
using UnityEngine;

namespace TinCan.Features.DayCycle
{
    /// <summary>On a <c>[SerializeReference]</c> <see cref="IDayClockDriver"/> field: the Inspector shows a picker of
    /// every driver type, then the picked driver's fields (DevTools/Editor/DayClockDriverPickerPropertyDrawer).</summary>
    public sealed class DayClockDriverPickerAttribute : PropertyAttribute { }
}
