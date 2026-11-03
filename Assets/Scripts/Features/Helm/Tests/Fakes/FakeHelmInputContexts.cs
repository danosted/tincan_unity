#nullable enable
using TinCan.Core.Domain.Input;
using TinCan.Features.Helm;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Helm.Tests
{
    /// <summary>The helm's context asset built in memory, like <see cref="FakeInputContexts"/> does for the core ones.</summary>
    public static class FakeHelmInputContexts
    {
        public static HelmsmanInputContext Helmsman()
        {
            var context = ScriptableObject.CreateInstance<HelmsmanInputContext>();
            context.Throttle = InputActionId.Create("Airship/Throttle");
            context.Yaw = InputActionId.Create("Airship/Yaw");
            context.Pitch = InputActionId.Create("Airship/Pitch");
            context.Leave = InputActionId.Create("Airship/Leave");
            return context;
        }
    }
}
