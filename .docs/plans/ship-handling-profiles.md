Status: Done; H1, H2 and H3 built 2026-11-27 (H1 and H2 play-checked: DesignedShipFlies, ShipyardRoundTrip, ShipPickerSwap; H3 compile and tests, live tuning to playtest); "Later" is open

# Ship handling profiles: a base for how a ship flies

## Why

How a ship flies is set in three places that don't agree:

- 13 inspector fields on `AirshipControllerView` (`Core/Ship/AirshipControllerView.cs`), set per prefab
  (`Airship_Prefab`, with overrides on `Galleon_Prefab`);
- for a designed ship, top speed and turn rate come from its parts (`ShipStatsProcessor`: thrust over mass, turning
  slowed by mass), through one global `ShipStatsTuning` in `ShipDesignsConfig.asset`, and silently replace the
  prefab's two values;
- nothing else (acceleration, drift, pitch, banking) can be changed per ship except on the prefab.

So a designer cannot make "a heavy barge" or "a nimble skiff" as one thing they can name, tune and reuse, and tuning
the prefab has no effect on a designed ship. We want a base first: a handful of flying experiences designers own.
Parts layer on top later.

## The idea

- **A handling profile is an asset.** `ShipHandlingProfile` (a ScriptableObject in `Core/Ship`, `TinCan.Ship`) holds
  all of a ship's handling: speed (forward, backward, acceleration, deceleration, drift blend), turning (turn speed,
  angular acceleration and deceleration), pitch (pitch speed, pitch level speed), banking (max bank angle, bank speed)
  and how much the world pitches it (external pitch influence). These are today's 13 fields, with the same meaning and
  tooltips, moved to one place. Profiles live in `Assets/Settings/ShipHandling/HANDLING_<Name>.asset`.
- **The core part picks the profile.** A design has exactly one core part (`ShipPartDefinition._isCore`, the helm
  today). A core part names its profile, so a different core is a different way to fly: `PART_CoreHelm` uses
  `HANDLING_Standard`, and a designer adds e.g. a skiff core (a new core part pointing at `HANDLING_Skiff`) without
  code. The shipyard already offers every core part in its panel.
- **The prefab's profile is the fallback.** `AirshipControllerView` holds one profile field instead of 13. A ship with
  no design (`Airship_Prefab`, `TestShip_Prefab`) flies on it; a designed ship flies on its core's.
- **Parts don't change how a ship flies (for now).** Mass, lift and thrust stay in the part assets, but nothing reads
  them for flight: no speed or turn from thrust over mass, and the design check drops "too heavy for the spirit"
  (`TooHeavy`) and "nothing drives it" (`NoThrust`). Health still comes from hull (it isn't handling). This pauses the
  spirit's lift limit of [spirit-cairn.md](spirit-cairn.md); it returns with the spirit's talents
  ([spirit-talents.md](spirit-talents.md)).
- **The layers, for later.** Profile (the base) → parts adjust it, relative to the profile → GAS attributes (boosts,
  talents) → world forces (`IAirshipFlightForces`: cloud, storm). Each layer only changes the one below, so the
  profile stays the designer's whole base. Today only the first, third and fourth exist.

## How it fits

- **The movement code doesn't change.** `AirshipMovementProcessor` and `AirshipMovementUseCase` keep reading
  `IAirshipView`; the view answers from its profile. Max forward speed and turn speed stay GAS attributes (boosts
  change them); their base values come from the profile.
- **Setting a ship's profile on the server.** `IAirshipTuning.ServerSetBaseStats(speed, turn, health)` becomes
  `ServerSetHandling(ShipHandlingProfile profile, float maxHealth)`: the view keeps the profile (it reads it each time, so edits apply live, H3), the attributes get
  its speed and turn as base values, health is filled to the max. Movement runs on the server only, so clients need
  only what already replicates (the attributes, the pose). A client that ever needs the profile can find it from
  the design, which every peer already has.
- **ShipDesigns.** `ShipDesignUseCase.TryApply` asks the core part for its profile (a core part with none: the
  ship keeps the prefab's, with a warning). `ShipStatsProcessor` keeps mass, lift, thrust and health, and drops
  speed and turn. `ShipStatsTuning` keeps only `MinHealth` (and `SpiritLift` while the shipyard still shows lift).
  The shipyard HUD shows the core's profile name and health instead of speed and turn.
- **Day one plays (almost) the same.** Every designed ship has the one helm core, so every designed ship flies on
  `HANDLING_Standard`: about what the Galleon's and Clipper's parts gave them (they flew 9.7 and 10 m/s, turning
  14 deg/s; now 10 m/s and 14 deg/s for all of them, acceleration 5 as on `Galleon_Prefab`). The bare
  `Airship_Prefab` (and the test ship on it) gets `HANDLING_Brisk`, its old prefab values (30 m/s, 45 deg/s).
  `ModularShip_Prefab` (and the Galleon on it) falls back to `HANDLING_Standard`. Both are made by **TinCan > Dev >
  Ship Handling > Build Assets** (`DevTools/Editor/ShipHandlingAssetBuilder.cs`), which never overwrites a profile
  that exists.

## Steps

### H1: the profile asset, on the prefab

- `ShipHandlingProfile` (values in the `ShipHandling` struct) in `Core/Ship`; `AirshipControllerView` reads it (one
  field); the two profiles above, assigned to the prefabs.
- Test: `ShipHandlingTests` (the view flies on its profile, the default without one, the server's handling over both).
- Verify: compile, tests, the existing flight scenarios (`CloudFlight`, `ShipDesigns`) unchanged.

### H2: the core picks it, parts become cosmetic for flight

- `ShipPartDefinition` gets a handling profile (read only on core parts); `PART_CoreHelm` → `HANDLING_Standard`.
- `IAirshipTuning.ServerSetHandling`; `ShipDesignUseCase` applies the core's profile; `ShipStatsProcessor`,
  `ShipStatsTuning`, the validator's flight check and the shipyard HUD as above (the HUD line is `ShipStats.ToString`:
  "flies as HANDLING_Standard, hull ..."). Tests updated (`ShipStatsProcessorTests`, `ShipDesignValidatorTests`,
  `ShipFrameValidatorTests`, `BuiltInShipDesignTests`); `DesignedShipFlies` expects the profile's top speed (10).
- Verify: compile, tests, `ShipDesigns`, `Shipyard` and `ShipPicker` scenarios, solo and host + client.
- Docs: `CODE_MAP.md` (airship movement, ship designs rows, "where does X live": "how a ship flies"),
  a note in [spirit-cairn.md](spirit-cairn.md) that the lift limit is paused.

### H3: tuned live

- The ship keeps the profile it flies on, not a copy of its values (`AirshipControllerView.Profile`), so an edit to
  the asset, in the Inspector during Play too, applies at once; the profile says so (`ShipHandlingProfile.Changed`,
  from `OnValidate`) and the view passes it on (`HandlingChanged`).
- Top speed and turn rate are GAS attributes, so the mediator gives them the new base through a GAS operation,
  `AbilitySystemUseCase.SetBaseValue` (through `IAbilityControllerBase.SetBaseValue`): the base changes and active
  effects are reapplied on top, so a boost running now still counts. Gameplay still changes attributes with effects;
  `SetBaseValue` is for a base that comes from data.
- Edits made in Play stay in the asset after Play (it is an asset, not the scene).
- Test: `ShipHandlingTests` (an edit applies at once; a profile it no longer flies on changes nothing),
  `AttributeBaseValueTests` (a new base keeps the boost on top, and remains when it ends).
- Verify: compile, tests.

## Later

- A second and third core (e.g. a skiff and a barge) once designers have tuned the standard one.
- Parts adjusting the profile (thrust over mass scaling speed relative to the profile's; mass slowing acceleration
  and drift; sails, oars, wings with their own traits, as in [spirit-cairn.md](spirit-cairn.md)).
- The spirit's lift limit back as a talent.

## Open questions

- None blocking. Profile names (`Standard`, `Brisk`) are placeholders for the designers to rename.
