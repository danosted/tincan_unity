Status: Approved (2026-10-14): greybox parts and the Galleon design built in this change; Blender meshes follow

# Airship parts pack: a big ship with decks, stairs and stations

## Why
The vibe is a big sailing ship in the air: room for a whole crew, stations throughout, two or more levels joined by
stairs, held up by envelopes and pushed by propellers. The shipyard (`modular-airship-builder.md`) can already build
in 3D on a 1 m grid. What was missing was a few parts, a player that fits through a 1-cell gap, deck spacing that
gives headroom, and a built-in design that shows it all. The final meshes come from a separate Blender project. This
plan is the contract those meshes follow, so they replace the greybox visuals without touching a single design.

## Decisions (2026-10-14)
- **Player capsule radius 1 -> 0.4** (`Assets/Prefabs/NetworkPlayer.prefab`, CharacterController and CapsuleCollider).
  At radius 1 the player was a 2 m ball and needed 2-cell doorways. Height stays 2 m, step offset 0.3 m, slope limit 45°.
- **Stairs now, ladders later.** Stairs work with the existing movement (a ramp collider under the slope limit).
  Ladders need a climbing movement mode (input context, handler, prediction) and are a feature of their own.
- **Decks are 3 cells apart.** A deck tile is a 0.2 m slab at the top of its cell. Two cells apart leaves 1.8 m
  headroom, less than the 2 m player; three cells leaves 2.8 m.

## The parts (greybox, `ShipDesignsAssetBuilder`)
| Part id | Cells (before rotation) | What it is | Stats (mass, lift, thrust, hull) |
|---|---|---|---|
| `hull.stairs` | 1 × 3 × 5, rising toward +Z | Ten steps over a ramp collider: 3 m up over 5 m (31°). Its top cells are the stairwell, so a deck tile cannot close it. | 15, 0, 0, 15 |
| `hull.rail` | 1 | A railing 1 m high along the cell's +X edge; the cell stays walkable. | 2, 0, 0, 3 |
| `hull.rail_gate` | 1 | The rail's two posts alone: a way through a railing (the Galleon's quarterdeck opens onto its starboard balcony through one). | 1, 0, 0, 2 |
| `hull.wall` | 1 × 3 × 1 | A 3 m wall along the +X edge, capped by a deck slab at its top, so it carries the deck above. The panel stops under the slab (no shared faces, so no z-fighting). | 6, 0, 0, 15 |
| `hull.wall_window` | 1 × 3 × 1 | The same wall with a 0.5 × 0.8 m window 1.1 m above the floor. | 5, 0, 0, 12 |
| `hull.bow` | 9 × 6 × 4 box from the keel up, plus the bowsprit's cells | A prow for a 9-wide hull: tapers from the hull to a point 4 m ahead, plank top flush with the main deck, rails on its outer edges, a bowsprit. Generated mesh (`Assets/Models/ShipParts/Greybox/Bow.asset`) with a convex collider. | 60, 0, 0, 120 |
| `prop.propeller` | nacelle cell + a 3 × 3 disc one cell behind (-Z) | A nacelle with a four-blade propeller. Static for now. | 25, 0, 400, 20 |

Yaw orientation k turns a part 90°·k clockwise seen from above: the +X edge of a rail or wall faces +X, -Z, -X, +Z for
k = 0, 1, 2, 3; stairs rise toward +Z, -X, -Z, +X.

## The Galleon (built-in design `Galleon`)
Three levels on a hull 9 cells wide and 16 long, with a pointed bow at +Z (472 parts; mass 2273, lift 2800,
12.7 m/s):
- **Hold** (floor at y -4): walls all round with windows, reached by a hatch staircase 3 wide from the main deck,
  railed along both sides and across the deep end.
- **Main deck** (y -1): railed sides running onto the bow, two staircases along the sides up to the quarterdeck, two
  masts carrying the envelope.
- **Quarterdeck** (y 2) over a cabin with windows: the helm in the middle, rails on every edge but the stair landings.
  The helm stand is 4.5 m wide but reserves only 3 cells, so the stairs land at x ±3, clear of it.
- One envelope plus four side balloons for lift, three propellers under the stern, mount plates (sockets for
  fittings) on every level.
Scenario `ShipStairs` (Shipyard test scene) flies it, stands the subject at the foot of the starboard stairs and
walks it up to the quarterdeck, solo and host + client.

### Playtest 2026-10-14 (first Galleon, 7 wide)
- Flicker along the deck edges: the wall panel's top shared a plane with its cap. Fixed in the wall prefab.
- The hatch (1 wide) was hard to get down: now 3 wide.
- The helm stand's handrail blocked the stair landings: the ship went 9 wide and the stairs moved outboard.
- Asked for: windows (`hull.wall_window`) and a bow that looks like a ship's (`hull.bow`).

## The mesh contract for Blender parts
1. **One file per part**, Y-up, -Z forward, 1 unit = 1 m (the same as the `manifest.json` already shipped with
   `Assets/Models/ShipComponents/FuelEquipment/`). Import with the file's scale and no generated colliders.
2. **Fits its cells.** The mesh stays inside the part's footprint of whole 1 m cells (the table above, or a new row).
   Anything that sticks out needs more cells, because overlap is checked on cells, not meshes.
3. **Pivot** at the centre of the origin cell's floor (the part definition's pivot offset is then (0, -0.5, 0)), or
   at the cell centre (offset zero). Say which one in the manifest.
4. **Walkable tops sit exactly on cell top faces** (a deck's top at +0.5 m from the cell centre), and edges that tile:
   a row of deck, rail or wall pieces must read as one continuous surface.
5. **Collision is simple and separate**: boxes, or a ramp for stairs, added on the Unity prefab or exported as
   `*_col` meshes. Never a mesh collider made from the render mesh. Stairs keep a smooth ramp at 45° or less, with the
   steps only in the visual.
6. **Budget**: a ship reaches several hundred parts. Keep hull pieces low-poly and share one material or atlas across
   all hull parts, so they batch.
7. **Sockets** where stations attach: named empties (`Socket_01`, ...) listed in the manifest. They become
   `ShipSocket` markers (`Assets/Scripts/Core/Ship/Sockets/ShipSocket.cs`) on the prefab.
8. **Moving parts** (propeller blades) are separate named children, with their spin axis given in the manifest.

Swapping in a mesh: put the FBX under `Assets/Models/ShipParts/<Part>/`, wrap it in the part's prefab under
`Assets/Prefabs/ShipParts/` with its colliders, and point the `PART_*` asset's visual at it. Designs do not change.

## Later
- Ladders (a climbing movement mode).
- Spinning propellers: a local view on the propeller visual that reads the ship's speed.
- More parts for the vibe: a hull side with portholes, a bowsprit, a figurehead, a crow's nest, a deck with a hatch
  cover, cabin doors.
