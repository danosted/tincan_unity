# Fuel Gauge Integration

A close-view maritime fuel gauge with an optional freestanding deck support and a separately rotatable indicator.

This file is generated with the assets. `manifest.json` contains the same contract as structured data for tools and importing agents.

## Files and scale

- `fuel_gauge.fbx` — Deck-mounted maritime fuel gauge with separate stand, static housing, and animated indicator nodes. (1 source unit = 0.1 m).

Asset type: `interactive_prop`.

Coordinate system: Y-up, -Z forward; one exported unit is one meter.

## Placement

Place the imported roots on the deck. Keep FuelGauge_Stand for the freestanding version, or omit it and attach FuelGauge_Static to a wall or machinery panel.

## Required nodes

- `FuelGauge_Stand` — Optional top-level deck pedestal; it does not move with the needle.
- `FuelGauge_Static` — Top-level housing and dial; parent of the indicator.
- `FuelGauge_Indicator` — Animated needle with its origin on the axle; child of FuelGauge_Static.

## Setup

1. Import without a rig or animation clips and preserve the three named nodes and their hierarchy.
2. Store FuelGauge_Indicator's imported local rotation as the neutral rotation.
3. Drive only the indicator around its local axle; never translate it or replace its pivot.
4. In Unity FBX imports, Blender local Y normally becomes local Z. Confirm once with the local transform gizmo and negate the axle if motion is reversed.
5. Add primitive colliders to the stand and housing, excluding the indicator and dial details.

## Movable part contract

- Target: `FuelGauge_Indicator`
- Blender source axle: `local_Y`
- Unity FBX axle hint: `local_Z`; verify with the local gizmo
- Exported pose: `neutral_zero_rotation`
- Empty: `-65°`
- Full: `65°`
- Remaining-fuel mapping: `angle_degrees = -65 + clamp(fuel_remaining, 0, 1) * 130`
- Spent-fuel conversion: `fuel_remaining = 1 - clamp(fuel_spent, 0, 1)`

## Collision

Use a box or cylinder for the housing plus simple boxes/cylinders for the optional stand; the indicator should have no collider.

## Examples

### Engine-agnostic fuel update

```text
fuel = clamp(fuel_remaining, 0, 1)
angle = -65 + fuel * 130
indicator.local_rotation = neutral_rotation * rotation_about_local_axle(angle)
```

### Unity C# core logic

```csharp
float angle = Mathf.Lerp(-65f, 65f, Mathf.Clamp01(fuelRemaining));
indicator.localRotation = neutralRotation * Quaternion.AngleAxis(angle, Vector3.forward);
```
