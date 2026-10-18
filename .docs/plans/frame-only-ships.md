Status: Done (2026-10-17): built, EditMode and every scenario green (solo and host + client)

# Frame-only ships: parts hang on the frame, the voxel builder goes

## Why
Ships are now drawn as frames ([ship-frames.md](ship-frames.md), S1–S4), but the voxel builder is still there beside it:
grid cells, block footprints, a part per deck tile, wall and rail, and two built-in ships (Starter, Galleon) made only of
those. Two ways to build the same thing cost code and confuse the shipyard. The developer wants to commit to the frame:
parts say which frame element they attach to, stairs become part of the frame, the voxel ships are rebuilt as frames,
and what only the voxel builder needed is deleted.

## The connection protocol
Every part names the kind of frame element it attaches to (`ShipPartAttach` on the part definition):

| Attaches to | What it stores | Where it stands | Examples |
|---|---|---|---|
| **Face** | the face id, a point (x, z) on it in frame units, a yaw | on a plank floor at that point, at the floor's height there | helm, cairn, engine, mount plate |
| **Node** | the node id, a yaw | at the node, following it | mast, propeller |

Built into the frame rather than placed as parts:
- **Edges:** a railing is the edge's own kind, `rail` or `gate`, and runs the whole edge. The gate is a railing with the
  middle left open.
- **Stairs:** a frame element of their own (below).
- **Faces:** what a face is made of is its fill (open, planks). A window fill (an opening) is left for later.

**Anchored, not placed.** A part keeps the element it hangs on, so it moves when the frame does (a node part with its
node, a face part with its floor's height). It is joined to the ship by construction: the old "does it touch something"
rules (blocks, adjacency, rest tolerance) go. What is still checked:
- the element is there, and is the right kind (a plank floor for a face part);
- a face part's point is inside its face;
- two parts' footprints (1 m blocks, yawed) do not overlap.

A face part's point is in ship space (x, z). Moving the floor's nodes changes its height, but not its place on the deck.
If the floor no longer reaches it, the validator says so.

## Stairs in the frame
A flight hangs from a **landing edge**, a floor edge on story *s* ≥ 1, and runs down into story *s − 1*, perpendicular to
the edge, on the side given by `dir` (+1 or −1 across the edge). It is as wide as the edge. Its run follows from the rise:
a fixed slope of 31°, so 3 m up takes 5 m. The validator wants:
- a plank floor of the story below under its foot;
- no plank floor of the landing's own story over the flight (the hatch must be open).

It builds as visible steps plus one sloped ramp collider, as the old stairs part did. In the shipyard: select a floor
edge and press the stairs key; press it again to flip the side; delete mode removes it.

## Lift: the spirit cairn, first step
The balloon and the envelope retire now, rather than being rebuilt on the frame and removed again
([spirit-cairn.md](spirit-cairn.md), first step). There is one cairn per design, a face part. The ship's lift is the
spirit's level: one tuning value (`ShipStatsTuning.SpiritLift`), the same for every ship until progression comes.
Designs without a cairn have no lift.

## Format version 4
- Part: `{"id", "part", "on": {"face": id, "x", "z"} | {"node": id}, "rot"}`; `rot` is now 0–3 (quarter yaw turns). The
  24 orientations and the grid go.
- Frame: edges get `"rail": "rail" | "gate"` (true in older files means `rail`), and the frame gets a `stairs` list:
  `{"id", "edge", "dir"}`.
- **Migration 3 → 4:** a part standing on a plank floor (underside within 0.25 m of it) becomes a face part, and one at a
  node becomes a node part. Anything else (every part of a design without a frame) cannot be anchored. It is moved to a
  top-level `dropped` list, kept in the file but not built. Nothing is lost silently.
- Network version 4.

## What is removed
- Parts: hull block, deck tile, armour block, wall, window wall, bow, rail, rail gate, stairs, balloon, envelope. Their
  prefabs, greybox meshes and settings assets go with them.
- Code: `ShipGridCell`, `ShipGrid`, the grid-aim parts of the shipyard (working level in parts mode, snapping to the
  ship's edge), S4's free positions (`ShipPartPos` as a stored position), and the 24-orientation table.
- Designs: the voxel Starter and Galleon. They are rebuilt as frames under the same names, so presets, the default design
  and the loadout keep their keys.

## The rebuilt ships
- **Starter:** a small frame sloop. One deck, a helm, a cairn, an engine and a mount plate; mast and propeller as node
  parts.
- **Galleon:** the same three levels at the same heights, so the scenarios' spots keep their meaning:
  - hold floor at −3.5 m, main deck at −0.5 m, quarterdeck at 2.5 m;
  - 9 m wide, with a pointed bow;
  - planked hull walls, and a cabin under the quarterdeck;
  - a hatch with three frame stairs from the main deck down to the hold, and two side stairs up to the quarterdeck;
  - the starboard balcony through a gate;
  - mount plates where the old ones were, the helm, a cairn, propellers on the stern nodes.

  Its loadout (`Galleon.fittings.json`) is rewritten for the new part ids. The old Raft and FrameGalleon stay as frame
  samples.

## Slices
1. **Anchors.** Part model, protocol, cairn, format 4 and its migration, validator, stats, pose, assembler, codecs.
2. **Frame stairs and gates.** Model, edits, rules and meshes.
3. **Shipyard.** Parts mode aims at floors and nodes, and the frame tools get stairs and gates.
4. **Ships.** Starter and Galleon rebuilt, the loadout rewritten, the scenarios updated, and the voxel parts and code
   deleted.
5. **Docs and verify.** EditMode green after every slice; at the end `verify -All` (solo and host + client).

## Risks
- **Scenario spots:** the Galleon's scenarios (ShipStairs, GalleonLeashClimb, GalleonChartTable, GalleonLoadout)
  depend on its layout. The heights are kept, and spots that move are updated in the same change.
- **User designs saved before this change** lose their parts to `dropped` unless they had a frame. That is accepted:
  the voxel builder is gone.

## Built (2026-10-17)
EditMode 1184/1184; `verify -All` passes solo and host + client. Two scenario setups had to change with the ships:
the chart table and the hold sit under a deck, so their teleports search for ground from 1 m up, not 3 m.
- *Protocol*: `ShipPartAttach` (Face, Node) on `ShipPartDefinition`, with `IsCairn`; `ShipPartAnchor` on
  `ShipPartPlacement`, replacing the stored position. `ShipPartPose` turns an anchor and the frame into a pose and 1 m
  blocks (`ShipPartBlocks`: overlap, and the block at a point). `ShipPartOrientation` is four yaws.
- *Validator*: anchors hold (`UnanchoredPart`), blocks within limits and apart, one core, at most one cairn
  (`ExtraCairn`), and the frame in one piece with the core's anchor. Lift is the cairn's spirit
  (`ShipStatsTuning.SpiritLift`, 3000) plus any part lift.
- *Frame*: `ShipFrameRailing` (None, Rail, Gate) on edges; `ShipFrameStair` (edge, dir) with the
  `AddStair`/`FlipStair`/`RemoveStair` edits. Stairs go when their edge does. Rules (`BadStairs`): lands on a plank
  floor below, no deck over the flight, no railing (a gate is fine) on the landing. Meshes: steps seen, a ramp collided
  (`ShipFrameMeshUse`), a gate is two posts. Tuning: `StairSlope` 31°, `StepRise` 0.2 m.
- *Format 4*, network 4 (pinned hash `557330f11623f375`). `ShipDesignMigrationV3` hangs parts that rest on a plank
  floor (or sit at a node) on it and moves the rest to `dropped`. Golden samples: v1, v2, v3.
- *Shipyard*: parts mode aims at the plank floor (snapped to 0.5 m) or the node the selected part needs. The working
  level and the snap-to-ship are gone. Frame mode adds **B** (railing, gate, none) and **T** (stairs on the open side,
  turned, removed). *New ship* is a 6 x 10 m deck with the helm, the cairn and an engine.
- *Ships* (`DevTools/Editor/ShipFrameDesigns.cs`):
  - Starter: a sloop with a helm, cairn, engine, mount plate and a mast on the bow node; 15.6 m/s.
  - Galleon: three stories at the old heights, hatch stairs and two quarterdeck flights, a gated balcony, ten mount
    plates and two propellers on the stern nodes.
  - Raft: helm, engine, cairn.
  - FrameGalleon is retired.
  - The Galleon loadout is rewritten from `ShipFrameDesigns.GalleonMounts`.
- *Removed*:
  - Parts and assets: hull block, deck tile, armour, wall, window wall, bow, rail, rail gate, stairs, balloon,
    envelope, with their prefabs, the bow mesh, and the armour and balloon materials.
  - Code: `ShipGridCell`, `ShipGrid`, `ShipPartPos`, `ShipFrameContact`, `ShipyardSnapProcessor`, the 24 orientations,
    the voxel Starter and Galleon builders.
- *Engine thrust* dropped from 400 to 200, so the light frame Starter cruises at about 15 m/s instead of the 30 m/s cap.
- *Scenarios*: Test_Shipyard's scenarios use the rebuilt ships:
  - counts updated;
  - ShipyardRoundTrip places mount plates;
  - FrameRoundTrip and FramePartPlace draw out from the Starter's starboard side;
  - FrameStories walks up the Galleon's hatch stairs.

  New commands: ShipyardFrameRailing, ShipyardFrameStairs.

**Not done:**
- Window and door openings in faces.
- Parts against walls (wall faces take no parts yet).
- Fitting mounts anywhere on a floor (S5 of ship-frames).
- The big-ship pass.
