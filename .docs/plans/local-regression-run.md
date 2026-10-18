Status: Draft

# Local regression run

## Goal
One command that checks a commit for regressions without touching the open Editor: EditMode tests, then every
scenario in `ScenarioCatalog` in every network topology, headless in containers. It runs in the background, nothing
takes focus, and it ends with a verdict that both a human and an agent can read.

The same command serves both. The developer runs it after committing to main and reads `report.html`. An agent
runs it as a background task, waits for it to exit, and reads `run.json`. There is no orchestration to do beyond
that.

Not a CI gate: nothing blocks a push. It is the local stand-in until there is a build server, built so that a build
server could later run the same script.

## Decisions (recommended options; review them)
1. **It tests a commit, not the working tree.** `regress.ps1 [-Ref <commit>]` (default `HEAD`) checks the commit out
   into its own git worktree (`../tincan_regress`, kept between runs so its `Library/` stays warm). Every result
   belongs to a commit hash, so "green" means "this commit was green", and work continues in the main tree meanwhile.
   The worktree is reset to the ref each run (`git checkout --detach <ref>` plus `git clean`), never edited.
   An agent that wants to check uncommitted work uses `verify.ps1`, not this.
2. **Unity runs in batch mode on Windows, not in a container.** Tests and the build run with the Windows Editor in
   batch mode against the worktree, a second project path, so the open Editor and `.tools/logs/editor.lock` are not
   involved. The licence is Unity Personal, activated on this PC through the Hub. Batch mode on the same PC uses that
   activation. Activating Personal inside a container is the awkward case, and this avoids it. R0 confirms that a
   batch-mode instance runs while the Editor is open on the main tree.
3. **Three topologies per scenario, from one build.**

   | Topology | Containers | Matches |
   |---|---|---|
   | **Dedicated** | dedicated server (`-server`) + headless client | the shipping setup |
   | **Host** | headless host (`-autohost`) + headless client | today's Editor Duo |
   | **Solo** | headless host alone (`-autohost`, `scenariomode:solo`) | today's Editor Solo |

   All three use the development Linux server build that `build.ps1 -Perf -Image` already makes. The game code has
   no `UNITY_SERVER` compile paths: server, host and client are decided at runtime. `perf.ps1` already runs this
   build as a headless client that owns a player, so a headless host is expected to work too. R0 confirms it. If it
   doesn't, the host topologies use a Linux standalone (non-server) build run with `-batchmode -nographics`. That
   is a second build, so it is the fallback, not the plan.
   `-Topology Dedicated,Host,Solo` (default: all) narrows a run.
4. **Scenarios run in parallel.** One Podman network per scenario-topology pair, so ports never clash, with a
   `-Parallel <n>` cap (default: cores / 2). A timeout or failure in one pair never stops the others. Solo failing
   does not skip that scenario's Host or Dedicated lanes, so the report shows every topology's result.
5. **No screenshots headless.** `Checkpoint` steps are recorded in the timeline but capture nothing when there is no
   renderer. The Editor run keeps them.
6. **Results are kept per run.** `Logs/regress/<time>_<commit>/`, like perf's run folders:
   - `run.json`: the machine-readable verdict (see [Agent use](#agent-use)).
   - `report.html`: the human-readable report (see [Report](#report)).
   - Each pair's `latest-summary.json`, peer reports and container logs.

   `regress.ps1 list` prints the verdict per commit.
7. **Exit codes match `verify.ps1`:** 0 all green, 1 something failed, 2 the environment was not usable (Podman not
   running, the build failed to start, the worktree is unusable).
8. **One run at a time, queued like the Editor lock.** `regress.ps1` holds `.tools/logs/regress.lock`, its own lock,
   separate from `editor.lock`, so a regression run and an Editor `verify.ps1` run can overlap. A second regression
   run waits (`-LockWaitSeconds`, default 1800; 0 stops at once with exit code 2), as `Enter-EditorLock` does in
   `TinCan.Common.psm1`.
9. **Agents may start it without asking.** It takes no focus and doesn't use the Editor, so the
   ask-before-play-runs rule (AGENTS.md) does not apply to it. It does load the CPU for a few minutes. If that
   bothers you during play or work, make it "ask first" instead.

## Agent use
- Start: `.\.tools\regress.ps1` as a background task (Claude Code: `run_in_background`), then wait for the exit
  notification. Don't poll.
- The last console line is `REGRESS PASS|FAIL|STOP <run folder>`. The exit code says the same.
- `run.json` holds the commit, the verdict, the EditMode result (counts, failing test names and messages), and one row
  per scenario-topology pair: status, duration, failed expectations, aborted steps, and the paths to the peer
  reports and container logs. An agent triages from it the way it triages `latest-summary.json` today
  (`.claude/skills/verify-feature`).
- A red pair is reproduced in the Editor with `verify.ps1 -Scenario <Name>` (Solo, Host) when it fails there too. A
  Dedicated-only failure is reproduced with `regress.ps1 -Scenario <Name> -Topology Dedicated`.

## Pieces

### R0. Spike: one scenario by hand
With the existing perf builds and `Logs/` mounted, run CoreBoot, then NetCatch, in each topology:
- **Dedicated:** `TinCanServer.x86_64 -server -scenario <Name>`, plus a client with `-autojoin <server> -scenario <Name>`.
- **Host:** the same with `-autohost` instead of `-server`.
- **Solo:** `-autohost -scenario <Name> -scenariomode solo` alone.

Also run one batch-mode Editor build from a worktree while the Editor is open on the main tree (decision 2).
Record what breaks. Gaps expected from reading the code:
- **Scene choice.** In the Editor, the menu or `verify.ps1` opens the scenario's test-range scene. A build starts in
  scene 0. Needs a `-scene <path>` launch argument (`Core/Domain/LaunchArguments.cs`), or the harness loading
  `Scenario.ScenePath` through NGO scene management on the server before the session starts. The test scenes are
  already in the build list (`TestRangeSceneBuilder`).
- **The server lane never starts on a dedicated server.** `ScenarioUseCase.TryStart` waits for a local player
  (`GetLocalPlayerActor`), which a dedicated server never has. The server role needs its own start condition
  (network active as server, no local player required). Host and Solo keep today's condition.
- **Checkpoints.** `ScreenCapture.CaptureScreenshot` in `ScenarioUseCase` must be skipped when headless
  (decision 5). Code reads `Application.isBatchMode` directly today (`SkyIslandBuilder`, `NGONetworkService`).
  Add the testable headless fact proposed in `dedicated-server-container.md` (D3) rather than one more direct read.
- **A headless host.** A host owns a local player with a camera and possession responders. Check that nothing
  throws without a renderer or input devices. The perf bots suggest it is fine, but they are clients.
- **Quitting.** After the summary, the server or host ends with `Application.Quit()`. Check the client quits too,
  or the script stops its container once the server has exited.
- **Output path.** Scenario reports go under `Logs/feature-telemetry/` (`ScenarioPaths`). Each pair's containers
  mount their own folder, so parallel runs don't share one.

### R1. Harness fixes (DevTools)
Close the gaps R0 confirms. Each change gets an EditMode test, and the Editor scenarios stay green
(`verify.ps1 -All`, once, confirmed by the developer).

### R2. `regress.ps1`
Thin script over the existing modules (`TinCan.Build`, `TinCan.Container`, `TinCan.Verify`), new code in a
`TinCan.Regress.psm1` only where nothing fits:
1. Take `regress.lock` (decision 8) and prepare the worktree at `-Ref`.
2. EditMode tests in batch mode (`-runTests -testPlatform EditMode`), results parsed into the run folder. A red test
   stops the run before the build.
3. Development Linux server build and image from the worktree.
4. Every scenario (or `-Scenario a,b`) in every topology (or `-Topology`), `-Parallel` pairs at a time, each with
   the scenario's timeout.
5. `run.json`, `report.html`, a summary table on the console, the `REGRESS` line, and a Windows toast.

It runs in the foreground. An agent runs it as a background task. The developer runs it in a spare terminal, or with
`-Detach`, which starts it as a hidden process and returns at once, leaving the toast and the run folder to report.

### R3. Docs
- `.tools/README.md`: the script, what it needs (Podman machine running, Linux Dedicated Server module), first-run cost.
- `NETWORK_TEST_HARNESS.md`: the headless topologies, what they cover and don't (screenshots).
- `.claude/skills/verify-feature` (and `.github/prompts/`): when to use `regress.ps1` instead of `verify.ps1`, and how
  to triage from `run.json`.
- `AGENTS.md` and `.docs/AI_CONFIGURATION.md`: `regress.ps1` is the full-suite check and needs no confirmation
  (decision 9). Editor `-All` before every shared-code commit becomes optional, and agents use `verify.ps1` for the
  feature in progress.

## Report
`report.html`, one self-contained file:
- The verdict, commit, time, duration, and the last green commit before this one (from earlier run folders), so a
  regression points to a commit range.
- A grid: one row per scenario, one column per topology, each cell pass or fail with its duration.
- For a red cell: the failed expectations and aborted steps from the peer reports, the last ~20 timeline lines, and
  links to the peer reports and container logs.
- Failing EditMode tests with their messages, when that tier failed.

## Day to day
| When | What | Who |
|---|---|---|
| While developing | `verify.ps1 -UpTo Tests`, plus the scenario being built, in the Editor | developer or agent |
| After committing to main | `regress.ps1`, in the background | developer or agent |
| Red | `report.html` (human) or `run.json` (agent). Fix it, or hand the run folder to an agent | developer or agent |

## Costs and risks
- The worktree's first `Library/` import is slow (comparable to a fresh clone). Later runs: an incremental build of
  a minute or two, then about 30 scenarios × 3 topologies in parallel. Expect around ten minutes on this PC; R2
  measures it. `-Topology` and `-Scenario` cut it for targeted checks.
- Disk: a second `Library/` and the build output.
- CPU: parallel pairs compete with whatever else runs. Lower `-Parallel` while playing.
- Scenarios tuned against Editor timing may be flaky headless. R0 and the first full runs will show which. Fix the
  scenario (sync through `WaitUntil`), never add fixed waits.

## Progress log
- 2026-10-16: drafted. Revised: agent-orchestrated through `run.json` and the exit code; host and solo topologies
  added next to dedicated; Personal licence noted.
