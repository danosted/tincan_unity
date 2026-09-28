Status: Done (2026-09-28; approved same day, developer: "make a POC with unity behavior")

# Spike: Unity Behavior as the authoring and live-debug layer for designed events

## Question
Can encounters be authored as Unity Behavior graphs (parallel lanes, shared blackboard) that run only on the server,
while the live graph view shows what is running? Context: `designed-events.md` (the runtime model is still open).

## Scope (throwaway, separate folder)
- Install `com.unity.behavior` (the version the Package Manager offers for 6000.4).
- `Assets/Spikes/BehaviorEvents/`: two custom nodes that call existing game code (`IShipBreakage.TryBreak` to break a
  part; a wait until the ship has no broken parts), a log/announce node, and a "Hull stress" graph on a world object
  that runs only on the host.
- Nothing outside the spike folder changes except `Packages/manifest.json`. Not committed without the developer's OK.

## What to measure
1. Live view: does the graph editor highlight the running node while hosting (and with a client connected)?
2. Server only: can the agent be kept from running on clients?
3. Builds: does the package add a build blocker like Visual Scripting's AOT stubs?
4. Diff: how readable is the graph asset in git?
5. Authoring from code: can a graph be created or edited without the UI (matters for AI-assisted authoring)?

## Result
Run 2026-09-28 with `com.unity.behavior` 1.0.16 (registry latest; the 6000.4 Editor bundles no version).

**What was built** (`Assets/Spikes/BehaviorEvents/`): `HullStressNodes.cs` (Break Ship Part, Wait Until Ship
Repaired, Announce), `HullStressSpikeHost.cs` (an `IInjectedView` next to a disabled agent; enables it only when
`INetworkService.IsServer` and turns off `AutoBreak`), `Editor/HullStressGraphBuilder.cs` (menu
`TinCan/Spikes/Build Hull Stress Graph`), the generated `HullStress.asset`, and `Spike_HullStress.unity` (a copy of
`Test_ShipDamage` with one "HullStressDirector (spike)" object). The graph: Start → Run In Parallel with two lanes.
Lane 1 announces, breaks part 0, then part `SecondPart` (a blackboard int), waits until the ship is repaired, then
announces again. Lane 2 is a timed "creak" track.

1. **Live view: yes, works.** With the director selected, the graph window switches to "Debug (HullStressDirector)".
   Finished nodes get a check and the running node ("Wait until the ship has no broken parts") gets a spinner, in both
   lanes. The same happened while hosting with a client connected (debugging is on the host's agent).
2. **Server only: yes, but through our own gate.** With NGO installed, `BehaviorGraphAgent` compiles as a
   **`NetworkBehaviour`** (a `versionDefines` in the package), and its only netcode option is "Run only on Owner".
   The spike keeps the agent disabled and enables it on the server. Host + client result: host agent enabled and
   running, client agent disabled, and the client saw parts 0 and 1 broken through normal ship-damage replication.
   Consequences: it breaks our `*NetworkMediator` naming rule (we cannot rename a package class). An agent on a
   networked prefab changes that prefab's NetworkBehaviour list. It ticks on Unity `Update` with `Time.deltaTime`, not
   on our simulation tick, so waits are frame time, not ticks.
3. **Builds: no Visual Scripting-style blocker found (not verified with a real build; builds are already blocked).**
   The IL2CPP generic-type stubs and link.xml are generated into `Library/`, not `Assets/`. Watch out: a build
   preprocessor **fails the build** if any graph references a node type that no longer resolves (a renamed or moved
   node class).
4. **Diff: poor.** One `.asset` holds the authoring model, a runtime copy, two blackboards and debug info: 1318 lines
   for 11 actions. Node values sit behind `rid` indirection, and every node is typed by
   `{class, ns, asm}` strings (`asm: Assembly-CSharp`). Renaming a node class or moving it into an asmdef breaks
   graphs silently, the same trap as `InteractionDefinition`. Reviewing a graph change in git is impractical; review
   happens in the graph window.
5. **Authoring from code: yes, via package internals.** `BehaviorAuthoringGraph`, `NodeRegistry`, `CreateNode`,
   `AddNodeToSequence` and `SetField` are `internal`, but the package grants `InternalsVisibleTo("Assembly-CSharp-Editor")`,
   so an editor script with no asmdef can build a full graph (nodes, lanes, literal values, blackboard links) and the
   editor lays it out readably. It is unsupported API and can break on any package update. Editing an existing graph
   by hand (in the YAML) is not realistic.

**Other findings**
- The Start node defaults to **Repeat**: after "the ship holds" the event started over. Designed events must turn
  that off.
- Nodes are not injected. They reach game services through a component on the agent (`GetComponent`), which is a
  service-locator seam we would need to design (e.g. one injected "event context" component).
- Entering Play in a scene that is not in Build Settings pops NGO's modal "Add Scene to Scenes in Build" dialog in
  host and clone. It blocks every `unity cmd` call (this is most likely what stalled the first attempt). A spike or
  test scene outside the build list needs "No - Continue" on each run.
- The install changed `ProjectSettings/EditorBuildSettings.asset` (the `com.unity.dt.app-ui` config object) and
  restarted the MPPM clone, which then re-imported the package.
- Visual Scripting warns "189 node options failed to load" after the install (unrelated to Behavior nodes; looks
  like it needs a node regeneration).

**Verdict:** good for the *live debug view* and for designers composing parallel lanes. As the runtime for designed
events it conflicts with the plan's model: frame time instead of ticks, no replicated state (clients get only side
effects, late joiners get nothing about the event), a NetworkBehaviour we cannot name or place freely, and assets we
cannot review in git. If kept, use it as a server-only authoring and debug shell whose nodes call the
`EventDirectorUseCase` / processors from `designed-events.md`, with event state still replicated by our own mediator.

**Decision (developer, 2026-09-28):** not adopted. The spike was removed (`Assets/Spikes/` deleted, the package
and the app-ui build-settings line reverted); this file is the record. Designed events use a code builder instead
(see `designed-events.md`).
