# Network test harness

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
| `autohost` | `-autohost` | Start Host on launch (`Features/UI/CommandLineSessionBootstrap.cs`). |
| `autojoin` | `-autojoin [address[:port]]` | Join on launch; defaults to `127.0.0.1:7777`. |
| `netsim:<preset>` | `-netsim <preset>` | Delay, jitter and loss on this peer's outgoing packets (see presets). |
| `bot:<route>` | `-bot <route>` | Play a scripted input route once the local player exists. Implies `telemetry`. |
| `telemetry` | `-telemetry` | Measure the local player and write a report. |

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
| `Pilot` | host | Takes the helm through the server's possession authority, then cruises, turns both ways and brakes (~58 s). The deck moves under the client. |
| `PilotTilt` | host | Takes the helm, pitches the deck down and up (Jump/Sprint at the helm), then banks through turns both ways (~40 s). |
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
| `start` | Move input on until the player visibly moves 2 cm. **The "sluggish" number.** | ~40 ms (acceleration), plus up to one tick |
| `stop` | Move input off until speed visibly halves. | ~175 ms (deceleration) |
| `jump` | Jump pressed until the player visibly rises 5 cm. | about one tick |
| `snaps` / `maxSnapM` | Frames that moved faster than any legal motion: teleports, hard corrections, and the player's own 30 Hz steps while sprinting (the render frame is far shorter than a tick). Phase 4 visual smoothing removes the latter. | 0 |
| `reversals` | Direction flips while input is steady: prediction fighting a correction. | 0 |
| `idleDriftCmPerS` | Horizontal creep while no input for 0.5 s: sliding on the deck. In walking routes it also picks up the tail of a sprint stop and replays, so read it from the tilt test. | 0 |
| `avgRttMs` | Transport round trip on a client. UTP estimates it from reliable acks, so it goes stale when the client sends little reliable traffic (the humanoid input stream is unreliable). Trust the preset until this is ack-based. | ~2 × preset send delay |
| `prediction` (client) | `acks` received; `matches` (prediction within 2 cm); `corrections` (rewind and replay; `meanCorrectionM` is how far the player moved); `snaps` (teleport or large divergence); `ackLatencyMs` (input sent until the server confirms it applied it). | matches ≈ acks; snaps only on spawn or teleport |
| `serverInputs` (host) | Per remote player: inputs received and consumed, `starved` ticks (no input: the last one repeated), `skipped` (queue overflow), queue depth. | starved ≈ 0 after connect, skipped 0, depth ~1–2 |

Latency lines read `p50 / p95 / max (n=samples, timeouts)`. A timeout means no response within 2 s.

The host's own player is local, so its numbers are the floor. The client's numbers are what a remote player feels.

## Extending

- **New route:** add a `BotRoute.Builder` chain to `BotRoutes.cs` and list it in `All`.
- **New preset:** add it to `NetworkConditionPresets.cs`.
- **New metric:** extend `MovementResponseTracker` (pure, tested in `NetTestHarnessTests.cs`) and
  `ResponseSummary`.
