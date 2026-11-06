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
| `Kind` | `Command`, `TextField`, `Toggle`, `Submenu`, `Back`, `Note` (a line of text, or a hidden status line); placeholders `Bindings` (the key rows), `Settings` (a settings page) and `Rows` (rows from an `IMenuRowSource`). `Binding`, `Slider` and `Choice` rows are generated, never authored. |
| `SettingsGroup` | For `Settings` rows: which group's settings the row expands to. |
| `RowSource` | For `Rows` rows: the `SourceId` of the `IMenuRowSource` whose rows it shows. |
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

## Theme and components

Screen UI is built from **components**: small UI Toolkit elements, each a C# class (`[UxmlElement]`, usable from
UXML and UI Builder too) with its own `.uss` beside it. The look comes from **tokens**; views only compose
components and feed them data.

| Piece | Where | What |
|---|---|---|
| Theme | `Assets/UI/Theme/TinCan.tss` | The panel's theme (set on `Settings/UI/DefaultPanelSettings`): Unity's default controls, then the files below, then every core component sheet. |
| Tokens | `Assets/UI/Theme/Tokens.uss` | Every colour, spacing step, radius and font size as a variable (`--tc-surface-*`, `--tc-text*`, `--tc-accent`, `--tc-warning`, `--tc-good`/`fair`/`bad`, `--tc-space-*`, `--tc-radius*`, `--tc-font-*`). **To restyle the game, change these.** |
| Controls | `Assets/UI/Theme/Controls.uss` | Unity's built-in controls inside our UI (under `.tc-root`): buttons, labels, text inputs (a dark well with light text), toggles, sliders. |
| Layout | `Assets/UI/Theme/Layout.uss` | Placement helpers with no look: `tc-fill`, `tc-side-column`, `tc-grid`. |
| Core components | `Assets/Scripts/Core/UI/Components/` (`TinCan.UI`) | `Backdrop`, `MenuPanel`, `MenuButton` (`Compact`), `MenuNote`, `ChoiceField`, `Card` (`Grow`, `Below`, `Heading`, `Hint`), `TabBar`, `KeyHintTable`, `HudCard`, `Meter` (its band by value: `Meter.Band`). Imported by the theme. |
| Feature components | the feature's `UI/` folder, e.g. `Features/Shipyard/UI/PartTile`; `DevTools/UI/DevReadout` | Their sheets are listed on the feature's installer, which implements `IExtension<StyleSheet>`; `UiLayers` adds them to the layers' root (`UiStyleSheets`). Core never names a feature's sheet, and the styles exist only where the feature loads. |

Rules, each checked by a test:
- **A sheet uses tokens for colours and font sizes**, never literal values (`UiThemeTests.EverySheet_TakesColoursAndFontSizesFromTheTokens`); only `Tokens.uss` holds them.
- **Every core component sheet is imported by the theme**, and every feature sheet is contributed by an installer (`UiThemeTests`).
- **Views and components set no colour, font size or weight in code** (`ScreenUi_TakesItsLookFromTheTheme`); only values from data stay in code (a meter's width, a thumbnail, show and hide).
- Class names are BEM-style with the `tc-` prefix: `tc-meter`, `tc-meter__fill`, `tc-meter__fill--bad`; each component exposes them as `UssClassName` constants.

**Adding a component:** write the class and its `.uss` beside it (tokens only). A core one goes in
`Core/UI/Components/` and gets an `@import` line in `TinCan.tss`; a feature one goes in the feature's `UI/` folder and
on the installer's style sheet list. Add a token only for a new kind of colour or size.

Offline scenes have no camera, so nothing clears the screen between frames: after a menu changes, the old one shows
faintly for a few frames under a translucent panel. That is the empty scene, not the theme; in a session the camera
clears every frame.

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

**A menu whose rows come from data** (one row per saved thing) is built in code with `MenuDefinition.Create` each time
it opens, so it is never stale, and shown with `IMenuSystem.Open`, over the menu that opened it (Back returns there).
Every data row shares one command, which reads its row from `MenuContext.ItemId`; prefix the ids so none clashes with
Back. A `Note` row in the definition shows a line of text (a refusal's reason). Example: the ship picker,
`Features/ShipPicker/ShipPickerUseCase.cs` (`BuildMenu`) and `PickShipMenuCommand.cs`.

**A status line in an authored menu** is a `Note` row with an empty label: hidden until a command sets its value with
`IMenuSystem.SetValue`, then the value is its text (`MenuUseCase.AddNoteRow`). Example: `Menu_Join`'s `status` row,
written by `JoinGameMenuCommand` ("Connecting to ...", then the host's refusal).

**Rows that change while the menu is open** come from an `IMenuRowSource` (`Core/UI/IMenuRowSource.cs`), registered
with `.As<IMenuRowSource>()`. An authored `Rows` row names it by `RowSource` and expands to its label as a heading, one
row per `MenuSourceRow` (a row that cannot be picked is a note), or the source's `EmptyLabel`. Row ids are
`{placeholder}/{key}`, so focus stays on a row as the list changes. `MenuUseCase` rebuilds on the source's `Changed`,
passes a picked row to `Invoke`, and calls `SetShown` as the row comes on and off screen, so a source works only while
seen. Example: `Menu_Join`'s `games` row and `LanGamesRowSource` (LAN discovery browses only while the list shows).

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

## Keyboard and gamepad in menus

Menus work without a mouse, through the same **Menu** context (`MenuInputContext`, slots Navigate and Submit), so the
keys are heard only while a menu is open:

| Input | Keyboard | Gamepad | Does |
|---|---|---|---|
| Navigate up / down | arrows, W / S | d-pad, left stick | Moves the focus between rows (wrapping); notes are skipped. |
| Navigate left / right | arrows, A / D | d-pad, left stick | Adjusts the focused row: a slider steps, a choice moves, a toggle flips. |

WASD is its own action (`Menu/NavigateLetters`) because the letters are also text: on a focused text field they are
typed and do not navigate; the arrows still leave the field.
| Submit | Enter | South (A) | Uses the focused row, as a click would. |
| Cancel | Esc | East (B), Start | Back, as above. |

- **The model owns the focus.** `MenuSnapshot.Focused` is the focused row; a new menu focuses its first usable row,
  and the same menu keeps its focus by item id while values change. `IMenuSystem.Focus`, `MoveFocus`, `Adjust` and
  `Submit` change it; `MenuFocusProcessor` is the pure rule (what can take focus, what left and right do).
- **`MenuNavigationUseCase`** reads Navigate and Submit every frame; a held direction repeats after 0.4 s, then every
  0.1 s, and the stronger axis wins.
- **The view shows it, with one focus.** `MenuPanel.ShowFocus` marks the row (`tc-menu-panel__row--focused`, themed
  in `MenuPanel.uss`), gives it UI Toolkit's own focus as well (so the theme's `:focus` border and the focus fill are
  always the same row, and a text field there takes the typing) and scrolls to it. A row the pointer presses takes the
  focus too, so mouse and keys never disagree.
- **UI Toolkit's own navigation is off.** It reads the devices itself: arrows, WASD and Tab move its focus, Enter
  clicks a focused button. `UiLayers.StopBuiltInNavigation` ignores those events at the panel's root, where they
  arrive even when nothing has focus; otherwise they move a second focus and Enter clicks twice
  (`MenuOverlayViewClickTests.UiToolkitsOwnNavigation_NeitherMovesTheFocusNorClicks`).
- Scenarios drive it with `ScriptedAction.MenuUp`, `MenuDown`, `MenuLeft`, `MenuRight` (directions are values: use a
  short `Hold`, under the 0.4 s repeat delay, for one step; a `Tap` carries no value) and `MenuSubmit` (a `Tap`), and read it
  with the `FocusedRow <item id>` probe.
- The gamepad bindings are in their own `Gamepad` control scheme; the Controls menu lists and rebinds the keyboard
  ones only, and Navigate and Submit are not rebindable, so a menu can always be reached.

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
`ResetBindingsMenuCommand`. `MenuOverlayView` renders rows in a scroll view, so long menus fit the screen. It is
reached through Settings > Controls > Key bindings.

## Settings menu (player settings)

Main menu > **Settings** (`Menu_Settings`) has one page per settings group (`Menu_Settings_Controls`,
`Menu_Settings_View`). A page's `Settings` row expands at snapshot time (`MenuSettingsRows`) into one row per
`SettingDefinition` of its group loaded in this scene: a slider becomes a `Slider` row (it carries `Min`, `Max`,
`Step`), a toggle a `Toggle`, a choice a `Choice` row (it carries its `Options`; the view draws `<` and `>`), text a
`TextField`. Then **Reset to defaults** for that page. The rows read and write `IPlayerSettings` directly (not the
menu's own values), which checks and saves them; a group with nothing loaded says so. Adding a setting needs no menu
work: see [`plans/player-settings.md`](plans/player-settings.md) and `CODE_MAP.md`, "A player setting". The pages
and the main menu row are built by **TinCan > Dev > Settings > Build Assets**.


## Automation and tests

- Model tests: `MenuUseCaseTests`, `MenuArbitrationProcessorTests` (requests, kinds, pinned, Esc, Back staying inside a request, the backdrop rules), `CursorPolicyUseCaseTests`, `MenuBindingRowsTests`, `MenuCommandRegistryTests`, `MenuCommandTests`, `HudUseCaseTests`, `MainMenuBootstrapTests`, `CommandLineSessionBootstrapTests`. Build menus in code with `MenuDefinition.Create(...)`; `Fakes/FakeMenuCommand`, `Fakes/FakeHudValues`, `Fakes/FakePossessionState`, `Fakes/FakeUiLayers`, `Fakes/FakeCursorDevice` exist.
- View tests: `MenuOverlayViewTests` draws both overlays into `FakeUiLayers` on a real panel (text editing, the Modal layer, the HUD under a Screen and a Modal). `MenuOverlayViewClickTests` runs the menu view in a simulated panel from `com.unity.ui.test-framework` (built in, 6.4.0, test assembly only; `UITestFixture`, `simulate.Click`, `simulate.FrameUpdate`): a click runs a row, the choice arrows and the slider write the setting, and a menu's backdrop keeps clicks from a panel underneath. Write new view tests that way.
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

- Rows are rendered top to bottom with no layout options; the view is a placeholder. Its look is the theme's, a
  grey placeholder until there is an art direction.
- Values are strings; parse in the command (see `JoinGameMenuCommand` for the port).
- Gamepad play outside menus has no bindings yet: only menus and Cancel have gamepad bindings.
- Menu assets are matched to commands by string ids; a renamed command breaks the row silently.
