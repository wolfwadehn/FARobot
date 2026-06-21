# Robot Functional Sequences

## 1. Robot startup

1. `MainWindow.OnLuxReady()`
2. `OpenRobot()` is wired and called
3. `RobotScene` is created if missing
4. Inside `new RobotScene()`:
   - mechanism, solver, box obstacle are built
   - `RobotViewModel` is created
   - `ViewModel.Joints` is populated with one `JointSliderModel` per articulated joint
   - ViewModel events are wired: `IKChanged → ComputeIK`, `BoxChanged → UpdateBox`, etc.
   - `ViewModel.SetIKPose(…)` fires the first `ComputeIK()`
5. `Lux.UIScene` switches to robot mode
6. `RobotWindow` is shown
7. `RobotWindow.SetScene(scene)` sets `DataContext = scene.ViewModel`
   — all XAML bindings activate and sliders initialize from ViewModel values

### Result

- Main viewport shows the 3D robot scene
- Floating palette shows FK, IK, obstacle, script, and triangle controls
- Slider values match the home pose from the first `ComputeIK()`

---

## 2. Forward kinematics slider flow

### What the binding does

Each robot joint has a `JointSliderModel` in `ViewModel.Joints`.  
The FK section is an `ItemsControl` in XAML; it generates one slider row per item.

Slider `ValueChanged` path:

1. User drags slider
2. WPF two-way binding writes new value to `JointSliderModel.Value`
3. `JointSliderModel.Value` setter writes to `m.JValue` and calls `mOnChanged()`
4. `mOnChanged` = `RobotScene.OnFK()`

### `OnFK()` does

1. `mGripper.Xfm = mTip.Xfm`
2. `CheckCollisions()`

### Underlying transform effect

When `JValue` changes in `Mechanism`:

1. joint value is stored
2. cached `_xfm` is cleared for the subtree
3. next render uses updated transforms

### Result

- Robot moves directly in joint space
- Collision state is recomputed immediately
- **Note:** FK sliders do not reflect IK solutions — they only control the robot in FK mode

---

## 3. Inverse kinematics slider flow

### What the binding does

The IK section has six explicit slider rows in XAML, each bound to a ViewModel property.

Example for the X slider:
```
Slider: Value="{Binding X, Mode=TwoWay}"
TextBox: Text="{Binding X, Converter={StaticResource dc}, Mode=TwoWay}"
```

When the user moves the X slider:

1. WPF writes new value to `ViewModel.X`
2. `ViewModel.X` setter updates `mX` and fires `IKChanged`
3. `IKChanged` → `RobotScene.ComputeIK()` runs
4. `PropertyChanged("X")` fires → TextBox updates automatically

When the user types a number in the X textbox:

1. `DoubleConverter.ConvertBack` parses the string (accepts `.` or `,`)
2. WPF writes the parsed double to `ViewModel.X`
3. Same path as above
4. Slider moves to the new value automatically via `PropertyChanged`

**No `mSyncingUI` flag is needed** — the WPF binding engine knows which updates came
from the source versus the target and prevents circular updates internally.

### `ComputeIK()` sequence

1. Build TCP orientation from Euler angles (X-then-Y-then-Z convention)
2. Subtract `LJointZ = 565 mm` from Z — the solver expects height above the L-joint plane, not above the floor
3. Back-solve wrist = TCP_pos − R × TCP_offset
4. Pass wrist pose to `mSolver.ComputeStances()` (up to 8 closed-form solutions)
5. Iterate solutions, apply first one where `a.OK` is true
6. Write solved angles into `mJoints[i].JValue`
7. `mGripper.Xfm = mTip.Xfm`
8. `CheckCollisions()`

### `LJointZ` explained

The `RBRSolver` is configured with `s2=0`, which means it treats the vertical offset from
the S-joint to the L-joint as zero. All Z coordinates fed to the solver are measured from
the L-joint height (565 mm above the floor), not from the floor itself. The constant:

```
LJointZ = Base.Sockets.Z (266.5 mm) + S-link.Sockets.Z (298.5 mm) = 565 mm
```

is subtracted in `ComputeIK()` before the solver call.

### Result

- User edits task-space pose
- Solver converts that into six joint angles
- Robot moves if at least one valid solution exists

---

## 4. TCP interpretation

The scene uses a configurable TCP offset (default 50 mm along tool Z).

### Runtime meaning

- `TcpOffset.Z = -50` means the tool tip is 50 mm in front of the flange along tool Z
- IK offsets the target back by this amount before solving (wrist = TCP − R × offset)
- TCP drawing reads the same offset to position the coordinate frame arrows

### Changing the offset

Via the gear button in the toolbar: opens `TcpOffsetDialog`.
`MainWindow.ShowTcpOffsetDlg()` sets `mRobotScene.TcpOffset = dlg.Offset`, which triggers `ComputeIK()`.

---

## 5. Box face snapping sequence

### Trigger

`RobotScene.Picked(obj)` reacts only if `obj == mBoxMesh`.

### `SnapToFace(hit)` sequence

1. Compute hit relative to box center (using `ViewModel.BX/BY/BZ`)
2. Compare `|rx|`, `|ry|`, `|rz|`
3. Infer dominant face
4. Choose `(newRx, newRy, newRz)` so tool Z points outward from that face
5. Call `ViewModel.SetIKPose(hit.X, hit.Y, hit.Z, newRx, newRy, newRz)`
   — this fires `IKChanged` once → `ComputeIK()` runs once
   — this fires `PropertyChanged` for all 6 IK properties → all 6 sliders update automatically

**No separate `SyncIKSliders()` call is needed** — the ViewModel propagates the update to the
sliders as a side effect of `SetIKPose()`.

### Result

- Clicking the box produces a TCP pose aligned to the hit face
- All six IK sliders and textboxes instantly show the new values

---

## 6. Triangle face snapping sequence

### Trigger

`RobotScene.Picked(obj)` iterates `mTris`, checks if `obj == tri.Mesh`.

### `SnapToTriNode(p1, normal)` sequence

1. `NormalToEuler(normal)` — converts face normal to `(Rx, Ry, Rz)` angles
2. `ViewModel.SetIKPose(p1.X, p1.Y, p1.Z, rx, ry, rz)`
   — same batch update as box snapping

### `NormalToEuler` math

Given a unit normal vector `n`, the goal is to find `Rx` and `Ry` such that after applying
`CoordSystem *= Rot(X,Rx) * Rot(Y,Ry)`, the new `VecZ` equals `n`.

The formula comes from the identity:
```
VecZ = (sin Ry, −sin Rx · cos Ry, cos Rx · cos Ry)
```
Solving: `Ry = asin(n.X)`, then `Rx = atan2(−n.Y, n.Z)`.

---

## 7. Obstacle slider flow

### Controls

The Obstacle section has three explicit slider rows in XAML, bound to `BX`, `BY`, `BZ`.

### Callback path

1. User moves slider → WPF writes new value to `ViewModel.BX` (for example)
2. `ViewModel.BX` setter fires `BoxChanged`
3. `BoxChanged` → `RobotScene.UpdateBox()` runs:
   - `mBoxXfm.Xfm = BoxWorldXfm`
   - `CheckCollisions()`

### Result

- Box moves in world space
- Link collisions update immediately

---

## 8. Script loading

### UI path

- Path textbox bound to `ViewModel.ScriptPath`
- `Load` button bound to `ViewModel.LoadCommand`

### Load sequence

1. User clicks Load → `LoadCommand.Execute()` → `LoadScriptRequested?.Invoke(ScriptPath)`
2. `RobotScene` subscribed: `LoadScriptRequested += LoadScript`
3. `LoadScript(path)`:
   - clear `mScript`
   - read file line by line
   - ignore blank lines and `#` comment lines
   - split on whitespace, parse first 6 values as `X Y Z Rx Ry Rz`
   - append to waypoint list

### Result

- Robot scene stores task-space waypoints for later playback

---

## 9. Script playback

### Start

1. User clicks Play → `PlayCommand.Execute()` → `PlayRequested?.Invoke()`
2. `RobotScene.TogglePlay()`:
   - if script is empty: trace `No script loaded`
   - else: `mScriptIdx = 0`, enable timer, `ViewModel.PlayLabel = "Stop"`
3. Play button label updates via `PropertyChanged` (no manual button reference needed)

### Tick (every 500 ms)

1. Read next waypoint
2. `ViewModel.SetIKPose(pt.X, pt.Y, pt.Z, pt.Rx, pt.Ry, pt.Rz)`
   — `IKChanged` fires → `ComputeIK()` runs
   — `PropertyChanged` fires for all 6 IK properties → all sliders update automatically

### Stop

- When the end of script is reached: `ViewModel.PlayLabel = "Play"`, timer stops
- When user clicks Stop: same

### Improvement vs old code

Previously, `TickScript()` set the 6 `mX/mY/…` fields and called `ComputeIK()` but did NOT
call `SyncIKSliders()`. This meant the visible IK sliders would show stale values during
playback. Now `SetIKPose()` always updates both the 3D robot and the sliders in one step.

---

## 10. Collision triangle add flow

### Trigger

User clicks `Add Triangle…` in `RobotWindow.xaml.cs`.

### Sequence

1. `RobotWindow.OnAddTriangle()` opens `TriangleDialog`
2. User enters: name, group, `P1`, `P2`, `P3`
3. `OnOK()` validates numeric input using `Util.TryParseDouble()`
4. `mScene.AddTri(name, group, p1, p2, p3)` is called
5. `AddTri()` creates `CollisionTri`; builds triangle mesh and `OBBTree`
6. Visual node is added to `mTriGroup`
7. `CheckCollisions()` runs
8. `RefreshTriList()` runs:
   - clears `ViewModel.Triangles`
   - repopulates with `CollisionTriVM` objects
9. `ObservableCollection` change notifies the XAML `ItemsControl`
10. Triangle list in sidebar updates automatically

### Result

- Triangle exists both visually and in collision space
- Sidebar list shows the new entry instantly

---

## 11. Triangle remove flow

1. User clicks `×` button next to a triangle
2. `CollisionTriVM.RemoveCommand.Execute()` → calls `() => RemoveTri(tri)` lambda
3. `RemoveTri(tri)`:
   - removes from `mTris`
   - removes visual node from `mTriGroup`
   - `tri.IsColliding = false`
   - `CheckCollisions()` runs
   - `RefreshTriList()` rebuilds `ViewModel.Triangles`
4. `ItemsControl` updates automatically

---

## 12. CSV triangle import/export

### Import

Accepted formats:

- current: `Name,Group,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z`
- legacy (10-column, no Group): `Name,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z`

The legacy format assigns the imported triangle to group `"Box"`.

### Export

Always exports current format with `Group`.

---

## 13. UI synchronization

### Current behavior

All UI ↔ model updates flow through `RobotViewModel`:

| Event | Sliders update? | Comment |
|---|---|---|
| User moves IK slider | (already there) | Binding is the source |
| User types in IK textbox | Yes | `PropertyChanged` moves slider |
| `ComputeIK()` via snap/script | Yes | `SetIKPose()` fires `PropertyChanged` |
| User moves FK slider | Slider stays | FK sliders directly drive `JValue`; no back-notification from IK |
| `ComputeIK()` sets joint angles | FK sliders stay | `JValue` is set directly on `Mechanism`, bypassing `JointSliderModel.Value` setter |

### FK slider limitation

FK sliders show the joint angles the user set in FK mode. They do **not** reflect the joint
angles computed by the IK solver. This is intentional: FK and IK are separate interaction
modes. Showing solver angles in FK sliders would make them confusing to use.

---

## 14. Important behavior notes

- IK selects the first valid solver stance only
- No explicit solution ranking exists
- No collision-aware IK solution selection exists
- No continuity optimization exists
- No visible tool mesh is attached to `mGripper`; it acts mainly as a transform anchor
- Default triangle group is `"Group1"` (empty group field in dialog is replaced with `"Group1"`)
