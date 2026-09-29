#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Gas;
using TinCan.Features.Airship.Damage;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Builds fake damage points under a FakeAirshipView so ShipDamageLocator finds them like the real fixture. Each
    /// point is a GAS actor (a FakeAbilityController holding health and max health), so effects hit it for real.
    /// This plain class matches the file name on purpose: Unity binds the file's MonoScript to it, not to the
    /// MonoBehaviour below. A MonoBehaviour that owns its script file in this editor-only assembly cannot be added
    /// with AddComponent ("it is an editor script"). FakeFuelTank.cs relies on the same arrangement.
    /// </summary>
    public static class FakeShipDamage
    {
        public const float MaxHealth = 100f;

        public static FakeShipDamagePoint[] AttachPoints(GameObject ship, int count, HealthAttribute health, MaxHealthAttribute maxHealth)
        {
            var sockets = new GameObject("ShipDamageSockets");
            sockets.transform.SetParent(ship.transform, false);
            var points = new FakeShipDamagePoint[count];
            for (int i = 0; i < count; i++)
            {
                var socket = new GameObject($"Socket_{i}");
                socket.transform.SetParent(sockets.transform, false);
                points[i] = socket.AddComponent<FakeShipDamagePoint>();
                points[i].Init(health, maxHealth);
            }
            return points;
        }

        /// <summary>Test assertions: the point's health as a fraction of max, read from its controller (-1 without health).</summary>
        public static float HealthFraction(IShipDamagePoint point) =>
            point.Controller.TryGetHealth(out var health) ? health.HealthPercentage : -1f;
    }

    public class FakeShipDamagePoint : MonoBehaviour, IShipDamagePoint, ITargetable
    {
        private readonly FakeAbilityController _controller = new();
        private HealthAttributeSet _health = null!;

        public int Index => transform.GetSiblingIndex();
        public Transform? Transform => transform;
        public IAbilityControllerBase? Controller => _controller;
        public Vector3 AimPoint => transform.position;
        public bool IsTargetable => true;
        public FakeAbilityController FakeController => _controller;

        public void Init(HealthAttribute health, MaxHealthAttribute maxHealth)
        {
            _health = new HealthAttributeSet(_controller, health, maxHealth);
            _health.InitializeBaseValues(FakeShipDamage.MaxHealth);
            _controller.RegisterAttributeSet(_health);
        }

        /// <summary>Test shortcut: sets health directly, as an effect from outside the breakage use case would.</summary>
        public void SetHealthFraction(float fraction) =>
            _controller.SetAttribute(_health.HealthDef, new AttributeValue(Mathf.Clamp01(fraction) * FakeShipDamage.MaxHealth));
    }
}
