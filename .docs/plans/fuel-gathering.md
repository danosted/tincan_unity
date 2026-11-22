Status: Draft (2026-11-22)

# Fuel from the islands: mine crystals, carry them aboard, feed the engine

## Why
Today the fuel loop is an event-mode minigame. A crystal rack hands out jerry cans, flying cans are netted out of the
sky, and the receiver is poured full. In the peaceful world ([peaceful-mode.md](peaceful-mode.md)), fuel should be a
reason to land instead: you sail to an island, mine fuel crystals out of its rock, carry them aboard and feed the
engine. Running low means you have to find the next island.

## Decisions (developer, 2026-11-22)
- **The fuel minigame leaves the game.** `cheesed_scene`'s profile has no crystal rack, jerry cans, flying cans or
  net. The ship's tank, its drain and the stall stay.
- **Fuel is gathered.** Crystals grow in island rock, and you have to visit islands to stock up.
- **An empty tank means hand power** (developer, 2026-11-22). With no fuel the engine is dead, and the crew has to make
  power by hand to move the ship.
- **Crystals are stockpiled aboard** (developer, 2026-11-22), in a crystal bin, and feed both the engine and the
  shield ([ship-shield.md](ship-shield.md): the bin is its SH3, the shield's fusing spends them).
- **Not in this plan:** refining crystals into better fuel, and better gathering (faster, more per vein). Both are
  later upgrades. This plan leaves room for them (see "Room for the upgrades") but builds neither.

## Today (verified 2026-11-22)
- **Tank:** the FuelTankState fixture is on every ship. `FuelConfig`: `Capacity` 100, `InitialLevel` 100,
  `DrainPerSecondAtFullThrottle` 0.75 (a full tank lasts about 2 min 15 s at full throttle), boost x2,
  `StallWhenEmpty`. Drain is in `FuelConsumptionUseCase` / `FuelConsumptionProcessor`.
- **Refill:** `PourFuelInteractionHandler` at the receiver (`MotorFillPortNetworkMediator`, the fitting
  `fuel.receiver`), `JerryCanLitres` 25. `DebugFreeRefuel` is on.
- **Firewood is the pattern to copy:** `DeadwoodNetworkMediator` (`IDeadwood`) holds a stock that regrows, worked out
  when read with no tick (`DeadwoodProcessor`). `GatherFirewoodInteractionHandler` puts one log in your hands as an
  item (`ITEM_Firewood`, `GE_Holding_Firewood`, `State.Carrying.Firewood`). Trees are placed by
  `IslandDetailPlacementProcessor` and spawned by `IslandTreesUseCase`, and the tree prefab carries Camping's mediator.
- **No interaction takes time today.** Repair moves forward one hammer swing at a time, and gathering firewood is a
  single press.

## Shape
### Crystal veins (feature `Gathering`, new assembly `TinCan.Features.Gathering`)
- **A vein** is a networked rock outcrop with crystals growing out of it. Like deadwood, it holds a stock that regrows
  (`VeinSize`, `RegrowSeconds`). Vein and deadwood share the stock math: lift `DeadwoodProcessor`'s math into one pure
  `RegrowingStock` in core that both use, rather than copying it.
- **Mining takes a moment.** Hold Interact on a vein for `MineSeconds` while a ring fills, and a crystal breaks off
  into your hands (`ITEM_FuelCrystal`, carried one at a time like a log). Letting go, looking away or walking off
  cancels it. This is the gathering skill as it starts.
- **Where veins grow:** `IslandDetails` places them the way it places trees: on bare rock, near the rim and inside
  caves (the answer to [island-life.md](island-life.md), question 2). A vein prefab carries Gathering's mediator. Main
  islands get a few veins and landmasses more, while small satellites get none, so where you land matters.
- **Looks:** warm amber crystal in grey island rock, glowing faintly at dusk. A bare vein shows stubs. The crystal is
  built from primitives in the look's library materials, then reviewed in play per `ART_DIRECTION.md`.

### Feeding the engine (Fuel, changed)
- The receiver takes a carried crystal (`FeedCrystalInteractionHandler`) and adds `FuelConfig.CrystalLitres` to the
  tank. Fuel knows the crystal through `FuelConfig.CrystalItem`, the way it knows `JerryCanItem` today, so Fuel and
  Gathering never reference each other.
- **Peaceful tuning:** the game profile gets its own `FuelConfig` (through its own Fuel installer asset), so the old
  sandbox scenes keep theirs. The aim is about 10 to 15 minutes of cruising per tank, with one crystal at about 10% of a
  tank, so a stop at one island fills you up. These numbers get tuned in playtest.
- **Running dry:** the tank still empties and the engine stalls, as today (`StallWhenEmpty`). The spirit keeps the
  ship aloft.

### The hand crank (Fuel, new)
- A crank by the engine, a station like the helm (`StationOccupancyUseCase`), mounted as the socket fitting
  `fuel.crank`. Take it and turn it: each turn (a press of Primary, read from the predicted input like the grapple)
  charges a little power, `CrankLitresPerTurn`, into the tank.
- It is slow on purpose: about a minute of cranking for a short hop, so a solo player can crank, then take the helm
  and limp to the nearest island. Two crew cranking (a second crank, or taking turns) charges it faster, which is the
  co-op version.
- It runs only while the tank is low (below `CrankMaxLevel`, a small reserve), so it never replaces gathering.
- One press of Primary is one turn (developer, 2026-11-22). A rhythm that rewards on-beat presses can come later as
  an upgrade.

## Room for the upgrades (not built here)
- **Better gathering:** the mining time comes from one place (the vein's `MineSeconds`). The upgrade later is a crew
  attribute that scales it, applied as an effect through the upgrade shape in `peaceful-mode.md`, and the same for
  crystals per break.
- **Refining:** the litres come from the item (`FuelConfig.CrystalLitres` per crystal item). A refined crystal later is
  another item worth more litres, made at a refinery station. Nothing about the tank changes.

## Slices
- **G1 Veins and mining.** The Gathering feature, `RegrowingStock`, the vein prefab, placement in IslandDetails, the
  hold-to-mine interaction with its ring, and the carried crystal.
- **G2 Feed the engine.** The receiver takes a crystal and the game's `FuelConfig` is added. (The minigame leaves the
  profile in peaceful-mode P0.)
- **G3 The hand crank.** The crank fitting and station, the charge per turn and the reserve cap, mounted by the
  Galleon's engine.
- **G4 Feel.** Vein visuals (full, bare, regrowing), a mining cue and sound, and the fuel HUD reading as "crystals'
  worth" rather than litres, if playtest asks for it.

## Progress
- **2026-11-22, G1 and G2 built (EditMode 1698/1698; `CrystalRun` passes solo and host + client in `Test_Galleon`;
  `IslandForage` and `CampLoop` still pass with veins on the islands; awaiting a playtest).**
  - Held work is in the core, as decided: `InteractionDefinition.HoldSeconds` and `InteractInputUseCase` (the press
    starts it; same target every tick; letting go or looking away cancels), and a HUD meter on the local player
    (`InteractWorkPresenter`). Repair and crafting can use it later.
  - `Features/Gathering/`: `VeinNetworkMediator` and `MineCrystalInteractionHandler` (IA_MineCrystal, 2.5 s);
    `RegrowingStock` lifted into `Core/Domain` and shared with deadwood.
  - Veins are an Island Details kind: 2 on a main island, 6 on a landmass, one against each cave's back wall; placed
    after everything else, so no island's trees or rocks moved.
  - The receiver takes a crystal (`FuelConfig.CrystalItem`, `CrystalLitres` 10). The fuel config is still the shared
    one, and `DebugFreeRefuel` stays on until the crank exists (G3); the game's own tuning is G4.
  - The tag database had two tags in memory but not on disk (the campfire bundle's and the crystal's); fixed, and the
    trap is in `CODE_MAP.md`.
- **Next:** G3 the crank, then the crystal bin (`ship-shield.md`, SH3), then G4 feel and tuning.

## Tests
EditMode: `RegrowingStock` (and deadwood still green on it), vein placement, the mine interaction (hold, cancel,
completion, a bare vein), the feed handler (adds litres, refuses at a full tank, refuses other items), the crank (charges per turn, stops at the reserve cap). Scenarios (in test-range scenes) `CrankStart`
(empty tank, crank, the engine runs again) and
`CrystalRun` in `Test_Islands`, solo and host + client: mine a crystal at a vein, carry it aboard, feed the receiver,
and the tank rises; the client sees the vein's stock drop.

## Open questions (for the developer)
1. **Hold-to-work:** add a duration to interactions in core, which repair, gathering and later crafting can all use,
   or keep it inside Gathering, reading the held Interact the way `CampPlacementUseCase` reads Primary?
   Recommendation: core, since there will be more of these.
2. **One crystal per trip, or a sack** (the first inventory) between the vein and the bin aboard? Recommendation: one
   per trip, with the crystal worth enough that a trip pays. Decide after playtest.
3. **The old minigame:** leave it out of the peaceful profile only (event mode keeps it), or delete the crystal rack,
   jerry cans, flying cans and net? Recommendation: leave them out of the profile for now, and delete later if event
   mode doesn't want them. (`peaceful-mode.md` had imagined the net skimming things from the cloud sea.)
