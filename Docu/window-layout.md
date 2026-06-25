# Window Layout

FApp has a single main window.  The robot controls are an **embedded panel**
(`RobotPanel`, a `UserControl`) docked on the right — there is no separate floating
window any more.  There is no status bar (removed with the 2D drawing editor).

---

## Main window

Defined in `FApp/MainWindow.xaml`.  
The root element is a `DockPanel` with a menu on top, a toolbar on the left, the
controls panel on the right, and the 3-D viewport filling the centre:

```
┌──────────────────────────────────────────────────────────┐
│  Menu bar  (DockPanel.Dock="Top")                        │
├────┬──────────────────────────────────────┬──────────────┤
│ TB │                                       │  RobotPanel  │
│    │   3-D viewport  (mContent)            │  (Dock=Right │
│    │                                       │   346 px)    │
└────┴──────────────────────────────────────┴──────────────┘
  TB = Toolbar host (mToolbarHost, 27 px wide, Dock="Left")
```

### Menu bar

Defined in XAML; each `MenuItem` wires directly to a method in `MainWindow.xaml.cs`
via `Click="MethodName"` — no indirection layer.

| Menu | Item | Handler | Effect |
|------|------|---------|--------|
| File | Open Cell… | `OpenCell()` | `mRobotScene.LoadCell()` from a `.cell` file |
| File | Save Cell… | `SaveCell()` | `mRobotScene.SaveCell()` to a `.cell` file |
| File | Exit | `Exit()` | `Close()` — WPF closes the window |
| View | Zoom Extents | `ZoomExtents()` | `Lux.UIScene?.ZoomExtents()` |
| View | Robot Controls | `OpenRobot()` | Toggle the docked controls panel |
| View | Robot Home | `RobotHome()` | `mRobotScene?.GoHome()` |
| View | TCP Legend… | `TcpLegend()` | Show RGB axis legend popup |
| — | About! | `About()` | Show version / expiry info |

`Alt+F4` closes the window natively (Windows default); no keyboard shortcut is
registered for anything else.

### Toolbar

`mToolbarHost` is a 27 px-wide `Border` on the left edge.
Its `Child` is a `Toolbar` instance (`FApp/Widgets/Toolbar.cs`), which is a plain
`StackPanel`.

Buttons are added programmatically via `Toolbar.AddButton(icon, tooltip, action)`.
Each call wraps a `TextBlock` icon in a `Border`, appends it to a `WrapPanel`, and
adds that panel to the stack.

| When added | Icon | Tooltip | Action |
|-----------|------|---------|--------|
| `OnLuxReady()` | `⚙` | TCP Offset | `ShowTcpOffsetDlg()` |

(The old `⊕` "Show Robot Controls" button is gone — the panel is always present
and toggled from **View ▸ Robot Controls**.)  Hover highlight is `#E4E4EC`; no
selected-state highlight.

### 3-D viewport

`mContent` is a `Border` that holds the Nori `WPFHost` GL control.
The host is initialised in the constructor:

```csharp
mContent.Child = WPFHost.Init(this, OnLuxReady);
```

`OnLuxReady` fires once the OpenGL surface is ready.
The active scene (`Lux.UIScene`) is always `mRobotScene` after startup.

---

## Robot controls panel (`RobotPanel`)

Defined in `FApp/RobotPanel.xaml`.  
It is a `UserControl` docked on the right of the main window (`DockPanel.Dock="Right"`,
346 px) — no longer a floating window.  **View ▸ Robot Controls** toggles its
visibility.

**Visual identity:** dark theme (`#252530` background, `#E8E8F0` text).  
The `DataContext` is `RobotScene.ViewModel`, set via `mRobotPanel.SetScene(scene)`
in `OnLuxReady`, so all sliders and textboxes update through WPF data binding.

### Layout (top to bottom inside a `ScrollViewer`)

```
Forward Kinematics       S/L/U/R/B/T sliders

Inverse Kinematics       [Home Position] [Set Home]
                         X Y Z Rx Ry Rz sliders

Geometry & Objects       [Import Geometry…]
                         (object combo)
                         Move:   X Y Z Rx Ry Rz   (selected object, 6 DOF)
                         Frame:  X Y Z Rx Ry Rz   (6 params)
                         [Calibrate…] [Frame @ Corner] [Clear]

Pick & Place             [Pick Pickup Surface] [Pick Place Surface]
                         [Generate Pick & Place] [Auto Collision-Free]
                         [Import Part…]

Obstacle                 BX BY BZ sliders

Script                   [robot_script.txt]
                         [Load] [Add Waypoint] [Play]
                         WP ─────●──── 0.0     (scrubber, tick per waypoint)
                         WP 1  [Move]  [×]      (one row per waypoint;
                         WP 2  [Pick]  [×]       action cycles Move→Pick→Place)

Collision Triangles      [Add Triangle…]  + list
```

The Geometry/Frame/Pick&Place/waypoint sections are documented in detail in
`cell-pick-place.md`.

Each slider row is a `StackPanel` containing:
- A label `TextBlock` (22 px wide, right-aligned)
- A `Slider` (min width 130 px, two-way binding)
- A `TextBox` (55 px, two-way binding through `DoubleConverter`)

Both the slider and the textbox bind to the **same ViewModel property**, so
editing either one updates the other automatically.

---

## Dialogs

### TCP Offset dialog (`TcpOffsetDialog.xaml`)

Opens from the ⚙ toolbar button.  
Three text boxes (X, Y, Z) for the wrist-to-TCP vector in millimetres.  
OK validates all three fields; Cancel discards.

### Add Triangle dialog (`TriangleDialog.xaml`)

Opens from the "Add Triangle…" button in the controls panel.  
Fields: Name, Group, P1(X/Y/Z), P2(X/Y/Z), P3(X/Y/Z).  
Import/Export buttons read/write a CSV file.

### Pallet Frame dialog (`PalletFrameDialog.xaml`)

Opens from **Calibrate…** in the Geometry & Objects section.  
Fields: P1 (origin), P2 (+X), P3 (+XY).  Import/Export read/write CSV.  Computes
the selected object's 6-parameter user frame; rejects collinear points.

---
