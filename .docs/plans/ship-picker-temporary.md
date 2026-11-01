Status: Done

# Ship picker (temporary)

## Idea
While playing, the host opens the pause menu, chooses **Change ship**, and picks any design from a list: built-in
and saved alike. The ships in play rebuild as that design. The point is to try shipyard designs in the live game
without typing a name or restarting with `-shipDesign`.

**This is a stopgap.** The lasting answer is a ship choice made after a connection is established, where designs live
on the server and are checked there (an extension of [hosting-and-joining.md](hosting-and-joining.md), not yet
planned). Until then the picker must stay easy to pull out: one folder, one assembly, one installer asset, and no
change to core or to any other feature.

## What exists today
- `IShipDesignStore.List()` returns every design key, built-in ones flagged (`Features/ShipDesigns/IShipDesignStore.cs`).
- `IShipDesigns.Select` + `TryApply` swap the design of ships in play on the server: the design is validated, then
  replicated, and every peer rebuilds the ship (`Features/ShipDesigns/ShipDesignUseCase.cs`). The shipyard's Launch
  (`ShipyardUseCase.Launch`) and the scenario command `FlyDesign` (`DevTools/Scenarios/ShipDesignsScenarioLibrary.cs`)
  already do exactly this.
- A feature adds rows to the main/pause menu through `IMainMenuRows` (`Core/UI/IMainMenuRows.cs`; the shipyard's row).
- A menu can be built in code (`MenuDefinition.Create`) and shown with `IMenuSystem.Open`; a command reads which
  row ran from `MenuContext.ItemId`, so one command serves every design row.

## Shape
A new feature, `TinCan.Features.ShipPicker` (`Assets/Scripts/Features/ShipPicker/`), referencing core UI, Core.Domain
(networking) and ShipDesigns. Nothing references it.

- `ShipPickerUseCase` (pure of Unity, tested): builds the picker menu from `IShipDesignStore.List()` (one row per
  design, labelled with the key, built-ins tagged "(built-in)", then Back), and `Pick(key)`: refuses on a client
  ("Only the host changes the ship."), loads the design, `Select`s it, `TryApply`s it to every ship in play, and
  reports the outcome (applied to n ships, or the store's or validator's reason).
- `OpenShipPickerMenuCommand`: the main-menu row; opens the menu the use case builds (rebuilt each time, so a design
  saved a moment ago is listed).
- `PickShipMenuCommand`: a design row; `Pick(context.ItemId)`, then closes the menu on success.
- `ShipPickerFeatureInstaller` (`IMainMenuRows`: **Change ship**, placed by the core before Quit), asset
  `Resources/Installers/ShipPickerFeatureInstaller.asset`.
- Feedback: the outcome is logged through `IEventPublisher` (source `ShipPicker`). If the menu framework has a note
  row usable from a code-built menu, a refusal also shows as a `Note` row; otherwise the log is enough for a stopgap.

The swap logic (Select + TryApply over the ships) is now in three places. It stays duplicated on purpose: moving it
into `IShipDesigns` would tie ShipDesigns to the picker's lifetime. If the lasting ship choice wants the same call,
that plan moves it.

## Slices
### P1. The picker
Feature assembly, use case, the two commands, the installer and its asset; added to `Profile_Galleon` (the live
game) and `Profile_Test_Shipyard`. Test `ShipPickerUseCaseTests`: rows match the store's list in order, built-ins
tagged; `Pick` on a client refuses and applies nothing; an unknown key and an invalid design are refused with the
reason and leave ships as they were; a valid key is selected and applied to every ship. A row in the `CODE_MAP.md`
feature index, marked temporary with a link to this plan.

### P2. Scenario `ShipPickerSwap`
In `Test_Shipyard` (host + client, and solo). The subject stands on the starter ship's deck; the host picks `Raft`
through the use case (a scenario command `PickShip <key>` in a `ShipPickerScenarioLibrary`, since menu clicks are not
scriptable from scenarios); both peers see `ShipDesignBuilt` with the Raft's hash and part count; the subject is still
`SubjectOnShip` after the rebuild. Then pick back to `Starter` and check again.

## Removing it
Delete `Features/ShipPicker/`, `Resources/Installers/ShipPickerFeatureInstaller.asset`, the two profile entries,
`ShipPickerUseCaseTests`, the scenario and its library and catalog entry, the `TinCan.Features.ShipPicker` reference
in `TinCan.DevTools.asmdef` and `TinCan.Tests.EditMode.asmdef`, the `CODE_MAP.md` row, and the ship-picker example
in `UI_FRAMEWORK.md` ("A menu whose rows come from data"). Nothing else names it.

## As built (P1, P2)
- `Pick` validates the design itself (`ShipDesignValidator`, as the shipyard's Launch does) before `Select`, so a
  design that cannot fly is never selected, even with no ship in play. Offline, a pick only selects.
- Design rows are labelled by `ShipDesignListing.ToString()` ("Raft (built in)", matching the shipyard's list); their
  ids are `ship/<key>` so no key can clash with Back.
- A refusal reopens the picker with the reason as a `Note` row; a pick closes every menu (back to the game).
- The scenario adds a probe `ShipFlies <key>` (replicated and built hash equal the stored design's). Phases cannot
  signal each other directly, so the subject jumps once it has checked the Raft; the server waits for its feet to rise
  (`SubjectFeetAbove`, replicated), lets it land, then picks `Starter` back.

## Risks and open questions
1. **Crew on deck during a rebuild.** Launch rebuilds ships in play already, but no scenario has players standing on
   deck while it happens. If the new floors are lower or elsewhere, the crew may fall. P2 checks the subject stays
   aboard on the Raft swap; how it feels is a human playtest.
2. **Fittings after a swap.** The ShipSockets loadout mounts a design's fittings when the ship is built, once per
   design, and despawns a fitting whose part goes. A Galleon to Corsair swap should drop the Galleon's fittings and
   mount the Corsair's (if it has a loadout), but this is untested. Check in the playtest; if fittings stay behind,
   note it here rather than fixing ShipSockets for a stopgap.
3. **Precedence.** A picked design wins over `-shipDesign` and the ship prefab's `ShipDesignPreset` for the rest of
   the session (that is what `Select` does). A new session starts from the usual order again.
4. **The shipyard design browser** (a list with pictures inside the shipyard) is deferred; this list covers the
   "which designs do I have" need for now.

## Docs to update with P1
`CODE_MAP.md` (feature index row), `UI_FRAMEWORK.md` (a feature menu built in code, if that pattern is new there).
