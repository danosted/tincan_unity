Status: Approved (2026-11-21)

# Island life: trees, landmasses and caves to explore and camp in

## Why
Sky islands are bare grassy discs with one campsite each ([peaceful-mode.md](peaceful-mode.md)). The peaceful mode
needs places worth landing on: woods to gather in, bigger land to wander, and caves to find and camp in.

## Decisions (developer, 2026-11-21)
- **All three, in one go:** island details (trees, boulders, bushes), a larger island type (the landmass), and caves.
- **Caves are separate cave rocks** set on an island's top (a hollow dome with a mouth), not tunnels carved into the
  island's mesh. The island mesh and the ship's push-out from rock stay as they are.
- **Trees are a source of firewood.** A tree is a networked deadwood pile (Camping's `IDeadwood`): its canopy holds
  the logs, one lobe per log, and grows back.
- **Nothing is pre-placed** (developer, 2026-11-22). No campsites stand on the islands. A player ties a log into a
  campfire bundle (Primary) and sets it down on any flat island ground with room (caves too, not the ship's deck): a
  ghost follows the aim, green where it fits, red where not, and Primary confirms. A tent roll is set down the same
  way. The island camps feature is gone.

## Shape
### SkyIslands (every peer, no network objects, as before)
- **Landmass:** a rare kind of main island (`LandmassChance` of main cells, rolled from its own hash so other cells keep
  their layout), 150 to 200 m in radius, centred in its cell, with no satellites. Its top rolls with real hills. Its
  collision top is cut into upright prisms (each patch of ground down to the rim), not wedges meeting at the axis,
  so a valley never gets an invisible floor.
- **Sites** (`SkyIslandSiteProcessor`, pure, from the shape seed): on a main island, the clearings (open ground to camp on: the
  middle, a few more on a landmass, one inside each cave) and the caves (one on a big enough main island by
  chance, two or three on a landmass), each on flat enough ground, inside the rim, clear of each other.
- **Cave rock** (`SkyIslandCaveMeshProcessor`): a lumpy dome shell, a chamber about 20 m across and 8 to 11 m high,
  a mouth facing the island's middle. Its visual is part of the island's mesh; its collision is convex pieces of the
  shell, under the island's body, so it is solid to the ship and players and is the ground probe's rock.
- `SkyIslandShape` (was the mesh processor's private `Shape`) answers the surface height anywhere on the top.

### IslandDetails (new feature, references SkyIslands)
- `IslandDetailPlacementProcessor` (pure): trees in groves, boulders and bushes on the grass of every island, clear of
  the clearings, the caves and their mouths, and the rim. The same on every peer.
- **Scenery** (`IslandSceneryUseCase`, every peer): boulders (solid) and bushes from pooled prefabs.
- **Trees** (`IslandTreesUseCase`, server): networked tree prefabs on main islands, a few per check, despawned with
  their island. Each is a `DeadwoodNetworkMediator` (IA_GatherFirewood, CampingConfig): no new firewood code.
- Trees, like camps, last as long as their island stands; they are not saved (the world save, peaceful-mode P2).

### Camping (changed): players place their camps
- `CampPlacementUseCase` (server, read from the predicted input like the grappling hook): Primary with a log ties a
  campfire bundle; Primary with a bundle or a tent roll sets it down where `CampPlacementFinder` says it fits (ground in
  reach, off the ship, not too steep, room). The campfire stands unlit (a log lights it); the tent stands pitched, and
  packing it gives the roll back and takes it away.
- `CampPlacementPreviewPresenter` (local player) shows the ghost with the same finder, so its colour is the answer.
- Clearings stay: open ground the details keep free of trees and rocks, so there is room to camp.

## Models
Primitives in the look's library materials, built by **TinCan > Dev > Island Details > Build Assets**: autumn trees
(three canopy colours: russet, burnt orange, leaf gold), lumpy boulders, bushes. The cave is procedural rock in the
island material, mossy on top. Please review in play; Blender models can replace the prefabs later.

## Tests
EditMode: sites, cave mesh, landmass layout and shape, prism pieces, detail placement, both detail use cases, camp
placement (processor, finder, use case), a placed tent packing away. Scenarios in `Test_Islands` (solo and host +
client): `IslandForage` (gather firewood from a tree, stand in a cave, set a campfire down in it) and `CampLoop` (in a
clearing: tie a bundle, set the campfire down, light it, set a tent down, pack it away).

Notes (handover, 2026-11-22):
- Builder order: GrappleHook (the placement ghost reuses its `M_GrappleAim_*` materials), then Camping, then Island
  Details.
- Not measured yet: the cost of the island collision as islands stream in (about 170 convex pieces per main island,
  about 500 per landmass), and the many campfire point lights.
- In third person the placement ghost can hide behind the character.

Scenario note: after a tent is packed, the server puts the roll in the subject's hands at once, so a lane must not
wait for "SubjectHolds none" in between.

## Progress
- 2026-11-21: built (see `CODE_MAP.md`, feature index: Sky islands, Island details).
- 2026-11-22: camps are placed by players, not pre-placed (Camping row); island camps removed. EditMode 1670/1670;
  `CampLoop`, `IslandForage`, `IslandsPerVoyage`, `IslandRam` and `ChartIslands` pass solo and host + client.
  Awaiting the developer's playtest.

## Open questions (for the developer)
1. Should trees sway (pillar 4, "alive")? A wind shader for foliage would do it for every tree at once.
2. Do caves want something to find inside (a glow, a resource, a spirit shrine)?
3. Landmass hitches: building one cooks about 500 convex pieces in a frame. If that shows, build it over a few frames.
