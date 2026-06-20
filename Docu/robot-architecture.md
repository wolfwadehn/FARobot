# Robot Architecture

## Relevant file tree

- `FApp/`
  - `MainWindow.xaml.cs`
  - `RobotScene.cs`
  - `RobotWindow.xaml`
  - `RobotWindow.xaml.cs`
  - `TriangleDialog.xaml`
  - `TriangleDialog.xaml.cs`
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

## Entry points

### Main robot startup path

From `FApp/MainWindow.xaml.cs`:

1. `OnLuxReady()`
2. `DwgHub.OpenRobotFn = OpenRobot`
3. `OpenRobot()`
4. `mRobotScene ??= new RobotScene()`
5. `Lux.UIScene = mRobotScene`
6. `mRobotWin = new RobotWindow()`
7. `mRobotScene.CreateUI(mRobotWin.Panel)`

This means the robot mode is integrated into the main app viewport and controlled by a floating palette window.

## Runtime ownership tree

- `MainWindow`
  - `mRobotScene : RobotScene?`
  - `mRobotWin : RobotWindow?`
- `RobotWindow`
  - `mPanel`
- `RobotScene`
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
  - `mSliders : Dictionary<string, Slider>`

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

## Solver joint order

`RobotScene` explicitly maps solver joints from `"SLURBT"`:

- `S`
- `L`
- `U`
- `R`
- `B`
- `T`

## Joint definitions

| Joint | Type | Axis | Min | Max | AsDrawn |
|---|---|---|---:|---:|---:|
| `S` | Rotate | `(0,0,1)` | -185 | 185 | 0 |
| `L` | Rotate | `(0,1,0)` | -180 | 25 | -90 |
| `U` | Rotate | `(0,1,0)` | -225 | 70 | 0 |
| `R` | Rotate | `(1,0,0)` | -365 | 365 | 0 |
| `B` | Rotate | `(0,1,0)` | -120 | 120 | 90 |
| `T` | Rotate | `(0,0,-1)` | -365 | 365 | 0 |

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

## Rendering model

`N:/Lux/VNodes/MechanismVN.cs` renders each mechanism node by:

- applying `Lux.Xfm = mMech.RelativeXfm`
- drawing `mMech.Mesh`
- switching color when `mMech.IsColliding`

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

## Floating palette UI

`FApp/RobotWindow.xaml` provides a scrollable panel only.  
`RobotScene.CreateUI(...)` dynamically populates it.

Sections:

1. `Forward Kinematics`
2. `Inverse Kinematics`
3. `Obstacle`
4. `Script`
5. `Collision Triangles`

## Key architectural observations

- The robot scene is self-contained in `RobotScene`.
- The mechanism definition is externalized in `mechanism.curl`.
- FK and IK both drive the same `Mechanism` object graph.
- Collision state is visualized by mutating render colors and `Mechanism.IsColliding`.
- The robot palette is not MVVM-driven; it is built imperatively in code.