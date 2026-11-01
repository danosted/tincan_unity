# Menu & HUD Framework

**Status:** Prototype (slice 0 of the ship-tasks prototype track). The *headless model* is the part meant to last; the UI Toolkit views are deliberately throwaway.

The framework separates three things so they can evolve independently:

| Layer | Lives in | What it is |
|---|---|---|
| **Data** | `Assets/UI/Menus/*.asset` (`MenuDefinition`) | Which menus exist and which rows they contain. Authored in the Inspector, no code. |
| **Headless model** | `Assets/Scripts/Core/UI/` (`TinCan.UI`) | `IMenuSystem` (menu requests + values), `IMenuCommand` (what a row does), `IHudValues` (named HUD texts and meters), `MainMenuBootstrap` (opens and closes the main menu with the session), the Cancel handlers, `ICursorPolicy` (the cursor). Pure C#, unit-tested, knows nothing about rendering. |
| **Views** | `Assets/Scripts/App/Views/` (`TinCan.App`) | `MenuOverlayView`, `HudOverlayView`: UI Toolkit code-built renderers of the model, drawn into `IUiLayers`. Replace these when real UI arrives; nothing else changes. |

Gameplay code never talks to a view. It asks for menus through `IMenuSystem`, reacts to rows through `IMenuCommand`, and shows numbers through `IHudValues`.

## Where things are wired

- `Assets/Resources/Installers/UiFeatureInstaller.asset` (a `FeatureInstaller`, see `FEATURE_INSTALLERS.md`) registers the model, the commands, the bootstrap, the layers and the cursor policy. Its `Panel Settings` field (`Assets/Settings/UI/DefaultPanelSettings.asset`) is the panel every layer draws on. The overlay views implement `IInjectedView`, so the lifetime scope injects them at container build. The `GameLifetimeScope` prefab carries two children, `MenuOverlay` and `HudOverlay`, each with the matching view component (no `UIDocument`: the layers own the only one). Every scene that contains that prefab gets the UI for free.
- The installer's `Main Menu` field points at `Assets/UI/Menus/Menu_Main.asset`. Without it the start menu does not appear (a warning is logged). It is also the pause menu.
- `Assets/UI/Menus/Menu_Join.asset` is the sub-menu with the address/port fields.

## The data: `MenuDefinition`

Create via **Assets > Create > TinCan > UI > Menu Definition**. Fields:

- `MenuId`: stable id used to key stored values (defaults to the asset name).
- `Title`: shown by the view.
- `Items`: rows, each with

| Field | Meaning |
|---|---|
| `ItemId` | Unique within the menu. Used by `Invoke`, `SetValue`, `GetValue` and by commands to read sibling values. |
| `Label` | Display text. |
| `Kind` | `Command`, `TextField`, `Toggle`, `Submenu`, `Back`. |
| `CommandId` | For `Command` rows: the id an `IMenuCommand` declares. |
| `Submenu` | For `Submenu` rows: another `MenuDefinition` to push. |
| `DefaultValue` | Initial value of `TextField` / `Toggle` rows (toggles use `"True"` / `"False"`). |

Values are stored per `MenuId/ItemId` and survive leaving and re-entering a menu for the lifetime of the session.

## The API: `IMenuSystem` and menu requests

The API is `Core/UI/IMenuSystem.cs`. A feature does not push onto a shared stack: it **requests** a menu under its own
owner name, `Request(new MenuRequest(owner, menu, kind, pinned))`, and `Withdraw(owner)`s it when it no longer
applies.

- **One request per owner, one stack per request.** Submenu rows push inside the request; Back pops inside it and,
  at its root, closes it. Back never walks into another feature's menu.
- **Who is on top:** `MenuArbitrationProcessor`. `MenuKind.Modal` (the game asking: overboard, the end screen) beats
  `Screen` (the player's own: main, pause, a chooser) beats `Panel` (beside the game); within a kind the newest
  request wins. `Current` is the top request's top menu.
- **Asking again is free.** Requesting the same menu id, kind and pin changes nothing (no `Changed`), so a use case
  may request every frame. A different menu, kind or pin replaces the request and counts as new.
- **Pinned** requests cannot be dismissed: `Back()` returns false at their root and `CloseAll()` skips them. Only the
  owner's `Withdraw` (or a row's `MenuContext.Close()`) closes them. This replaced the menus that reopened themselves
  every tick.
- A command closes **its own** request with `context.Close()` (`MenuContext.Owner` is the row's request).
- `Open(menu)` is a shim for older callers: an unpinned `Screen` request owned by the menu's id. `CloseAll()` closes
  every unpinned request.

`MenuSnapshot` carries `MenuId`, `Title`, `CanGoBack`, the request's `Owner`, `Kind` and `Pinned`, and a list of
`MenuItemRow (ItemId, Label, Kind, Value)`. A view only needs the snapshot; it never sees the asset.

Owners today: `MenuOwners.Main` (main and pause menu), `Respawn.Overboard` (pinned modal) and `Respawn.Leash`
(`OverboardMenuUseCase`), `Fittings` (`ShipFittingMenuUseCase`), `Shipyard` (`ShipyardMenuInputHandler`), `Voyage.End`
(pinned modal, `VoyageEndScreenPresenter`).

## Layers: where screen UI is drawn

`IUiLayers` (`Core/UI/UiLayers.cs`) is the one screen-space surface: one `UIDocument`, with one container per
`UiLayer` added in enum order (`World`, `Hud`, `Panels`, `Menus`, `Modal`, `Toasts`, `Dev`), so a later layer draws
over an earlier one and no sort order is typed anywhere. A view adds its elements under `Root(layer)`. The menu
overlay draws a modal request on `Modal` and any other menu on `Menus`; the HUD is on `Hud`; the shipyard panels on
`Panels`; the net harness readout on `Dev`. World-space UI (the attribute bars) is not on it. Architecture rules fail
on a `UIDocument` or `sortingOrder` outside `UiLayers`, and on `OnGUI` outside an `Editor` folder.

**What sits behind a menu** is a rule table, `MenuBackdropRules`: a `Screen` dims the scene and hides the HUD; a
`Modal` dims the scene and keeps the HUD (the respawn countdown and the voyage line belong with those choices); a
`Panel` changes nothing. The dim backdrop takes the pointer, so nothing under a menu can be clicked.

## Adding a command

1. Implement `IMenuCommand` (in the core `TinCan.UI` assembly or any feature assembly that references it):

```csharp
public class OpenSettingsMenuCommand : IMenuCommand
{
    public const string Id = "OpenSettings";
    private readonly MenuDefinition _settings;
    public OpenSettingsMenuCommand(MenuDefinition settings) { _settings = settings; }
    public string CommandId => Id;
    public void Execute(MenuContext context) => context.Menus.Open(_settings);
}
```

   `MenuContext` gives you the `IMenuSystem`, the `MenuId`/`ItemId` that was invoked, and `GetValue(itemId)` for sibling rows (this is how `JoinGameMenuCommand` reads `address` and `port`).

2. Register it in `UiFeatureInstaller.Install` (or your own feature installer):

```csharp
builder.Register<OpenSettingsMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
```

   `MenuCommandRegistry` collects every `IMenuCommand` registration and dispatches by `CommandId`, the same discovery style as interaction handlers.

3. Put a `Command` row with that `CommandId` in a `MenuDefinition`. Unknown ids are ignored silently, so double-check spelling.

Existing commands: `StartHost`, `JoinGame`, `Quit` (`Assets/Scripts/Core/UI/Commands/`).

**A feature can add main-menu rows.** Its installer implements `IMainMenuRows` (`Core/UI/IMainMenuRows.cs`); the rows go
before Quit, only in scenes that load the feature (`MainMenuComposition`). The shipyard adds "Shipyard" this way.

## Adding a menu

1. Create the `MenuDefinition` asset and fill in rows.
2. Reach it either as a `Submenu` row of an existing menu (no code) or by requesting it from a command or use case (inject `IMenuSystem`; pick an owner name, a kind, and whether it is pinned).
3. Nothing else: the view renders any snapshot generically.

## Showing a HUD value

`IHudValues` is a tiny key-to-text store. Write a presenter (an `ITickable` in TinCan.Features) that computes the text and calls `Set`; call `Remove` when the value no longer applies. `HudOverlayView` renders every key as a label on a grey card at the top left (hidden while the HUD is empty, and under a `Screen` menu); keys are shown in insertion order.

```csharp
public class ShipDamageHudPresenter : ITickable
{
    public const string HudKey = "Hull breaches";
    ...
    public void Tick()
    {
        if (broken == 0) _hud.Remove(HudKey);
        else _hud.Set(HudKey, broken.ToString());
    }
}
```

Register the presenter as `.As<ITickable>()`. `Set` with an unchanged text does not raise `Changed`, so calling it every frame is fine.

## Showing a HUD meter

A meter is a labelled bar: `IHudValues.SetMeter(key, fill)` with a fill in [0, 1] (clamped), and `RemoveMeter(key)`
when it no longer applies. `HudOverlayView` draws meters above the text lines, filled red (empty) to green (full). A
change below a tenth of a percent does not raise `Changed`, so presenters can set a meter every frame. Examples:
- the ship's health, `Features/Airship/Damage/ShipHealthHudPresenter.cs`, which reads the ship controller's replicated
  `HealthAttributeSet` on every peer;
- the fuel tank, `Features/Airship/Fuel/FuelHudPresenter.cs` (level over capacity).

## Input: who owns Cancel

Nothing in the UI reads a key. Cancel reaches the menus through the input contexts ([`INPUT.md`](INPUT.md)):

- `MainMenuBootstrap` opens the main menu while offline and closes it when a session becomes Host/Client.
- While a menu is open the **Menu** context is live: it silences every gameplay context (no movement, look or ability
  input) and routes Cancel to `MenuBackInputHandler`: `Back()` inside the top request (the last Back closes it). When
  the top request is pinned at its root, it opens the pause menu (the main menu, as a `Modal`, so it sits over the
  pinned one); Esc again closes the pause menu and the pinned one is back.
- Otherwise Cancel goes to the highest live context that wants it: the free camera frees the cursor, and only if
  nobody took it does the **Global** context open the main menu (`OpenMenuInputHandler`: offline, or in your own body). One press, one effect.

To give Cancel a new meaning somewhere, add a route to that situation's context and a handler; do not read the key.

## The cursor: one owner

`ICursorPolicy` (`Core/UI/CursorPolicyUseCase.cs`) is the only writer of the cursor, through `UnityCursorDevice`, and
only when the decided state changes. The rule (`CursorPolicyProcessor`): a menu open frees it; otherwise the newest ask
(`Request(owner, CursorState)`, until `Release(owner)`) wins: the shipyard locks it while looking and frees it
otherwise, the free camera's toggle asks for its state; with no ask, it is locked while the player controls something
in a session and free otherwise. An architecture rule fails on any other write to `Cursor.lockState` or
`Cursor.visible`. The policy does not take `IMenuSystem` (the shipyard's menu commands lead to its stage, which asks
for the cursor: a resolve cycle); `MenuCursorPresenter` passes it the menu's open state on every change.

## Session start without the menu

`CommandLineSessionBootstrap` reads the process arguments: `-autohost`, or `-autojoin [address[:port]]` (defaults `127.0.0.1:7777`). Useful for builds acting as unattended test clients.

## Controls menu (key bindings)

A `MenuItemKind.Bindings` row expands at snapshot time into one `Binding` row per key the player can change
(`IInputBindings.Slots`: "Humanoid: Jump - Space"). Invoking a Binding row waits for a key (the value reads
"Press a key..."; Esc cancels); a refused key adds a `Note` row saying why. The Reset to Defaults row runs
`ResetBindingsMenuCommand`. `MenuOverlayView` renders rows in a scroll view, so long menus fit the screen.

## Automation and tests

- Model tests: `MenuUseCaseTests`, `MenuArbitrationProcessorTests` (requests, kinds, pinned, Esc, Back staying inside a request, the backdrop rules), `CursorPolicyUseCaseTests`, `MenuBindingRowsTests`, `MenuCommandRegistryTests`, `MenuCommandTests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests`. Build menus in code with `MenuDefinition.Create(...)`; `Fakes/FakeMenuCommand`, `Fakes/FakeHudValues`, `Fakes/FakePossessionState`, `Fakes/FakeUiLayers`, `Fakes/FakeCursorDevice` exist.
- View tests: `MenuOverlayViewTests` draws both overlays into `FakeUiLayers` on a real panel (text editing, the Modal layer, the HUD under a Screen and a Modal).
- Scenario probes for any scenario (`DevTools/Scenarios/UiScenarioLibrary.cs`): `TopMenu <id|none>`, `HudShown`, `HudHidden`, `CursorLocked`, `CursorFree`; press Esc with `Tap(ScriptedAction.Cancel)`. `OverboardRespawn` and `VoyageLoop` use them.
- Play-mode automation: scenarios and bots press `ScriptedAction` intents (`DevTools/ScriptedAction.cs`), which `ScriptedActionMap` turns into context actions on `IScriptedInput`; the input reader merges them with the keyboard under the same context rules. Injecting Input System events does not work while the Editor is unfocused; use this seam instead.

## Replacing the views

Write a new `MonoBehaviour` implementing `IInjectedView` that takes `IMenuSystem` and `IUiLayers` via `[Inject]`, subscribes to `Changed`, and renders `Current` under the layer that fits its kind. Add it to the `GameLifetimeScope` prefab; injection is automatic. The model, commands, menus and tests are untouched.

## Feature panels

A feature with a screen of its own can draw it itself, model and view split the same way: a pure presenter builds a
snapshot, and a code-built UI Toolkit view under `IUiLayers.Root(UiLayer.Panels)` renders it (never a document of its
own). The shipyard does this: `ShipyardPanelPresenter` builds the parts and controls panels, `ShipyardPanelView` draws
them, and clicks over a panel are kept from the build (`IsPointerOver` checks the picked element is one of its own,
since the panel is shared with every layer). The keys a panel shows
come from `IInputBindings.DescribeAction`, never typed text, so rebinding shows.

## Known limits

- Rows are rendered top to bottom with no layout options; the view is a placeholder.
- Values are strings; parse in the command (see `JoinGameMenuCommand` for the port).
- No navigation with keyboard/gamepad yet; the overlay is mouse-driven.
- Menu assets are matched to commands by string ids; a renamed command breaks the row silently.
