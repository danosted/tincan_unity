Status: Approved

# Ship lost and respawn

## Why
A ship sinking deep into the cloud sent each player overboard on their own: the deck passed `CharacterResetDepth`
(40 m below the surface) while the ship's own emergency (30 m, 15 s) was still counting, and when that ran out nothing
happened (the expiry handler was a no-op). Going under with the ship should be one event for the whole crew.

Then (2026-11-24): the loss lived in the Voyage feature, which the game's profile does not load (peaceful mode), so in
`cheesed_scene` the countdown ran out and nothing happened at all. The loss moved to its own feature, Ship Loss, which
works with or without a voyage.

## Decisions (developer, 2026-11-24)
- **A sunk ship kills the whole crew**, like dying in Minecraft: a screen offers **Respawn** (the "bed": here the
  ship's last safe spot), and with a voyage also **Restart** (a new voyage from the start).
- **Respawn puts the ship at the last spot where it flew safely above the sea**, whole (full hull and full fuel). The
  world keeps its progress: with a voyage, the same voyage, destination and layout.
- **The 15 s sinking countdown stays.** Past `EmergencyDepth` the crew can still climb out before everyone is lost.
- **A ship lost to its health running out gets the same screen**, Respawn included.
- **The loss is its own feature, not the voyage's** (option 1 of three; the others were a respawn fallback inside the
  cloud boundary, or loading Voyage in the game).
- **Over a hole or rift the ship's depth counts from the sea's level around it** (`ICloudSurfaceQuery.GetSeaHeight`), not
  from the hole's floor 400 m down: falling into one is sinking, so the crew must steer clear of them.

## How it works
- **Core contracts** (`Core/Ship`): `IShipLossSource` (a feature that can lose a ship, and the last pose where it was
  safe), `IShipRecovery` (the ship is brought back: each feature makes its part whole), `IAirshipTeleport` (a network
  teleport, so peers snap instead of sliding), `AirshipUpright` (a respawn pose has no pitch or bank).
- **Cloud boundary** (`Features/CloudBoundary/CloudBoundaryUseCase.cs`): the emergency running out loses the ship. The
  emergency starts `EmergencyDepth` below the sea's level (holes and rifts not counted). A ship above the open sea
  (not over a hole: `CloudBoundaryProcessor.IsSafe`) records its upright pose every tick. A
  player below `CharacterResetDepth` goes overboard only when not with a ship (their moving ground is not the ship);
  the crew aboard shares the ship's fate. Recovery clears the countdown.
- **Ship Loss** (`Features/ShipLoss/`, in `Profile_Base`): `ShipLossUseCase` loses the ship on health or on any loss
  source, sets the replicated flag (`ShipLossNetworkMediator` on the ShipLossState fixture) and holds the ship still
  (`IAirshipHold`). `ShipLostScreenPresenter` shows `Menu_ShipLost` on every peer while lost. Respawn
  (`RespawnShipMenuCommand`, a server RPC) teleports the ship to the first source's safe pose (else where it was first
  seen), runs every `IShipRecovery` and resets the crew aboard. `IShipLoss.BringBack` does the same at any pose.
- **Voyage** (`Features/Voyage/VoyageUseCase.cs`, builds on Ship Loss): Lost while `IShipLoss.IsLost`, underway again
  once it is not. Restart after a loss brings the ship back at the voyage's start (`BringBack`), then begins a new
  voyage. Its Restart row reaches the Lost screen through the `ship-lost-choices` row source (`VoyageLostChoices`).
- **Whole ship:** `ShipBreakageUseCase` (parts and health restored) and `FuelConsumptionUseCase` (tank full; the
  jerry-can crate stays) implement `IShipRecovery`.
- **Menu:** `Menu_ShipLost`: Respawn, the choices row source, Quit (**TinCan > Dev > Ship Loss > Build Assets**).

## Verify
- EditMode: `ShipLossUseCaseTests`, `CloudBoundaryUseCaseTests`, `CloudBoundaryProcessorTests`, `VoyageUseCaseTests`,
  `ShipBreakageUseCaseTests`, `FuelConsumptionUseCaseTests`.
- Scenario `ShipLostNoVoyage` (Test_Galleon, the game's profile; solo and host + client): the ship hurt, dropped deep
  into the cloud, lost on both peers after the countdown, the subject presses Respawn on the keyboard, the ship whole
  where it sank.
- Scenario `ShipLostRespawn` (Test_Voyage, solo and host + client): the same with a voyage, underway again after.

## Open
- A player already overboard (in the Respawn feature's choice) when the ship is lost is reset aboard with the crew,
  but keeps their overboard state and menu until they choose; the two flows are not joined yet.
- The safe spot can later become a hole (holes drift); the ship would then fall again. Not handled.
