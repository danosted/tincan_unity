---
name: verify-feature
description: Run the TinCan verify loop (compile, EditMode tests, solo scenario, host + client scenario) for a feature and triage the result. Use after any gameplay or networking change, when asked to "verify", "run the scenario", "check it works on host and client", or before reporting feature work as done.
---

# Verify a feature

The canonical description is `.docs/NETWORK_TEST_HARNESS.md` ("Scenarios" and "Test range"). This is the procedure.

1. **Pick the scenario** for the feature from `Assets/Scripts/DevTools/Scenarios/ScenarioCatalog.cs`. If the feature has
   none, stop and use the `add-scenario` skill first. A slice is not done without one.
2. **Run** from the repo root, in PowerShell, with the Editor open:
   ```powershell
   .\.tools\verify.ps1 -Scenario <Name> -SaveDirtyScenes
   ```
   - Add `-UpTo Solo` while iterating, then run the full ladder once at the end.
   - Add `-TestFilter <Fragment>` to narrow the EditMode tier.
   - Use `.\.tools\verify.ps1 -UpTo Tests` when there is no scenario yet.
   - The script opens the scenario's test-range scene, syncs the MPPM clone, and restores the starting scene.
3. **Read the exit code:** 0 means every tier passed, 1 means a tier failed, and 2 means the Editor was blocked (a
   modal or a wedged pipeline). An exit of 2 says nothing about the code.
4. **Triage a failure by tier:**
   - **Compile:** fix the errors, and run `unity cmd recompile_status` to confirm.
   - **Tests:** read the failing test's message and fix the code, not the test, unless the test is wrong.
   - **Solo/Duo:**
     - Open `Logs/feature-telemetry/<Name>/latest-summary.json` first, then the failing peer's `latest-<role>.json`.
     - The `timeline` interleaves steps with domain events, warnings and errors.
     - Look at the checkpoint PNGs listed in the output.
     - For errors not in the report, run `unity cmd console --level error --tail 20` (add `--project-path` with the
       clone path for the client).
   - Match the symptom against the troubleshooting table in `.docs/NETWORK_TEST_HARNESS.md` before digging further.
5. **Report** the tier lines, each tier's pass or fail with its timing, and what a human still has to playtest. Do not
   report "done" on a solo pass alone. The host + client tier is the one that proves replication.
