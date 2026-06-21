# Beginner's Guide to FApp

This guide is for someone who knows C# but has not seen this codebase before.
It gives mental models first, then points you at the actual files.
Skip sections you already know.

---

## Table of Contents

1. [What is FApp?](#what-is-fapp)
2. [Two modes, one window](#two-modes-one-window)
3. [How to start reading the code](#how-to-start-reading-the-code)
4. [Mental model: WPF data binding](#mental-model-wpf-data-binding)
5. [Mental model: the robot arm](#mental-model-the-robot-arm)
6. [Mental model: FK vs IK](#mental-model-fk-vs-ik)
7. [Mental model: the scene graph and Lux](#mental-model-the-scene-graph-and-lux)
8. [Mental model: the script player](#mental-model-the-script-player)
9. [How the pieces talk to each other](#how-the-pieces-talk-to-each-other)
10. [How to make a change safely](#how-to-make-a-change-safely)
11. [Common stumbling blocks](#common-stumbling-blocks)

---

## What is FApp?

FApp is a Windows desktop app for editing 2D sheet-metal drawings and simulating
a 6-axis robot arm. It is a single WPF executable (`Bin/FApp.exe`). The rendering
is done by **Nori/Lux**, an in-house GPU library — think of it as a lightweight
OpenGL wrapper with a scene-graph API.

---

## Two modes, one window

The app runs in two very different modes that share the same main window:

```
┌──────────────────────────────────────────────────────────┐
│  File | Edit | View | About                              │
├────────────────────────────────────────────────────────── │
│        │                                                  │
│Toolbar │   Lux viewport (GPU surface)                     │
│        │   — Mode A: draws 2D geometry (DwgScene)         │
│        │   — Mode B: shows 3D robot arm (RobotScene)      │
│        │                                                  │
├────────┴──────────────────────────────────────────────────│
│  Status / Input bar                                       │
└──────────────────────────────────────────────────────────┘
                                     ┌──────────────────────┐
                                     │  Robot sidebar       │
                                     │  (RobotWindow)       │
                                     │  — FK sliders        │
                                     │  — IK sliders        │
                                     │  — Obstacle box      │
                                     │  — Script controls   │
                                     └──────────────────────┘
```

**Mode A — 2D drawing editor** (default on startup):
- `Lux.UIScene` is a `DwgScene`
- All the `Widget` subclasses handle drawing commands (Line, Arc, …)
- Entities live in a `Dwg2` object

**Mode B — robot simulation** (View → Robot…):
- `Lux.UIScene` is a `RobotScene`
- A separate floating window (`RobotWindow`) shows the sidebar
- The `DwgScene` is suspended while the robot scene is active

Switching between them: `Lux.UIScene` is just a property. Setting it tears down the
old scene and brings up the new one.

---

## How to start reading the code

**Start here:**

```
FApp/MainWindow.xaml.cs  — OnLuxReady() wires everything at startup
FApp/RobotScene.cs       — robot simulation logic
FApp/RobotViewModel.cs   — all robot UI state (sliders, commands)
FApp/RobotWindow.xaml    — sidebar layout in XAML
```

**Reading order for the robot:**

1. `RobotScene` constructor — see how the mechanism, solver, and ViewModel are created
2. `RobotViewModel` — understand what data the UI needs
3. `RobotWindow.xaml` — understand how XAML binds to the ViewModel
4. `ComputeIK()` and `OnFK()` — the two update paths when sliders move

**For the 2D drawing editor:**

1. `DwgHub.cs` — the central hub that all widgets read from
2. Any `Widget` subclass (e.g., `LineWidget`) — learn how a command is structured
3. `DwgScene.cs` — how the drawing renders

---

## Mental model: WPF data binding

The hardest concept in this codebase for a beginner is how the robot sidebar works
without explicit "update the slider" calls in the code.

**Old approach (before the refactoring):**
```
User drags slider
  → slider ValueChanged event fires
  → code sets mSyncingUI = true   ← guard flag to break circular updates
  → code calls ComputeIK()
  → ComputeIK() calls SyncIKSliders()
  → SyncIKSliders() manually sets each slider.Value = ...
  → mSyncingUI = false
```
This created a fragile web: 12 sliders, each with an event handler, all needing the
same guard flag to prevent infinite loops.

**Current approach (MVVM):**
```
User drags slider
  → WPF writes new value to ViewModel.X   ← binding handles this automatically
  → ViewModel.X setter fires IKChanged
  → RobotScene.ComputeIK() runs
  → robot moves in viewport
  → ComputeIK() calls js.Refresh() on each FK slider model
  → WPF reads the new values and updates the FK slider controls
```
No guard flag. No manual slider updates. WPF handles the sync.

**The three pieces of MVVM:**

| Name | File | Job |
|---|---|---|
| **Model** | `RobotScene.cs`, Nori `Mechanism` | the real robot: math, geometry, physics |
| **ViewModel** | `RobotViewModel.cs` | numbers the UI shows and edits |
| **View** | `RobotWindow.xaml` | visual layout; reads/writes the ViewModel |

**INotifyPropertyChanged — what it does:**

`RobotViewModel` implements `INotifyPropertyChanged`. This means: every time a property
changes, it fires `PropertyChanged`. WPF subscribes to that event and updates any bound
control automatically.

```csharp
// In RobotViewModel:
public double X {
    get => mX;
    set { mX = value; Notify(); IKChanged?.Invoke(); }
    //               ↑ tells WPF "X changed, update any Slider/TextBox bound to X"
    //                          ↑ tells RobotScene "re-solve IK"
}
```

**Two-way binding — what it does:**

```xml
<Slider Value="{Binding X, Mode=TwoWay}"/>
```

- **→ from VM to control**: when `ViewModel.X` changes, WPF moves the slider
- **← from control to VM**: when the user drags the slider, WPF writes back to `ViewModel.X`

WPF will not create a loop: if the VM writes X=5, the slider moves to 5, but WPF
does not then call the setter again with 5.

**ObservableCollection — what it does:**

`ViewModel.Triangles` is an `ObservableCollection<CollisionTriVM>`.
A plain `List<>` is silent — WPF never knows when you add or remove items.
`ObservableCollection` raises events when items are added or removed, so the
sidebar's triangle list updates instantly without any extra code.

**ICommand — what it does:**

Buttons in the sidebar use `Command="{Binding HomeCommand}"` instead of click events.
`HomeCommand` is a `DelegateCommand` (a small wrapper around a plain `Action`):

```csharp
HomeCommand = new DelegateCommand(() => HomeRequested?.Invoke());
```

When the button is clicked, WPF calls `HomeCommand.Execute()` → lambda fires → event fires
→ `RobotScene.GoHome()` runs. The button has no direct knowledge of `RobotScene`.

---

## Mental model: the robot arm

The robot is a **Fanuc 6-axis arm**. Think of it as six hinges chained together:

```
Floor
 └─ Base (fixed)
     └─ S  (swivel — rotates the whole arm left/right)
         └─ L  (lower arm — tilts forward/backward)
             └─ U  (upper arm — tilts forward/backward)
                 └─ R  (wrist roll — spins the forearm)
                     └─ B  (wrist bend — tilts the hand)
                         └─ T  (tool roll — spins the tool flange)
                             └─ Tip  (the flange; the TCP attaches here)
```

This chain of hinges is called the **kinematic chain**. Each hinge has:
- a rotation axis (X, Y, or Z)
- a minimum and maximum angle
- a current angle (`JValue`)

The chain is stored as `Mechanism` objects linked parent→child in `mechanism.curl`
(a Nori binary format). `RobotScene` loads this file on startup.

**TCP** = Tool Center Point. It is a point offset from the Tip flange in the tool's
own coordinate system. The TCP offset is configurable (via the TCP Offset dialog).
The TCP is what you position with the IK sliders — not the raw flange.

**Coordinate frames:**

The solver works in a frame where Z=0 is the L-joint axis height (565 mm above the floor).
The UI shows world coordinates (floor = Z=0). The conversion: `world Z = solver Z + 565`.
This is why `ComputeIK` subtracts `LJointZ` before calling the solver, and `GoHome` adds it.

---

## Mental model: FK vs IK

There are two ways to control the arm, and they must stay in sync:

**FK — Forward Kinematics** ("I know the joint angles; where does the TCP end up?")

```
Set joint angles → mechanism computes Tip.Xfm → read TCP world position
```

- You directly drag the S, L, U, R, B, T sliders
- The mechanism propagates the change through the chain
- `OnFK()` reads `mTip.Xfm`, extracts the TCP position and orientation,
  and writes them to the IK sliders so they stay current

**IK — Inverse Kinematics** ("I know where I want the TCP; what joint angles do I need?")

```
Set TCP position/orientation → solver finds joint angles → mechanism updates
```

- You drag the X, Y, Z, Rx, Ry, Rz sliders
- `ComputeIK()` builds the target orientation matrix, subtracts the TCP offset to get
  the wrist position, calls `RBRSolver.ComputeStances()` (analytic closed-form solver),
  picks the first valid solution, writes the joint angles
- `ComputeIK()` then calls `js.Refresh()` on each FK slider so they show the new angles

**The sync contract:**

| Event | Who updates | What is updated |
|---|---|---|
| User drags FK slider | `OnFK()` | IK sliders (X, Y, Z, Rx, Ry, Rz) |
| User drags IK slider | `ComputeIK()` | FK sliders (S, L, U, R, B, T) |
| Snap to face / SnapToTriNode | `SetIKPose()` | both (IK triggers IKChanged → ComputeIK → FK) |
| Home / GoHome | `SetIKPose()` | both |
| Script tick | `SetIKPose()` | both |

**`SetIKPose()` vs `SetIKDisplay()`:**

- `SetIKPose()` — changes IK values **and** fires `IKChanged` → triggers `ComputeIK()`.
  Use when you want the robot to actually move to a new position.
- `SetIKDisplay()` — changes IK values **without** firing `IKChanged`.
  Use in `OnFK()` to update the display after joint angles have already been applied;
  you don't want to re-solve IK on top of a FK change.

---

## Mental model: the scene graph and Lux

Lux is an immediate-mode GPU renderer with a scene-graph layer on top. Think of it
like a tree of renderable objects — when you change a node, Lux redraws the affected
parts.

**VNode** = Visual Node. Everything renderable is a subclass of `VNode`:

| VNode type | What it does |
|---|---|
| `GroupVN` | holds child nodes; removing/adding children updates the scene |
| `Mesh3VN` | renders a triangulated mesh |
| `XfmVN` | applies a transform matrix to its child node |
| `MechanismVN` | renders an entire mechanism tree (the whole robot) |
| `TcpVN` | draws the TCP axes and ring overlay (custom, streams every frame) |
| `InfoVN` | draws the text overlay showing joint angles and position |
| `TraceVN` | draws trace lines from the Nori debug tracer |

**Scene tree for the robot:**

```
GroupVN (Root)
 ├── MechanismVN(mMech)      — draws all robot links, colored red on collision
 ├── XfmVN(mGripper)         — draws a gripper mesh at the tip flange
 ├── XfmVN(mBoxXfm)          — applies BX/BY/BZ translation to...
 │    └── Mesh3VN(mBoxVN)    —  ...the obstacle box (blue; red when hit)
 ├── GroupVN(mTriGroup)       — collision triangles added by the user
 ├── TcpVN                   — TCP cross-hair (streaming, redrawn every frame)
 ├── InfoVN                  — text overlay (streaming, redrawn every frame)
 └── TraceVN.It              — debug trace lines
```

**Streaming nodes vs static nodes:**

Most nodes compute their geometry once and cache it.
`TcpVN` and `InfoVN` have `Streaming = true`, meaning their `Draw()` method is called
every frame. They read live data (`mTip.Xfm`, `ViewModel.X` etc.) each time.

**XfmVN** is just a transform wrapper. `mBoxXfm.Xfm = Matrix3.Translation(BX, BY, BZ)`
instantly moves the box in the 3D view — no mesh rebuild needed.

---

## Mental model: the script player

The script system lets you record a sequence of TCP poses and play them back.

**File format** (`robot_script.txt`):
```
# Each line: X Y Z Rx Ry Rz  (mm and degrees, space-separated, invariant culture)
1166.0 0.0 1161.0 -90.0 0.0 0.0
1000.0 200.0 900.0 -90.0 0.0 0.0
```

Lines starting with `#` or blank lines are ignored.

**Workflow:**
1. Move the arm to a desired position using IK or FK sliders
2. Click **Add** — appends the current X Y Z Rx Ry Rz to `robot_script.txt`
3. Repeat for each waypoint
4. Click **Load** — reads the file into `mScript` (a `List` of tuples)
5. Click **Play** — starts a `DispatcherTimer` that fires every 500 ms
6. Each tick calls `TickScript()` → `SetIKPose()` for the next waypoint
7. Click **Stop** (same button, label toggles) — stops the timer

`DispatcherTimer` fires on the UI thread, so `SetIKPose()` can safely update the ViewModel
and trigger a WPF binding update without any thread synchronization.

---

## How the pieces talk to each other

Here is the full event flow from a user interaction:

**Dragging an IK slider (e.g., X):**
```
User drags X slider
  → WPF writes value to ViewModel.X (via TwoWay binding)
  → ViewModel.X setter calls Notify() → WPF updates X TextBox
  → ViewModel.X setter fires IKChanged
  → RobotScene.ComputeIK() runs
      → builds rotation matrix from Rx/Ry/Rz
      → subtracts LJointZ from Z
      → subtracts TCP offset to get wrist position
      → calls mSolver.ComputeStances(wrist, orientation)
      → picks first valid solution; writes mJoints[i].JValue
      → calls js.Refresh() on each JointSliderModel
          → fires PropertyChanged("Value") on each
          → WPF updates each FK slider to the new joint angle
      → calls mGripper.Xfm = mTip.Xfm  (move the gripper mesh)
      → calls CheckCollisions()
          → tests box OBB vs each link OBB
          → tests triangle OBBs vs each link OBB
          → sets Mechanism.IsColliding → link turns red
          → sets mBoxVN.Color → box turns red/blue
```

**Dragging an FK slider (e.g., S):**
```
User drags S slider
  → WPF writes value to JointSliderModel.Value (via TwoWay binding)
  → JointSliderModel.Value setter calls mMech.JValue = value
      → mechanism propagates FK through the chain; mTip.Xfm updates
  → JointSliderModel.Value setter calls OnFK()
      → reads mTip.Xfm (the new flange transform)
      → computes TCP = flange origin + flange rotation × TCP offset
      → calls MatrixToEuler() to get Rx/Ry/Rz from the rotation matrix
      → calls ViewModel.SetIKDisplay(x, y, z, rx, ry, rz)
          → sets all 6 backing fields
          → fires PropertyChanged for each → WPF updates IK sliders
          → does NOT fire IKChanged → no redundant IK solve
      → calls mGripper.Xfm = mTip.Xfm
      → calls CheckCollisions()
```

---

## How to make a change safely

**Add a new IK field** (e.g., a seventh axis):
1. Add a `double W` property to `RobotViewModel` (same pattern as X/Y/Z)
2. Add a slider row to `RobotWindow.xaml`
3. Read `ViewModel.W` in `ComputeIK()`
4. Set `ViewModel.W` in `OnFK()` or `SetIKPose()`

**Add a new button:**
1. Add an `ICommand FooCommand` property and `event Action? FooRequested` to `RobotViewModel`
2. Wire it in the constructor: `FooCommand = new DelegateCommand(() => FooRequested?.Invoke())`
3. Subscribe in `RobotScene`: `ViewModel.FooRequested += DoFoo`
4. Add the `<Button Command="{Binding FooCommand}"/>` to `RobotWindow.xaml`
5. No code-behind needed

**Change a slider range:**
Only touch `RobotWindow.xaml` — find the `<Slider Minimum="..." Maximum="..."/>` and edit the numbers.
The range is a pure view concern; the ViewModel and scene don't care.

**Add a new section to the sidebar:**
Copy an existing section block in `RobotWindow.xaml`. Use the `SectionHeader`, `SliderLabel`,
and `SliderBox` styles defined in `Window.Resources` to stay consistent.

---

## Common stumbling blocks

**"Why doesn't my slider update when I change the ViewModel?"**  
Check that the property setter calls `Notify()`. If you set the backing field directly (e.g.,
`mX = 5` instead of `X = 5`), `PropertyChanged` never fires and the slider stays stale.
`SetIKPose()` and `SetIKDisplay()` set backing fields directly *on purpose*, then call
`Notify(nameof(X))` etc. explicitly — this is the only safe exception.

**"Why does the robot not move when I change ViewModel.X from code?"**  
The setter fires `IKChanged`, which calls `ComputeIK()`, which updates the joint angles.
If you bypass the setter (field assignment), none of this happens. Always go through the
property setter, or call `SetIKPose()` / `SetIKDisplay()`.

**"Why is Z = 1161 at home, but the solver docs say Z = 596?"**  
The IK sliders show *world* Z (floor = 0). The solver works in its own frame where Z = 0
is at the L-joint axis height (565 mm). The conversion is: `solver Z = world Z − 565`.
`ComputeIK()` does this subtraction; `GoHome` and `SetIKPose` always pass world Z.

**"What is `D2R()`?"**  
An extension method on `double` in Nori. `90.0.D2R()` = `90 × π/180` = 1.5708 radians.
Used before passing angles to the solver and rotation matrix constructors, which expect radians.
The UI always works in degrees.

**"The build fails with 'file is locked by FApp (xxxxx)'"**  
FApp.exe is running. Close it first, then rebuild. This is just a file-copy step; the C# compilation
itself has already succeeded.

**"Where is the robot arm mesh defined?"**  
Not in this repo. The geometry comes from `N:/Wad/FanucX/mechanism.curl`, a Nori binary on the
network drive. `mechanism.curl` contains both the joint definitions and the per-link 3D meshes.
You cannot open or edit it here.

**"I added a WPF binding and get a binding error at runtime, but no crash."**  
WPF swallows binding errors silently by default. Check the Visual Studio Output window (Debug
output tab) for lines containing "System.Windows.Data Error". The most common cause: a property
name typo in the XAML, or the property is a public field instead of a property (WPF requires
`get;` auto-properties — public fields are not bindable).
