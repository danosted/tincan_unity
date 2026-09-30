Status: Done (2026-09-30; built unattended in one pass, awaiting the developer's review and playtest)

# Input contexts, routed commands and rebinding

The model and how-tos are in [`../INPUT.md`](../INPUT.md). What listens to what right now is in the generated
[`../INPUT_MAP.md`](../INPUT_MAP.md). This file records why the model exists and what was built.

## Why

Input was one hand-written service (`UnityInputService`) mapping `ActionNames` strings to hardcoded keys, and about
eight classes polled it directly:
- Esc had three independent readers.
- The only context mechanism was a global `InputGate` bool.
- There was no binding data, so there could be no Controls menu.
- A station had no way to own the mouse. The gunner's body turned with the aim, and the cannon's yaw wrapped at 180°
  (`first-voyage.md`, parked until this plan).

## Decisions (developer, 2026-09-29)
- **Backend:** a Unity `InputActionAsset`. Game code never sees it; typed `InputActionId` assets name the actions.
- **Listeners:** continuous input is read per context into the predicted state. Discrete input is routed to one
  handler per command, and the highest live context consumes the press.
- **Scope:** the foundation plus the cannon's Gunner context, and a simple Controls menu.
- **Devices:** keyboard and mouse now. A gamepad later is a control scheme, with no code change.

## What was built (phases)
1. **Parity.**
   - `Assets/Input/TinCanControls.inputactions`, with maps Global, Camera, Humanoid, Airship, Gunner, FreeCamera and
     DevTools.
   - One `InputActionId` per action.
   - `IInputReader` (`InputSystemReader`) replaces `IInputService`; `ActionNames`, `InputGate`, `UnityInputService` and
     `PossessionInputController` are deleted.
   - Consumers take their typed context in the constructor.
   - Scripted input is keyed by action. Bots and scenarios use `ScriptedAction` intents through `ScriptedActionMap`.
2. **Contexts and router.**
   - `InputContextUseCase` evaluates conditions every frame (possession kind, a tag on the possessed actor, menu open,
     rebinding) and resolves blocking (`InputContextProcessor`).
   - `InputRoutingUseCase` dispatches routes to `InputCommandHandler<T>`s: menu back, open menu, exit vehicle, switch
     possession, toggle cursor, toggle net overlay.
   - Features contribute contexts via `FeatureInstaller.IExtension<InputContext>`.
3. **GAS.** `GameplayInput` lists the actions that press it, so Primary on foot and Fire at a cannon share a bit.
   `InputBindingConfig` is deleted; the bit order is `InputConfig.GameplayInputs` (Sprint 0, Primary 1, Interact 2,
   unchanged).
4. **Gunner.**
   - `Context_Gunner` is live on `State.Occupying.Cannon` and blocks Humanoid and Camera.
   - `GunnerAimUseCase` steers a clamped aim from `Gunner/Aim` and writes `HumanoidInputState.StationAim` through
     `IHumanoidInputContributor`.
   - The server clamps again (`CannonAimProcessor.ClampAim`).
   - The presenter shows the local aim.
   - `CannonShot` now checks that the aim holds at the yaw limit and that the body does not turn.
5. **Rebinding and Controls menu.**
   - `InputRebindingUseCase` (`IInputBindings`) runs interactive rebinding under the Rebinding context.
   - A key is refused when an action that can be live at the same time already uses it
     (`InputBindingConflictProcessor`).
   - Overrides are saved to PlayerPrefs.
   - `Menu_Controls` shows one row per key (the `MenuItemKind.Bindings` expansion), plus Reset to Defaults. It is
     linked from `Menu_Main`.
6. **Guards and docs.**
   - `ArchitectureRulesTests.Input_IsReadOnlyThroughContexts`.
   - `InputMapTests`: the map is current and every route has a handler.
   - `INPUT.md`, and updates to CODE_MAP, ARCHITECTURE, UI_FRAMEWORK, TASK_GUIDES, TUTORIAL and NETWORK_TEST_HARNESS.
   - Data is built by **TinCan > Dev > Input > Build Assets** (`DevTools/Editor/InputAssetBuilder.cs`); the map is
     written by **Write Input Map**.

## Deviations from the approved draft
- The runtime lives in a new core assembly, `TinCan.Input` (`Core/Input/`), not in `Core/Infrastructure`. That keeps
  one assembly as the only Input System user. Contracts are in `Core/Domain/Input/`.
- Instead of one typed snapshot reader per context, each context subclass exposes typed action slots, and systems read
  them through the single `IInputReader`. This has the same effect with less code.
- `IControllable.IsControlsEnabled` still guards the humanoid gather. It is about a parked body, not input.
- Scripted input takes `ScriptedAction` intents rather than raw action ids. The Pilot routes steer the ship with ship
  intents instead of "Jump/Sprint at the helm".

## Follow-ups (not done)
- Gamepad scheme and bindings, and keyboard/gamepad navigation of menus.
- The helm (`plans/helm-station.md`) could become a station context like the Gunner if possession is dropped.
- Mouse sensitivity and invert-Y settings (a natural next row in Controls).
