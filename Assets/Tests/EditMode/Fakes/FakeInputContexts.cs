#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Context assets built in memory, with one fresh action id per slot, for tests of the systems that read them.</summary>
    public static class FakeInputContexts
    {
        public static HumanoidInputContext Humanoid()
        {
            var context = ScriptableObject.CreateInstance<HumanoidInputContext>();
            context.Move = InputActionId.Create("Humanoid/Move");
            context.Jump = InputActionId.Create("Humanoid/Jump");
            context.Sprint = InputActionId.Create("Humanoid/Sprint");
            context.Interact = InputActionId.Create("Humanoid/Interact");
            context.Primary = InputActionId.Create("Humanoid/Primary");
            context.Secondary = InputActionId.Create("Humanoid/Secondary");
            return context;
        }

        public static AirshipInputContext Airship()
        {
            var context = ScriptableObject.CreateInstance<AirshipInputContext>();
            context.Throttle = InputActionId.Create("Airship/Throttle");
            context.Yaw = InputActionId.Create("Airship/Yaw");
            context.Pitch = InputActionId.Create("Airship/Pitch");
            return context;
        }

        public static CameraInputContext Camera()
        {
            var context = ScriptableObject.CreateInstance<CameraInputContext>();
            context.Look = InputActionId.Create("Camera/Look");
            return context;
        }

        public static GlobalInputContext Global()
        {
            var context = ScriptableObject.CreateInstance<GlobalInputContext>();
            context.Cancel = InputActionId.Create("Global/Cancel");
            context.SwitchPossession = InputActionId.Create("Global/SwitchPossession");
            return context;
        }

        /// <summary>A plain context with the given condition, priority and actions.</summary>
        public static InputContext Plain(string name, InputContextActivation activation, int priority, bool blocksAllLower = false,
            IEnumerable<InputActionId>? actions = null, IEnumerable<InputRoute>? routes = null, IEnumerable<InputContext>? blocks = null)
        {
            var context = ScriptableObject.CreateInstance<InputContext>();
            context.name = name;
            context.Configure(activation, priority, blocksAllLower: blocksAllLower);
            context.SetContents(blocks ?? new InputContext[0], actions ?? new InputActionId[0], routes ?? new InputRoute[0]);
            return context;
        }

        public static IReadOnlyList<IHumanoidInputContributor> NoContributors => new IHumanoidInputContributor[0];
    }
}
