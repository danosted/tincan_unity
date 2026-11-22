Status: Draft (2026-11-22)

# Encounter zones: challenges that unfold in the world, entered and left by sailing

## Why
The peaceful world ([peaceful-mode.md](peaceful-mode.md)) is calm by default. Event mode is everything intense that
exists today: hazards, the cannon, damage and repair, gas squalls, skill checks. It should be something the crew sails
into, not something that happens to them. Like the events in an MMO such as Guild Wars 2, a challenge belongs to a
place in the world. You see it, you go in and it unfolds around you, and you can always leave by sailing out.

## Decisions (developer, 2026-11-22)
- Nothing in the peaceful world starts an event by itself.
- **Encounters are zones in the world, not separate instances.** A zone is a place with a boundary you can see. The
  event unfolds inside it, in the same world, with no scene change.
- **No prompt.** Sailing in is the choice. A cue tells everyone the event is about to start, and turning back before
  it ends means nothing starts.
- **You exit by leaving.** Sailing (or walking) out of the zone takes you out of the event.
- **The first zone is a storm whose epicenter you cure** ([storm-crossing.md](storm-crossing.md)). It is also the
  vertical slice for how clouds, light and sound make weather in the world. The voyage itself stays out of the game.

## The flow
1. **Seen from afar.** The zone reads as a different place at the edge of the calm sky. It goes on the chart once seen.
2. **The cue.** When the ship crosses the edge, every player gets the same warning: the zone's own build-up (for the
   storm, the light dims, the wind rises and thunder rolls), a HUD line with the zone's name, and a short countdown
   (`WarningSeconds`, about 8 s). If the ship turns back out before it ends, nothing starts.
3. **Inside.** The zone's event runs within its bounds. Its hazards spawn inside it, and out of the zone none of it
   reaches you.
4. **Leaving.**
   - When the ship sails out, the event ends as abandoned: no reward and no punishment. The zone rests, then can be
     entered again.
   - A player who falls or walks out while the ship stays inside is out of it, and the event goes on for the rest.
5. **Done.** On success or failure the zone rests (`RestSeconds`), shows that it is spent, and the calm comes back.
   Rewards come later (they fit the upgrade shape in `peaceful-mode.md`).

## What this means for the game's profile
The event features stay installed in `cheesed_scene`'s profile, but they only run when a zone asks. So peaceful
mode's P0 does not need a second profile: it turns the event features from "always on" into "on in a zone". What has
to change, to be checked feature by feature:
- **Event director:** `EventDirectorSettings.AutoStart` off. Zones start their event by id through the director.
- **Voyage:** out of the game profile. It starts by itself (`VoyageConfig.AutoStart`), and `VoyageUseCase.Begin`
  rolls a new layout seed, which would swap the persistent world's islands. The storm is the zone's own event, with
  no voyage in it.
- **Ambient spawners** (sky hazards, random breakage `ShipBreakageUseCase.AutoBreak`, boons, gates, skill checks, gas
  pockets): each becomes something an event turns on, and only inside the zone's bounds.
- **Cannon and damage and repair** stay as they are. They matter only when something attacks, which now happens in
  zones.

## Shape (feature `Encounters`, new assembly `TinCan.Features.Encounters`)
- `EncounterZoneNetworkMediator` on the zone object. It replicates the state (Idle, Warning, Running, Resting), the
  bounds and when the warning ends.
- `EncounterZoneProcessor` (pure) holds the rules: ship in or out, the warning running out, turning back, abandoning,
  success, failure, resting.
- `EncounterZoneUseCase` (server tick) watches the ship and the players against the bounds and starts and ends the
  zone's event through the director. It also says who is inside (`IEncounterZones`: "is this point in a running
  zone, and how deep in it?"), which is how the spawners and the storm's look keep to the zone.
- **The cue** is a gameplay cue (`gameplay-cues.md`) that the zone's event contributes, plus an event HUD line (the
  existing `EventHudPresenter`).
- **Placement:** in the first slice, one zone spawned by the server at a spot in `EncounterConfig`, within easy reach
  of the start in `cheesed_scene`. Later, a rare kind of layout cell from the world seed, the way landmasses are
  rolled.

## Slices
- **E0 Quiet by default.** Part of peaceful-mode P0: the director's auto-start off, the voyage out, and the ambient
  spawners off unless an event turns them on.
- **E1 The zone in the world.** The object, its bounds and their look (placeholder), the replicated state, the depth
  query, and the warning, turn-back, abandon and rest rules.
- **E2 The storm.** The first zone's event, its look and its sound, and curing it at its eye:
  [storm-crossing.md](storm-crossing.md).

## Progress
- **2026-11-22, E0 built.** `Features/Encounters/` with `EncounterPressureUseCase` in `Profile_Galleon`, in place of
  the voyage: on the server's first tick, every `ISessionParticipant` is set inactive (no auto-started events, no
  random breakage, no skill-check encounters, no gas fields). Tested in EditMode only: scripted runs already hold the
  same features off (`HarnessGameplayOverrides`), so a scenario cannot see it. E1 turns the pressure on while a zone
  runs.

## Tests
EditMode: the zone processor (enter, turn back during the warning, start, abandon, succeed, fail, rest), the use case
with a fake director, and the zone query. Scenario `EncounterZone` in a test-range scene, solo and host + client: the
ship enters and both peers see the warning; it turns back and nothing starts; it enters again and the event runs; it
sails out, the event is abandoned and the zone rests.

## Open questions (for the developer)
1. **Zones on foot:** is a zone always a region you sail into, or can it also be on an island (a cave, a ruin) that
   players walk into on foot?
2. **Failing:** when the ship's health runs out inside a zone, is the ship pushed out of the zone, repaired? Or does the
   damage stay until it is repaired by hand?
3. **After a win:** the zone rests for a while, or is gone for good in this world?
