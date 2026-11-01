Status: Approved (2026-10-31: try option B); fifth in [core-systems-roadmap.md](core-systems-roadmap.md)

# Player vitality: can the world hurt a crew member?

## Why
Every danger in TinCan hits the ship: hazards lower its health, gas pockets break its fittings, breaches leak its fuel.
A person can only fall overboard. That may be the right game (the ship is the character), but it is undecided, and
several plans would change with the answer: gas pockets near a player, hazards that strike the deck, cannon recoil,
the overboard flow.

## Today (verified 2026-10-31)
- Players have no health attribute in use. `Attr_Health` and `Attr_MaxHealth` (with `HealthAttributeSet`) live on the
  ship and on damage points. The player is a GAS actor (`AbilityNetworkMediator`), so effects, tags and attributes
  already work on it.
- Respawn (`Features/Respawn/`) is "one place for what a respawn does": `RespawnCause`, `RespawnRules`,
  `CarriedItemOutcome` (what happens to what you hold), the leash and winch.
- Input contexts can be switched off by conditions (`IInputContextConditions`), stations can be left
  (`StationOccupancyUseCase`), items can be dropped.

## Options
**A. No personal harm (keep today).** The ship takes every hit; the crew's only personal risk is falling. Cheapest, and
keeps the game about the ship. Write it down as a design rule so plans stop circling it.

**B. Knocked down, never killed (recommended to try).** A blast close to a player (a gas pocket going off, a hazard
striking the deck, standing behind a firing cannon) knocks them down: they drop what they hold, leave their station
and cannot act for a few seconds. A crewmate can help them up sooner (an interaction). No death, no respawn timer, but
an incident costs the crew time and makes a second player useful.

**C. Health, downed, revive.** Players have health; at zero they are downed, a crewmate revives them, and if nobody
does they respawn through Respawn's rules (a new `RespawnCause.Downed`). The most systems, and it pulls the game towards
combat.

## What B would add
- **Core: an "incapacitated" rule.** One tag (`State.Incapacitated`) that every system respects: input contexts turn
  off the humanoid's actions (through `IInputContextConditions`), stations release the occupant, held items drop
  through the same path as `CarriedItemOutcome`, possession is kept. That rule is the core piece; it is also what C
  would need.
- **GAS:** `GE_KnockedDown` (duration effect granting the tag and a cue: a camera tilt and a sound), applied by the
  server.
- **Sources (features):** `GasPocket` bursts and sky hazard deck strikes apply it within a radius; tuned in their
  configs.
- **Help up:** `HelpUpInteractionHandler` in the Respawn feature (it already owns "what happens to a crew member"),
  removing the effect.
- Tests: the rule (each system reacts to the tag), the handler, the sources' radius check. Scenario
  `KnockdownHelpUp`: a gas pocket goes off next to the client's player; the host helps them up.

## Decisions (developer, 2026-10-31)
1. **Try B** (knocked down, never killed), behind a feature installer so a playtest can switch it off. The
   incapacitated rule is the core piece and would also serve C.
2. **Gas pockets are the only source at first** (they exist and are on the chart); hazards come after a playtest.

## Verification (for B)
- EditMode suite green, plus the new tests.
- `verify.ps1 -Scenario KnockdownHelpUp`, and `GasSquallEvent` again, solo and host + client (asked for first).
- Human: does being knocked down feel fair and readable, or just annoying?
