# Functional Sequences

---

## 1. Robot mode startup sequence

```
App.MainWindow()  ──────────────────────────────────────────────────
  Lib.Init()
  Lib.Register(ZipStmLocator / FileStmLocator)   ← asset path setup
  InitializeComponent()                          ← parses MainWindow.xaml
  mContent.Child = WPFHost.Init(this, OnLuxReady)
  mToolbarHost.Child = new Toolbar()
  VNode.RegisterAssembly(...)
  Loaded → App.CheckExpired()                    ← version expiry nag

WPFHost.Init() (Nori internal) ─────────────────────────────────────
  Creates OpenGL surface
  Fires callback → OnLuxReady()

OnLuxReady() ───────────────────────────────────────────────────────
  new SceneManipulator()    ← wires mouse pan / orbit / zoom to Lux
  mRobotScene = new RobotScene()
    Mechanism.Load("N:/Wad/FanucX/mechanism.curl")
    mTip    = FindChild("Tip")
    mJoints = FindChild("S","L","U","R","B","T")
    new RBRSolver(arm dimensions, joint limits)
    Lib.Tessellate = FastTess2D.Process
    Build box mesh + OBBTree
    Build per-link OBBTrees from each link's Mesh3 (for collision)
    new RobotViewModel()
    Wire ViewModel events → ComputeIK / UpdateBox / GoHome /
                            SelectedObjectChanged / ObjMoved / FrameEdited / ...
    GoHome()                            ──→ triggers first IK solve
    Start DispatcherTimer (40 ms, script playback, disabled)
    BgrdColor = Gray(64);  Bound = (-1200,-1200,0, 1200,1200,1500)
    Root = GroupVN([MechanismVN, gripper, box, triGroup, geomGroup,
                   partGroup, TcpVN, FrameVN, InfoVN, TraceVN])

  Lux.UIScene = mRobotScene        ← 3-D scene goes live
  mRobotPanel.SetScene(mRobotScene)← DataContext = ViewModel (panel already docked)
  toolbar.AddButton("⚙")           ← TCP Offset button appears
```

The first `GoHome()` (→ `SetIKPose` → `ComputeIK`) runs the analytic solver and
poses the arm at the home position the moment the window opens.  **View ▸ Robot
Controls** later just toggles `mRobotPanel.Visibility`.

---

## 2. Forward kinematics slider sequence

The user drags a joint slider (e.g. the **B** joint) in the controls panel.

```
RobotPanel.xaml  Slider.Value changes
  │  (two-way binding)
  ▼
JointSliderModel.Value setter
  mMech.JValue = value          ← writes angle into Mechanism node
  Notify("Value")               ← slider TextBox refreshes
  mOnChanged()                  ← the Action delegate stored at construction

  ▼
RobotScene.OnFK()
  tipCs = CoordSystem.World * mTip.Xfm    ← Nori forward-kinematics result
  tcp   = tipCs.Org + offset vector        ← apply wrist→TCP offset
  (rx, ry, rz) = MatrixToEuler(tipCs)     ← matrix → Euler angles (degrees)
  ViewModel.SetIKDisplay(tcp, rx,ry,rz)   ← updates IK textboxes,
                                             does NOT fire IKChanged
  foreach joint → js.Refresh()            ← pushes updated angles to FK sliders
  mGripper.Xfm = mTip.Xfm                ← moves gripper VNode
  UpdateAttachedPart()                   ← if a part is held, it follows the flange
  CheckCollisions()

  ▼
CheckCollisions()                         ← robot collision is ALWAYS on
  For each (linkMesh, linkOBB):
    wLink = linkOBB.With(m.Xfm)         ← link in world space
    linkHit |= Collider.Check(wLink, boxOBBW)
    for each collision triangle:  groupHit[tri.Group] |= Check(wLink, tri.OBB)
    for each imported object:      objHit[k]          |= Check(wLink, obj.OBB.With(obj.Xfm))
    m.IsColliding = linkHit             ← link turns red in scene
  if part attached:                       ← part only collides while held
    for box + each object:  partHit |= Check(partOBB.With(partXfm), …)
  recolour box / triangles / objects / part;  InCollision drives the ⚠ banner
```

**Key insights:**
- `SetIKDisplay` updates the IK sliders to reflect the new TCP position after a
  joint drag but does *not* call `ComputeIK()`, so there is no IK→FK→IK loop.
- Link collision OBBs are built from each link's **`Mesh3`** (the `CMesh`/TopoMesh
  path threw and left the robot with no collision geometry).
- The held part's collision OBB is slightly **eroded** so resting on a surface
  doesn't false-trigger; the part participates only between Pick and Place.

---

## 3. Collision triangle workflow

### 3a. Add triangle

```
User clicks "Add Triangle…" in the controls panel
  ▼
RobotPanel.OnAddTriangle()
  new TriangleDialog()
  dlg.ShowDialog()        ← modal; user fills Name, Group, P1/P2/P3

  if result is true:
    mScene.AddTri(dlg.TriName, dlg.Group, dlg.P1, dlg.P2, dlg.P3)

RobotScene.AddTri(name, group, p1, p2, p3)
  GroupColor(group)
    → looks up or assigns color from sGroupPalette
    → also assigns WPF color for the sidebar label
  new CollisionTri(name, group, color, p1, p2, p3)
    normal = (p2-p1) × (p3-p1)  (normalized)
    Mesh   = Mesh3Builder([p1,p2,p3]).Build()
    OBB    = OBBTree.From(Mesh)
    VN     = new Mesh3VN(Mesh) { Mode=Glass, Color=color }
  mTris.Add(tri)
  mTriGroup.Add(tri.VN)        ← makes triangle visible in 3-D scene
  CheckCollisions()            ← immediate collision check
  RefreshTriList()             ← rebuilds ViewModel.Triangles list

RefreshTriList()
  ViewModel.Triangles.Clear()
  for each tri in mTris:
    ViewModel.Triangles.Add(
      new CollisionTriVM(name, group, wpfBrush, () => RemoveTri(tri)))
  ← WPF ItemsControl updates sidebar list automatically
```

The triangle can also be added via CSV import in the dialog.
CSV columns: `Name, Group, P1X, P1Y, P1Z, P2X, P2Y, P2Z, P3X, P3Y, P3Z`.

### 3b. Remove a triangle

```
User clicks "×" next to a triangle in the sidebar
  ▼
CollisionTriVM.RemoveCommand (DelegateCommand)
  → lambda captured in RefreshTriList: () => RemoveTri(tri)

RobotScene.RemoveTri(tri)
  mTris.Remove(tri)
  mTriGroup.Remove(tri.VN)     ← triangle disappears from scene
  tri.IsColliding = false      ← resets mesh color before discarding
  CheckCollisions()
  RefreshTriList()
```

### 3c. Collision grouping behavior

Triangles are assigned to named **groups** (e.g. `"Group1"`, `"Box"`).
The group name is a free-form string entered in the Add Triangle dialog.

**Color assignment:**  
The first triangle in a group sets the group's color from `sGroupPalette`
(`[Cyan, Green, Yellow]`).  All subsequent triangles in the same group share
that color.  The built-in box is always blue.

**Collision propagation:**  
In `CheckCollisions()`, the code tracks hit state *per group*, not per triangle:

```csharp
Dictionary<string, bool> groupHit = [];
foreach (var tri in mTris) groupHit.TryAdd(tri.Group, false);

foreach (var (m, linkOBB) in mLinkOBBs) {
    ...
    foreach (var tri in mTris)
        if (bc.Check(wLink, tri.OBB.With(Matrix3.Identity)))
            groupHit[tri.Group] = true;
    m.IsColliding = linkHit;
}

foreach (var tri in mTris)
    tri.IsColliding = groupHit[tri.Group];   // whole group turns red
```

This means: if *any* triangle in a group is hit, *all* triangles in that group
turn red simultaneously.  This is intentional — it lets you mark a conceptual
object (e.g. "workpiece") with multiple triangles and see the whole object react
as a unit.

---

## 4. Pick-and-place playback sequence

```
User presses Play
  ▼
RobotScene.TogglePlay()
  ResetPart()                  ← drop part to its rest pose; mFiredUpto = 0
  ViewModel.WaypointPos = 0;  mPlayTimer.IsEnabled = true

mPlayTimer tick (every 40 ms) → TickScript()
  WaypointPos += PlayStep      ← advances the scrubber toward the last waypoint
    ▼ (WaypointScrubbed)
  ApplyWaypointPos()           ← interpolates pos+Euler between bracketing waypoints
    → ViewModel.SetIKPose(...) → ComputeIK() → arm moves, UpdateAttachedPart(),
                                              CheckCollisions()
  FireActionsUpto(floor(pos))  ← on ARRIVAL at a waypoint, run its action:
                                   Pick  → AttachPart()  (part fixes to flange)
                                   Place → PlacePart()   (part released in place)
  at the last index → stop, Play label resets
```

The part is grabbed/released exactly when the robot *reaches* the Pick/Place
waypoint (arrival), and rides the flange in between.

---

## 5. Collision-free re-route sequence

```
User presses Auto Collision-Free → RobotScene.PlanCollisionFree()
  ComputeGraspRel()            ← capture part-vs-flange grasp at the pickup
  SaveJoints()
  for each consecutive waypoint pair (a → b):
     carrying = state after a's action (Pick → true, Place → false)
     via = Rrt(a, b, carrying)            ← straight test; else goal-biased RRT
        PoseFree(p, carrying)             ← SolveWorld(p) && !HasCollision(carrying)
        SegmentFree(a,b,carrying)         ← sample every ~25 mm
        Shortcut(path, carrying)          ← drop redundant via-points
     insert via-points as Move waypoints; keep b (with its action)
  RestoreJoints()
  SaveScript(); AfterScriptChanged()      ← list + scrubber refresh
```

`PoseFree` temporarily solves IK and restores the joints, so the displayed pose is
unchanged after planning.  See `cell-pick-place.md` §8 for the full planner and the
rationale for using RRT rather than reinforcement learning.
