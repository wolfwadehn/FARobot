# FApp Architecture

## Overview

FApp is a WPF-based 2D drawing editor targeting .NET 10 on Windows, built for sheet-metal part editing
at TRUMPF. It couples a Nori-backed Lux renderer for GPU-accelerated 2D/3D display with a phase-driven
widget command system for interactive geometry construction. The application reads and writes DXF, GEO,
and CURL file formats and ships with a custom UI-automation test harness built on FlaUI.

---

## Project Layout

```
FApp.slnx
├── FApp/            WPF application (WinExe, net10.0-windows, UseWPF=true)
├── Test/            Headless unit/integration tests (GLFW host, Nori TestRunner)
├── UITest/          UI automation tests (FlaUI UIA3 backend)
└── Tools/Console/   CLI tooling (coverage analysis, version injection)
```

External dependencies live outside the repo:

- `N:\`  — Nori library root (Nori.Core, Nori.Host.WPF, Nori.Lux, Nori.Host.GLFW)
- `C:\Work\FlaUI\` — FlaUI source referenced directly (FlaUI.Core, FlaUI.UIA3)
- `F:\` — alias for `C:\Work\FApp\` (same physical path, used in scripts and runtime paths)

---

## Application Bootstrap

### App.xaml.cs

Entry point. Before the WPF message pump starts it:

1. Forces `InvariantCulture` on all threads so decimal separators are always `.`
2. Registers both `Dispatcher.UnhandledException` and `AppDomain.UnhandledException` handlers
3. Calls `CheckExpired()` — reads the assembly version (`Year.Month.Build.Revision`) and exits if
   the month field is past the allowed window (time-limited internal builds)

### MainWindow.xaml.cs  ·  constructor

The single constructor does all wiring in a fixed order:

```
Lib.Init()
  → register ZipStmLocator("pix:", "FApp.wad")    (release)
    or FileStmLocator("pix:", "F:/Wad/")           (dev — wad not found)
  → Lib.AddNamespace("FApp")                       (makes Nori find FApp types)
InitializeComponent()                              (XAML parse)
mContent.Child = WPFHost.Init(this, OnLuxReady)   (Lux GPU surface)
DwgHub.IBar    = new InputBar(mStack, mStatus)
mToolbarHost.Child = new Toolbar()
VNode.RegisterAssembly(Assembly.GetExecutingAssembly())
```

`OnLuxReady` fires after the GPU context is ready; it builds the `DwgScene`, sets up
`DwgHub.WidgetMap` / `DwgHub.MethodMap` by scanning the assembly, and creates the snap handler.

---

## Window Layout

```
┌─────────────────────────────────────────────┐
│  Menu bar  (File / Edit / View / About)      │  DockPanel.Dock=Top
├──────────┬──────────────────────────────────┤
│          │                                   │
│ Toolbar  │   Lux render surface              │
│ 114 px   │   (mContent Border)               │
│          │                                   │
├──────────┴──────────────────────────────────┤
│  mStatus (TextBlock) │ mStack (StackPanel)   │  DockPanel.Dock=Bottom
└─────────────────────────────────────────────┘
```

- **Toolbar host** (`mToolbarHost`, `Border`) — contains a `Toolbar` StackPanel
- **Render surface** (`mContent`, `Border`) — Nori's `WPFHost` installs a `HwndHost` here
- **Status bar** (`mStatus`, `TextBlock`) — shows step prompts from the active widget
- **Input row** (`mStack`, `StackPanel`) — dynamically populated by `InputBar`

---

## Resource System (pix: prefix)

All asset paths use the virtual prefix `pix:`. `Lib` resolves them via the registered `IStmLocator`:

| Context  | Locator            | Root path              |
|----------|--------------------|------------------------|
| Dev      | `FileStmLocator`   | `F:\Wad\`              |
| Release  | `ZipStmLocator`    | `Bin\FApp.wad`         |

Key resources:

- `pix:Modes/manifest.txt` — toolbar button list (drives `Toolbar` construction)
- `pix:Modes/<CmdName>.png` — 16×16 icon per command
- `pix:Modes/cmd-prompt.txt` — per-command display names, field labels, and step prompts

---

## Drawing Model

The drawing is a `Dwg2` (Nori type). It holds a flat list of `Ent2` entities organised into named
layers (`Layer2`). Entity types include:

- `E2Poly` — polyline/polygon (open or closed, lines + arcs)
- `E2Point` — single point
- `E2Text` — placed text
- `E2Dimension` — parametric dimension annotation
- `E2Bendline` — bend-line with angle/radius/k-factor
- `E2Insert` — block reference (E2Block + transform)
- `E2Solid` — filled region
- `E2Spline` — cubic spline

Special layers by convention:

| Name            | Color       | Role                             |
|-----------------|-------------|----------------------------------|
| `0`             | default     | main geometry (mandatory)        |
| `CleanupMarker` | Red         | diagnostic markers from cleanup  |
| `Dimension`     | DarkGreen   | dimension entities               |
| `helper`        | Yellow      | construction geometry            |

---

## Central Coordinator — DwgHub

`DwgHub` (static class, `FApp/DwgEdit/DwgHub.cs`) is the single hub that everything else reads from
or writes to.

### Key state

| Property      | Type                     | Purpose                                              |
|---------------|--------------------------|------------------------------------------------------|
| `Dwg`         | `Dwg2?`                  | Current drawing; setter wires up snap handler        |
| `Widget`      | `Widget?`                | Active interactive command; setter calls Activate()  |
| `Phase`       | `int`                    | Current phase within the active widget               |
| `IBar`        | `InputBar`               | Bottom input panel (set once at startup)             |
| `WidgetMap`   | `Dict<ECmd, Type>`       | ECmd → Widget subclass (built by reflection)         |
| `MethodMap`   | `Dict<ECmd, Action<Dwg2>>` | ECmd → non-interactive command handler             |
| `KeyboardMode`| `bool`                   | true = text input active, false = mouse driving      |
| `MousePos`    | `Point2`                 | Snapped mouse world position                         |
| `RawMousePos` | `Point2`                 | Unsnapped mouse world position                       |

### Startup reflection scan

`DwgHub` scans the assembly once (lazily on first use):

- **WidgetMap** — finds all `Widget` subclasses decorated with `[DwgCmd(ECmd.X)]`, stores `ECmd → Type`
- **MethodMap** — finds all static methods on `DwgCmds` decorated with `[DwgCmd(ECmd.X)]`, wraps in
  `Action<Dwg2>`, stores `ECmd → Action`

### Mouse event pipeline

```
Nori Hub.Mouse.Moves
  → DwgHub.DoMouse(pix, leftPressed)
      → RawMousePos = ToWorld(pix)
      → if snap enabled: MousePos = DwgSnap.Snap(RawMousePos, PickAperture)
      → update [Click(n)] / [Unsnapped] fields on active widget
      → DwgHub.WidgetVN.Redraw()          (triggers DrawFeedback on next frame)
      → if leftPressed: OnClick(pt)
```

`OnClick` manages phase transitions:

```
OnClick(pt)
  → set [Click(n)] field for current phase
  → Phase++
  → if Phase == widget.Phases: widget.Completed() → reset
```

### Keyboard mode

Activated when the user clicks a TextBox in the input bar. While active, mouse-move updates to widget
fields are suppressed so typed coordinates are not overwritten. Deactivated by ESC or by the next
mouse click that advances the phase.

---

## Command & Widget System

### ECmd enum

Canonical list of ~100 command identifiers (Arc, Circle, Line, Pick, Move, …). Every toolbar button,
menu item, and keyboard shortcut maps to exactly one `ECmd`.

### Non-interactive commands (MethodMap)

Decorated with `[DwgCmd(ECmd.X)]` on a static method in `DwgCmds`. They receive a `Dwg2`, operate
directly, and return. Examples: `GridSettings`, `LaunchLayersDlg`, `DoDeleteHelperLines`.

### Interactive commands (WidgetMap → Widget subclasses)

All interactive commands inherit from `Widget` (abstract). The class hierarchy:

```
Widget
└── DwgWidget                   base for all drawing commands
    ├── PolyMaker               creates new poly entities
    ├── PolyMaker2              edit with parallel/perpendicular logic
    ├── PolyEditor              single-click edge operations (chamfer, fillet, …)
    │   └── PolyCornerEditor    corner operations with CTRL=all-corners
    ├── PolyEditor2             segment operations (trim, extend)
    ├── DwgArranger             transformations (move, rotate, scale, mirror)
    ├── DwgLayouter             array operations (polar, rectangular)
    └── PickWidget              selection (window/crossing, CTRL/SHIFT modifiers)
```

### Widget lifecycle

```
1. Activate(true)
     → reflect field list from [Click], [Textbox], [Checkbox], [Choice] attributes
     → count phases from [Phase(n)] attributes
     → InputBar.SetWidget(this)   — creates UI elements in mStack

2. Interaction loop (n = 0..Phases-1):
     Mouse move  → field[Click(n)].SetValue(MousePos)  → DrawFeedback()
     Mouse click → OnClick(pt)                          → Phase++
     Text input  → OnTypeIn(n, text)                   → parse & store
     Key press   → OnKey(info)                         → optional intercept

3. Completed()
     → build UndoStep (ModifyDwgEnts or subclass)
     → UndoStep.Push()
     → DwgHub.Widget = null (or self for [CanRepeat] + CTRL)

4. Deactivate
     → InputBar.Clear()
     → DwgHub.WidgetVN.Redraw()
```

### WField — field descriptor

Each `Widget` subclass declares its input as public fields/properties with attributes. `WField`
wraps one field and carries its metadata:

| Attribute        | UI element  | Field type          |
|------------------|-------------|---------------------|
| `[Textbox(n)]`   | TextBox     | `double`, `int`, `Point2` |
| `[Checkbox(n)]`  | CheckBox    | `bool`              |
| `[Choice(n)]`    | ComboBox    | any `enum`          |
| `[Click(n)]`     | (none)      | `Point2` set by nth click |

Supporting attributes on fields:

- `[Phase(n)]` — field is "active" only during phase n
- `[Variant(value)]` — field visible only when another field equals `value`
- `[Angle]` — TextBox stores radians internally but shows/accepts degrees
- `[Unsnapped]` — receives raw (unsnapped) mouse position
- `[NoMouseMove]` — not updated on mouse moves, only on click
- `[HotKey(key)]` — keyboard shortcut toggles a CheckBox or ComboBox item

Supporting attributes on the widget class:

- `[CanRepeat]` — CTRL+click replays previous click sequence
- `[NoKeyboardMode]` — disables keyboard text entry
- `[NoMouseInput]` — ignores mouse clicks (keyboard-only command)
- `[AlwaysShowFeedback]` — calls `DrawFeedback` even when widget is inactive

---

## Toolbar

`Toolbar` (StackPanel, `FApp/Widgets/Toolbar.cs`) is constructed once and set as the child of
`mToolbarHost`. It reads `pix:Modes/manifest.txt` line by line:

| Line format       | Result                                                  |
|-------------------|---------------------------------------------------------|
| `;…`              | comment, skip                                           |
| blank             | horizontal separator + new WrapPanel group              |
| `CommandName`     | 16×16 icon button for that ECmd                         |
| `>Group:A,B,C`    | full-width dropdown button opening a popup sub-menu     |

Current manifest:

```
Line

>Curves:Arc,Circle
```

Every button Border gets `AutomationProperties.AutomationId = cmdName`, and its Image child gets
`AutomationProperties.Name = cmdName`, enabling FlaUI test targeting.

Buttons whose ECmd appears in neither WidgetMap nor MethodMap are rendered at 0.2 opacity (disabled).

---

## Input Bar

`InputBar` (`FApp/Widgets/InputBar.cs`) manages the bottom strip. It owns:

- **`mStatus` TextBlock** — shows the current step prompt (uses `*bold*` markup for key terms)
- **`mPanel` StackPanel** — dynamically populated with TextBox/CheckBox/ComboBox controls

When `SetWidget(widget)` is called (from `Widget.Activate`):

1. `Clear()` removes all existing controls
2. For each `WField` in `widget.Fields`, `CreateUI(field)` adds the appropriate control
3. `SetPhase(0)` highlights the first active control and sets the prompt text

`SetKeyboardMode(bool)` changes the TextBox foreground color: black = keyboard active, gray = mouse
driving the value.

---

## Drawing Scene & Rendering

`DwgScene` (extends `Scene2`, `FApp/DwgScene.cs`) contains several `VNode` layers composited in
draw order. Each `VNode` subclass knows how to draw itself into the Lux command buffer:

| VNode                | z-level | Draws                                                |
|----------------------|---------|------------------------------------------------------|
| `DwgGridVN`          | −5/−6   | adaptive grid (main lines + subdivisions)            |
| `DwgConsLineVN`      | —       | construction lines (dark green, dotted)              |
| `DwgSnapVN`          | —       | snap indicators (+/×/□) and alignment labels         |
| `WidgetVN`           | —       | active widget's `DrawFeedback()` output              |

Grid opacity fades when zoom makes grid spacing fall below 10–50 px, preventing visual clutter at
extreme scales.

`FoldedPartScene` (`Scene3`) is a secondary 3D scene activated by `BendWidget` to show a fold
preview alongside the 2D drawing.

---

## Snap System

`DwgSnap` (created by `DwgHub` when `Dwg` is assigned) maintains:

- **Snap points** — computed each frame from nearby `Ent2` entities: endpoint, midpoint, center,
  tangent, intersection, quadrant
- **Construction lines** — angles and reference lines shown as infinite dotted green lines that
  guide perpendicular/parallel snapping
- **Pick aperture** — `3.5 * DPI scale * pixel scale` world units; anything within this radius snaps

`DwgHub.DoMouse` calls `DwgSnap.Snap(raw, aperture)`, which returns either a snap point or
the raw position, and populates `DwgSnapVN` and `DwgConsLineVN` for visual feedback.

---

## Undo/Redo

All drawing mutations go through `UndoStep.Push()`. The undo stack is held by the Nori `UndoStack`
attached to `Dwg2`. Common step types:

- `ModifyDwgEnts` — add/remove entities (most widget `Completed()` calls)
- `ModifyEntLayer` — reassign entity to different layer
- `ClubbedStep` — group multiple steps into one undo unit

`MainWindow` wires `Undo`/`Redo` menu items; `OnEditMenuOpened` reads the step descriptions from the
stack to populate the menu item text.

---

## File I/O

`MainWindow` dispatches save/load based on file extension:

| Extension | Read                  | Write             |
|-----------|-----------------------|-------------------|
| `.dxf`    | Nori `DXFReader`      | Nori `DXFWriter`  |
| `.geo`    | `GEOReader`           | `GEOWriter`       |
| `.curl`   | Nori `CurlReader`     | Nori `CurlWriter` |

CURL format is only available when the `PIXDEVELOPER` environment variable is set.

`GEOReader` and `GEOWriter` handle TRUMPF's sheet-metal exchange format. The writer auto-explodes
`E2Insert` blocks, detects outer vs. inner contours by bound containment, and infers bend types.

`SaveSelectedAs` creates a new `Dwg2` containing only the selected entities and saves it — useful for
exporting sub-geometry.

---

## MRU (Most-Recently-Used)

`MRUList` (`FApp/WPF/MRUList.cs`) stores up to nine file paths in a memo file. The File menu's
submenu is rebuilt on open via `UpdateFileMenu`, with numbered accelerators (`_1`–`_9`).

---

## Command Routing (Menu & Hotkeys)

`CmdRouter` (`FApp/WPF/CmdRouter.cs`) connects XAML menu items to code:

1. At startup it reflects over the `Window` to find all methods with `[CmdSink]` attributes
2. It traverses the XAML tree to find `MenuItem` elements that have a `Tag` attribute
3. Each tag string matches a `[CmdSink]` method name; the menu item's `Click` is wired to invoke it
4. `InputGestureText` values in XAML are parsed to register keyboard shortcuts via `PreviewKeyDown`

---

## Layer Management UI

`LayersDlg` (modal window, `FApp/WPF/LayersDlg.xaml.cs`) wraps a `TableEdit` control backed by
`LayersTableVM`. The table shows layer Name, Color, and LineType. Row selection changes the drawing's
active layer. The "More Actions…" menu exposes move-to-layer, select-layer, and purge operations
defined as `TableVM.Cmds` entries.

`EditBend` (modal window, `FApp/WPF/EditBend.xaml.cs`) edits a single `E2Bendline` (angle, radius,
k-factor) and optionally refreshes the 3D fold preview.

---

## Drag Operations

`DragOp` (`FApp/DwgEdit/DragOp.cs`) is an abstract base for rubber-band-style interactions that
bypass the Widget phase system. A `DragOp2D` subclass receives `Start / Move / Finish / Cancel`
notifications through `DragOpVN`. The main user is `Ent2Selector`:

- **Left-to-right rectangle** — window select (fully contained entities)
- **Right-to-left rectangle** — crossing select (any intersecting entities)
- **SHIFT held** — toggle selection, preserve existing

---

## UI Automation / FlaUI Integration

The `UITest` project uses FlaUI's UIA3 backend:

```
AppDriver
  → UIA3Automation
  → Application.Launch("Bin/FApp.exe")
  → GetMainWindow(timeout=10s)
```

Tests are discovered by reflection from `[UIFixture(id, name)]` classes and `[UITest(id, name)]`
methods, run sequentially, and report PASS/FAIL. Exit code 1 on any failure.

Element targeting relies on the AutomationIds set in `Toolbar.MakeButton`:

```csharp
AutomationProperties.SetAutomationId(border, cmdName);  // e.g. "Line", "Arc", "Curves"
AutomationProperties.SetName(image, cmdName);
```

---

## Build & Ship

Development build:

```
dotnet build FApp.slnx
```

Release (Ship.bat, run from `F:\`):

1. `dotnet publish FApp/FApp.csproj -c Release -o F:\Publish\`
2. `7z a … FApp.wad F:\Wad\*` — package resource files
3. `dotnet run --project Tools\Installer\VersionInjector` — inject build version
4. `ISCC.exe F:\Tools\Installer\FApp.iss` — Inno Setup installer

The application has an expiry check: `AssemblyVersion` encodes `Year.Month.Build.Revision`.
`CheckExpired()` exits the process if the month field is stale.

---

## Code Style Conventions

- Box-art file headers (`╔═╦╗ …`)
- `#region class Foo ----…` blocks padded to column 100
- 3-space indentation
- Field prefixes: `m` (instance), `s` (static)
- No explicit `private` keyword
- File-scoped namespaces (`namespace FApp;`)
- No comments unless the *why* is non-obvious
