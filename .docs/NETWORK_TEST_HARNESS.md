# Network test harness

Two tools share this harness. **Routes and telemetry** measure how movement feels to a remote player. **Scenarios**
(see [below](#scenarios-feature-tests-in-the-live-game)) check that a feature works on host and client, and give a
verdict an agent can read.

A host has zero latency to itself, so a bug in how a *remote* client feels (sluggish movement, rubber-banding)
cannot be seen by pressing Play and Start Host. The harness reproduces a remote client in the Editor, plays a
fixed input route with a bot, and writes numbers you can compare between changes.

Code: `Assets/Scripts/DevTools/` (assembly `TinCan.DevTools`), registered by
`Assets/Resources/Installers/NetTestHarnessFeatureInstaller.asset`. With none of the flags below it registers
nothing, so normal play is unaffected.

## Flags

Every flag works on a build's command line or as a **Multiplayer Play Mode player tag**
(`Core/Domain/LaunchArguments.cs`). A tag `name` means `-name`; a tag `name:value` (or `name=value`) means
`-name value`.

| Tag | Command line | Effect |
|---|---|---|
| `autohost` | `-autohost` | Start Host on launch (`Core/UI/CommandLineSessionBootstrap.cs`). |
| `autojoin` | `-autojoin [address[:port]]` | Join on launch; defaults to `127.0.0.1:7777`. |
| `joindelay:<s>` | `-joindelay <seconds>` | With `autojoin`: join that many seconds after launch instead of at once (late-join checks). |
| `seed:<n>` | `-seed <n>` | Seed every gameplay random stream (`IRandomSource`): hazards, flying cans and ship breakage repeat from run to run. Perf runs pass one (`perf.ps1 -Seed`). |
| `serverfps:<n>` | `-serverfps <n>` | With `-server`: the dedicated server's frame cap (default and minimum: the tick rate, 30). The transport is read once per frame, so the cap bounds how fast input is picked up (measured: 60 fps saves ~16 ms of input latency for ~24 % more server CPU; .docs/PERFORMANCE.md). |
| `noinputlead` | `-noinputlead` | On a client: turn off the input-lead controller (`InputLeadProcessor`), which steers the client's time lead from the server's input queue depth. For before/after comparisons; see [`plans/input-queue-lead.md`](plans/input-queue-lead.md). |
| `netsim:<preset>` | `-netsim <preset>` | Delay, jitter and loss on this peer's outgoing packets (see presets). |
| `bot:<route>` | `-bot <route>` | Play a scripted input route once the local player exists. Implies `telemetry`. |
| `telemetry` | `-telemetry` | Measure the local player and write a report. |
| `botloop` | `-botloop` | With `bot`: restart the route when it ends, so the load lasts the whole run (perf runs). No telemetry report per lap. |
| `perf` | `-perf` | Run the perf sampler: frame, tick, GC, physics, draw-call and network numbers over a window, written to `Logs/perf/`. Options and reports: [`PERFORMANCE.md`](PERFORMANCE.md). |

A batch-mode client (`-batchmode -nographics`, a headless bot) caps itself at 60 fps, as a server caps itself at the
tick rate (`NGONetworkService`); uncapped, one spun at ~4,600 fps on about three cores.

**Presets** (`DevTools/NetworkConditionPresets.cs`). Each peer delays only what it sends, so give host and client
the same preset: the round trip is then twice the send delay.

| Preset | Send delay | Jitter | Loss | Round trip |
|---|---|---|---|---|
| `Lag50` | 25 ms | 3 ms | 0% | ~50 ms |
| `Lag100` | 50 ms | 5 ms | 1% | ~100 ms |
| `Lag200` | 100 ms | 10 ms | 2% | ~200 ms |
| `Lossy` | 50 ms | 15 ms | 5% | ~100 ms |

The presets use UTP's built-in simulator layer, which NGO enables in the Editor and in development builds. No
extra package is needed.

**Routes** (`DevTools/BotRoutes.cs`). Moves are paired (forward then back), so the player stays on the deck.

| Route | Run it on | What it does |
|---|---|---|
| `DeckWalk` | client | Starts, stops, strafes, jumps and sprints, three times over (~50 s). |
| `Pilot` | host | Takes the helm station through the server's station occupancy, then cruises, turns both ways and brakes (~58 s). The deck moves under the client. |
| `PilotTilt` | host | Takes the helm, pitches the deck up and down (the Helmsman context's Pitch; Space/Shift by default), then banks through turns both ways (~40 s). |
| `Idle` | either | Stands still for 45 s: measures deck creep (`idleDriftCmPerS`) and snaps. |

**TinCan > Dev > Net Harness > Run Tilt Test (Lag100)** runs `PilotTilt` on the host and `Idle` on the client.

## Running a measurement

**TinCan > Dev > Net Harness > Run Host + Client (Lag100)** (`DevTools/Editor/NetHarnessPlayerTagsMenu.cs`), or
from the shell:

```bash
unity cmd menu --path "TinCan/Dev/Net Harness/Run Host + Client (Lag100)"
```

The menu does four things:
1. It gives **Player 1** (this Editor) `autohost`, `netsim:Lag100`, `bot:Pilot`, and **Player 2** `autojoin`,
   `netsim:Lag100`, `bot:DeckWalk`.
2. It launches Player 2 if it is not running (MPPM does not relaunch it after an Editor restart).
3. It enters Play.
4. **When Play ends it removes the tags again**, from the players and from `ProjectSettings/VirtualProjectsConfig.json`.
   A run interrupted by a crash or restart is cleaned up on the next Editor start. The tags never stay in your
   normal Play or leak to collaborators through the committed project settings.

**Clear Tags** removes them by hand. MPPM has no public API for tags, so the menu reaches into its internals and logs
an error if a Unity upgrade moves them. The fallback is by hand: create the tags under **Project Settings >
Multiplayer > Playmode**, then assign them in **Window > Multiplayer > Multiplayer Play Mode**.

While a bot route runs, the host disables the cloud-boundary character reset (`DevTools/HarnessGameplayOverrides.cs`).
Otherwise a ship that sinks below the reset depth respawns the bots every tick.

Both peers connect on their own and the bots start. After about a minute each peer writes its report to
`Logs/net-telemetry/` (git-ignored): a timestamped file plus `latest-host.json` and `latest-client1.json`. The
Console also gets a `[NetTelemetry]` line every 10 s and a one-line JSON report at the end. Then:

```bash
unity cmd editor_stop
```

**F3** toggles the live overlay in each game view.

## Reading a report

Everything is measured on the locally **rendered** pose, in the local space of the platform the movement layer says
the player stands on. Ship motion itself never counts as player motion. Results are split into `shipStill` and
`shipMoving`.

| Field | Meaning | Physics floor |
|---|---|---|
| `start` | Move input on until the player visibly moves 2 cm. **The "sluggish" number.** | one tick: the first tick already moves ~8 cm (acceleration 75 m/s²), plus the wait for that tick (up to 33 ms) |
| `stop` | Move input off until speed visibly halves. | ~50 ms from a walk (deceleration 75 m/s²), plus up to one tick |
| `jump` | Jump pressed until the player visibly rises 5 cm. | about one tick |
| `snaps` / `maxSnapM` | Frames where the **drawn** body (the interpolated `Visual`) moved faster than any legal motion: teleports, the spawn fall, and any correction or tick step the smoothing failed to hide. | 0 outside spawn and teleports |
| `reversals` | Direction flips while input is steady: prediction fighting a correction. | 0 |
| `idleDriftCmPerS` | Horizontal creep while no input for 0.5 s: sliding on the deck. In walking routes it also picks up the tail of a sprint stop and replays, so read it from the tilt test. | 0 |
| `avgRttMs` | Transport round trip on a client. UTP estimates it from reliable acks, so it goes stale when the client sends little reliable traffic (the humanoid input stream is unreliable). Trust the preset until this is ack-based. | ~2 × preset send delay |
| `prediction` (client) | `acks` received; `matches` (prediction within 2 cm); `corrections` (rewind and replay; `meanCorrectionM` is how far the player moved); `snaps` (teleport or large divergence); `ackLatencyMs` (input sent until the server confirms it applied it). | matches ≈ acks; snaps only on spawn or teleport |
| `serverInputs` (host) | Per remote player: inputs received and consumed, `starved` ticks (no input: the last one repeated), `skipped` (queue overflow), queue depth. | starved ≈ 0 after connect, skipped 0, depth ~1–2 |

Latency lines read `p50 / p95 / max (n=samples, timeouts)`. A timeout means no response within 2 s.

The floors come from the player's tunables (`HumanoidControllerView` on `NetworkPlayer.prefab`: walk speed, acceleration,
deceleration). `HumanoidMovementFeelTests` (EditMode) pins them in ticks: walking speed within 3, a stop within 3 from a
walk and 6 from a sprint, a reversal within 6. Retune there first; in first person the whole view glides with the body,
so gentle values feel sluggish (2026-10-04: 30/20 m/s² measured stop p95 ~550 ms; 75/75 measured ~117 ms).

The host's own player is local, so its numbers are the floor. The client's numbers are what a remote player feels.

## Scenarios: feature tests in the live game

Routes measure movement. **Scenarios check that a feature works**, on host and client, and give a pass or fail
verdict that an agent can read without a human watching. Code: `Assets/Scripts/DevTools/Scenarios/`.

**One command** runs the tiers, cheapest first, and stops at the first red one:

```powershell
.\.tools\verify.ps1 -Scenario NetCatch                  # compile, EditMode tests, solo run, host + client run
.\.tools\verify.ps1 -Scenario NetCatch -UpTo Solo -TestFilter Scenario
.\.tools\verify.ps1 -Scenario NetCatch,RepairLoop    # a batch: compile and tests once, then each scenario
.\.tools\verify.ps1 -All                               # every scenario in ScenarioCatalog
```

In a batch a failing scenario does not stop the others (its Duo is skipped when Solo failed); a table at the end
lists every result. Run `-All` before committing a change that touches shared code.

| Tier | What runs | Typical time | Catches |
|---|---|---|---|
| Compile | `AssetDatabase.Refresh`, then the Editor's compile state; errors from `recompile_status` | ~10 s unchanged | build breaks, including scripts broken before the run |
| Tests | `unity cmd run_tests --mode EditMode` (optional `-TestFilter`) | ~10 s | processor, use case and handler logic |
| Solo | menu **TinCan > Dev > Scenarios > *X* (Host)** | ~15 s | wiring, installers, assets, server flow |
| Duo | menu **TinCan > Dev > Scenarios > *X* (Host + Client, Lag100)** | ~40 s | replication, prediction, what the remote player sees |

Exit code 0 means every tier passed, 1 means a tier failed, and 2 means the Editor was not usable (busy, timed
out, or a modal dialog is open). The script guards against the MPPM and Editor failures seen in practice:

- It stops on a blocking dialog such as "Scene(s) Have Been Modified" instead of hanging.
- If an open scene is marked modified, the script stops before Unity can raise its save dialog. Pass
  `-SaveDirtyScenes` to save it first. Play sessions no longer mark the scene dirty on their own (see the cloud
  entry in the `CODE_MAP.md` traps), so a dirty scene now means a real unsaved edit.
- A wedged pipeline is woken by focusing the Editor window. With `-RestartEditor`, the script also uses
  `unity close --force` and `unity open` as a last resort, which discards unsaved changes.
- It opens the scenario's [test-range scene](#test-range) and restores your scene at the end.
- Before a host + client run it reopens the host's scene in every clone. Otherwise a clone that relaunched with an
  empty scene "plays" nothing, and one holding an old copy of a rebuilt scene runs that. It first waits (up to 30 s)
  for the clone to leave Play mode from the previous run, since a playing clone refuses to open a scene. Afterwards
  it confirms the clone's active scene and stops with `STOP` if it doesn't match, or if no clone is running at all.
  Before this check, a failed switch went unnoticed and only showed up as the client never joining.
- Before every play tier it sets each Game view, in the Editor and in every clone, to **Play Unfocused**, so Play
  doesn't pull Unity or Player 2 in front of whatever the developer is working in. The setting has been seen to
  revert, so the script enforces it each run. A focus log of a full InteractRack run (compile, tests, solo, host +
  client) showed the developer's window keeping focus throughout.
- The harness only starts a scenario in the scene it belongs to (`Scenario.ScenePath`). A scenario player tag left
  over from an interrupted run logs a warning in any other scene instead of breaking it.
- Each scenario tier line shows how long the run took.

The menu also waits for Player 2 to report `Launched` before entering Play, and retries MPPM's tag file when a clone
holds it.

### Anatomy

A scenario (`ScenarioCatalog.cs`) has three phases:

- **Arrange** runs on the server and sets up the world around the *subject*: it spawns, equips or breaks things
  through `Do(...)` commands. The subject is the client's player, or the host's own player in a solo run.
- **Act** runs on the subject's peer through real input (`Hold`, `Tap` with `ScriptedAction` intents, which obey the input
  contexts like keys; see `INPUT.md`), so prediction and replication are
  exercised. It checks what that peer sees (`WaitUntil`, `Expect`) and takes screenshots (`Checkpoint`).
- **Assert** runs on the server and checks the authoritative outcome.

A host + client run plays Arrange then Assert on the host (the *server lane*) and Act on the client (the *subject
lane*). A solo run plays both lanes side by side on the host, so the phases interact exactly as they do across the
network. Its report prefixes each line with `server:` or `subject:`. Phases synchronise through replicated state (`WaitUntil`), never through fixed waits. A failed `Do` or
a timed-out `WaitUntil` aborts the run. A failed `Expect` is recorded and the run continues, so one report lists
every broken expectation.

Commands and probes come from `IScenarioLibrary` classes, one per feature (`NetCatchScenarioLibrary.cs`), plus
`CommonScenarioLibrary` (`SubjectReady`, `SubjectHasTag`, `SubjectLacksTag`, `NoRemotePlayers`) and `GameplayCueScenarioLibrary`
(`CueCount "Cue.X:Execute|Active|Removed:n"`, `CueActive`, `CueInactive`; counted on each peer from the scenario start). The catalog entry registers its
libraries only when that scenario runs, so a library's feature dependencies are resolved only then.

**Late join.** `.JoinLate(seconds)` on the builder makes the host + client menu give the client
`joindelay:<seconds>`. The server lane starts as soon as the host's player exists, so its first Arrange steps run
while no client is connected; `Expect("NoRemotePlayers")` after them proves the ordering, and fails loudly if the delay
was too short. The client then checks what it joined into. `ShipDamageLateJoin` is the example.

### Output

`Logs/feature-telemetry/<Scenario>/` (git-ignored) contains:

| File | Content |
|---|---|
| `latest-summary.json` | **Read this first.** Overall `passed`, and each peer's status, failures and report path. The host writes it once every client has reported, or after 20 s, in which case a missing client counts as a failure. |
| `latest-<role>.json` | One peer's full report: status, failures, expectations, checkpoint paths and a `timeline`. The timeline interleaves steps with every domain event published on that peer (`ScenarioEventRecorder`), plus warnings and errors. |
| `<role>/NN-<checkpoint>.png` | Screenshots from the latest run only. |

The Console gets `[Scenario]` lines and a final `[Scenario] SUMMARY {json}`. Play mode ends by itself about 1 s
after the summary is written. Flags, also usable as MPPM player tags: `scenario:<name>` and `scenariomode:solo`.

### Adding a scenario for a feature slice

1. **Pick the area scene** the scenario runs in (see [Test range](#test-range) below). Reuse an area when the
   feature belongs to it. Add an area only when the feature needs installers no existing area loads.
2. **Put the commands and probes** the feature needs in an `IScenarioLibrary` under `DevTools/Scenarios/`.
   - Commands act on the server through the feature's own interfaces.
   - Probes read state that is replicated to the peer running them.
   - Stand the subject with `ScenarioPlacement.OnGround`, never at a height computed from something else.
3. **Add a `ScenarioEntry`** to `ScenarioCatalog`, with `.InScene(TestScenes.<Area>)`, and list it in `All`.
4. **Add a `(Host)` and a `(Host + Client, Lag100)` menu pair** in `DevTools/Editor/ScenarioMenu.cs`.
5. **The slice is done** when `.\.tools\verify.ps1 -Scenario <Name>` exits 0.

The same steps as an agent procedure: `.claude/skills/add-scenario/SKILL.md` (Claude) and
`.github/prompts/add-scenario.prompt.md` (Copilot).

## Test range

Scenarios run in small scenes built for testing, not in the POC scene. A scenario loads only its feature area, on a
bare ship, with no clouds or scenery. That cuts false failures: there are no ropes or stairs to block rays, and no
features the scenario did not ask for. It does not make runs noticeably faster. A Play session's fixed cost (entering
and leaving Play mode with domain and scene reload, connecting, spawning) is about 18 s whatever the scene holds;
see the timings in `.docs/plans/test-range.md`.

| Part | Where | What |
|---|---|---|
| Test ship | `Assets/Prefabs/Test/TestShip_Prefab.prefab` | Variant of `Airship_Prefab`: same components, no art. A flat deck with its top at ship-local y -3.49 (the real mid deck) and a raised foredeck from z 10 to the bow with its top at -1.815 (the real `FloorFront`, where the cannons stand), reached by a ramp at z 6..10, so authored fixture poses still land on a deck. Low rails. |
| Area scenes | `Assets/Scenes/Test/` (`TestScenes.cs`) | One per feature area. Root objects: the `GameLifetimeScope` and `NetworkService` prefabs, a sun, a fall catcher, and the cloud view with its visuals off. Only the scope's feature profile differs. |
| Profiles | `Assets/Settings/FeatureProfiles/Test/` | `Profile_Test_Core` (UI, cloud submersion, harness, tags, items, targeting, interaction, test range), plus one profile per area that includes it. |
| Test-range installer | `Assets/Settings/FeatureProfiles/Test/TestRangeFeatureInstaller.asset` | Registers the test ship with NGO at runtime. The real ship is registered through `DefaultNetworkPrefabs`, which is not edited. |

Stations come from the area profile's installers, at their authored poses, exactly as in the game. Scenarios only
move the subject.

| Area scene | Profile adds | Scenarios |
|---|---|---|
| `Test_Core` | nothing (core and the base features only) | CoreBoot |
| `Test_ShipDamage` | Fuel, ShipDamage | ShipDamage, RepairLoop, ShipDamageLateJoin, AimPitch, InteractRack |
| `Test_NetCatch` | Fuel, FlyingCan | NetCatch, EquipCycle (its items belong to those features) |
| `Test_Cannon` | Stations, Cannon, SkyHazards | CannonShot, HazardStrike |
| `Test_Voyage` | Fuel, ShipDamage, Stations, Cannon, SkyHazards, Voyage, SkyIslands | VoyageLoop, LateJoinBoarding, IslandsPerVoyage, IslandRam |
| `Test_Helm` | Stations, Helm (the test ship has a quarterdeck at the airship's height for the helm) | HelmSteer |

**The scenes are generated.** `DevTools/Editor/TestRangeSceneBuilder.cs` (**TinCan > Dev > Test Range > Rebuild
Scenes**) builds every scene from its `Areas` table and adds them to the build list. Clients load the host's scene
through NGO, so a scene missing from the build list leaves the client out. Change the set-up in the builder and
rebuild; do not edit a test scene by hand. `ScenarioSceneTests` fails if a scenario's scene is missing or not in the
build list.

**Adding an area:** create its profile in `Assets/Settings/FeatureProfiles/Test/` (include `Profile_Test_Core`), add
a constant to `TestScenes`, add a row to `TestRangeSceneBuilder.Areas`, and run Rebuild Scenes.

**Running:** the scenario menu opens the scenario's scene first, and reopens your scene when Play ends. It refuses to
switch while the open scene has unsaved changes. `verify.ps1` opens the scene itself, reopens it in every MPPM clone,
and restores your scene at the end. `.InScene(null)` runs a scenario in whatever scene is open, for a deliberate
check in the POC scene.

### Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Scenarios take about a minute each instead of 10-25 s, `Main thread operation timed out after 5000ms` in the Console, or the client sometimes never joins | A long-lived Editor (a day of domain reloads and Play sessions; 3+ GB per Editor) slows everything. Restart the Editor and its clone from Unity Hub. Measured: InteractRack solo 57 s → 9 s, host + client 60 s → 23 s after a restart. The timeout lines themselves are harmless: a CLI poll landed while the main thread was busy. |
| `verify.ps1` stops with "Unity Hub is not running" or "no Unity Editor is open" | The Editor waits on the Hub (for example a terms dialog after an update), or no Editor is open. Start the Hub, answer its dialog yourself, open the project, rerun. |
| Solo passes, host + client: `until SubjectReady timed out`, client report missing | The clone ran an old copy of the scene. Clones do not reload a scene that changed on disk; `verify.ps1` reopens it in each clone. From the menu, reopen the scene in Player 2 by hand. |
| `VContainerException ... CloudEnvironmentView` | The area profile lacks `CloudSubmersionFeatureInstaller`. Include `Profile_Test_Core`. |
| No ship, players stand on the grey fall catcher | The scene's scope has no profile, so the test-range installer did not register the ship. Run Rebuild Scenes. |
| A station is missing | Its installer is not in the area's profile. |
| Exit code 2, "modal" | A dialog is open in the Editor. Close it; with unsaved scene changes rerun with `-SaveDirtyScenes`. |
| Host + client refused, prefab hashes differ | See the note printed by `verify.ps1`: `EditorUtility.SetDirty(prefab)` and `AssetDatabase.SaveAssets()` on the host. |
| A teleported subject floats or sinks | Place with `ScenarioPlacement.OnGround`. On the server, a client's body may still be falling when the command runs. |
| Debug lines from the network tick flicker | Tick-rate drawing needs a hold time (`TargetingGizmos.DrawDebug(..., seconds)`); a zero-length line lasts one frame. |

## Extending

- **New route:** add a `BotRoute.Builder` chain to `BotRoutes.cs` and list it in `All`.
- **New preset:** add it to `NetworkConditionPresets.cs`.
- **New metric:** extend `MovementResponseTracker` (pure, tested in `NetTestHarnessTests.cs`) and
  `ResponseSummary`.
