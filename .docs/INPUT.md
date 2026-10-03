# Input

How player input works: where keys are defined, what decides who hears them, and how to add an action, a context or a
command. The generated [INPUT_MAP.md](INPUT_MAP.md) lists every context, action, key, route and handler as they are
now. Plan and rationale: [plans/input-contexts.md](plans/input-contexts.md).

## The model in one paragraph

Keys live in one Unity Input System asset. Game code never names a key or an action string. It refers to **actions**
through typed assets (`InputActionId`). Actions are grouped into **contexts**. A context turns on while its condition
holds: you possess your body, a menu is open, or your body carries `State.Occupying.Cannon`. While it is on, it may
**silence** other contexts. Only the actions of live, unsilenced contexts listen; every other action reads as released.
Systems then use the actions in one of two ways:

- **Read them every frame** through `IInputReader`, via the typed slots of the context they own. Movement, look and
  cannon aim work this way.
- **Route them to a command** that one handler carries out. Cancel and switching possession work this way.

## The pieces

| Piece | What it is | Where |
|---|---|---|
| Actions asset | Maps, actions and default keys (`Keyboard&Mouse` scheme). Edited in Unity's Input Actions editor. | `Assets/Input/TinCanControls.inputactions` |
| `InputActionId` | One asset per action, pointing at it by id. Carries the display name, meaning and whether the Controls menu lists it. | `Assets/Input/Actions/`, `Core/Domain/Input/InputActionId.cs` |
| `InputContext` | When it is live (`Activation`), its `Priority`, what it silences (`Blocks`, `BlocksAllLower`), its actions and its `Routes`. A subclass adds typed slots for the system that reads it. | `Assets/Input/Contexts/`, `Core/Domain/Input/InputContext.cs` |
| Context subclasses | `GlobalInputContext` and `CameraInputContext` (`Core/Domain/Input/`), `HumanoidInputContext` (`Core/Humanoid/`), `HelmsmanInputContext` (`Features/Helm/`), `GunnerInputContext` (`Features/Weapons/Cannon/`), `FreeCameraInputContext` (`Features/FreeCamera/`). | next to the system that reads them |
| `InputCommand` + handler | One command type per meaning (`MenuBackCommand`). Exactly one `InputCommandHandler<T>` carries it out; returning false passes the press to the next context down. | commands in `Assets/Input/Commands/`, both classes next to the system they drive |
| `GameplayInput` | An ability input: one bit of the predicted `HumanoidInputState`. It lists the actions that press it, so Fire at a cannon and Primary on foot are the same bit. Bit order is `InputConfig.GameplayInputs`. | `Assets/Abilities/Inputs/`, `Core/Domain/Abilities/Inputs/GameplayInput.cs` |
| `InputConfig` | The root: the actions asset, every action id, the core contexts, the ability inputs in bit order. | `Assets/Input/InputConfig.asset`, `Core/Input/InputConfig.cs` |

The runtime is the core system `TinCan.Input` (`Core/Input/`, installed by `InputFeatureInstaller`). It is the only
assembly that references Unity's Input System:

- **`InputContextUseCase`** runs every frame. It evaluates each context's condition (`InputContextConditions`),
  resolves silencing (`InputContextProcessor`), and switches the live actions on and all others off. Nothing pushes or
  pops contexts.
- **`InputSystemReader`** implements `IInputReader`. It reads a private copy of the actions asset, merges `ScriptedInput`,
  and builds the ability-input bits.
- **`InputRoutingUseCase`** runs every frame. For each pressed action it walks the live contexts from the top. The first
  route whose handler accepts the command consumes the press, so Esc in a submenu steps back and does not also open
  the main menu.
- **`InputRebindingUseCase`** implements `IInputBindings`, the Controls menu's model. It loads the saved overrides
  (`PlayerPrefsInputBindingStore`), runs interactive rebinding under the Rebinding context, refuses a key that a
  simultaneously live action already uses (`InputBindingConflictProcessor`), and saves.

Features contribute their own contexts through `FeatureInstaller.IExtension<InputContext>`, so an unloaded feature's
context never runs. Examples: `CannonFeatureInstaller`, `HelmFeatureInstaller`, `FreeCameraFeatureInstaller`, `NetTestHarnessFeatureInstaller`.

## Contexts today

See [INPUT_MAP.md](INPUT_MAP.md) for the full table. In short, from the top:

1. **Rebinding** silences everything while the Controls menu waits for a key.
2. **Menu** silences everything below it; Cancel steps back.
3. **DevTools** (only while the harness runs): F3 toggles the readout.
4. **Gunner** (the `State.Occupying.Cannon` tag) silences Humanoid and Camera. The mouse swings the barrel, the body holds
   still, and the aim reaches the server as `HumanoidInputState.StationAim` (`GunnerAimUseCase`).
   **Helmsman** (the `State.Occupying.Helm` tag, same priority; the two never hold together) silences Humanoid but not
   Camera, so the helmsman looks around while steering. Throttle, turn and pitch reach the server as
   `HumanoidInputState.StationAxes` (`HelmInputUseCase`); Leave (E) is the Interact bit.
5. **FreeCamera** (possessing anything but your body).
6. **Humanoid** (possessing your body): move, jump, sprint and the ability inputs.
7. **Camera**: mouse look for whatever you control.
8. **Global**: Cancel opens the main menu when nothing above wanted it; Tab switches control.

## How to

**Read an action in a system.**

1. Add a slot to the context subclass the system owns, or add a subclass next to the system.
2. Take `IInputReader` and that context in the constructor.
3. Read `ReadVector2(context.Move)`, `IsPressed(context.Jump)` and so on.

Do not check menus or possession yourself; the context already does.

**Add an action.**

1. Add it to `TinCanControls.inputactions` in the Input Actions editor, with its default key in the `Keyboard&Mouse`
   group.
2. Add its display name and meaning to `InputAssetBuilder.Meanings` (`DevTools/Editor/`).
3. Point a context at it in the builder.
4. Run **TinCan > Dev > Input > Build Assets**, then **Write Input Map**.

**Add a discrete command** (a key that *does* something, rather than a state that is read):

1. Add an `InputCommand` subclass and an `InputCommandHandler<T>` next to the system it drives.
2. Register the handler `.As<IInputCommandHandler>()` in that system's installer.
3. Add a route (action to command) to the context in `InputAssetBuilder`, then rebuild.

The handler returns false when the command does not apply, so a lower context can take the press.

**Add a context.**

1. Create it in `InputAssetBuilder`, choosing its condition, priority and what it silences.
2. If it belongs to a feature, have the feature's installer implement `IExtension<InputContext>`, serialize the context
   and register it with `RegisterInstance`.
3. Otherwise add it to `InputConfig.Contexts`.

For a station that takes over the controls, see the Gunner: a tag condition, `Blocks` Humanoid and Camera, and an
`IHumanoidInputContributor` that writes the station's part of the predicted input.

**Make a key an ability input.** Add the action to the `GameplayInput`'s actions (in the builder). A new ability input
is appended to `InputConfig.GameplayInputs`, never inserted, so existing bits keep their numbers.

**Drive input from a bot or scenario.** Use `ScriptedAction` intents (`DevTools/ScriptedAction.cs`), for example
`.Hold(0.3f, ScriptedAction.GunnerFire)`. `ScriptedActionMap` turns each intent into a context's action and value.
Scripted presses obey contexts exactly like keys: `GunnerFire` does nothing unless you man a cannon.

## Rules (checked by tests)

- Only `TinCan.Input` (plus the asset builder and the tests) references Unity's Input System. No other script reads
  `Keyboard.current`, `Mouse.current` or `UnityEngine.Input`, looks an action up by name, or writes a binding path
  (`ArchitectureRulesTests.Input_IsReadOnlyThroughContexts`).
- Every routed command has a handler, and `INPUT_MAP.md` matches the assets (`InputMapTests`).

## Controls menu

Main menu > **Controls** (`Assets/UI/Menus/Menu_Controls.asset`). Its `Bindings` row expands into one row per
rebindable key; composite parts such as Move Forward count separately. Clicking a row waits for a key, and Esc
cancels. A refused key shows why below the list. **Reset to Defaults** drops every override. The overrides are saved in
PlayerPrefs (`TinCan.InputBindings`) and loaded at start. A gamepad later means adding a control scheme and bindings to
the asset, with no code change; the menu lists `Keyboard&Mouse` bindings today.
