Status: Done (awaiting a human playtest)

# Playtest tuning, 2026-09-30: hazards ahead, two cannons, repair reach and aim

Feedback from the first voyage playtest, and what changed.

## 1. Sky hazards come from ahead and scale with the crew

- The field box (`SkyHazardConfig.FieldMin/Max`) is now ahead of the bow: x -45..45, y -10..25, z 90..170 m in the
  ship's level heading frame (it was starboard only, x 30..110).
- `SkyHazardFieldProcessor.ForCrew` sizes the field for the players aboard (`IHumanoidActor`s): each player beyond the
  first adds `MaxAlivePerExtraPlayer` (2) to `MaxAlive` (4) and multiplies `SpawnInterval` (6 s) by
  `SpawnIntervalScalePerExtraPlayer` (0.8). Read each tick, so a mid-voyage join counts at once.

## 2. A cannon on each side, on the foredeck

- Two fixtures: `CannonStationFixture` (starboard, 3.9, -1.815, 17, facing +x) and `CannonStationPortFixture`
  (port, -1.9, -1.815, 17, facing -x), mirrored about the centreline x = 1 on the airship's `FloorFront`, between
  the front mast and the bow fencing (overlap-checked against the airship).
- The developer chose to keep them broadsides and widen the swing: `CannonConfig.YawLimit` 75 -> 105, so each barrel
  swings past the bow into the hazard field.
- The test ship gained a matching foredeck (top at -1.815 from z 10 to the bow, a ramp at z 6..10, rails), so the
  cannons stand on a deck in `Test_Cannon` too.
- Scenarios use the starboard cannon, picked by its ship-local x so every peer agrees (`CannonScenarioLibrary`,
  new `FaceCannon` command); `CannonShot` expects the aim to hold at 105.

## 3. The repair scan starts higher and further back

- `TargetingDefinition.SourceOffset` now applies to every aim source, not just `BodyOffset`
  (`TargetingProcessor.SourcePoint`). The three existing definitions had a zero offset.
- `TD_RepairScan`: offset (0, 0.5, -1), range 3 -> 4 (keeps the reach in front). A leak at the player's feet was more
  than 60° below the eye and outside the cone; from above and behind it falls inside.

## 4. Seeing which leak the tool will hit

- The scan used to run only while the trigger was held, so there was nothing to aim by. `ShipRepairUseCase` now runs
  it every tick for the local player while they hold the tool (`AimedTarget`; the ability is granted on the owner
  too), and `RepairAimHighlightPresenter` brightens that leak's marker: its own colour moved toward white by
  `ShipDamageConfig.AimHighlightBrightness`. No pulse or scale change (a first version wobbled; the developer found
  it confusing). The existing repair progress bar is unchanged.

## Still to judge in a playtest

- Hazard pace for 1 vs 2+ players; whether 90..170 m ahead gives enough time to aim.
- Whether 105° feels right, and whether the barrels clip the bow fencing when aimed low across the bow.
- Leak Socket_3 (-3, -1.6, 16) sits 0.4 m from the port cannon's carriage.
- Whether the brightened marker reads clearly enough; the repair reach from behind the eye.
