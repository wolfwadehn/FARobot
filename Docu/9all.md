# Robot Functionality Inspection

## Relevant file tree

- `FApp/`
  - `MainWindow.xaml.cs`
    - Application startup
    - Robot scene activation via `OpenRobot()`
    - Scene switching between drawing view and robot view
  - `RobotScene.cs`
    - Core robot runtime
    - FK and IK UI construction
    - Script playback
    - Box obstacle management
    - Custom triangle collision management
    - TCP overlay and info overlay
    - Contains:
      - `RobotScene`
      - `TcpVN`
      - `InfoVN`
      - `CollisionTri`
  - `RobotWindow.xaml`
    - Floating robot control palette layout
  - `RobotViewModel.cs`
    - Bindable robot state (INotifyPropertyChanged)
    - JointSliderModel, CollisionTriVM, DelegateCommand, DoubleConverter
  - `RobotWindow.xaml.cs`
    - Wires DataContext to RobotViewModel; handles Add Triangle dialog
  - `TriangleDialog.xaml`
    - Dialog for adding/importing/exporting collision triangles
  - `TriangleDialog.xaml.cs`
    - Validation and CSV import/export for triangle definitions

- `N:/Wad/FanucX/`
  - `mechanism.curl`
    - Robot kinematic definition
    - Joint hierarchy
    - Joint axes
    - Joint limits
    - Mesh file bindings

- `N:/Core/Sim/`
  - `Mechanism.cs`
    - Kinematic chain model
    - Joint transforms
    - Cached world transforms
    - Collision mesh lookup
  - `RBRSolver.cs`
    - Inverse kinematics solver for the 6-axis RBR robot
  - `OBBTree.cs`
    - Collision hierarchy generation from meshes
  - `OBBCollider.cs`
    - OBB-tree traversal and final triangle collision checking

- `N:/Core/Geom/`
  - `Collision.cs`
    - Primitive collision tests:
      - OBB vs OBB
      - OBB vs triangle
      - triangle vs triangle

- `N:/Lux/VNodes/`
  - `MechanismVN.cs`
    - Rendering adapter from `Mechanism` to scene graph nodes
    - Link color switches to collision color when `IsColliding == true`

---

## Scope of the robot subsystem

The robot subsystem is a 3D simulation mode hosted inside the main application viewport and controlled by a separate floating palette window.

It combines:

1. A loaded articulated mechanism from `mechanism.curl`
2. Forward kinematics through direct joint updates
3. Inverse kinematics through `RBRSolver`
4. A movable box obstacle
5. User-defined collision triangles
6. Continuous collision checks after every meaningful state change
7. Scene overlays for TCP axes and live numeric status
8. Simple waypoint script playback

The robot mode is not isolated as a separate app. It is integrated into the main window and reuses the app’s Lux rendering infrastructure.

---

## Runtime ownership and object tree

### Application ownership tree

- `MainWindow`
  - `mRobotScene : RobotScene?`
  - `mRobotWin : RobotWindow?`
  - Lux main viewport
  - toolbar / command system

- `RobotWindow`
  - `mScene : RobotScene`  (for Add Triangle dialog only)
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
  - `X, Y, Z, Rx, Ry, Rz : double`  (IK pose)
  - `BX, BY, BZ : double`            (obstacle position)
  - `ScriptPath, PlayLabel : string`
  - `Joints : JointSliderModel[]`
  - `Triangles : ObservableCollection<CollisionTriVM>`

### Scene graph tree

The Lux root node for robot mode is built in `RobotScene` as:

- `GroupVN Root`
  - `MechanismVN(mMech)`
  - `mGripper : XfmVN`
  - `mBoxXfm : XfmVN`
    - `mBoxVN : Mesh3VN`
  - `mTriGroup : GroupVN`
    - `CollisionTri.VN`
    - `CollisionTri.VN`
    - `...`
  - `TcpVN`
  - `InfoVN`
  - `TraceVN.It`

Important notes:

- `MechanismVN` recursively exposes child `Mechanism` objects through `GetChild`.
- The robot links are rendered as a hierarchical scene graph, not as one flat mesh.
- `mGripper` currently carries an empty `GroupVN`, so it behaves as a transform anchor more than a visible tool mesh.
- `TcpVN` and `InfoVN` are streaming overlay nodes that redraw every frame.

---

## Mechanism hierarchy from `mechanism.curl`

The robot definition is:

- `FANUCX`
  - `Base`
    - `S`
      - `L`
        - `U`
          - `R`
            - `B`
              - `T`
                - `Tip`

This is the actual articulated chain used by the scene.

### Joint order used by the runtime

`RobotScene` builds the joint array from the name string `"SLURBT"`:

- `S`
- `L`
- `U`
- `R`
- `B`
- `T`

These six are the solver-controlled axes.

### Joint metadata from the mechanism file

| Joint | Type | Axis vector | Min | Max | As Drawn |
|---|---|---:|---:|---:|---:|
| `S` | Rotate | `(0,0,1)` | -185 | 185 | 0 |
| `L` | Rotate | `(0,1,0)` | -180 | 25 | -90 |
| `U` | Rotate | `(0,1,0)` | -225 | 70 | 0 |
| `R` | Rotate | `(1,0,0)` | -365 | 365 | 0 |
| `B` | Rotate | `(0,1,0)` | -120 | 120 | 90 |
| `T` | Rotate | `(0,0,-1)` | -365 | 365 | 0 |

### Socket chain from the mechanism file

Each child attaches using parent sockets:

- `Base` socket: `(0,0,266.5)`
- `S` socket: `(150,0,298.5)`
- `L` socket: `(0,0,770)`
- `U` socket: `(290.5,0,0)`
- `R` socket: `(725.5,0,0)`
- `B` socket: `(0,0,-153)`
- `T` socket: `(0,0,-21)`

These socket offsets, combined with joint rotations, define the full forward kinematics.

---

## How forward kinematics works

Forward kinematics is handled by `Mechanism`.

### Core transform rules

For a `Mechanism` node:

- `RelativeXfm`
  - starts from the parent socket position
  - applies either:
    - translation along `JVector` for translational joints
    - rotation about `JVector` for rotary joints
- `Xfm`
  - is the accumulated transform:
  - `RelativeXfm * Parent.Xfm`

### Joint value updates

When `JValue` changes:

- the node stores the new value
- the entire subtree invalidates cached `_xfm`
- property change notification is raised

That means each joint edit immediately affects all downstream link transforms.

### Rendering path

`MechanismVN.SetAttributes()` uses:

- `Lux.Xfm = mMech.RelativeXfm`
- `Lux.Color = collisionColor or normalColor`

Because the scene graph itself is hierarchical, relative transforms are enough. Lux composes them as it descends through child nodes.

---

## How inverse kinematics works

`RobotScene` constructs:

- `mSolver = new RBRSolver(150, 770, 0, 0, 1016, 175, mMin, mMax)`

The solver is parameterized with robot geometry and joint limits.

### IK input state

The IK target is represented by:

- translation:
  - `mX`
  - `mY`
  - `mZ`
- orientation:
  - `mRx`
  - `mRy`
  - `mRz`

These are UI-controlled target values.

### Home coordinate system

The scene defines a home frame:

- `mHome = CoordSystem((1166, 0, 596), XAxis, YAxis)`

This is the base reference used to convert UI target offsets into world coordinates.

### IK computation sequence

`ComputeIK()` does the following:

1. Start from world coordinates
2. Apply X rotation by `mRx`
3. Apply Y rotation by `mRy`
4. Apply Z rotation by `mRz`
5. Compute target TCP position:
   - `mHome.Org + (mX, mY, mZ)`
6. Offset by `cs.VecZ * 50`
7. Call:
   - `mSolver.ComputeStances(mCS.Org, mCS.VecZ, mCS.VecX)`
8. Iterate up to 8 solutions
9. Pick the **first** solution where `OK == true`
10. Write the six solved joint angles into `mJoints[i].JValue`
11. Update gripper transform
12. Re-run collision checks

### Important IK behavior

- The solver computes up to 8 stances.
- The scene selects the **first valid** solution only.
- There is no ranking for:
  - shortest motion
  - nearest previous pose
  - elbow-up vs elbow-down preference
  - collision avoidance
- If no solution is valid, the method does not explicitly reset the robot. The previously applied joint state remains in effect.

---

## TCP model and orientation behavior

The robot uses a 50 mm TCP offset convention.

### Evidence in code

- IK target is shifted by `+ cs.VecZ * 50`
- TCP visualization draws from:
  - `fcs.Org - fcs.VecZ * 50`

This means the mechanism tip and the displayed TCP are offset from each other by 50 mm along the tool Z direction.

### TCP overlay

`TcpVN.Draw()` renders:

- X axis arrow in red
- Y axis arrow in green
- Z axis arrow in blue
- a white ring at the TCP origin

### Face snapping behavior

When the user picks the box mesh:

- `RobotScene.Picked(obj)` checks `obj == mBoxMesh`
- `SnapToFace(Lux.PickPos)` is called

`SnapToFace()`:

1. Computes the hit point relative to the box center
2. Determines which face is dominant by largest absolute axis distance
3. Assigns TCP orientation so tool Z points outward from that face
4. Converts hit position into robot target offsets
5. Runs IK
6. Synchronizes only the IK sliders

This is a direct pose-from-face interaction mode.

---

## Robot control UI structure

The robot palette is declared in `RobotWindow.xaml` and driven by `RobotViewModel` via WPF data binding.
`RobotWindow.SetScene(scene)` sets `DataContext = scene.ViewModel` to activate the bindings.

### Sections created

1. `Forward Kinematics`
2. `Inverse Kinematics`
3. `Obstacle`
4. `Script`
5. `Collision Triangles`

### FK section

One slider is created per articulated joint found in `mMech.EnumTree()` where `Joint != EJoint.None`.

The callback is:

- set `m.JValue = newValue`
- call `OnFK()`

### IK section

Six sliders:

- `X`
- `Y`
- `Z`
- `Rx`
- `Ry`
- `Rz`

Each callback updates one field and calls `ComputeIK()`.

### Obstacle section

Three sliders:

- `BX`
- `BY`
- `BZ`

Each callback updates the box position and calls `UpdateBox()`.

### Script section

Contains:

- path textbox
- `Load` button
- `Play` / `Stop` button

### Collision triangles section

Contains:

- `Add Triangle...` button
- dynamic list of triangles
- delete buttons per triangle

---

## Functional sequences

## 1. Robot mode startup sequence

1. `MainWindow.OnLuxReady()`
2. `DwgHub.OpenRobotFn = OpenRobot`
3. `OpenRobot()`
4. `mRobotScene ??= new RobotScene()`
5. `Lux.UIScene = mRobotScene`
6. `mRobotWin = new RobotWindow()`
7. `mRobotWin.Show()`
8. `mRobotWin.SetScene(mRobotScene)`  — sets DataContext, activates all XAML bindings

### Startup effect

- The main viewport switches to robot scene rendering.
- The floating palette is displayed.
- The mechanism, obstacle, overlays, and collision structures are ready before UI interaction.

---

## 2. Forward kinematics slider sequence

1. User moves a joint slider
2. WPF `Slider.ValueChanged` fires
3. `setter(e.NewValue)` runs
4. `m.JValue = value`
5. `Mechanism` invalidates subtree transforms
6. `OnFK()` runs
7. `mGripper.Xfm = mTip.Xfm`
8. `CheckCollisions()` runs
9. `MechanismVN` redraws links
10. Colliding links switch to collision color

### Result

The robot pose changes directly from joint space.

---

## 3. Inverse kinematics slider sequence

1. User moves `X/Y/Z/Rx/Ry/Rz`
2. WPF `Slider.ValueChanged` fires
3. Corresponding `mX/mY/mZ/mRx/mRy/mRz` is updated
4. `ComputeIK()` runs
5. Target coordinate system is built
6. `RBRSolver.ComputeStances(...)` calculates candidate poses
7. First valid stance is selected
8. Joint values are written to `mJoints`
9. Forward transforms update through `Mechanism`
10. `mGripper.Xfm = mTip.Xfm`
11. `CheckCollisions()` runs

### Result

The robot pose changes from task space.

---

## 4. Box face snapping sequence

1. User picks the blue box in the 3D view
2. `Picked(obj)` confirms the box mesh was hit
3. `SnapToFace(hitPoint)` runs
4. Closest box face direction is inferred
5. Tool orientation is assigned from that face
6. `ViewModel.SetIKPose(hit.X, hit.Y, hit.Z, newRx, newRy, newRz)` is called
   — fires `IKChanged` once → `ComputeIK()` runs
   — fires `PropertyChanged` for all 6 IK properties → all sliders update automatically

### Result

The TCP aligns to a box face with a plausible outward-facing orientation.

---

## 5. Obstacle box update sequence

1. User moves `BX`, `BY`, or `BZ`
2. The corresponding field is updated
3. `UpdateBox()` runs
4. `mBoxXfm.Xfm = BoxWorldXfm`
5. `CheckCollisions()` runs

### Result

The box moves in world space and link collisions are recomputed immediately.

---

## 6. Script playback sequence

### Loading

1. User enters or keeps a path
2. Clicks `Load`
3. `LoadScript(path)` reads lines
4. Blank lines and `#` comments are ignored
5. First 6 whitespace-separated values are parsed as:
   - `X Y Z Rx Ry Rz`
6. Waypoints are appended to `mScript`

### Playing

1. User clicks `Play`
2. `DispatcherTimer` is enabled with 500 ms interval
3. On each tick:
   - next waypoint becomes current target
   - `ComputeIK()` runs

### Stopping

- automatic when script ends
- manual by clicking `Stop`

### Important observation

The script path updates robot state, but there is no call to synchronize the visible IK sliders during `TickScript()`. The internal target changes, but the slider UI may not reflect the currently playing waypoint.

---

## 7. Collision triangle workflow

### Add triangle

1. User clicks `Add Triangle...`
2. `TriangleDialog` opens
3. User enters:
   - name
   - group
   - `P1`
   - `P2`
   - `P3`
4. `OnOK` validates numeric values
5. `AddTri(...)` creates a `CollisionTri`
6. A mesh is built from three points
7. An `OBBTree` is built from that mesh
8. The triangle visual node is added to `mTriGroup`
9. `CheckCollisions()` runs
10. `RefreshTriList()` updates the palette

### Remove triangle

1. User clicks delete button
2. Triangle is removed from:
   - `mTris`
   - `mTriGroup`
3. `CheckCollisions()` runs
4. UI list refreshes

### Import/export

CSV format:

- `Name,Group,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z`

Legacy import without `Group` is also accepted.

---

## Collision model

## Collision assets used

### Robot links

For each `Mechanism` node:

- if `CMesh` exists, use that
- else if `Mesh` exists, use that

Then:

- `OBBTree.From(...)` is built once and stored in `mLinkOBBs`

### Box obstacle

The box is not loaded from file. It is generated procedurally:

- rectangle from `(-100,-100)` to `(100,100)`
- extruded to height `200`
- translated by `(0,0,-100)` during mesh build

This creates a centered box.

### Custom triangles

Each custom triangle:

- builds a one-triangle mesh
- creates an `OBBTree`
- stores a render node

---

## Collision check pipeline

`RobotScene.CheckCollisions()` performs the scene-level check.

### Step 1: Prepare world-space collision trees

- obstacle:
  - `boxOBBW = mBoxOBB.With(BoxWorldXfm)`
- link:
  - `wLink = linkOBB.With(m.Xfm)`

### Step 2: Prepare collider

- `using var bc = OBBCollider.Borrow()`

This reuses a pooled collider object.

### Step 3: Prepare group hit map

- `groupHit["Box"] = false`
- every triangle group added as `false`

### Step 4: For each link

For each `(Mechanism m, OBBTree linkOBB)`:

1. transform link OBB tree into world space
2. test against world-space box
3. test against every custom triangle
4. set:
   - `m.IsColliding = linkHit`
   - `groupHit[group] = true` when hit

### Step 5: Apply visuals

- `mBoxVN.Color = Red or Blue`
- every triangle in a hit group becomes red
- every link with `IsColliding = true` is rendered in collision color by `MechanismVN`

---

## Narrow-phase collision internals

`OBBCollider.Check(a, b, oneCrash = true)` implements hierarchical collision traversal.

### Broad strategy

- if `a` has fewer triangles than `b`, swap them
- transform tree `B` into `A` space
- first test root OBB vs root OBB
- recurse only if root boxes overlap

### Recursion cases

The traversal processes pairs of entities:

- OBB vs OBB
- OBB vs triangle
- triangle vs OBB
- triangle vs triangle

### Primitive tests used

From `Collision.cs`:

- `Collision.Check(OBB, OBB)`
  - SAT with 15 axes
- `Collision.Check(pts, tri, OBB)`
  - SAT box-triangle test
- `Collision.TriTri(...)`
  - triangle-triangle intersection

### Performance-oriented details

`OBBCollider` caches transformed content from tree B:

- transformed points
- transformed triangles
- transformed OBBs

This is managed by rung counters so repeated transforms are avoided within one collision pass.

It also remembers the last colliding triangle pair and tries that pair first on the next call when `oneCrash == true`.

---

## Collision grouping behavior

Custom triangles are grouped by name string.

### Group semantics

- If one triangle in a group is hit, **all triangles in that same group** are colored as colliding.
- The box obstacle also participates as a named group:
  - group key: `Box`

### Important behavior detail

`TriangleDialog` defaults the triangle group to `"Group1"` when the field is left empty.

The box obstacle collision state is tracked with a dedicated `bool boxHit` variable inside
`CheckCollisions()`, separate from any triangle group dictionary. A triangle group named `"Box"`
does not interfere with the box's color, and vice versa.
- all triangles in group `Box` will behave as one visual collision set

This appears intentional or at least accepted by the current implementation, but it is important because it couples the box obstacle and default triangle group visually.

---

## Rendering behavior during collision

`MechanismVN.SetAttributes()` uses:

- normal color: `mMech.Color`
- collision color: `(255, 64, 32)`

So the robot links themselves do not compute their own collision rendering. They only react to `Mechanism.IsColliding`.

The box and custom triangles manage color directly inside `RobotScene.CheckCollisions()`.

---

## Data fields with functional meaning

### Pose and target fields

- `mX, mY, mZ`
  - IK target offset from `mHome.Org`
- `mRx, mRy, mRz`
  - IK orientation angles
- `mCS`
  - computed target coordinate system

### Obstacle fields

- `mBX, mBY, mBZ`
  - box world translation

### Kinematic fields

- `mMech`
  - full mechanism tree
- `mTip`
  - tip node
- `mJoints`
  - solver-controlled joint nodes
- `mMin`, `mMax`
  - joint limits copied from mechanism

### Collision fields

- `mBoxOBB`
  - box collision tree
- `mLinkOBBs`
  - per-link collision trees
- `mTris`
  - custom collision triangles

### ViewModel fields (bindable state)

- `ViewModel.X/Y/Z/Rx/Ry/Rz` — IK pose; two-way bound to sliders and textboxes
- `ViewModel.BX/BY/BZ` — obstacle position
- `ViewModel.Joints` — `JointSliderModel[]`, one per robot joint; bound to FK `ItemsControl`
- `ViewModel.Triangles` — `ObservableCollection<CollisionTriVM>`; bound to triangle list
- `ViewModel.PlayLabel` — `"Play"` or `"Stop"`; bound to Play button text

---

## Notable implementation findings

### 1. FK and IK both drive the same underlying mechanism

There is no duplicate robot state. Both modes update the same `Mechanism` joint objects.

### 2. FK sliders are not synchronized after IK

When IK computes new joint values:

- `mJoints[i].JValue` changes
- but FK sliders are not updated to reflect the solved joint angles

So the FK section may become stale after IK interaction or script playback.

### 3. IK sliders stay in sync via ViewModel

All pose-setting paths (`SnapToFace`, `SnapToTriNode`, `TickScript`) call
`ViewModel.SetIKPose(x, y, z, rx, ry, rz)`. This fires `PropertyChanged` for all six
IK properties, which WPF propagates to the bound sliders and textboxes automatically.
No `SyncIKSliders()` method is needed.

### 4. Script playback is fully reflected in the UI

`TickScript()` calls `ViewModel.SetIKPose()`, so the IK sliders and textboxes track each
waypoint as it plays. Previously this was a known gap; it is now resolved.

### 5. First-valid-solution strategy is simplistic

The solver may return multiple valid stances, but the system always chooses the first valid one. There is no optimization against:

- joint travel
- singularity proximity
- continuity from previous pose
- collisions

### 6. Collision checks are immediate and global

Every relevant input action triggers a full per-link collision pass against:

- the box
- every custom triangle

This is simple and predictable.

### 7. Custom triangles are world-static

They are checked as:

- `tri.OBB.With(Matrix3.Identity)`

So they do not have a separate editable transform. Their geometry is already in world coordinates.

### 8. The box is the only pickable interaction used here

`Picked()` only reacts when the picked object is exactly `mBoxMesh`.

---

## Strengths of the current design

- Clear separation between:
  - main app host
  - floating control palette
  - robot scene logic
- Good reuse of general-purpose infrastructure:
  - `Mechanism`
  - `MechanismVN`
  - `RBRSolver`
  - `OBBTree`
  - `OBBCollider`
- Collision model is significantly better than pure AABB testing
- Dynamic UI makes the scene self-contained
- The mechanism definition is externalized in `mechanism.curl`

---

## Limitations and likely future improvement points

### UI synchronization

Most obvious gap:

- FK sliders should reflect solved joint values after IK
- IK sliders should reflect script playback and any other programmatic pose change

### IK solution selection

A better chooser could prefer:

- nearest current joint configuration
- non-colliding stance
- minimal wrist flip
- minimal base motion

### Collision reporting detail

Current output is mostly binary:

- link colliding or not
- group colliding or not

Possible extensions:

- exact colliding triangle pair
- contact points
- distance to collision
- per-link/per-triangle list

### Triangle groups

Defaulting the group to `Box` is convenient but couples custom planes/triangles with the obstacle box color state. A separate default group might be clearer.

### Tool model

`mGripper` is currently only an empty transformed anchor. If a visible or collidable end effector is needed, this is the attachment point.

---

## Practical mental model

The simplest correct mental model of this robot system is:

- `Mechanism` is the robot body and transform chain
- `RBRSolver` converts task-space targets into six joint angles
- `RobotScene` owns all robot-specific runtime state
- `RobotWindow` is only a dynamic control surface
- `OBBTree` and `OBBCollider` provide mesh-based collision checks
- every user action eventually updates joints, updates transforms, and rechecks collisions

That is the core functional loop of the subsystem.