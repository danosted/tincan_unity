# Design Discussion: Player Loadouts & Skills

**Status:** Brainstorm — nothing here is decided. This document distills a design discussion (Brams, Dan — 2026-08-27) into workable proposals and open questions. Treat every section as a proposal until explicitly locked.

---

## 1. Core model: roles from stations, not from characters

The working idea is that abilities come from the world, not from a chosen archetype:

- **Role-shifting:** interacting with a station (helm, workbench, medbay) or a piece of equipment grants an ability set. You *are* what you're standing at.
- **Specialization as unlockables:** instead of branching archetypes (Deckhand → Gunner/Engineer), specialized *crafted equipment and stations* unlock upgraded or additional abilities. Progression lives in the ship and its gear.

### Open questions

- **Per-player progression vs. shared world items** — determines whether specialization is a personal investment or a team resource. Unresolved.
- **Self-serve crafting vs. shared tech tree** — does the Deckhand craft their own upgrades, or does the team unlock a pool anyone can pull from? Unresolved.
- **Class commitment vs. loadout** — the biggest open tension in the discussion (see §5).

## 2. Vertical slice target

Proposed first slice: **encounter-based scripted challenges.**

- The team must survive/resolve a scripted challenge around the ship.
- Simplest starting point: fixed stations placed around the ship. One player pilots (ship movement and orientation); the rest crew the stations.
- The encounter design question is two-sided: what happens *around* the ship (the threat), and how does it stress the ship and crew (the tasks it generates)?
- Leaning toward something **Spaceteam-like** for the first version (see §3) — acknowledged as not easy, but the preferred direction.

## 3. Challenge design: design problems by solution type

Proposal: classify encounter problems by the *kind of interaction* that solves them, and build encounters as mixes of these:

| Solution type | Examples |
|---|---|
| Driving/steering | Steering the ship, operating the crane — same verb, different stations |
| FPS-adjacent | Firing cannons at a kraken, shooting a precise rope line to climb faster |
| Timing / rhythm | Pumping water is more effective in the right cadence (cf. Clair Obscur trigger timing) |
| Memory | Signal flags whose meanings are looked up somewhere other than where the flags are |
| Puzzle | Combining scattered hints to diagnose a non-obvious fault; wiring puzzles |
| Spatial planning | Cargo storage (see §4) |

### Information/action separation (Spaceteam principle)

Deliberately place the *information* needed to solve a problem away from the *place* it is solved, to generate communication (and productive chaos):

- Signal flag codebook in the captain's cabin; flags hoisted at the bow.
- "Engine low on oil" shows on the helmsman's screen; oil is filled down at the engine.

Both participants explicitly liked this direction — strongest point of agreement in the discussion.

### Difficulty levers

- **Cascading trade-offs:** venting boiler pressure prevents an explosion/fire but blinds the helmsman with steam; an unfixed leak makes the ship harder to maneuver.
- **Constant vs. interrupt tasks:** distinguish tasks that need continuous attention (steering) from tasks that *disrupt*.
- **Performance-scaled difficulty:** throw extra problems at crews doing well; don't pile complexity onto a crew still reading signals aloud to each other.
- **Inverse encounters:** stealth-style objectives — prep everything, set the course, then shut *everything* down to glide silently past a sea monster attracted by noise. Tension through inaction.

## 4. Cargo as a spatial system

Cargo storage as a planning challenge with physical consequences:

- Items don't weigh what they "take up": an empty barrel is light but bulky; a large chest can't be split.
- With multiple storage rooms, weight distribution affects handling: heavy load port-side makes the ship drift that way; weight low in the hull slows it down.
- Creates a logistics meta-task: knowing where spare cannonballs and fuel are, without dumping everything in one spot and unbalancing the ship.

This couples the "spatial planning" solution type directly to the pilot's "driving" problem — an example of the cross-role synergy the design wants.

## 5. Class / signature design

Proposal (DRG-inspired): everyone can do everything, but each player has a **signature** — a couple of passive bonuses plus one teamwork ability. No hard capability locks.

Sketched signatures (names in Danish, kept as proposed):

- **Navigator:** spyglass that pings objects into shared UI for the whole crew; better dark vision (below deck); hears problems from further away; pinged problems may resolve faster if answered quickly.
- **Bådsmand (Bosun):** regenerating stash of insta-fix hull patches; reads gauges at distance (fuel, engine pressure); possibly close-range "detective vision"; can hand out patches or deploy an auto-pump.
- **Gast (Deckhand):** fast traversal — swings across the ship, no fall damage; camera unaffected by ship sway; can lay ropes as shortcuts for others.
- **Våbenofficer (Weapons officer):** better accuracy, better personal gun (boardings), crosshair on cannons; can supply others with ammo.
- **Skibskok (Ship's cook):** faster revives; carries heavy things alone or two at a time; hands out coffee for temporary speed boosts.

**Design intent:** cross-class synergy — abilities should make you feel like you're amplifying *others'* specializations, not just your own play. (Flagged as a goal to check the brainstorm against, not yet verified against the list above.)

### Open tension: commit vs. loadout

Two models were floated and **not reconciled**:

1. **Commit to a class at start** — pick your signature before the encounter (the framing used in the sketches above).
2. **Loadout/equipment-driven spec** — you don't commit; your abilities come from what you equip. Equip a coffee pot *and* a spyglass and you're effectively dual-specced.

Model 2 fits the station/equipment-driven core in §1 more naturally; model 1 gives cleaner identity and easier balancing. This is the main fork to resolve before building the ability system — it dictates whether abilities hang off *players* or off *items*, which are different fundamental systems.

## 6. Process note

Agreed direction for design documentation: markdown files in the repo (git-tracked), under a `design-docs/` style folder — human-readable, AI-editable. This document is the first artifact of that.

---

## Consolidated open questions

1. Per-player progression vs. shared world items (§1)
2. Self-serve crafting vs. shared team tech tree (§1)
3. Class commitment vs. equipment loadout — where do abilities live, on players or items? (§5)
4. Scope of the first vertical slice: which solution types and how many stations? (§2, §3)
5. Do the sketched signatures actually deliver cross-class synergy, or do they need rework against that goal? (§5)
