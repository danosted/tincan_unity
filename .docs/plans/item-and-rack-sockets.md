Status: Approved (the developer asked for the Carry socket, then left; the design calls below were made in their
absence and are theirs to revisit)

# Items, held visuals and the net rack as sockets (delete the shared TinCan.Features block)

## Why
The last files in the shared `TinCan.Features` block, `Carry/NetRackNetworkMediator` and `Carry/NetSwingVisualView`,
are net-catch minigame content sitting on other prefabs: the fuel fixture and the shared player prefab. Looking
closer, the whole Items core leaks the same way:
- The core Items installer lists every feature's item: jerry can (Fuel), catching net (minigame), repair tool
  (ShipDamage).
- Each item's held visual is a hidden child baked into `NetworkPlayer.prefab` (`Visual/Carry_*`).

Items therefore exist in every scene, whether or not the feature that makes them work is loaded. This is the
half-works case from `feature-containment.md`.

## Decisions
1. **Features contribute their items** through `FeatureInstaller.IExtension<ItemDefinition>`, as they already
   contribute fixtures, cues, events and ability grants. The core Items installer keeps a list for core items (empty
   today) and builds the catalog from both.
2. **An item brings its own held visual.** `ItemDefinition` holds a prefab instead of a child name.
   `EquipmentNetworkMediator` instantiates it once under the player's `Visual` (every peer, driven by the replicated
   item id), then shows and hides it. The three `Carry_*` children become prefabs under `Assets/Prefabs/Items/`,
   keeping their local pose and names, so the scenario visual checks are unchanged.
3. **Net-swing presentation lives on the net's visual prefab** (`NetSwingVisualView`, minigame assembly). It animates
   its own transform and reads the holder's tags from the controller above it.
4. **The net rack is the minigame's own ship fixture.** It gets its own networked prefab: a `NetworkObject`, an
   `EntityNetworkMediator`, `NetRackNetworkMediator`, and the collider and visuals from `FuelSystem`. It has a
   `ShipFixtureDefinition` at the same ship-local pose it has today, and `FlyingCanFeatureInstaller` lists it. A
   fuel-only scene no longer shows a rack that hands out a net nobody can use.
5. **EquipCycle moves to `Test_NetCatch`,** whose profile loads the features that own its items (Fuel and the
   minigame). `Test_Core` only loads core, so it has no items now.
6. **`EquipmentNetworkMediator` injects its core services directly** (the item catalog, the binder, events). It still
   used optional resolution, which phase 5 missed because it used the non-generic `TryResolve(out x)` form.
7. **`Carry/` and the `TinCan.Features` asmdef are deleted,** along with every reference to them.

## Verification
- Compile and EditMode tests, including the rules.
- The full scenario batch: EquipCycle (visuals shown and hidden), NetCatch (the rack fixture, taking the net, the
  swing), and the rest unchanged.
