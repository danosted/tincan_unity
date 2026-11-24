Status: Approved

# Ship lost and respawn

## Why
A ship sinking deep into the cloud sent each player overboard on their own: the deck passed `CharacterResetDepth`
(40 m below the surface) while the ship's own emergency (30 m, 15 s) was still counting, and when that ran out nothing
happened (the expiry handler was a no-op). Going under with the ship should be one event for the whole crew.

## Decisions (developer, 2026-11-24)
- **A sunk ship kills the whole crew**, like dying in Minecraft: a screen offers **Respawn** (the "bed": here the
  ship's last safe spot) or **Restart** (a new voyage from the start).
- **Respawn puts the ship at the last spot where it flew safely above the sea**, whole (full hull and full fuel). The
  world keeps its progress: the same voyage, destination and layout.
- **The 15 s sinking countdown stays.** Past `EmergencyDepth` the crew can still climb out before everyone is lost.
- **A ship lost to its health running out gets the same screen**, Respawn included.

## How it works
- **Core contracts** (`Core/Ship`): `IShipLossSource` (a feature that can lose a ship, and the last pose where it was
  safe), `IShipRecovery` (the ship is brought back: each feature makes its part whole), `IAirshipTeleport` (a network
  teleport, so peers snap instead of sliding), `AirshipUpright` (a respawn pose has no pitch or bank).
- **Cloud boundary** (`Features/CloudBoundary/CloudBoundaryUseCase.cs`): the emergency running out loses the ship. A
  ship above the open sea (not over a hole: `CloudBoundaryProcessor.IsSafe`) records its upright pose every tick. A
  player below `CharacterResetDepth` goes overboard only when not with a ship (their moving ground is not the ship);
  the crew aboard shares the ship's fate. Recovery clears the countdown.
- **Voyage** (`Features/Voyage/VoyageUseCase.cs`): Lost on health or on any loss source. While Lost it holds the ship
  still (`IAirshipHold`). Respawn (`RespawnShipMenuCommand`, a server RPC) teleports the ship to the safe pose (else
  the voyage's start), runs every `IShipRecovery`, resets the crew aboard and goes underway again. Restart after a
  loss does the same at the voyage's start, then begins a new voyage.
- **Whole ship:** `ShipBreakageUseCase` (parts and health restored) and `FuelConsumptionUseCase` (tank full; the
  jerry-can crate stays) implement `IShipRecovery`.
- **Menu:** `Menu_VoyageLost` gets Respawn first (**TinCan > Dev > Voyage > Build Assets**).

## Verify
- EditMode: `CloudBoundaryUseCaseTests`, `CloudBoundaryProcessorTests`, `VoyageUseCaseTests`,
  `ShipBreakageUseCaseTests`, `FuelConsumptionUseCaseTests`.
- Scenario `ShipLostRespawn` (Test_Voyage, solo and host + client): the ship hurt, dropped deep into the cloud, Lost
  on both peers after the countdown, the subject presses Respawn, underway again with the ship whole where it sank.

## Open
- A player already overboard (in the Respawn feature's choice) when the ship is lost is reset aboard with the crew,
  but keeps their overboard state and menu until they choose; the two flows are not joined yet.
- The safe spot can later become a hole (holes drift); the ship would then fall again. Not handled.
