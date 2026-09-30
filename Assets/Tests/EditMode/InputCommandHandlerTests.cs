#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>Command handlers the input contexts route to (the menu's are in MainMenuBootstrapTests).</summary>
    public class InputCommandHandlerTests
    {
        private sealed class FakeBoarding : IVehicleBoardingUseCase
        {
            public bool InVehicle;
            public int Exits;

            public void BoardVehicle(Guid requesterActorId, IVehicleBoardable boardable) { }

            public bool ExitVehicle()
            {
                if (!InVehicle) return false;
                Exits++;
                InVehicle = false;
                return true;
            }
        }

        [Test]
        public void ExitVehicle_InAVehicle_LeavesIt_AndConsumesCancel()
        {
            var boarding = new FakeBoarding { InVehicle = true };
            var handler = new ExitVehicleInputHandler(boarding);
            var command = ScriptableObject.CreateInstance<ExitVehicleCommand>();

            Assert.That(handler.TryHandle(command), Is.True);
            Assert.That(boarding.Exits, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(command);
        }

        [Test]
        public void ExitVehicle_InOwnBody_Declines_SoCancelFallsThrough()
        {
            var handler = new ExitVehicleInputHandler(new FakeBoarding());
            var command = ScriptableObject.CreateInstance<ExitVehicleCommand>();

            Assert.That(handler.TryHandle(command), Is.False);
            UnityEngine.Object.DestroyImmediate(command);
        }
    }
}
