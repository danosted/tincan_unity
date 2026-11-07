Status: Approved (2026-11-07)

# Hazards and boons: a turret cannon, roaming comets, boon gates, skill checks, crew chains

## Progress
- **C1 built (2026-11-07), awaiting its play run and a playtest.**
  - **Turning:** `CannonAimLimits.TurnsFully` (`YawLimit` 180): the yaw wraps into -180..180 in `ClampAim` and
    `Steer`. Proxies already blend with `LerpAngle`.
  - **Misfires:** the sweep skips only the firing cannon's own colliders (`ICannon.Base`) and humanoids.
    `SegmentHit` now carries the collider it hit, so `CannonFireUseCase` can tell a hit on its own ship (a collider
    under `ShipRoot`). The hull is not a target, so that hit applies the ordinary hit effect (`GE_CannonballHit`,
    -100) to `ICannon.ShipController`. A separate own-ship effect was built and then removed (developer, 2026-11-07:
    keep it simple unless strictly necessary). A misfire hurts as much as a hit on a hazard does.
  - **Scenarios:** `CannonShot` now aims to 190 and expects -170, on the gunner's aim and on the replicated barrel. A
    mouse nudge cannot do it: the sweep per frame depends on frame rate, and 0.1 s swept 228 deg in the first run, so
    the mouse wrap is an EditMode test. The new `CannonMisfire` fires inboard at our own deck. A new scenario needs its
    menu items in `DevTools/Editor/ScenarioMenu.cs`, or verify waits 240 s for a summary that never comes.
  - **First play run (2026-11-07):** `GalleonSternCannon` passed solo and host + client. `CannonShot` failed on the
    mouse nudge (fixed as above). `CannonMisfire` never started (no menu items; fixed).
  - **Rerun (2026-11-07):** `CannonShot` and `CannonMisfire` pass solo and host + client. The misfire reached the ship
    as an own-ship hit (`CannonballHitEvent` with `OwnShip=True`, health 1000 to 975 on every peer, with the separate
    -25 effect since removed). C1 still needs a human playtest.

## Why
The playtest on 2026-11-07 ([playtest-2026-09-30-tuning.md](playtest-2026-09-30-tuning.md), "Playtest") found two
problems:
- **The cannon looks like a turret but stops at ±105°.** That reads as a bug.
- **Sky hazards are annoying.** They are dull to shoot (slow, they home in, one hit kills). Someone has to man a
  cannon the whole time. They never stop coming, and they follow the ship around. The cause is the design (a field that
  keeps spawning around the ship and homes on it), not the numbers, so this plan replaces it instead of retuning it.

The developer's direction (2026-11-07):
- A full 360° turret. A misfired ball hits our own ship.
- Hazards as stray "comets" or similar projectiles that roam according to an entropy level. The vision is not fixed
  yet, so this part starts as a lab.
- An experiment: boon points that give the ship a boon when it is steered through them.
- Shooting hazards gives a boon too, through GAS, using the same mechanism.

## Current state (what each slice changes)
- Cannon swing: `CannonConfig.YawLimit` 105 (`Features/Weapons/Cannon/CannonConfig.cs`). `CannonAimProcessor` clamps
  to it, and the `CannonShot` scenario expects the aim to hold at 105.
- Own-ship hits: `CannonFireUseCase.Ignores` skips every collider under the firing ship and every humanoid, so a ball
  passes through our own hull.
- Hazards (`Features/SkyHazards/`): a field ahead of the bow that keeps itself full (`SkyHazardFieldProcessor`;
  `FieldMin/Max`, `MaxAlive`, `SpawnInterval`, crew scaling). Field hazards home on the ship at `DriftSpeed`
  (`HazardDriftProcessor`). Contact applies `GE_HazardImpact` to the ship and removes the hazard. Zones along the
  route, from the voyage seed and shown on the chart (`HazardZoneLayoutProcessor`, `SkyHazardChartSource`), make the
  field thicker. `ISkyHazardSpawner.SpawnAt` is used by scenarios and events.

## C1: a 360° turret whose misfires hit our own ship
- **Swing:** the yaw is no longer limited. `CannonAimLimits` gets an "unlimited" yaw (or `YawLimit` 180 is read as no
  clamp, with the angle wrapping instead of stopping at a limit). Elevation keeps its limits.
- **Own-ship hits:** the sweep stops skipping the firing ship. It still skips the firing cannon's own fixture (the
  ball starts inside it) and still skips humanoids (open question 1).
- **What the hit does:** a ball that hits our own hull applies the ordinary hit effect (`GE_CannonballHit`) to the
  ship. A misfire hurts as much as a hit on a hazard does; whether it should also open a leak is open question 2.
- **Visuals:** the barrel may pass through rigging or the other cannon when aimed inboard. That is accepted; fix it
  only if a playtest says it looks bad.
- **Tests:** `CannonAimProcessor` wraps the yaw with no limit; the sweep's ignore rule (own fixture skipped, own hull
  hit) moves into a small, testable rule.
- **Scenario:** `CannonShot` aims past 180 and expects the yaw to wrap. A new `CannonMisfire` aims at our own deck,
  fires, and expects ship health to drop. Both run solo and host + client in `Test_Cannon`.

## H0: stop the drip (deletion)
- Switch off the field around the ship and homing for good: remove `SkyHazardFieldProcessor`, `HazardDriftProcessor`,
  and the field and drift fields on `SkyHazardConfig`, along with their tests.
- Keep: the hazard entity, contact damage, one-hit kill, `SpawnAt` for scenarios and events, zones and the chart (H1
  may reuse them or replace them).
- Until H1 lands, a voyage has no hazards. That is an honest baseline to playtest boons against.

## H1: comets (lab first; the design is not decided)
Built in a lab scene first, with the numbers on a config asset, so the developer can try the feel before it goes
into a voyage.
- **A comet is a moving hazard on its own path.** It crosses the sky and does not home on the ship. It can hit the
  ship only if the ship is in its path, so the helm can dodge and the gunner can clear it.
- **Entropy drives the sky.** It is one server-side value per voyage (`ISkyEntropy`, replicated), 0 for calm and 1
  for wild. It decides how many comets are out, how fast they go, how much their paths curve, and how big they are.
  Where the value comes from is open question 3.
- **Paths are computed, not simulated.** Like the cannonball, a comet's path is a function of its start, velocity and
  spawn tick, so every peer draws it without per-tick replication. Contact runs on the server, as now.
- **Lab:** a scene with a slider for entropy, one ship, and a cannon. The developer judges the feel; the numbers go
  into the config.
- **Scenario:** `CometCrossing` spawns one comet on a known path that crosses the ship, and expects contact damage.

## B1: boon gates (an experiment)
- **A boon gate is a place in the sky.** It is a ring or a glowing patch that the ship has to steer through. When the
  ship's hull passes through it, the server applies the gate's GAS effect and the gate is used up (or recharges, set
  per gate).
- **A boon is data.** `BoonDefinition`: a `GameplayEffectDefinition`, who gets it (the ship, or every crew member
  aboard), a cue tag, and a name for the HUD. The first boons are open question 4.
- **Placement** comes from the voyage seed, like zones: off the straight route, so going for a boon is a steering
  choice. It respects `IWorldObstacleQuery` and shows on the chart (a new `ChartObstacleKind.Boon`).
- **Assembly:** `TinCan.Features.Boons`, its own installer and test assembly, plus a row in the CODE_MAP feature
  index.
- **Tests:** passing through a gate applies its effect exactly once; a used gate does nothing; placement is the same
  for the same seed.
- **Scenario:** `BoonGate` places a gate in front of the test ship, flies through it, and expects the effect on
  every peer.

## B2: shooting gives boons
- A hazard or comet can carry a `BoonDefinition`. When a cannonball destroys it, the server applies the boon (to the
  gunner, the ship, or the crew, as the definition says). It reuses B1's definition and grant path, with no second
  mechanism.
- Which comets carry boons, and which boons, depends on entropy (wilder sky, better rewards), once H1 has a feel.
- **Scenario:** `ShootBoon` spawns a comet that carries a boon with `SpawnAt`, shoots it, and expects the boon.

## B3: skill checks (short minigames that grade a boon)
The developer's idea (2026-11-07): skill-based minigames decide how good a boon is. A boon becomes a short burst of
skill, not a long chore.

### The framework (built once, with the first kind)
- **A check is data.** `SkillCheckDefinition`: its kind, difficulty, length, and which station runs it.
- **It gives a grade.** Miss, Good or Perfect. `BoonDefinition` gets one GAS effect per grade (Perfect: stronger or
  longer). A Miss gives nothing and costs nothing, so a check never becomes a chore.
- **Who gets it.** A boon gate (B1) or a boon comet (B2) opens a check for the player at the matching station: the
  helmsman for a gate, the gunner for a comet shot. A check with no one at the station is a Miss.
- **It runs on the player's own machine.** The check runs on that machine in its own input context (`SkillCheck`,
  [INPUT.md](../INPUT.md); it never reads a key directly). The machine sends its grade to the server, which applies the
  boon. Co-op trusts the client's grade; the server checks only that a check was open for that player and that the
  grade arrived within its time.
- **Each kind is a pure processor plus a view,** like the fuel minigame (`Features/Airship/Fuel/Minigame/`). The
  processor takes inputs and time and returns progress and a grade, and has EditMode tests. The view only draws.
- **Assembly:** `TinCan.Features.SkillChecks` (the framework and its kinds). Boons references it, never the other way
  round.
- **Scenario:** `SkillCheckGrade` opens a check, the scenario drives its inputs for a Perfect and then for a Miss,
  and expects the matching boon (or none) on every peer.

### The kinds
Each kind is its own small slice. The first one is built together with the framework.
1. **Pulsing gauge** (first, with the framework). Press while the needle is in the band. The band's width and the
   pulse speed set the difficulty.
2. **Button combo.** Three to five directions (left, right, up, down) in order, against the clock. A wrong press
   restarts the combo instead of failing it.
3. **Timed press, parry** (Clair Obscur inspired). A comet about to hit the ship opens a "brace" check for the crew
   near where it will hit. Perfect deflects it, Good halves the damage, Miss takes the full hit. This links H1's
   comets to skill checks and makes a hit the crew's fault instead of random. Needs H1.
4. **Skill shots.** At the cannon: hit moving targets, or keep the crosshair on a moving target while the shot
   charges, or fire on a pulsing hit gauge. This is how you shoot a boon comet (B2) well.
5. **Pressure valve.** Keep a drifting needle inside a band for a few seconds. Fits gas and fuel boons.
6. **Signal lamp.** A lamp flashes a sequence, and you repeat it. The navigator's check.
7. **Radio or compass tuning.** Sweep a dial until the sound locks onto the signal. You play it by ear, not by eye
   (it needs the audio mixer, [audio-mixer.md](audio-mixer.md)).
8. **Crank rhythm.** Crank in time with a pulse. Fits a speed boon.
9. **Two-player sync.** Two players press within a short window of each other, or one holds the aim while the other
   fires. It needs two crew members, so it is co-op only: with one player aboard it is never chosen.
10. **Catching boon orbs.** A gate bursts into orbs that the crew catches with the net. It reuses the jerry can catch
    (`FlyingCanSpawningService`, `NetCatchUseCase`). It is graded by how many orbs are caught.

## B4: challenges (orchestrating checks) and crew chains
The developer's idea (2026-11-07): put checks together in different ways, and chain them between crew members.

### Challenges: checks put together as data
- **A challenge is a small tree of steps** (`SkillChallengeDefinition`). Whatever starts a boon (a gate, a comet, an
  event) starts a challenge. A single check is the simplest challenge, so B3 becomes a challenge with one step.
- **Ways to put steps together:**
  - **Sequence:** steps one after another.
  - **Parallel:** several checks at once, at different stations (the helm holds the valve while the gunner tracks).
  - **Relay:** each step goes to a different crew member than the step before. This is where chains come from.
  - **Branch:** the next step depends on the last grade (a Perfect opens a harder bonus step, a Miss ends it).
  - **Escalate:** the same check repeats, harder each time, until a Miss. The grade is how far the crew got.
- **Who gets a step** is a rule on the step: a station, the player nearest a point, any crew member, or "not the last
  player". A step whose rule finds no one is skipped (with one player aboard, a relay falls back to the same player).
- **The challenge's grade** comes from its steps by a rule on the challenge: the worst step, the average, or the
  number of Perfects. The challenge's `BoonDefinition` is granted from that grade.
- **The server runs challenges** (`SkillChallengeProcessor`, a pure step machine with EditMode tests, and a use case
  that opens each step's check on the right player through B3's path). Checks themselves still run on the player's own
  machine.
- **Designed events can start a challenge** with a new action (`StartChallenge`), so an event can, for example, open
  a parry relay during a comet storm. Challenges are not built *as* designed events (developer, 2026-11-07: not
  convinced). Whether they ever merge stays open.

### Authoring: one data model, the source left open
The developer has no fixed vision yet for how challenges are made: by hand as ScriptableObject configs, generated by
procedure, or generated by an agent and stored (the developer leans this way, with a worry that it could become
bloated). So the plan fixes only the data model and keeps where it comes from open:
- **The step tree is plain serializable data** (`SkillChallengeSpec`: steps, combinators, rules, the grade rule),
  independent of where it comes from. The runner takes only a spec.
- **A source produces specs** (`ISkillChallengeSource`). The first one is hand-made `SkillChallengeDefinition`
  assets that wrap a spec, two or three of them, enough to playtest. Other sources plug in later without touching the
  runner:
  - **Procedural:** built from a seed and a budget (number of steps, combinators allowed, difficulty from entropy).
    Only the generator's parameters are stored.
  - **Agent-written:** an agent writes specs as text files (JSON) into a catalog folder, and they are loaded like
    assets.
- **Guarding against bloat**, whatever the source:
  - The grammar stays small: the five combinators, the step rules and the grade rules, and nothing that runs code.
  - A validator (an EditMode test over the whole catalog) fails on an invalid spec, on one that cannot finish with a
    single player, and on duplicates.
  - The catalog has a size the developer sets: adding a challenge past it means removing one.
- **The source is decided after a playtest** of the hand-made challenges (open question 9).

### Crew chains
- **A chain is crew-wide.** When a crew member finishes a check with Good or Perfect, a chain window opens. If
  another check finishes inside it, the chain may grow and the window opens again. A Miss, or a window that runs out,
  ends the chain.
- **What grows a chain is left open for exploration and game feel** (developer, 2026-11-07). The rules are settings
  on a `CrewChainRules` asset, not code, so a playtest can change them:
  - the window's length;
  - whether the same player can grow the chain or only a different one;
  - whether any check counts or only checks in the same challenge;
  - whether a Good grows the chain or only a Perfect;
  - how much strength each extra link adds.
  The pure chain processor takes these rules as input, and its tests cover each setting. A lab scene with these
  settings live lets the developer try them; the values that feel right go into the asset.
- **What a chain gives:**
  - Boons get stronger as the chain grows.
  - Using different kinds of check in one chain gives a bonus.
  - At set lengths (for example 3 and 5) a **finisher** boon is granted: a special boon only chains can earn.
- **Strength through GAS: a core change (developer, 2026-11-07).** Effects have fixed modifiers today
  (`GameplayEffectDefinition` has no level or scale). `ApplyEffect` takes an optional magnitude scale (default 1) that
  multiplies the effect's modifiers, and optionally its duration. It is kept on the active effect, so it is removed
  correctly. Any feature can use it, not just chains. It is a slice of its own before chains (**G1**), with
  `AbilitySystemUseCase` tests, and a note in [ARCHITECTURE.md](../ARCHITECTURE.md)'s GAS section.
- **The state is on the server and replicated:** chain length, the window's end, and who extended it last. The HUD
  shows it to everyone, so a crew member can see when to jump in.
- **Challenges and chains work together:** a relay step that finishes inside a chain window grows the chain, so a
  designed relay is the easiest way to build a long chain.
- **Assembly:** challenges and chains live in `TinCan.Features.SkillChecks` with the checks, since they are the same
  system. Split them out only if it grows too big.
- **Tests:** the step machine (sequence, parallel, relay, branch, escalate; grade rules; skipped steps); the chain
  rules (grows only on a different player, ends on a Miss or a timeout, finishers at their lengths).
- **Scenario:** `CrewRelay`, host and client. A relay challenge gives the first step to the host and the second to the
  client. Both are driven to a Perfect. The scenario expects chain length 2 and the tier 2 boon on both peers. (Solo,
  the relay falls back to the same player and the chain does not grow.)

## Order
C1 → H0 → B1 → B3 (framework and pulsing gauge, then the combo) → B4 challenges (hand-made source) → G1 (GAS
magnitude scale) → B4 chains (with their lab) → H1 (lab, then into the voyage) → B2 → B3 parry and skill shots → the other B3 kinds, one at a time, in whatever order playtests ask for.
C1 and H0 are clear and small. B1, the first checks and B4 do not depend on comets, so boons, challenges and chains can
be playtested early. B2, the parry and the skill shots need H1.

## Open questions (for the developer)
1. **Can a misfire hit crew?** Today balls pass through humanoids. Player vitality is planned
   ([player-vitality.md](player-vitality.md), option B: knocked down, never killed); a crew hit could knock someone
   down once it exists.
2. **Should a misfire also open a leak?** Today it takes ship health (the ordinary hit effect). It could also open a
   leak socket (`IShipBreakage`), which gives the crew a repair job.
3. **Where does entropy come from?** Choose between a route profile (calm start, wild middle, calm approach), a value
   that rises with time aloft, one driven by the crew's state (leaks, fuel), set by events (`DesignedEvents`), or a
   mix of these.
4. **First boons.** Candidates: a burst of speed, a hull repair, gas or fuel, a faster cannon reload for a while.
5. **Zones:** do comets keep the hazard zones (thick regions on the chart), or does entropy replace zones entirely?
6. **Does every boon go through a skill check?** Either every gate and boon comet opens a check, or some boons are
   free (fly through, get it) and only the better ones ask for skill.
7. **What happens to the player during a check?** Choose between: the check takes over the station's input (the
   helm holds its course, the cannon holds its aim), or it shows over normal play. The first is safer; the second is
   harder.
8. **Does entropy make checks harder?** For example, a narrower band and faster pulses in a wild sky, to match the
   better rewards there.
9. **Where do challenges come from?** Hand-made assets, procedural from a seed, written by an agent and stored, or a
   mix. Decided after the hand-made ones are playtested. If an agent writes them: is that at development time (files
   committed and reviewed like any asset) or while the game runs? The plan assumes development time; generating them
   while the game runs would be a much larger plan.
10. ~~Chain strength~~ Decided 2026-11-07: a core GAS magnitude scale (G1).
11. **Chain rules:** left open for exploration (developer, 2026-11-07). They are settings on `CrewChainRules`, tried in
    the chain lab.
