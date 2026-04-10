# Dev Scene Setup Guide: Dev_AirshipTest.unity

## Goal: Experience Flying a Basic Airship

This guide sets up your `Dev_AirshipTest` scene with an atmospheric sky, sun, clouds, a simple airship model, and integrates your player character for boarding.

### Setup Instructions

#### 1. Open or Create the Scene
1. If you haven't already, create a new scene: `File → New Scene`.
2. Choose the **URP** template and select **"Outdoor Basic"**.
3. Save it to `Assets/Scenes/Dev/` and name it `Dev_AirshipTest`.
4. (If you already created it, just open `Assets/Scenes/Dev/Dev_AirshipTest.unity`)

#### 2. Configure Player Character (From `Dev_PlayerMovement`)

**Option A: Duplicate from existing scene (Recommended)**
1. Open `Dev_PlayerMovement.unity`.
2. Select your "Player" GameObject in the Hierarchy.
3. `Ctrl+C` (Copy).
4. Open `Dev_AirshipTest.unity`.
5. `Ctrl+V` (Paste) the Player into the scene.
6. Adjust its Y position (e.g., `Y=1.5`) so it's above the ground.

**Option B: Recreate (if Option A fails)**
1. Follow steps 3-7 from `Assets/Scenes/Dev/DEV_SETUP.md` to recreate your Player GameObject.
2. **Important:** Ensure your Player's `CharacterController` height is `1.8`, and its `Ground Layer` (in PlayerMovement script) is set to "Ground".

#### 3. Create a Simple Airship Model

For now, we'll use basic shapes. This will be replaced by an actual model later.

1. `GameObject → 3D Object → Cube`
2. Rename it "Airship_Body"
3. Position: `(0, 10, 0)` (or higher, depending on where you want it to fly)
4. Scale: `(5, 2, 10)` (gives it a basic airship shape)
5. `GameObject → 3D Object → Capsule` (Child of Airship_Body)
6. Rename it "ControlRoom_Placeholder"
7. Position: `(0, 0.5, 4)` (front, top of the body)
8. Scale: `(0.8, 0.8, 0.8)`

#### 4. Add Airship Physics & Controls (Scripts to be created in next step)

We'll add placeholder components and create the scripts in the next step. For now, conceptually:

1. Select "Airship_Body"
2. `Add Component → Rigidbody` (Uncheck "Use Gravity", set Drag to 0.5, Angular Drag to 0.5)
3. `Add Component → AirshipController` (Will create this soon)

#### 5. Enhance Atmospheric Elements

The URP Outdoor Basic scene already has a good start. We can enhance it:

1. **Volumetric Clouds:**
   - In the Hierarchy, select "Directional Light"
   - In the Inspector, enable `Volumetric Clouds` if not already on (requires URP asset setup)
   - Adjust `Density`, `Altitude`, `Color` to get a beautiful look
   - If volumetric clouds are not an option for now, simple textured planes can be used.

2. **Adjust Lighting:**
   - Select "Directional Light"
   - Play with `Intensity`, `Color`, and `Shadow Type` to get the desired mood.
   - Consider adding a `Post-processing Volume` (`GameObject → Volume → Global Volume`) with some `Bloom`, `Vignette`, and `Color Grading` for cinematic feel.

#### 6. Add Ground Reference (Optional, but Recommended)

1. `GameObject → 3D Object → Plane`
2. Rename it "GroundReference"
3. Set Scale to `(50, 1, 50)`
4. Position at `(0, -1, 0)` (below player spawn and airship, for height reference)
5. Set its Layer to "Ground" (if not already there, create this layer)

### Input Mapping (Player Character)

- **WASD:** Move Forward/Left/Back/Right
- **Space:** Jump
- **Left Shift:** Sprint
- **Mouse:** Look Around
- **Escape:** Toggle Cursor Lock

### What's Being Set Up

- ✅ Basic atmospheric sky, sun, and (optional) clouds
- ✅ Player character ready for movement
- ✅ Placeholder airship model with initial Rigidbody settings
- ✅ Ground reference for scale

### Next Steps

After this scene is set up:
1. We will create the `AirshipController.cs` script for basic waypoint movement.
2. We will implement the boarding and relative parenting logic.
3. We will iterate on the aesthetic appeal of the sky and clouds.
