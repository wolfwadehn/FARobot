# Beginner's Guide

This guide assumes you know C# basics but have not worked with WPF, 3-D rendering,
or robot kinematics before.

---

## 1. How to start reading the code

**Start here, in this order:**

1. `MainWindow.xaml` — see the window layout in XML  
2. `MainWindow.xaml.cs` — follow `OnLuxReady()` and `OpenRobot()`  
3. `RobotWindow.xaml` — understand what each slider/button does  
4. `RobotViewModel.cs` — understand what data exists and how it flows  
5. `RobotScene.cs` — the 3-D logic that reacts to ViewModel changes  

Do not start with `RobotScene.cs` — it references Nori types (`Mechanism`,
`OBBTree`, `RBRSolver`) that will make no sense until you understand what
the UI is doing.

**Useful search strategy:**  
When you see something in the XAML like `{Binding X}`, search for `public double X`
in `RobotViewModel.cs`.  When you see a method call like `ComputeIK()`, search
for it in `RobotScene.cs`.

---

## 2. Mental model: WPF data binding

In `RobotWindow.xaml`, a slider looks like this:

```xml
<Slider Value="{Binding X, Mode=TwoWay}" .../>
<TextBox Text="{Binding X, Converter={StaticResource dc}, Mode=TwoWay, ...}"/>
```

Both the slider and the textbox are bound to the **same property** `X` on the
`DataContext`.  The `DataContext` is `RobotViewModel`.

What "binding" means:
- When code sets `ViewModel.X = 750`, WPF automatically moves the slider to 750
  and puts `"750.0"` in the textbox.  No manual sync code needed.
- When the user moves the slider or types in the textbox, WPF automatically calls
  the `X` setter on the ViewModel.  No event handler needed.

The `ViewModel.X` setter looks like this:

```csharp
public double X { get => mX; set { mX = value; Notify(); IKChanged?.Invoke(); } }
```

`Notify()` tells WPF "the value of X changed, please update any bound controls".  
`IKChanged?.Invoke()` tells `RobotScene` to recompute the IK solution.

This pattern — ViewModel fires `PropertyChanged`, Scene subscribes to events
— is called **MVVM** (Model-View-ViewModel).  The key benefit: the 3-D scene
(`RobotScene`) and the UI (`RobotWindow`) never talk to each other directly.

**`DoubleConverter`** is the `{StaticResource dc}` in the XAML.  It converts
between the `double` stored in the ViewModel and the `string` shown in the
textbox.  It accepts both `.` and `,` as the decimal separator, so the app works
regardless of the regional settings on the computer.

---

## 3. Mental model: the robot arm

The robot is a **Fanuc** 6-axis arm.  Think of it as a chain of rigid links
connected by rotating joints:

```
Floor
  │
  ▼  S — Swing (rotates the whole arm left/right around vertical axis)
  │  L — Lower arm (tilts from the base)
  │  U — Upper arm (elbow)
  │  R — Rotation (forearm twist)
  │  B — Bend (wrist bend)
  │  T — Twist (wrist rotation)
  │
  ▼ Tip  (end effector / tool flange)
     └─ TCP  (Tool Center Point — the actual tool tip, offset from the flange)
```

The physical description of the arm (link lengths, joint limits, mesh geometry)
is stored in `mechanism.curl` and loaded by `Mechanism.Load()` at startup.
You do not need to understand the file format — just know that after loading,
you have a tree of `Mechanism` nodes you can query.

---

## 4. Mental model: FK vs IK

**Forward Kinematics (FK)** — "given joint angles, where is the tip?"

You set the angles:
- S = 0°, L = -45°, U = 90°, R = 0°, B = -45°, T = 0°

The Nori `Mechanism` code computes the world position and orientation of every
link in the chain, and you can read `mTip.Xfm` to get the end-effector matrix.

In FApp, the six joint sliders in the sidebar do FK.  Dragging one slider
immediately moves the arm in 3-D.

**Inverse Kinematics (IK)** — "given a desired tip position and orientation,
what joint angles are needed?"

You set the target:
- X = 750 mm, Y = 0 mm, Z = 1161 mm, Rx = -90°, Ry = 0°, Rz = 0°

The `RBRSolver` computes up to 8 possible sets of joint angles that can reach
that pose.  The code picks the first valid one and applies it.

In FApp, the X/Y/Z/Rx/Ry/Rz sliders in the sidebar do IK.  Dragging one slider
causes the solver to run and all six joint sliders to update.

**The two directions are linked:**  
After an IK solve, the joint sliders update (FK display reflects IK result).  
After a joint drag, the IK textboxes update (IK display reflects FK result).  
The key rule preventing an infinite loop: `SetIKDisplay()` pushes values to the
IK sliders *without* firing `IKChanged`, so it does not trigger another IK solve.

---

## 5. Mental model: the scene graph and Lux

**Lux** is the Nori rendering engine.  You talk to it through the static `Lux`
class.

**`Lux.UIScene`** is the currently active scene.  Set it to switch what is
rendered in the main viewport.  In FApp there is only one scene after startup:
`mRobotScene` (a `Scene3`).

**VNode** (Visual Node) is the basic drawable unit.  A scene has a tree of
VNodes called `Root`.  Each frame, Lux traverses the tree and calls:
1. `SetAttributes()` — sets OpenGL state (color, line width, etc.)
2. `Draw()` — emits geometry via `Lux.Lines(...)`, `Lux.Text(...)`, etc.

Types of VNode in the robot scene:

| VNode | What it draws |
|-------|--------------|
| `MechanismVN` | All robot link meshes (the arm itself) |
| `XfmVN` | A transform wrapper — positions a child VNode using a matrix |
| `Mesh3VN` | A single 3-D mesh (the obstacle box, collision triangles) |
| `TcpVN` | Three colored arrows + ring at the TCP position |
| `InfoVN` | Text overlay (TCP coordinates, joint angles) in the viewport |
| `TraceVN` | Debug text output from `Lib.Trace()` |
| `GroupVN` | Container that holds a list of child VNodes |

You rarely need to write new VNodes unless you want to add new visual elements
to the 3-D scene.

---

## 6. Mental model: the script player

The script is a plain text file where each line is a waypoint:

```
750.0 0.0 1161.0 -90.0 0.0 0.0
800.0 100.0 1200.0 -90.0 10.0 0.0
```

Each row is: `X Y Z Rx Ry Rz` (millimetres and degrees).

**Load** reads the file into `mScript` (a `List<(double X,Y,Z,Rx,Ry,Rz)>`).  
**Add** appends the current IK pose as a new line to the file.  
**Play / Stop** toggles a `DispatcherTimer` that fires every 500 ms.

Each timer tick calls `TickScript()`, which takes the next waypoint and calls
`ViewModel.SetIKPose(...)`.  This triggers `IKChanged`, which runs `ComputeIK()`,
which moves the arm.

When the end of the list is reached, the timer stops and the Play button reverts
to "Play".

---

## 7. Mental model: how the pieces talk to each other

```
RobotWindow (XAML)
   ↕  WPF data binding (no code)
RobotViewModel
   ↕  C# events (IKChanged, BoxChanged, HomeRequested, ...)
RobotScene
   ↕  Nori API (Lux, Mechanism, RBRSolver, OBBTree, ...)
Nori rendering engine
```

- **XAML ↔ ViewModel:** automatic via WPF binding — no code-behind needed  
- **ViewModel → Scene:** events (`IKChanged?.Invoke()` in each setter)  
- **Scene → ViewModel:** method calls (`ViewModel.SetIKDisplay(...)`,
  `ViewModel.Triangles.Add(...)`)  
- **Scene → Nori:** Nori API calls (`Lux.UIScene = ...`, `mMech.JValue = ...`)  
- **MainWindow:** acts as a wiring layer — creates the scene and window, opens
  dialogs, handles menu clicks

The only place where `RobotScene` and `RobotWindow` ever meet directly is
`RobotWindow.SetScene(scene)`, which sets the `DataContext`.  After that, they
communicate entirely through the ViewModel.

---

## 8. Common stumbling blocks

**"I changed the ViewModel but nothing happened on screen."**  
Check that your property calls `Notify()`.  Without it, WPF does not know the
value changed.

**"Dragging the IK slider causes an infinite loop / stack overflow."**  
You used `SetIKPose()` (fires `IKChanged`) instead of `SetIKDisplay()` (does not)
inside `OnFK()`.  Use `SetIKDisplay` whenever you are pushing FK results into the
IK display to avoid round-tripping.

**"The arm jumps to a strange pose when I move a slider."**  
The IK solver may have switched to a different solution branch.  The solver returns
up to 8 solutions; the code picks the first valid one.  If the first valid solution
changes (e.g. elbow flips), the arm jumps.

**"A new VNode I added is not visible."**  
Make sure you added it to `Root` (the `GroupVN`).  Lux only renders nodes
reachable from `Root`.  Also check `SetAttributes()` — if you set `Lux.Color`
with alpha = 0 the geometry is invisible.

**"Collision detection is not working for my new triangle."**  
`AddTri()` builds the `OBBTree` from the three points.  If the three points are
collinear (all on a line) the OBB has zero volume and no collision will be
detected.  Make sure your three points form a real triangle.

**"The `Mechanism` tree has the joints but `mTip.Xfm` gives wrong results."**  
The mechanism's forward kinematics only updates when joint angles are set through
the `Mechanism` API (`JValue`).  Make sure you are setting angles on the correct
joint nodes (`mJoints[i]`, not some other node).
