# Robot Functional Sequences

## 1. Robot startup

1. `MainWindow.OnLuxReady()`
2. `OpenRobot()` is wired and called
3. `RobotScene` is created if missing
4. `Lux.UIScene` switches to robot mode
5. `RobotWindow` is shown
6. `RobotScene.CreateUI(...)` builds the controls

### Result

- Main viewport shows the 3D robot scene
- Floating palette shows FK, IK, obstacle, script, and triangle controls

---

## 2. Forward kinematics slider flow

### UI path

For each articulated mechanism node where `Joint != EJoint.None`, a slider is created.

Slider callback:

1. slider `ValueChanged`
2. `m.JValue = value`
3. `OnFK()`

### Runtime path

`OnFK()` does:

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

---

## 3. Inverse kinematics slider flow

### IK controls

The IK section exposes:

- `X`
- `Y`
- `Z`
- `Rx`
- `Ry`
- `Rz`

Each slider callback:

1. updates one internal target field
2. calls `ComputeIK()`

### `ComputeIK()` sequence

1. start with `CoordSystem.World`
2. apply `Rx` rotation
3. apply `Ry` rotation
4. apply `Rz` rotation
5. build TCP target using `mHome.Org + (mX,mY,mZ)`
6. offset by `cs.VecZ * 50`
7. call `mSolver.ComputeStances(mCS.Org, mCS.VecZ, mCS.VecX)`
8. iterate up to 8 solver solutions
9. select first `OK` solution
10. write solved joint values into `mJoints`
11. update `mGripper.Xfm`
12. call `CheckCollisions()`

### Result

- User edits task-space pose
- Solver converts that into six joint angles
- Robot moves if at least one valid solution exists

---

## 4. TCP interpretation

The scene uses a 50 mm TCP offset convention.

### Evidence

- IK target is translated by `+ cs.VecZ * 50`
- TCP drawing uses `fcs.Org - fcs.VecZ * 50`

### Meaning

- `Tip` and visual TCP are separated by 50 mm along tool Z
- Tool orientation is central to both IK and TCP visualization

---

## 5. Box face snapping sequence

### Trigger

`RobotScene.Picked(obj)` reacts only if `obj == mBoxMesh`.

### `SnapToFace(hit)` sequence

1. compute hit relative to box center
2. compare `|rx|`, `|ry|`, `|rz|`
3. infer dominant face
4. assign `(mRx,mRy,mRz)` so tool Z points outward
5. set `(mX,mY,mZ)` from hit point relative to `mHome.Org`
6. call `ComputeIK()`
7. call `SyncIKSliders()`

### Result

- Clicking the box produces a TCP pose aligned to the hit face
- Only IK sliders are synchronized afterward

---

## 6. Obstacle slider flow

### Controls

The obstacle section exposes:

- `BX`
- `BY`
- `BZ`

### Callback path

1. slider changes one of `mBX/mBY/mBZ`
2. `UpdateBox()` runs
3. `mBoxXfm.Xfm = BoxWorldXfm`
4. `CheckCollisions()` runs

### Result

- Box moves in world space
- Link collisions update immediately

---

## 7. Script loading

### UI path

- path textbox
- `Load` button

### Load sequence

1. `LoadScript(path)`
2. clear `mScript`
3. read file line by line
4. ignore blank lines
5. ignore `#` comment lines
6. split remaining lines on whitespace
7. parse first 6 values as:
   - `X Y Z Rx Ry Rz`
8. append to waypoint list

### Result

- Robot scene stores task-space waypoints for later playback

---

## 8. Script playback

### Start

1. click `Play`
2. if script empty: trace `No script loaded`
3. reset `mScriptIdx`
4. enable `DispatcherTimer`
5. button text becomes `Stop`

### Tick

Every 500 ms:

1. read next waypoint
2. assign `mX,mY,mZ,mRx,mRy,mRz`
3. call `ComputeIK()`

### Stop

- when end of script is reached
- or when user clicks `Stop`

### Important observation

`TickScript()` does not call `SyncIKSliders()`.  
The robot moves, but the visible IK sliders may not match the current waypoint.

---

## 9. Collision triangle add flow

### Trigger

User clicks `Add Triangle...`

### Sequence

1. `TriangleDialog` opens
2. user enters:
   - name
   - group
   - `P1`
   - `P2`
   - `P3`
3. `OnOK()` validates numeric input
4. `AddTri(...)` creates `CollisionTri`
5. triangle mesh is built
6. `OBBTree` is built for the triangle
7. visual node is added to `mTriGroup`
8. `CheckCollisions()` runs
9. list UI is refreshed

### Result

- Triangle exists both visually and in collision space

---

## 10. Triangle remove flow

1. user clicks delete button
2. triangle is removed from `mTris`
3. visual node is removed from `mTriGroup`
4. `CheckCollisions()` runs
5. list refreshes

---

## 11. CSV triangle import/export

### Import

Accepted formats:

- current:
  - `Name,Group,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z`
- legacy:
  - `Name,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z`

### Export

Always exports current format with `Group`.

---

## 12. UI synchronization gaps

### Present sync behavior

- `SnapToFace()` synchronizes IK sliders
- direct IK edits naturally already match UI
- FK sliders are not synchronized after IK
- script playback does not synchronize IK sliders

### Practical consequence

UI can become stale relative to actual robot state.

---

## 13. Important behavior notes

- IK selects the first valid solver stance only
- no explicit solution ranking exists
- no collision-aware IK solution selection exists
- no continuity optimization exists
- no visible tool mesh is attached to `mGripper`; it acts mainly as a transform anchor