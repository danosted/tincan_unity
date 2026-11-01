Status: Draft (2026-11-01)

# Remove old third-party content

## Why
Two asset-store packs from the prototype still ship with the project: `Assets/Stylized_Pirate_Ship/` (38 files,
25 MB) and `Assets/Fantasy Skybox FREE/` (102 files, 60 MB). Each brings its own style, which
[`ART_DIRECTION.md`](../ART_DIRECTION.md) now replaces, and they slow imports and clones. Decided 2026-11-01: remove
them; keep the volumetric clouds.

## What uses them today (surveyed 2026-11-01, by GUID and by path in code)
| Pack | Used by | Notes |
|---|---|---|
| Stylized pirate ship | `Prefabs/Airship/Airship_Prefab.prefab` nests `StylShip_Unity.prefab` | `Airship_Prefab` is the base of `Prefabs/Test/TestShip_Prefab.prefab` and, through it, `Prefabs/Airship/ModularShip_Prefab.prefab` (the root every designed ship builds on); also referenced by `Prefabs/Singletons/GameLifetimeScope.prefab` and `DefaultNetworkPrefabs.asset`. |
| | `Prefabs/Airship/Parts/HelmStation.prefab`: the wheel and stand meshes and `StylShip_Elements_Mat` | Built by `DevTools/Editor/HelmAssetBuilder.cs` (lines 45-46 name the pack's FBX and material). The helm is the core part of every ship. |
| | `Prefabs/Modules/Cannon_Module.prefab` nests `StylShip_Cannon.prefab` | Referenced only by `DefaultNetworkPrefabs.asset`; the cannon station is now a fitting (`Weapons.Cannon`). Check whether anything still spawns it. |
| Fantasy skybox | `FS002_Day.mat` as the skybox of `Scenes/cheesed_scene.unity`, `cvg_airship_default.unity`, `drm_cloud_environment.unity` | Test scenes use something else or nothing. |
| Volumetric clouds (`Assets/ThirdParty/VolumetricCloudsURP/`, 1.4 MB) | The cloud sea and environment | **Kept**: rendering tech rather than a style, and pillar 6 (air you can see) needs it. Stays in `ThirdParty/` with its local fixes (CODE_MAP, known traps). |

## Steps
Each step leaves every scene and scenario working; compile, EditMode tests, and the affected scenarios after each.
1. **The old cannon module.** Confirm nothing spawns `Cannon_Module` (code, fixtures, scenes); remove it from
   `DefaultNetworkPrefabs.asset` and delete the prefab.
2. **Our own helm.** A Blender helm (wheel and stand) to the mesh contract and the art direction, built by a script in
   `.tools/blender/`; `HelmAssetBuilder` wraps it instead of the pack's meshes. The wheel is a moving part (it turns
   with the rudder), which fits the ship part motion work. The helm's footprint and seat pose stay as they are, so
   designs and the helm scenarios hold.
3. **A ship root without the pirate ship.** Make the ship root that `TestShip_Prefab` and `ModularShip_Prefab` derive
   from free of `StylShip_Unity` (strip the nested model, or rebase both on a new geometry-free root), keeping every
   component, the NetworkObject's hash rules (`ShipDesignsAssetBuilder.Rehash`) and the on-board volume. Check every
   scene and scenario that flies `Airship_Prefab` itself and move it to a designed ship (Galleon or Corsair).
4. **The sky.** Replace `FS002_Day` in the three scenes with the painted sky from the look slice (art direction,
   "Light and shading"); until that exists, URP's procedural sky tinted from the palette.
5. **Delete both packs**, then search for any missing references (Console, a GUID search of `Assets/` and
   `ProjectSettings/`), and update CODE_MAP and any doc that names them.

## Verify
The full scenario suite, solo and host + client, after steps 2, 3 and 5 (ask the developer before each play run).
A playtest of the helm, the Galleon and the Corsair.

## Open
- Step 3 may be easiest after the ship picker (`ship-picker-temporary.md`) settles which ships the scenes fly.
