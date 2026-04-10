# Airship System

**Document Version:** 1.0
**Last Updated:** January 19, 2026
**Status:** Design Specification (Pre-Implementation)
**Maintainer:** AI Assistant + Lead Designer

---

## Overview

The Airship System enables players to pilot large floating vessels through 3D space. Players can board an airship, move around inside it while it's in motion (player position is relative to vessel), pilot it from a control room, and set autopilot waypoints for autonomous navigation.

### Key Features

- ✅ **3D Flight Physics** - Unrestricted movement in all axes (unlike character controller)
- ✅ **Relative Positioning** - Player moves with the vessel when inside
- ✅ **Boarding/Docking** - Enter/exit seamlessly with state transitions
- ✅ **Pilot Control** - Manual control from designated control room
- ✅ **Autopilot** - Set waypoints and let the vessel navigate autonomously

---

## Architecture

### System Components

#### 1. **AirshipController** (Core)
Manages airship movement, orientation, and state.

```
AirshipController
├── Rigidbody (or kinematic movement)
├── AirshipPhysics (acceleration, deceleration, drag)
├── DockingSystem (boarding/exiting)
├── PilotingSystem (control input handling)
├── AutopilotSystem (waypoint navigation)
└── PassengerManager (track who's aboard)
```

#### 2. **Player Relative Positioning**

When player boards vessel:
1. Player's parent transform = Airship's transform
2. Player position becomes local to airship
3. As airship moves, player moves with it automatically
4. Physics raycast ground checks must account for moving vessel

#### 3. **Control Modes**

```
Airship States:
├── Idle (parked on ground)
├── Piloted (player at controls)
│   ├── PilotView (first-person from control room)
│   └── FreeLook (player still moves around inside)
├── Autopilot (autonomous navigation to waypoint)
│   └── PlayerCanExplore (move freely inside while flying)
└── Docked (passenger entrance/exit)
```

---

## Physics Model

### Movement Characteristics

Unlike the player character controller, airships use **unrestricted 3D movement**:

```csharp
// Airship can move in all directions
velocity = forward * speedZ + right * speedX + up * speedY;

// No forced gravity (can hover indefinitely)
// No ground friction (air resistance instead)
```

### Key Differences from Character Controller

| Aspect | Character | Airship |
|--------|-----------|---------|
| Gravity | Forced (falls) | Optional (can hover) |
| Collision | CharacterController | Rigidbody or kinematic |
| Movement | 2D + gravity | Full 3D |
| Acceleration | Per-frame lerp | Velocity-based |
| Parent | None (rooted to world) | Can parent player |

### Proposed Physics Parameters

```csharp
[Header("Flight Physics")]
public float maxSpeedForward = 30f;      // Units/sec
public float maxSpeedVertical = 15f;     // Units/sec (up/down)
public float maxRotationSpeed = 45f;     // Degrees/sec (pitch/yaw/roll)
public float acceleration = 10f;         // Units/sec²
public float deceleration = 5f;          // Drag coefficient
public float hoverDrag = 2f;             // Resistance when idle
```

---

## Boarding System

### State Transitions

```
PlayerGrounded
    ↓ [Enter BoardArea]
PlayerCanBoard [Display: "Press E to board"]
    ↓ [Press E]
PlayerBoarding [Animation/Transition]
    ↓ [Complete]
PlayerAboard [In airship, can move around]
    ↓ [Reach ControlRoom]
PlayerPiloting [At controls, can steer]
    ↓ [Press E to exit controls]
PlayerAboard [Back to moving freely inside]
    ↓ [Reach ExitPoint]
PlayerExiting [Animation/Transition]
    ↓ [Complete]
PlayerGrounded [Back to normal movement]
```

### Docking Implementation

#### Boarding
1. Detect player in boarding area (collider trigger)
2. Show "Press E to Board" prompt
3. On input:
   - Disable player's own CharacterController
   - Make player a child of airship
   - Move player to boarding spawn point (inside airship)
   - Set airship state to "HasPassenger"
   - Enable "Move Inside Airship" controls

#### Exiting
1. Player reaches exit point (collider trigger)
2. Show "Press E to Exit" prompt
3. On input:
   - Store airship velocity
   - Remove player from airship hierarchy
   - Restore player as independent entity
   - Move player to exit point (outside airship)
   - Re-enable player's CharacterController
   - Apply stored momentum (optional: inherit airship velocity)

---

## Control Systems

### Piloting Controls

**From Control Room (Manual Piloting)**

```
Input → AirshipInput
├── WASD / Thumbstick → Horizontal movement (forward/strafe)
├── Space/Ctrl → Vertical movement (up/down)
├── Mouse/Look → Pitch & Yaw rotation
├── Q/E → Roll rotation
└── Shift → Speed boost
```

**Output → AirshipController**
```csharp
airship.SetPilotInput(Vector3 direction, Vector3 rotation, float throttle);
```

### Free Movement Inside Airship

While aboard but not piloting:
- Player uses normal character movement (WASD)
- Player moves relative to airship's local space
- Player can navigate to control room

---

## Autopilot System

### Waypoint Navigation

```csharp
public class Waypoint
{
    public Vector3 position;
    public float radius = 5f;  // Arrival threshold
    public float arrivalSpeed = 0.5f;  // Speed when approaching
}
```

### Autopilot Behavior

1. **Input Phase**
   - Player designates waypoint (UI, map, or click in world)
   - Airship calculates heading to waypoint

2. **Flight Phase**
   - Autopilot steers toward waypoint
   - Maintains altitude/speed settings
   - Slows down as approaching destination

3. **Completion**
   - Arrives at waypoint (within radius)
   - Can auto-dock, hold position, or await new waypoint

### Player Freedom While Autopilot Active

- ✅ Player can move around inside airship
- ✅ Player can access other systems
- ✅ Player can cancel autopilot at any time
- ✅ UI shows waypoint progress

---

## Data Structures

### Airship Entity

```
GameObject: Airship
├── Transform (world position/rotation)
├── Rigidbody or KinematicBody
├── AirshipController.cs
├── AirshipPhysics.cs
├── DockingSystem.cs
├── PilotingSystem.cs
├── AutopilotSystem.cs
├── Models/
│   ├── Hull (visual mesh)
│   ├── Propellers (animated)
│   └── Interior (cabin, control room)
├── Areas/
│   ├── BoardingArea (entry trigger)
│   ├── ExitArea (exit trigger)
│   └── ControlRoom (piloting position)
└── Internals/
    ├── InteriorCamera (inside view)
    └── ControlSeat (pilot position)
```

### Player State While Aboard

```csharp
public enum AirshipRole
{
    NotAboard,
    Passenger,      // Moving around inside
    Pilot,          // At controls
    Engineer,       // (Future: manage systems)
    Commander       // (Future: strategic control)
}
```

---

## Known Limitations & Future Work

### Phase 1 Limitations
- Single pilot (no multi-player crew)
- Simple navigation (no obstacles/wind)
- Basic autopilot (no complex routing)
- No interior physics interactions (everything relative)

### Planned Expansions
- **Engineering Station** - Manage power, shields, weapons
- **Crew System** - Multiple players with different roles
- **Damage Model** - Hull integrity, part damage
- **Combat** - Airship-to-airship engagement
- **Atmosphere Physics** - Wind, pressure, fuel consumption

---

## Implementation Phases

### Phase 1: Core Experience (MVP - Focus on FEEL)

**Goal:** Experience the sensation of flying on an airship with beautiful atmosphere

**Approach:**
- Fake smooth movement between fixed waypoints (linear interpolation, not physics)
- Boarding system to make player relative to vessel
- Beautiful scene with sky, sun, and clouds
- Iterate on what feels good, not perfection

#### Phase 1A: Scene & Aesthetics
- [ ] Create `Dev_AirshipTest` scene with URP sky
- [ ] Add directional light (sun) with realistic shadows
- [ ] Implement cloud layer (particles or simple planes)
- [ ] Basic terrain or platforms below for reference
- [ ] Lighting and mood that feels immersive

#### Phase 1B: Airship Movement
- [ ] Simple AirshipController (waypoint-based movement)
- [ ] Lerp between waypoints smoothly
- [ ] Rotation toward flight direction
- [ ] No complex physics yet — just smooth motion

#### Phase 1C: Boarding & Relative Position
- [ ] Docking system (E to board/exit)
- [ ] Player parent/unparent logic
- [ ] Player moves with airship automatically
- [ ] Test the FEELING of being aboard while it flies

#### Phase 1D: Polish & Iteration
- [ ] Adjust movement speed/feel
- [ ] Test boarding/exiting smoothness
- [ ] Iterate on camera experience
- [ ] Gather feedback on what feels right

**Success Criteria:**
- ✅ Can board airship with E key
- ✅ Airship smoothly moves between waypoints
- ✅ Player moves with airship while aboard
- ✅ Can exit gracefully
- ✅ Scene feels atmospheric and immersive
- ✅ The experience FEELS like flying an airship

### Phase 2: Control Polish (Weeks 3-4)
- [ ] Real flight physics (when we know what feels right)
- [ ] Pilot input system
- [ ] Refined movement model

### Phase 3: Autopilot (Weeks 5-6)
- [ ] Waypoint system
- [ ] Autonomous navigation
- [ ] Integration

### Phase 4: Content & Expansion (Week 7+)
- [ ] Multiple airships
- [ ] Interior exploration
- [ ] Systems/stations

---

## Related Documentation

- `.docs/ARCHITECTURE.md` - Overall game structure
- `.docs/PLAYER_SPAWNING.md` - How players enter world (potential airship spawning)
- `.docs/NETWORKING.md` - Multi-player airship synchronization (future)
- `Assets/Scenes/Dev/Dev_AirshipTest.unity` - Development scene (to be created)

---

## Design Decisions

### Finalized (January 19, 2026)

1. **Player Look Rotation** ✅ DECIDED
   - Player rotation is **independent** of airship yaw
   - Implement fluid adjustment to prevent jarring rotations during sudden airship turns
   - Priority: "What feels most natural and fun" — iterate if needed

2. **Momentum on Exit** ✅ DECIDED
   - Player **inherits airship velocity** when exiting
   - This is the most intuitively sound choice — momentum should transfer
   - Iterate based on feel during implementation

3. **Interior Scale** ✅ DECIDED
   - **Physically accurate layout** with fantasy scaling
   - Inspiration: *Howl's Moving Castle*, *Final Fantasy airships* — deceptively large interiors with magical dimensionality
   - Interior can be significantly larger than exterior suggests (within fantasy world logic)
   - Focus: Spacious, exploratory interior with multiple decks and systems
   - Not realistic scale, but grounded fantasy rules (not TARDIS-level absurdity)

### Still Open (Deferred)

4. **Docking Animation** — To be decided during Phase 1 implementation
5. **Multiple Vessels** — To be decided based on multiplayer requirements

