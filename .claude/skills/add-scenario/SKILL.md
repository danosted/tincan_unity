---
name: add-scenario
description: Add a TinCan feature scenario (an automated host + client test in the live game) for a feature slice, in the right test-range scene. Use when a feature has no scenario, when asked to "add a scenario", "test this in play", or when a slice needs its verify loop.
---

# Add a scenario

The canonical description is `.docs/NETWORK_TEST_HARNESS.md` ("Anatomy", "Adding a scenario for a feature slice" and
"Test range"). Read those sections first. This is the procedure.

1. **Pick the area scene.**
   - Reuse `TestScenes.Core`, `ShipDamage` or `NetCatch` when the feature's installers are already in that area's
     profile (`Assets/Settings/FeatureProfiles/Test/`).
   - Otherwise add an area:
     - a profile that includes `Profile_Test_Core`;
     - a `TestScenes` constant;
     - a row in `DevTools/Editor/TestRangeSceneBuilder.Areas`.
     Then run **TinCan > Dev > Test Range > Rebuild Scenes** (`unity cmd menu --path "TinCan/Dev/Test Range/Rebuild Scenes"`).
   - Never edit a test scene by hand.
2. **Design the three lanes** before writing code:
   - Arrange (server) sets the world up.
   - Act (the subject's peer) uses real input (`Hold`, `Tap` with a `ScriptedAction` intent such as
     `ScriptedAction.Interact`; add an intent to `DevTools/ScriptedAction.cs` + `ScriptedActionMap` for a new action)
     and checks what that peer sees.
   - Assert (server) checks the authoritative outcome.
   - Lanes synchronise through `WaitUntil` on replicated state, never through fixed waits.
3. **Write a library** `DevTools/Scenarios/<Feature>ScenarioLibrary.cs` (`IScenarioLibrary`), one type per file.
   - Commands go through the feature's interfaces and are server-only unless they drive the subject's own camera.
   - Probes read replicated state.
   - Place the subject with `ScenarioPlacement.OnGround`.
4. **Register it:**
   - Add a `ScenarioEntry` in `ScenarioCatalog.cs` with `.InScene(TestScenes.<Area>)`, list it in `All`, and register
     the library in the entry's lambda.
   - Add the menu pair in `DevTools/Editor/ScenarioMenu.cs`.
5. **Verify with the `verify-feature` skill:** `.\.tools\verify.ps1 -Scenario <Name> -SaveDirtyScenes` must exit 0 on
   both the Solo and Duo tiers.
6. **Finish:**
   - Add a progress note to the feature's plan in `.docs/plans/`.
   - Add a row to `CODE_MAP.md` if the feature is new.
   - Report the scenario's tier results.
