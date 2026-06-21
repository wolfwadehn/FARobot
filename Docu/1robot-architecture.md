# Robot Architecture

## Relevant file tree

- `FApp/`
  - `MainWindow.xaml.cs`
  - `RobotScene.cs`
  - `RobotViewModel.cs`  ← new; holds all bindable robot state
  - `RobotWindow.xaml`   ← now contains the full sidebar layout in XAML
  - `RobotWindow.xaml.cs`
  - `TriangleDialog.xaml`
  - `TriangleDialog.xaml.cs`
  - `TcpOffsetDialog.xaml.cs`
  - `Util.cs`            ← shared ParseDouble / TryParseDouble
- `N:/Wad/FanucX/`
  - `mechanism.curl`
- `N:/Core/Sim/`
  - `Mechanism.cs`
  - `RBRSolver.cs`
  - `OBBTree.cs`
  - `OBBCollider.cs`
- `N:/Core/Geom/`
  - `Collision.cs`
- `N:/Lux/VNodes/`
  - `MechanismVN.cs`

---

## Entry points

### Main robot startup path

From `FApp/MainWindow.xaml.cs`:

1. `OnLuxReady()`
2. `DwgHub.OpenRobotFn = OpenRobot`
3. `OpenRobot()`
4. `mRobotScene ??= new RobotScene()`  — builds mechanism, solver, ViewModel, and wires events
5. `Lux.UIScene = mRobotScene`
6. `mRobotWin = new RobotWindow()`
7. `mRobotWin.SetScene(mRobotScene)`   — sets `DataContext = scene.ViewModel`

The floating palette window binds to `RobotViewModel` via standard WPF `DataContext`.
No imperative slider construction occurs.

---

## Runtime ownership tree

- `MainWindow`
  - `mRobotScene : RobotScene?`
  - `mRobotWin : RobotWindow?`
- `RobotWindow`
  - `mScene : RobotScene`  (for "Add Triangle" dialog only)
  - `DataContext → RobotViewModel`
- `RobotScene`
  - `ViewModel : RobotViewModel`
  - `mMech : Mechanism`
  - `mTip : Mechanism`
  - `mJoints : Mechanism[]`
  - `mSolver : RBRSolver`
  - `mBoxMesh : Mesh3`
  - `mBoxOBB : OBBTree`
  - `mBoxVN : Mesh3VN`
  - `mBoxXfm : XfmVN`
  - `mLinkOBBs : Dictionary<Mechanism, OBBTree>`
  - `mTriGroup : GroupVN`
  - `mTris : List<CollisionTri>`
  - `mPlayTimer : DispatcherTimer`
- `RobotViewModel`
  - `X, Y, Z, Rx, Ry, Rz : double`  (IK pose, two-way bound to sliders)
  - `BX, BY, BZ : double`            (obstacle position)
  - `ScriptPath : string`
  - `PlayLabel : string`
  - `Joints : JointSliderModel[]`    (one per robot joint; bound to FK ItemsControl)
  - `Triangles : ObservableCollection<CollisionTriVM>`

---

## Scene graph tree

The robot scene root is built as:

- `GroupVN Root`
  - `MechanismVN(mMech)`
  - `mGripper : XfmVN`
  - `mBoxXfm : XfmVN`
    - `mBoxVN : Mesh3VN`
  - `mTriGroup : GroupVN`
    - triangle visual nodes
  - `TcpVN`
  - `InfoVN`
  - `TraceVN.It`

---

## Mechanism hierarchy

From `N:/Wad/FanucX/mechanism.curl`:

- `FANUCX`
  - `Base`
    - `S`
      - `L`
        - `U`
          - `R`
            - `B`
              - `T`
                - `Tip`

---

## Solver joint order

`RobotScene` explicitly maps solver joints from `"SLURBT"`:

- `S`
- `L`
- `U`
- `R`
- `B`
- `T`

---

## Joint definitions

| Joint | Type | Axis | Min | Max | AsDrawn |
|---|---|---|---:|---:|---:|
| `S` | Rotate | `(0,0,1)` | -185 | 185 | 0 |
| `L` | Rotate | `(0,1,0)` | -180 | 25 | -90 |
| `U` | Rotate | `(0,1,0)` | -225 | 70 | 0 |
| `R` | Rotate | `(1,0,0)` | -365 | 365 | 0 |
| `B` | Rotate | `(0,1,0)` | -120 | 120 | 90 |
| `T` | Rotate | `(0,0,-1)` | -365 | 365 | 0 |

---

## Mechanism transform model

From `N:/Core/Sim/Mechanism.cs`:

- `RelativeXfm`
  - applies parent socket offset
  - then joint motion
- `Xfm`
  - accumulates full world transform recursively
- setting `JValue`
  - invalidates cached subtree transforms
  - triggers redraw and state propagation

---

## Rendering model

`N:/Lux/VNodes/MechanismVN.cs` renders each mechanism node by:

- applying `Lux.Xfm = mMech.RelativeXfm`
- drawing `mMech.Mesh`
- switching color when `mMech.IsColliding`

---

## Robot overlays

### `TcpVN`

Draws:

- X axis arrow in red
- Y axis arrow in green
- Z axis arrow in blue
- a white TCP ring

### `InfoVN`

Draws text overlay with:

- absolute TCP position
- `Rx/Ry/Rz`
- `S/L/U/R/B/T` joint angles

---

## Floating palette UI

`FApp/RobotWindow.xaml` declares the complete sidebar in XAML.
`RobotWindow.DataContext` is `RobotViewModel`; sliders and textboxes use WPF two-way bindings.

Sections:

1. `Forward Kinematics` — `ItemsControl` bound to `RobotViewModel.Joints`
2. `Inverse Kinematics` — six explicit slider rows bound to `X, Y, Z, Rx, Ry, Rz`
3. `Obstacle` — three slider rows bound to `BX, BY, BZ`
4. `Script` — textbox bound to `ScriptPath`; Load/Play buttons use `ICommand`
5. `Collision Triangles` — `ItemsControl` bound to `RobotViewModel.Triangles`

The "Add Triangle…" button uses a `Click` event handler in `RobotWindow.xaml.cs`
because it must show a dialog before passing data to `RobotScene.AddTri()`.

---

## Key architectural observations

- The robot scene is self-contained in `RobotScene`.
- The mechanism definition is externalized in `mechanism.curl`.
- FK and IK both drive the same `Mechanism` object graph.
- Collision state is visualized by mutating render colors and `Mechanism.IsColliding`.
- The robot palette is MVVM-driven: `RobotViewModel` is the single source of truth for
  all mutable state. `RobotScene` subscribes to ViewModel events; `RobotWindow.xaml`
  consumes ViewModel properties via data binding.

---

## MVVM pattern — beginner guide

**The problem MVVM solves:**  
Before the refactoring, `RobotScene.CreateUI()` was a 101-line method that built sliders,
wired their `ValueChanged` events, manually copied values back whenever code moved the robot,
and needed a `mSyncingUI` flag to prevent circular updates (slider moves robot → robot moves
slider → slider moves robot → …).  
That approach ties the logic and the UI together in one place, making it hard to follow either.

**MVVM separates them into three layers:**

| Layer | File | Responsibility |
|---|---|---|
| **Model** | `RobotScene.cs`, `Mechanism.cs` | The actual robot: geometry, solver, collision math |
| **ViewModel** | `RobotViewModel.cs` | Holds the values the UI shows and edits; fires events when they change |
| **View** | `RobotWindow.xaml` | Declares the visual layout; binds to ViewModel properties |

**INotifyPropertyChanged in plain language:**  
`RobotViewModel` implements `INotifyPropertyChanged`. This is C#'s standard way of saying
"tell WPF when one of my properties changes value". Every property setter calls `Notify()`,
which fires a `PropertyChanged` event. WPF listens for that event and updates bound controls
automatically — no manual code needed.

```
User drags slider
  → WPF writes new value to ViewModel.X
  → ViewModel.X setter fires IKChanged event
  → RobotScene.ComputeIK() runs
  → Robot moves in 3D viewport

Code calls ViewModel.SetIKPose(x, y, z, rx, ry, rz)
  → ViewModel fires PropertyChanged for each of the 6 properties
  → WPF updates all bound sliders and textboxes automatically
  → No mSyncingUI flag needed
```

**Two-way binding in plain language:**  
`Value="{Binding X, Mode=TwoWay}"` on a Slider means:
- When the slider moves → write the new value into `ViewModel.X`
- When `ViewModel.X` changes → move the slider to match

The textbox alongside uses the same binding with a `DoubleConverter` (in `RobotViewModel.cs`)
that translates `double ↔ string` and accepts both `.` and `,` as decimal separators.

**ObservableCollection in plain language:**  
`ViewModel.Triangles` is an `ObservableCollection<CollisionTriVM>`.  
An ordinary `List<>` doesn't tell WPF when items are added or removed.  
`ObservableCollection<>` does — so the triangle list in the sidebar updates immediately when
`RobotScene.AddTri()` or `RemoveTri()` runs, without any extra refresh code.

**ICommand in plain language:**  
The Load and Play buttons use `Command="{Binding LoadCommand}"` instead of `Click` event
handlers. `LoadCommand` is a `DelegateCommand` (in `RobotViewModel.cs`) that wraps a plain
`Action`. This keeps button logic inside the ViewModel (testable) instead of in code-behind.

**SetIKPose() batch method:**  
If each of the 6 IK properties fired `IKChanged` individually, snapping to a face would
trigger 6 separate `ComputeIK()` calls. `SetIKPose(x,y,z,rx,ry,rz)` sets all 6 backing
fields at once, fires `PropertyChanged` for each (so the sliders update), then fires
`IKChanged` exactly once (so `ComputeIK()` runs once).
