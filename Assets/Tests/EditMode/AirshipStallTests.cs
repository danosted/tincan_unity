#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Features.Airship;
using TinCan.Tests.EditMode.Fakes;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class AirshipStallTests
    {
        [TestCase(-1f)]
        [TestCase(1f)]
        public void Stall_RemovesThrustInBothDirections_AndRestoresItWhenCancelled(float throttle)
        {
            var gameObject = new GameObject("StallTest");
            try
            {
                // Use the real mediator and authored ability to cover the attribute-to-movement wiring.
                var mediatorType = Type.GetType("TinCan.Network.Infrastructure.AirshipNetworkMediator, Assembly-CSharp", true)!;
                var mediator = (IAirshipView)gameObject.AddComponent(mediatorType);
                var view = gameObject.GetComponent<AirshipControllerView>();
                var controller = new FakeAbilityController();
                var flightSpeed = AssetDatabase.LoadAssetAtPath<GameplayAttribute>("Assets/Abilities/Attributes/Attr_FlightSpeed.asset");
                var stall = AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Abilities/AbilityDefinitions/GA_EngineStall.asset");
                Assert.That(flightSpeed, Is.Not.Null);
                Assert.That(stall, Is.Not.Null);
                var attributes = new AirshipAttributeSet(controller, flightSpeed, null!);
                controller.SetAttribute(flightSpeed, new AttributeValue(view.MaxForwardSpeed));
                mediatorType.GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mediator, view);
                mediatorType.GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mediator, attributes);
                var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), new FakeTimeService(), new FakeEventPublisher());
                abilities.GrantAbility(controller, stall);

                Assert.That(abilities.TryActivateAbility(controller, stall), Is.True);
                Assert.That(mediator.MaxForwardSpeed, Is.Zero);
                Assert.That(mediator.MaxBackwardSpeed, Is.Zero);
                var processor = new AirshipMovementProcessor();
                var input = new AirshipInputState { Throttle = throttle };
                float speed = throttle * 8f;
                for (int i = 0; i < 20; i++)
                {
                    speed = processor.CalculateLinearSpeed(speed, input, mediator.MaxForwardSpeed, mediator.MaxBackwardSpeed,
                        mediator.AccelerationRate, mediator.DecelerationRate, 1f);
                }
                Assert.That(speed, Is.Zero, "Holding throttle must not sustain motion after a stall.");

                Assert.That(abilities.TryActivateAbility(controller, stall), Is.True);
                Assert.That(mediator.MaxForwardSpeed, Is.EqualTo(view.MaxForwardSpeed));
                Assert.That(mediator.MaxBackwardSpeed, Is.EqualTo(view.MaxBackwardSpeed));
                speed = processor.CalculateLinearSpeed(0f, input, mediator.MaxForwardSpeed, mediator.MaxBackwardSpeed,
                    mediator.AccelerationRate, mediator.DecelerationRate, 1f);
                Assert.That(speed * throttle, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
