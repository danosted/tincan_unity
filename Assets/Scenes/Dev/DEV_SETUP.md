# Dev Scene Setup Guide

## Dev_PlayerMovement.unity - First Person Controller Sandbox

This scene tests the basic first-person character controller with WASD movement and mouse look.

### Setup Instructions

#### 1. Create the Scene
1. In Unity, go to `File → New Scene`
2. Save it to `Assets/Scenes/Dev/` and name it `Dev_PlayerMovement`

#### 2. Create Ground Plane
1. `GameObject → 3D Object → Plane`
2. Name it "Ground"
3. Set Scale to (10, 1, 10) to make it larger
4. Add a `BoxCollider` (should be automatic)
5. Set its Layer to "Ground" (create this layer if needed)

#### 3. Create Player Character
1. `GameObject → 3D Object → Capsule`
2. Name it "Player"
3. Position at (0, 1, 0)
4. Set Scale to (0.6, 1.8, 0.6) for a human-like proportion

#### 4. Add CharacterController
1. Select the Player object
2. `Add Component → Character Controller`
3. Set Height to 1.8
4. Keep Center at (0, 0, 0)

#### 5. Add Camera
1. Select Player
2. `GameObject → Create Empty` (child of Player)
3. Name it "CameraHolder"
4. Position at (0, 0.6, 0) - head position
5. `Add Component → Camera`
6. Delete any audio listener duplicates if they exist

#### 6. Attach Scripts
1. Select Player
2. Add `PlayerMovement` script as a component
3. In Inspector, set Ground Layer to "Ground"
4. In Inspector, assign the CameraHolder to the PlayerCamera script's `_cameraTransform` field

#### 7. Add Player Camera Script
1. Select Player
2. Add `PlayerCamera` script as a component
3. In Inspector, drag the CameraHolder object into the `_cameraTransform` field

#### 8. Add Lighting (Optional but Recommended)
1. `GameObject → Light → Directional Light`
2. Position it above and angle it to light the scene

#### 9. Test the Scene
1. Press Play
2. Use WASD to move
3. Use Mouse to look around
4. Press Space to jump
5. Hold Shift to sprint
6. Press Escape to unlock cursor

### Input Mapping

| Key | Action |
|-----|--------|
| W/A/S/D | Move Forward/Left/Back/Right |
| Space | Jump |
| Left Shift | Sprint |
| Mouse | Look Around |
| Escape | Toggle Cursor Lock |

### What's Being Tested

- ✅ WASD movement with smooth acceleration/deceleration
- ✅ Mouse look with pitch/yaw rotation
- ✅ Jumping with gravity
- ✅ Ground detection with raycast
- ✅ Sprint multiplier
- ✅ Cursor locking for immersive control

### Next Steps

Once this works, the foundation is ready for:
- Animation integration (walking, running, jumping)
- Network synchronization (NetCode)
- Additional movement modes (crouching, swimming, flying)
- Weapon systems
- Interaction systems

### Script Parameters

#### PlayerMovement
- `Move Speed`: Walk speed in units/sec (default: 5)
- `Ground Drag`: Acceleration/deceleration on ground (default: 5)
- `Sprint Multiplier`: Speed boost when sprinting (default: 1.5x)
- `Jump Force`: Upward impulse on jump (default: 5)
- `Jump Cooldown`: Delay between jumps (default: 0.25s)
- `Air Multiplier`: Acceleration in air (default: 0.4x)
- `Ground Layer`: Layer mask for ground detection

#### PlayerCamera
- `Mouse Sensitivity`: Look speed multiplier (default: 2)
- `Max Look Angle`: Max pitch angle up/down (default: 90°)
