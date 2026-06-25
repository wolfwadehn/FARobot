# Robot Cell — Pick & Place, Collision & Planning

This guide documents the cell-authoring features added on top of the base FK/IK
robot scene: importing geometry, per-object user frames, pick-and-place teaching,
the carried part, collision detection, the collision-free planner, and saving /
loading a cell.

It complements `beginner-guide.md` (FK/IK, MVVM, scene graph) and
`expert-reference.md` (low-level code references).  Read those first if the terms
*ViewModel*, *VNode*, *Mechanism*, or *OBBTree* are unfamiliar.

> **Note on the UI host.** The robot controls used to live in a separate floating
> `RobotWindow`.  They are now an embedded `RobotPanel` (a `UserControl`) docked
> on the right of `MainWindow`, beside the GL viewport — one window.  All bindings
> and handlers moved unchanged into `RobotPanel.xaml` / `RobotPanel.xaml.cs`.

---

## 1. The workflow at a glance

```
Import Geometry  →  Move / Calibrate frame  →  Import Part
        │                                          │
        ▼                                          ▼
   Pick Pickup Surface  ──►  Pick Place Surface  ──►  Generate Pick & Place
        │                                          │
        ▼                                          ▼
   (waypoint list: Move / Pick / Place)   →   Auto Collision-Free  →  Play
        │
        ▼
   File ▸ Save Cell …  /  Open Cell …
```

Every step writes into `RobotViewModel`; `RobotScene` reacts through events and
updates the 3-D scene, exactly as the base FK/IK pipeline does.

---

## 2. Panel sections (`RobotPanel.xaml`)

| Section | Controls | Backing |
|---------|----------|---------|
| Forward Kinematics | one slider per joint (S,L,U,R,B,T) | `ViewModel.Joints` |
| Inverse Kinematics | X/Y/Z/Rx/Ry/Rz sliders, **Home Position**, **Set Home** | `ViewModel.X..Rz`, `HomeCommand`, `OnSetHome` |
| Geometry & Objects | **Import Geometry…**, object combo, **Move** (6 DOF), **User frame** (6 params), **Calibrate…/Frame @ Corner/Clear** | `ViewModel.Objects`, `SelectedObject`, `ObjX..ObjRz`, `FrX..FrRz` |
| Pick & Place | **Pick Pickup Surface**, **Pick Place Surface**, **Generate Pick & Place**, **Auto Collision-Free**, **Import Part…** | scene methods |
| Obstacle | BX/BY/BZ | `ViewModel.BX/BY/BZ` |
| Script | path, **Load / Add Waypoint / Play**, **WP** scrubber, waypoint list | `ViewModel.Waypoints`, `WaypointPos` |
| Collision Triangles | **Add Triangle…**, list | `ViewModel.Triangles` |

---

## 3. Geometry & objects

**Import.** `RobotScene.ImportGeometry(path)` loads an STL (`STLReader`) or OBJ
(`Mesh3.LoadObj`) by extension, wraps it in a `SceneObject`, and **appends** it —
multiple geometries are allowed.  Each `SceneObject` carries:

- the render mesh + a `Mesh3VN` wrapped in an `XfmVN` (`Node`) — so it can move;
- an `OBBTree` (`OBB`) for collision (null if the mesh is too degenerate to build
  one — logged as a warning);
- a 6-DOF **placement** (`X,Y,Z,Rx,Ry,Rz` → `Xfm`);
- a **user frame** (`HasFrame`, `FX..FRz` → `Frame`, plus optional 3 calibration
  points `CP1..CP3`).

**Selection & move.** The combo box binds to `ViewModel.SelectedObject`.  Selecting
an object pushes its placement and frame into the panel (`OnSelectObject` →
`SetObjMove` / `SetObjFrame`, which do *not* re-fire edit events).  Editing the
Move fields fires `ObjMoved` → `OnObjMoved`, which writes back to the object,
calls `ApplyPlacement()`, and re-checks collisions.  The selected object is tinted
blue (`SelObjColor`) in the scene.

**Placement convention.** `Xfm = Matrix3.To(Pose(X,Y,Z,Rx,Ry,Rz))` — the object's
mesh origin lands at world `(X,Y,Z)`; all-zero leaves it at its file coordinates.

---

## 4. Per-object user frames

Each object has its own 6-parameter frame (position + XYZ Euler), used to report
pickup/place coordinates relative to that object and as the part's rest placement.

Three ways to set it, all writing the same six `FX..FRz` values:

1. **Type** the six values in the *User frame* grid (`FrameEdited` → `OnFrameEdited`).
2. **Calibrate…** — the 3-point dialog (`PalletFrameDialog`); `SetPalletFrame`
   builds a `CoordSystem` (`BuildFrame`: P1 origin, P2 → +X, P3 on the +XY side,
   `Z = X×(P3−P1)`, `Y = Z×X`), converts to 6 params, and stores the 3 points.
3. **Frame @ Corner** — click a corner of the object; `SetFrameFromCorner` takes
   the nearest bounding-box corner (world bounds via `Mesh.GetBound(obj.Xfm)`),
   offsets 50 mm inward in X and Y, and calibrates that object.

Each object's frame triad (red/green/blue) is drawn by `FrameVN` for every object
with `HasFrame`.

---

## 5. Pick & place teaching and the part

**Import Part.** `ImportPart(path)` loads a movable part placed at the **selected
object's frame** (`mPartHomeXfm`).  It keeps the render mesh (`mPartVN`) and an
**eroded** collision OBB (`BuildPartOBB`, see §7).

**Pickup / place.** *Pick Pickup Surface* / *Pick Place Surface* arm a click
(`mPickMode`).  `Picked(obj)` routes the click:

- on an object in *Corner* mode → `SetFrameFromCorner`
- on the part or an object in *Pickup* mode → `SetPickup` (world hit point;
  orientation is taken from **home**, so the robot only translates to it)
- on an object in *Place* mode → `SetPlace`

The clicked triangle is highlighted orange (`HighlightWorldFace`), and the pickup
is marked with a magenta approach arrow (`FrameVN`).  Status text reports the point
in the clicked object's frame (`InFrame`).

**Attach / detach (actions, not proximity).** During playback each waypoint's
action fires **on arrival**:

- **Pick** → `AttachPart` rigidly fixes the part to the flange
  (`mPartRelXfm = partXfm × tip⁻¹`); thereafter `UpdateAttachedPart` keeps
  `part = mPartRelXfm × tip`.
- **Place** → `PlacePart` releases it where it currently sits.

`ResetPart` returns the part to `mPartHomeXfm` on **Home Position**, on Play
restart, and on cell load.

---

## 6. Waypoints

A waypoint is `(X,Y,Z,Rx,Ry,Rz, EAction)` where `EAction ∈ {Move, Pick, Place}`.
The list lives in `mScript` and is mirrored to `ViewModel.Waypoints` for the UI.

- **Add Waypoint** records the current robot pose as a `Move` waypoint.
- The waypoint list row shows **WP n** (click → scrub to it), the **action**
  (click cycles Move→Pick→Place, `CycleAction`), and **×** (remove).
- **Generate Pick & Place** writes the standard cycle in **world coordinates**:
  `Home → above-pickup → pickup[PICK] → above-pickup → above-place → place[PLACE]
  → above-place → Home` (the place leg is skipped if no place point is set).
- **Play / WP scrubber** — `mPlayTimer` (40 ms) animates `ViewModel.WaypointPos`
  from 0 to the last index as one cycle; `ApplyWaypointPos` linearly interpolates
  position and Euler between bracketing waypoints for smooth motion.  The slider
  has a tick per waypoint.

The script is persisted to `ViewModel.ScriptPath` (`SaveScript`) and parsed back
by `LoadScript`.

### IK solution selection

`ComputeIK` → `SolveWorld` chooses among the analytic solver's up-to-8 solutions
by `SolutionCost`: a dominant penalty for nearing any joint limit or the wrist
singularity (B≈0), then least joint travel from the current pose.  This keeps the
arm on a continuous branch instead of flipping the wrist to a limit.

---

## 7. Collision detection

`CheckCollisions` runs on every robot move, every object move, part import, and
attach/place.  It is **always on for the robot**.

| Pair | When checked |
|------|--------------|
| robot links ↔ obstacle box | always |
| robot links ↔ collision triangles | always |
| robot links ↔ each imported object (at `obj.Xfm`) | always |
| carried part ↔ box and each object | **only while attached** |

Colliding links render red (`MechanismVN` honours `Mechanism.IsColliding`); hit
objects/box/part turn red; a red **⚠ COLLISION** banner shows in the viewport
(`InfoVN`, driven by `RobotScene.InCollision`).

Two important details discovered and fixed:

- **Link OBBs are built from each link's `Mesh3`**, not its `CMesh` (the TopoMesh
  path threw and was being swallowed, leaving the robot with *zero* collision
  geometry).  All seven links now build OBBs.
- **The part's collision OBB is eroded** (`BuildPartOBB`: ~3 mm inset, a thin axis
  collapses to a centre sheet).  This gives a small clearance so a part *resting
  on* or brushing a pallet surface does not false-trigger, while real penetration
  still flags.  The part participates in collision **only between Pick and Place**.

---

## 8. Collision-free planner (RRT)

**Auto Collision-Free** re-routes the current waypoints so each transfer avoids
collisions (`PlanCollisionFree`):

1. For each consecutive pair, if the straight segment collides it runs a
   **goal-biased RRT** in world XYZ (orientation fixed at home), then shortcuts
   the result, inserting the minimum intermediate `Move` waypoints.
2. The carried part's swept volume is included on segments **between Pick and
   Place** (grasp captured at the pickup, `ComputeGraspRel`).
3. Original Pick/Place waypoints and their actions are preserved.

Building blocks:

| Method | Role |
|--------|------|
| `SolveWorld(cs)` | analytic IK for a world TCP pose; applies best solution |
| `HasCollision(carrying)` | boolean collision query (no colour side-effects) |
| `PoseFree(p, carrying)` | reachable + collision-free at point `p` (saves/restores joints) |
| `SegmentFree(a,b,carrying)` | samples the segment every ~25 mm |
| `Rrt(start,goal,carrying)` | goal-biased RRT (≤4000 iters) + `Shortcut` |

**Why not reinforcement learning?**  Collision-free routing in a known cell is a
solved geometric problem.  Sampling-based planners (RRT/PRM, as used by MoveIt/
OMPL) are deterministic enough, need no training, run in milliseconds–seconds, and
inherit safety directly from the collision checker.  RL would require extensive
training, produce non-deterministic motions that are hard to certify for collision
safety, and add no benefit for free-space waypoint generation.  RL is only
worthwhile for problems with unknown dynamics (learned grasping from perception,
contact-rich assembly) — not this.

---

## 9. Set Home

`mHome` is a full world TCP pose (position + orientation), default arm's-length.
**Set Home** (`SetHome`) captures the robot's current pose as the new home and, if
a pickup exists, regenerates the waypoints so they start from it.  **Home Position**
(`GoHome`) drives there and resets the part.

---

## 10. Saving and loading a cell

**File ▸ Save Cell… / Open Cell…** serialise the whole cell to JSON
(`SaveCell` / `LoadCell`, `System.Text.Json`).  Geometry is referenced by
**absolute mesh path**, so keep the source STL/OBJ files in place.

```jsonc
{
  "Objects": [
    { "Path": "...sample_pallet.obj",
      "Pose":  [x,y,z,rx,ry,rz],          // 6-DOF placement (world)
      "HasFrame": true,
      "Frame": [x,y,z,rx,ry,rz],          // user frame (world)
      "Calib": [p1x,p1y,p1z, p2…, p3…] }  // 3 calibration points or null
  ],
  "Part":   { "Path": "...sample_part.obj", "Place": [x,y,z,rx,ry,rz] },
  "Pickup": [x,y,z],                       // or null
  "Place":  [x,y,z],                       // or null
  "Home":   [x,y,z,rx,ry,rz],
  "Box":    [bx,by,bz],
  "Waypoints": [ { "P": [x,y,z,rx,ry,rz], "A": "Move|Pick|Place" } ]
}
```

`LoadCell` clears the current cell, re-imports each mesh, restores placements,
frames, the part, pickup/place, home, box, and waypoints, then homes the robot
and zooms to fit.  DTOs are at the bottom of `RobotScene.cs` (`CellDto`, `ObjDto`,
`PartDto`, `WpDto`).

---

## 11. File formats

**Script** (`robot_script.txt`) — one waypoint per line, world coordinates:

```
# Waypoints. Actions: PICK / PLACE.
X Y Z Rx Ry Rz            ← Move
X Y Z Rx Ry Rz PICK       ← attach the part on arrival
X Y Z Rx Ry Rz PLACE      ← release the part on arrival
```

**Cell** (`*.cell`) — JSON, schema in §10.

---

## 12. Key code map (`RobotScene.cs`)

| Concern | Methods |
|---------|---------|
| IK / FK | `ComputeIK`, `SolveWorld`, `SolutionCost`, `OnFK`, `CurrentWorldPose` |
| Geometry | `ImportGeometry`, `LoadMesh`, `SceneObject`, `OnSelectObject`, `OnObjMoved` |
| Frames | `SetPalletFrame`, `SetFrameFromCorner`, `BuildFrame`, `OnFrameEdited`, `ClearPalletFrame` |
| Pick / place | `Picked`, `SetPickup`, `SetPlace`, `PickupPose`, `HighlightWorldFace`, `FindFace` |
| Part | `ImportPart`, `BuildPartOBB`, `AttachPart`, `PlacePart`, `UpdateAttachedPart`, `ResetPart` |
| Waypoints | `AddWaypoint`, `CycleAction`, `RemoveWaypoint`, `GoToWaypoint`, `LoadScript`, `SaveScript`, `ApplyWaypointPos`, `TogglePlay`, `TickScript`, `GenerateWaypoints` |
| Collision | `CheckCollisions`, `HasCollision`, `InCollision` |
| Planner | `PlanCollisionFree`, `ComputeGraspRel`, `PoseFree`, `SegmentFree`, `Rrt`, `Shortcut` |
| Cell I/O | `SaveCell`, `LoadCell`, `PoseFrom` |
| Home | `GoHome`, `SetHome` |
