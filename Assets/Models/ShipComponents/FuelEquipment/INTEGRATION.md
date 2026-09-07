# Maritime Fuel Equipment Integration

Two 10 L can variants, a 20 L standalone motor, a two-handed sky-can catching pole, and a three-slot wooden deck rack with iron rails. Geometry and sockets are supplied; gameplay logic and physics belong in the game.

This file is generated with the assets. `manifest.json` contains the same contract as structured data for tools and importing agents.

## Files and scale

- `jerry_can_red.fbx` — Red fuel can with removable cap and grip, catch, and pour sockets. (1 source unit = 1 m).
- `jerry_can_teal.fbx` — Teal fuel can with removable cap and grip, catch, and pour sockets. (1 source unit = 1 m).
- `fuel_motor.fbx` — Standalone twin-cylinder motor with open fuel inlet and hinged cap. (1 source unit = 1 m).
- `can_catcher.fbx` — Player-wielded catching pole with two grip sockets and an upward-opening hook. (1 source unit = 1 m).
- `jerry_can_rack.fbx` — Low timber rack with iron retaining rails, four deck anchors, and three can placement sockets; cans exported separately. (1 source unit = 1 m).

Asset type: `interactive_props`.

Coordinate system: Y-up, -Z forward; one exported unit is one meter.

## Placement

Each file has one root at its floor contact/butt end, recentered to zero. Source units are meters. Pole length is approximately 1.94 m; cans are approximately 0.55 m tall.

## Required nodes

- `JerryCan_Red / JerryCan_Teal` — Independent can roots. Each has _Cap, _Grip, _Catch, and _Pour children prefixed with its root name.
- `FuelMotor_Refill` — Target at the open fuel inlet; local +Z points out of the tank in Blender.
- `FuelMotor_CapHinge` — Open pose is authored zero; rotate local X +90 degrees to close. Parent of FuelMotor_Cap.
- `CanCatcher_MainGrip / CanCatcher_SupportGrip` — Hand placement points at 0.20 m and 0.70 m along the shaft. Attach the player hand to MainGrip using its inverse local transform.
- `CanCatcher_Catch` — Trigger/attachment point inside hook. Align a falling can's _Catch point here while keeping the can upright.
- `JerryCanRack / JerryCanRack_Slot_01 / JerryCanRack_Slot_02 / JerryCanRack_Slot_03` — Static rack root at deck level and three can-root placement sockets, spaced 0.44 m apart. Align an existing can root to a socket's full transform; the socket includes its floor-contact offset.

## Setup

1. Import each file as a separate prefab, preserving named empty sockets and mesh hierarchy; no character rig or animation clips are supplied.
2. Place JerryCanRack at deck level. Its footprint including anchor ears is 1.42 by 0.634 m and its height is 0.30 m. The rack exports empty; add either can variant to any Slot_01/02/03 transform with unit local scale and identity local rotation. Keep one can per slot, disable its free physics while stored, and restore physics when lifted out. Slot occupancy and storage logic are not supplied.
3. Use one rigidbody per can. Add a small catch trigger near its handle; use swept collision/continuous collision detection for fast falling cans.
4. When a falling can enters the hook trigger, align its _Catch socket to CanCatcher_Catch, disable its free-fall physics and attach it. Hold at most one can; detach and restore physics on release.
5. For refuelling, remove the can cap along source +Z and keep the motor cap open. Align the _Pour point over FuelMotor_Refill, with the can tilted so the spout points into the tank.
6. Transfer fuel only while in range, pouring, and open: amount = min(rate * dt, canFuel, motorCapacity - motorFuel). Subtract and add the same amount. Clamp both reservoirs and prevent duplicate transfer calls.
7. Motor begins empty. Hide FuelMotor_EmptyPlate and FuelMotor_EmptyText after fuel is added; enable your motor effects only while fuel remains. The motor has no external driven machinery.
8. Socket positions survive coordinate conversion; source +Z normally maps to engine +Y. Inspect local axes before applying cap rotations or a first-person hand pose.

## Collision

Use boxes for can shells and motor body, capsules for pole shaft and hook segments, and separate small triggers for catch/refill. For the static rack use separate boxes for the floor, rails, and dividers, plus one optional trigger per bay; leave all three openings clear. Avoid one convex hull over the hook or rack openings. Do not give decorative ribs, bolts, or labels colliders.

## Examples

### Bounded fuel transfer

```text
if pouring and caps_open and spout_in_range:
    amount = max(0, min(rate * dt, can.fuel, motor.capacity - motor.fuel))
    can.fuel -= amount
    motor.fuel += amount
```
