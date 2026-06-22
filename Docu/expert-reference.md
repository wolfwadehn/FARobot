# Expert Reference

Code-level explanations with file and line references.
All line numbers are approximate and refer to the files after the drawing-mode
cleanup.

---

## Scene graph & rendering

**Entry point:** `RobotScene.cs` — constructor, `~line 10`

```
Scene3 (Nori)
└── Root: GroupVN
    ├── MechanismVN(mMech)          renders all link meshes from mechanism.curl
    ├── mGripper: XfmVN             applies mTip.Xfm; child is an empty GroupVN
    │                               (placeholder for a future tool mesh)
    ├── mBoxXfm: XfmVN              applies BoxWorldXfm; child is mBoxVN
    │   └── mBoxVN: Mesh3VN         the blue/red 200×200×200 mm obstacle cube
    ├── mTriGroup: GroupVN          collision triangles (Mesh3VN added dynamically)
    ├── TcpVN                       streaming; draws TCP axes + ring
    ├── InfoVN                      streaming; draws text overlay (pose + angles)
    └── TraceVN                     Nori singleton; output from Lib.Trace()
```

`Streaming = true` on `TcpVN` and `InfoVN` means Lux redraws them every frame.
All other nodes are event-driven: call `node.Redraw()` or modify a property that
internally calls it.

`MechanismVN` is a Nori built-in that knows how to traverse the mechanism tree
and render each mesh at its current world transform.  When `JValue` changes on
any joint, the mechanism tree updates its transforms and `MechanismVN` picks up
the change automatically.

---

## Mouse event pipeline

**Entry point:** `Nori.Host.WPF` — `SceneManipulator` (created in `OnLuxReady`)

`new SceneManipulator()` connects Nori's internal mouse/keyboard observables
(`Hub.Mouse`, `Hub.Keyboard`) to the active `Lux.UIScene`.  It handles:
- Left-drag → orbit
- Right-drag / middle-drag → pan
- Scroll wheel → zoom
- Left-click on a mesh → `scene.Picked(obj)`

**Pick callback:** `RobotScene.Picked(object obj)` — `RobotScene.cs ~line 81`

Nori resolves which drawable object (mesh) was clicked and passes it as `obj`.
FApp checks:

```csharp
public override void Picked(object obj) {
    if (obj == mBoxMesh)          { SnapToFace(Lux.PickPos); return; }
    foreach (var tri in mTris)
        if (obj == tri.Mesh)      { SnapToTriNode(tri.P1, tri.Normal); return; }
}
```

`Lux.PickPos` is the 3-D world position of the click on the mesh surface.

No mouse input is handled in `MainWindow.xaml.cs` or `RobotWindow.xaml.cs`.
All 3-D interaction goes through `Picked()`.

---

## Mechanism hierarchy from `mechanism.curl`

`mechanism.curl` is a binary Nori serialisation format.  You cannot read it in a
text editor, but after `Mechanism.Load()` you get an in-memory tree:

```
root (mMech)
 ├── S  (Swing)     JMin=-180  JMax=+180
 ├── L  (Lower)     JMin=-90   JMax=+155
 ├── U  (Upper)     JMin=-170  JMax=+190
 ├── R  (Rotation)  JMin=-180  JMax=+180
 ├── B  (Bend)      JMin=-135  JMax=+135
 ├── T  (Twist)     JMin=-360  JMax=+360
 └── Tip            no joint; the end-effector frame
```

Actual limit values come from the file, not from code.  The code reads them:

```csharp
// RobotScene.cs constructor
for (int i = 0; i < 6; i++) {
    mMin[i] = mJoints[i].JMin;
    mMax[i] = mJoints[i].JMax;
}
```

The limits are then passed to `RBRSolver` so it rejects solutions that would
violate joint limits.

Each node also carries one or more mesh objects (`m.Mesh`, `m.CMesh`) used for
rendering and collision.  `m.CMesh` is a simplified collision mesh; `m.Mesh` is
the full visual mesh.

---

## Socket chain and forward kinematics

Nori's `Mechanism` computes FK via a **socket chain**: each node stores its
transform relative to its parent's socket.  When you set `JValue` on a joint,
the node rotates around the socket axis by that angle, and all children's world
transforms update accordingly.

From code's perspective, you just write:

```csharp
mJoints[i].JValue = angleDegrees.D2R();
```

...and `mTip.Xfm` returns the updated end-effector world transform on the next
read.  The chain computation happens inside Nori.

`CoordSystem.World * mTip.Xfm` multiplies the identity world frame by the
tip's local-to-world matrix, giving a `CoordSystem` with:
- `.Org` — tip origin in world mm
- `.VecX`, `.VecY`, `.VecZ` — tip orientation axes in world space

---

## `mBoxOBB` — the obstacle box

**Construction** (`RobotScene.cs` constructor):

```csharp
var boxPoly = Poly.Rectangle(-100, -100, 100, 100);   // 200×200 square in XY
mBoxMesh    = Mesh3.Extrude([boxPoly], 200,
                  Matrix3.Translation(0, 0, -100),     // start Z at -100
                  ETess.Medium);                        // triangulation quality
mBoxOBB     = OBBTree.From(mBoxMesh);
mBoxVN      = new Mesh3VN(mBoxMesh) { Mode = EShadeMode.Glass, Color = Color4.Blue };
mBoxXfm     = new XfmVN(BoxWorldXfm, mBoxVN);
```

The result is a 200×200×200 mm cube centred at the origin.  `XfmVN` positions
it at `(BX, BY, BZ)` from the ViewModel via:

```csharp
Matrix3 BoxWorldXfm => Matrix3.Translation(ViewModel.BX, ViewModel.BY, ViewModel.BZ);
```

**Collision check** — each frame (or after every pose change):

```csharp
var boxOBBW = mBoxOBB.With(BoxWorldXfm);   // transform OBB to world space
using var bc = OBBCollider.Borrow();
bool hit = bc.Check(linkOBBWorld, boxOBBW);
```

`OBBTree.With(matrix)` returns a new OBBTree in the given space without
modifying the original — important because `mBoxOBB` is reused every frame.

---

## How forward kinematics works

**Trigger:** user drags a joint slider → `JointSliderModel.Value` setter

```csharp
// JointSliderModel.cs
public double Value {
    get => mMech.JValue;
    set { mMech.JValue = value; Notify(); mOnChanged(); }  // mOnChanged = OnFK
}
```

**`RobotScene.OnFK()`** (`RobotScene.cs ~line 141`):

```csharp
void OnFK() {
    var tipCs = CoordSystem.World * mTip.Xfm;          // read FK result
    var off   = mTcpOffset;
    var tcp   = tipCs.Org                              // tip flange position
              + tipCs.VecX * off.X                    // + wrist-X component
              + tipCs.VecY * off.Y                    // + wrist-Y component
              + tipCs.VecZ * off.Z;                   // + wrist-Z component
    var (rx, ry, rz) = MatrixToEuler(tipCs);
    ViewModel.SetIKDisplay(tcp.X, tcp.Y, tcp.Z + LJointZ, rx, ry, rz);
    foreach (var js in ViewModel.Joints) js.Refresh(); // push new angles to sliders
    mGripper.Xfm = mTip.Xfm;
    CheckCollisions();
}
```

`+ LJointZ` (= 565 mm) is added back to Z because the IK display uses world Z
(from the floor), while the solver works in the L-joint frame (above the
shoulder).

**`MatrixToEuler()`** — extracts Rx, Ry, Rz from an orientation matrix using
the X-then-Y convention:

```csharp
// cs is the coordinate system of the tip
double ry = Asin(Clamp(cs.VecZ.X, -1, 1));
double rx = (Abs(Cos(ry)) > 1e-6) ? Atan2(-cs.VecZ.Y, cs.VecZ.Z) : 0;
double rz = (Abs(Cos(ry)) > 1e-6) ? Atan2(-cs.VecY.X, cs.VecX.X) : 0;
```

This is the inverse of the construction used in `ComputeIK()`:
`cs *= Rot(X, Rx) * Rot(Y, Ry) * Rot(Z, Rz)`.

---

## How inverse kinematics works

**Trigger:** user moves an IK slider → `ViewModel.X` (or Y/Z/Rx/Ry/Rz) setter
→ `IKChanged?.Invoke()` → `ComputeIK()`

**`RobotScene.ComputeIK()`** (`RobotScene.cs ~line 121`):

```csharp
void ComputeIK() {
    // Step 1: build TCP orientation from Euler angles (X-then-Y convention)
    var cs  = CoordSystem.World;
    cs     *= Matrix3.Rotation(EAxis.X, ViewModel.Rx.D2R());
    cs     *= Matrix3.Rotation(EAxis.Y, ViewModel.Ry.D2R());
    cs     *= Matrix3.Rotation(EAxis.Z, ViewModel.Rz.D2R());

    // Step 2: work in the L-joint frame (subtract 565 mm from world Z)
    var tcp = new Vector3(ViewModel.X, ViewModel.Y, ViewModel.Z - LJointZ);

    // Step 3: back-solve wrist from TCP (subtract TCP offset in wrist frame)
    var wrist = tcp - cs.VecX * mTcpOffset.X
                    - cs.VecY * mTcpOffset.Y
                    - cs.VecZ * mTcpOffset.Z;
    mCS = cs * Matrix3.Translation(wrist);

    // Step 4: analytic solver — up to 8 solutions
    mSolver.ComputeStances(mCS.Org, mCS.VecZ, mCS.VecX);

    // Step 5: apply first valid solution
    for (int j = 0; j < 8; j++) {
        var a = mSolver.Solutions[j];
        if (!a.OK) continue;
        for (int i = 0; i < 6; i++)
            mJoints[i].JValue = a.GetJointAngle(i);
        break;
    }
    mGripper.Xfm = mTip.Xfm;
    CheckCollisions();
}
```

`RBRSolver` is a Nori closed-form 6-DOF solver.  It is initialised with the
arm's Denavit-Hartenberg parameters:

```csharp
// (a1, a2, a3, d1, d4, d6)  — link lengths and offsets in mm
new RBRSolver(150, 770, 0, 0, 1016, 175, mMin, mMax)
```

Solutions that violate joint limits are flagged `OK = false` and skipped.

---

## TCP model and orientation behavior

**TCP offset** — the vector from the wrist flange to the actual tool tip,
expressed in the *wrist frame* (not world frame):

```csharp
Vector3 mTcpOffset = new(0, 0, -50);   // default: 50 mm along negative wrist-Z
```

The offset can be changed via the TCP Offset dialog (⚙ toolbar button).

**In `ComputeIK`:**  
To find the wrist position from a desired TCP position, the offset is removed:

```
wrist = tcp − R × offset
```

where `R` is the 3×3 rotation part of the TCP orientation matrix.

**In `OnFK`:**  
The reverse — to find TCP from the current wrist:

```
tcp = tipOrigin + VecX×off.X + VecY×off.Y + VecZ×off.Z
```

**`TcpVN.Draw()`** (`RobotScene.cs ~line 330`) renders three arrows:
- Red arrow along wrist `VecX`
- Green arrow along wrist `VecY`
- Blue arrow along wrist `VecZ`

Each arrow is 180 mm long with a 30 mm arrowhead.  A white circle of radius
20 mm is drawn in the VecX-VecY plane at the TCP position.

**Orientation convention:** rotations are applied in X-then-Y order.
- `Rx = -90°` means the tool points downward (Z-axis of tool = −world-Y).
- `Ry = ±90°` tilts the tool toward ±X.

---

## Face snapping behavior

**Entry:** `RobotScene.Picked(object obj)` when user clicks the box mesh.

```csharp
void SnapToFace(Point3 hit) {
    double rx = hit.X - ViewModel.BX;   // relative to box centre
    double ry = hit.Y - ViewModel.BY;
    double rz = hit.Z - ViewModel.BZ;
    double ax = Abs(rx), ay = Abs(ry), az = Abs(rz);

    double newRx, newRy, newRz;
    if      (ax >= ay && ax >= az)   // X face
        (newRx, newRy, newRz) = rx > 0 ? (0, 90, 0) : (0, -90, 0);
    else if (ay >= az)               // Y face
        (newRx, newRy, newRz) = ry > 0 ? (-90, 0, 0) : (90, 0, 0);
    else                             // Z face
        (newRx, newRy, newRz) = rz > 0 ? (0, 0, 0) : (180, 0, 0);

    ViewModel.SetIKPose(hit.X, hit.Y, hit.Z, newRx, newRy, newRz);
}
```

The click position on the face is used as the new TCP target position.
The orientation is set to one of the six canonical poses so that the tool
approaches the face perpendicularly (VecZ points outward from the face).

**Triangle snapping** (`SnapToTriNode`) calls `NormalToEuler(normal)`:

```csharp
static (double Rx, double Ry, double Rz) NormalToEuler(Vector3 n) {
    double ry    = Asin(Clamp(n.X, -1, 1));
    double cosRy = Cos(ry);
    double rx    = Abs(cosRy) > 1e-6 ? Atan2(-n.Y, n.Z) : 0;
    return (rx * R2D, ry * R2D, 0);
}
```

This finds Rx, Ry such that after applying `Rot(X,Rx)*Rot(Y,Ry)`, the tool's
VecZ axis equals `normal`.  Rz is always set to 0.

---

## Robot control UI structure

**`RobotWindow.xaml`** is a `ScrollViewer` containing a single `StackPanel`
with five sections:

### 1. Forward Kinematics — `ItemsControl` bound to `ViewModel.Joints`

`ViewModel.Joints` is a `JointSliderModel[]` populated in `RobotScene`
constructor from `mMech.EnumTree()` (only joints where `m.Joint != EJoint.None`).

The `DataTemplate` creates one slider row per entry.  The slider's `Value`
property two-way-binds to `JointSliderModel.Value`, which in turn reads/writes
`mMech.JValue`.

### 2. Inverse Kinematics — six fixed slider rows + Home button

`Command="{Binding HomeCommand}"` → `DelegateCommand(() => HomeRequested?.Invoke())`
→ `RobotScene.GoHome()` → `ViewModel.SetIKPose(home coordinates)`.

### 3. Obstacle — BX / BY / BZ

Slider binds to `ViewModel.BX` etc., setter fires `BoxChanged` → `UpdateBox()`
→ `mBoxXfm.Xfm = BoxWorldXfm` + `CheckCollisions()`.

### 4. Script

`LoadCommand` → reads file into `mScript` list.  
`AddCommand` → appends current IK pose as a line to the script file.  
`PlayCommand` → toggles `mPlayTimer`; each tick calls `TickScript()`.

### 5. Collision Triangles

`ObservableCollection<CollisionTriVM>` bound to `ItemsControl`.  
WPF updates the list automatically when items are added/removed because
`ObservableCollection` implements `INotifyCollectionChanged`.

Each `CollisionTriVM` holds a `RemoveCommand` that captures a closure over the
specific `CollisionTri` object:

```csharp
new CollisionTriVM(t.Name, t.Group, brush, () => RemoveTri(t))
```

---

## Tool model

Currently FApp does not render a tool mesh.  The "gripper" in the scene is:

```csharp
mGripper = new XfmVN(Matrix3.Identity, new GroupVN([]));
```

An `XfmVN` wrapping an **empty** `GroupVN`.  It exists as a placeholder so
that a tool mesh can be added later without restructuring the scene graph.

After each FK or IK solve, the gripper transform is updated:

```csharp
mGripper.Xfm = mTip.Xfm;   // gripper follows the tip frame
```

To add a tool mesh, replace the inner `GroupVN([])` with a `Mesh3VN` built from
the tool's geometry, or add child nodes to the `GroupVN`.  The `XfmVN` will
automatically position and orient the child at the tip frame.
