# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build
dotnet build "FRobot\FRobot.csproj" -c Debug

# Launch
Start-Process "Bin\FRobot.exe"

# Run unit tests
dotnet run --project Test\FRobot.Test.csproj

# Run UI automation tests (requires FRobot.exe in Bin\)
dotnet run --project UITest\FRobot.UITest.csproj
```

Kill a running instance before rebuilding: `Stop-Process -Name FRobot -Force -ErrorAction SilentlyContinue`

## Drive Mappings

Two mapped drives are essential:

| Drive | Points to | Purpose |
|-------|-----------|---------|
| `F:` | `C:\Work\FRobot\` | Project root alias used in scripts and resource paths |
| `N:` | Nori library root | External rendering/framework dependency |

The app reads resources via the `pix:` virtual prefix. In dev it resolves to `F:\Wad\` (raw files); in release it reads from `Bin\FRobot.wad` (zipped). `F:` and `C:\Work\FRobot\` are the same location, so editing either path edits the same files.

## Architecture

FRobot is a WPF .NET 10 2D CAD drawing editor. It has four projects:

- **`FRobot/`** — main WPF application (`WinExe`, outputs to `Bin\`)
- **`Test/`** — unit tests using the Nori `TestRunner` framework (GLFW-hosted)
- **`UITest/`** — FlaUI-based UI automation tests (standalone console exe)
- **`Tools/Console/`** — headless CLI tool

External dependency **Nori** (`N:` drive) provides: rendering (`Lux`), geometry types (`Point2`, `Poly`, etc.), the `Lib` service locator, reactive streams (`Hub.Mouse`, `Hub.Keyboard`), and the test runner infrastructure.

### Command & Widget System

Every toolbar action is an `ECmd` enum value (defined in `DwgEdit/DwgCmds.cs`).

**Interactive commands** (need mouse/keyboard input) → subclass `Widget`, add `[DwgCmd(ECmd.Xxx)]`:
```csharp
[DwgCmd (ECmd.Line)]
class LineWidget : Widget { ... }
```
`DwgHub.WidgetMap` auto-discovers all concrete `Widget` subclasses at startup via reflection.

**Non-interactive commands** (dialog or one-shot) → add a static method with `[DwgCmd(ECmd.Xxx)]` to `DwgCmds`:
```csharp
[DwgCmd (ECmd.LayerSetup)]
static void LaunchLayersDlg (Dwg2 dwg) => LayersDlg.Launch (dwg);
```
`DwgHub.MethodMap` discovers these the same way.

To add a new command: add a value to `ECmd`, add an entry to `Wad/Modes/cmd-prompt.txt` (name + input labels + step prompts), add the icon PNG to `Wad/Modes/`, add it to `Wad/Modes/manifest.txt`, then implement either a `Widget` subclass or a `DwgCmds` method.

### Toolbar Manifest

`Wad/Modes/manifest.txt` drives the toolbar. Syntax:

```
Line          ← regular button (ECmd name)
              ← blank line = horizontal separator
>Group:A,B    ← group button that opens a popup with buttons A and B
;Comment      ← ignored
```

### Resource System (`pix:` prefix)

`Lib.OpenRead("pix:Modes/Line.png")` resolves via the registered `FileStmLocator` or `ZipStmLocator`. In `MainWindow` constructor:
```csharp
var file = Lib.GetLocalFile ("FRobot.wad");
Lib.Register (File.Exists (file) ? new ZipStmLocator ("pix:", file) : new FileStmLocator ("pix:", "F:/Wad/"));
```

### Input Bar & Widget Fields

When a widget activates, `DwgHub.Widget` is set and `Widget.Activate()` scans the widget's fields for attributes:
- `[Textbox(n)]` → text input box
- `[Checkbox(n)]` → checkbox
- `[Choice(n)]` → combobox (field must be an enum type)
- `[Click(n)]` → set by the nth mouse click (field must be `Point2`)

`cmd-prompt.txt` provides the label text and phase prompts for each command's input boxes.

### FlaUI UI Tests

`UITest/AppDriver.cs` launches `Bin\FRobot.exe` and exposes `MainWindow`. Toolbar buttons are identified by `AutomationId` = the `ECmd` name string (e.g. `"Line"`). Group buttons use the group name as `AutomationId`. Images inside buttons have `AutomationProperties.Name` = command name.

```csharp
[UIFixture (1, "My Tests")]
class MyTests {
   [UITest (1, "description")]
   void Test () {
      using var driver = new AppDriver ();
      var btn = driver.MainWindow.FindFirstDescendant (driver.CF.ByAutomationId ("Line"));
      // ...
   }
}
```

## Code Style

Full rules in `CODING.md`. Key points:

**File header** (box-art comment, every `.cs` file):
```csharp
// ╔═╦╗
// ║╬╠╬╦╗ FileName.cs
// ║╔╣╠║╣ One-line description
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
```

**Naming**: private instance fields `mName`, private static fields `sName`, enums prefixed `E`, interfaces prefixed `I`. Never write `private` explicitly.

**Formatting**: 3-space indent, K&R braces, space before `(` in calls (`MyMethod (arg)`), file-scoped namespaces.

**Top-level types** wrapped in `#region class Foo ----` (padded to column 100). Internal segments (`// Properties`, `// Methods`, etc.) use `// ─────` or `#region` padded to column 70, in fixed order: Constructors → Properties → Methods → Implementation → Fields.

**C# idioms**: prefer collection expressions `[]`, spread `[.. expr]`, target-typed `new ()`, pattern matching, `switch` expressions, `?.`/`??=`. Use `=>` for single-expression members.
