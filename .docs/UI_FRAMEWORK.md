# Menu & HUD Framework

**Status:** Prototype (slice 0 of the ship-tasks prototype track). The *headless model* is the part meant to last; the UI Toolkit views are deliberately throwaway.

The framework separates three things so they can evolve independently:

| Layer | Lives in | What it is |
|---|---|---|
| **Data** | `Assets/UI/Menus/*.asset` (`MenuDefinition`) | Which menus exist and which rows they contain. Authored in the Inspector, no code. |
| **Headless model** | `Assets/Scripts/Core/UI/` (TinCan.Features) | `IMenuSystem` (menu stack + values), `IMenuCommand` (what a row does), `IHudValues` (named HUD texts and meters), `MainMenuBootstrap` (opens and closes the main menu with the session), the Cancel handlers. Pure C#, unit-tested, knows nothing about rendering. |
| **Views** | `Assets/Scripts/App/Views/` (`TinCan.App`) | `MenuOverlayView`, `HudOverlayView`: UI Toolkit code-built renderers of the model. Replace these when real UI arrives; nothing else changes. |

Gameplay code never talks to a view. It opens menus through `IMenuSystem`, reacts to rows through `IMenuCommand`, and shows numbers through `IHudValues`.

## Where things are wired

- `Assets/Resources/Installers/UiFeatureInstaller.asset` (a `FeatureInstaller`, see `FEATURE_INSTALLERS.md`) registers the model, the commands and the bootstrap. The overlay views implement `IInjectedView`, so the lifetime scope injects them at container build. The `GameLifetimeScope` prefab carries two children, `MenuOverlay` and `HudOverlay`, each with a `UIDocument` (panel settings: `Assets/Settings/UI/DefaultPanelSettings.asset`) and the matching view component. Every scene that contains that prefab gets the UI for free.
- The installer's `Main Menu` field points at `Assets/UI/Menus/Menu_Main.asset`. Without it the start menu does not appear (a warning is logged).
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

## The API: `IMenuSystem`

```csharp
MenuSnapshot? Current { get; }      // immutable view model of the top menu, null when closed
bool IsOpen { get; }
event Action? Changed;              // fired after every mutation; views re-render on it

void Open(MenuDefinition menu);     // push
void Back();                        // pop (closes when the stack empties)
void CloseAll();
void Invoke(string itemId);         // run a row: command, submenu, back, or toggle flip
void SetValue(string itemId, string value);
string GetValue(string itemId);
```

`MenuSnapshot` carries `MenuId`, `Title`, `CanGoBack` and a list of `MenuItemRow (ItemId, Label, Kind, Value)`. A view only needs the snapshot; it never sees the asset.

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

## Adding a menu

1. Create the `MenuDefinition` asset and fill in rows.
2. Reach it either as a `Submenu` row of an existing menu (no code) or by calling `IMenuSystem.Open` from a command or use case (inject `IMenuSystem`).
3. Nothing else: the view renders any snapshot generically.

## Showing a HUD value

`IHudValues` is a tiny key-to-text store. Write a presenter (an `ITickable` in TinCan.Features) that computes the text and calls `Set`; call `Remove` when the value no longer applies. `HudOverlayView` renders every key as a label; keys are shown in insertion order.

```csharp
public class FuelHudPresenter : ITickable
{
    public const string HudKey = "Fuel";
    ...
    public void Tick() => _hud.Set(HudKey, Mathf.RoundToInt(tank.Level).ToString());
}
```

Register the presenter as `.As<ITickable>()`. `Set` with an unchanged text does not raise `Changed`, so calling it every frame is fine.

## Showing a HUD meter

A meter is a labelled bar: `IHudValues.SetMeter(key, fill)` with a fill in [0, 1] (clamped), and `RemoveMeter(key)`
when it no longer applies. `HudOverlayView` draws meters above the text lines, filled red (empty) to green (full). A
change below a tenth of a percent does not raise `Changed`, so presenters can set a meter every frame. Example: the
ship's health, `Features/Airship/Damage/ShipHealthHudPresenter.cs`, which reads the ship controller's replicated
`HealthAttributeSet` on every peer.

## Input: who owns Cancel

Nothing in the UI reads a key. Cancel reaches the menus through the input contexts ([`INPUT.md`](INPUT.md)):

- `MainMenuBootstrap` opens the main menu while offline and closes it when a session becomes Host/Client.
- While a menu is open the **Menu** context is live: it silences every gameplay context (no movement, look or ability
  input) and routes Cancel to `MenuBackInputHandler` (`Back()`; the last Back closes the menu).
- Otherwise Cancel goes to the highest live context that wants it: the helm lets go (`ExitVehicleInputHandler`), the
  free camera frees the cursor, and only if nobody took it does the **Global** context open the main menu
  (`OpenMenuInputHandler`: offline, or in your own body). One press, one effect.

To give Cancel a new meaning somewhere, add a route to that situation's context and a handler; do not read the key.

`MenuOverlayView` frees the cursor while a menu is open and re-locks it on close when a session is active.

## Session start without the menu

`CommandLineSessionBootstrap` reads the process arguments: `-autohost`, or `-autojoin [address[:port]]` (defaults `127.0.0.1:7777`). Useful for builds acting as unattended test clients.

## Controls menu (key bindings)

A `MenuItemKind.Bindings` row expands at snapshot time into one `Binding` row per key the player can change
(`IInputBindings.Slots`: "Humanoid: Jump - Space"). Invoking a Binding row waits for a key (the value reads
"Press a key..."; Esc cancels); a refused key adds a `Note` row saying why. The Reset to Defaults row runs
`ResetBindingsMenuCommand`. `MenuOverlayView` renders rows in a scroll view, so long menus fit the screen.

## Automation and tests

- Model tests: `MenuUseCaseTests`, `MenuBindingRowsTests`, `MenuCommandRegistryTests`, `MenuCommandTests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests`. Build menus in code with `MenuDefinition.Create(...)`; `Fakes/FakeMenuCommand`, `Fakes/FakeHudValues`, `Fakes/FakePossessionState` exist.
- Play-mode automation: scenarios and bots press `ScriptedAction` intents (`DevTools/ScriptedAction.cs`), which `ScriptedActionMap` turns into context actions on `IScriptedInput`; the input reader merges them with the keyboard under the same context rules. Injecting Input System events does not work while the Editor is unfocused; use this seam instead.

## Replacing the views

Write a new `MonoBehaviour` implementing `IInjectedView` that takes `IMenuSystem` (and `INetworkService` for cursor handling) via `[Inject]`, subscribes to `Changed`, and renders `Current`. Add it to the `GameLifetimeScope` prefab; injection is automatic. The model, commands, menus and tests are untouched.

## Known limits

- Rows are rendered top to bottom with no layout options; the view is a placeholder.
- Values are strings; parse in the command (see `JoinGameMenuCommand` for the port).
- No navigation with keyboard/gamepad yet; the overlay is mouse-driven.
- Menu assets are matched to commands by string ids; a renamed command breaks the row silently.
