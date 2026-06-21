# Robot Collision Model

## Collision-related files

- `FApp/RobotScene.cs`
- `FApp/TriangleDialog.xaml.cs`
- `N:/Core/Sim/OBBTree.cs`
- `N:/Core/Sim/OBBCollider.cs`
- `N:/Core/Geom/Collision.cs`
- `N:/Core/Sim/Mechanism.cs`
- `N:/Lux/VNodes/MechanismVN.cs`

## Collision objects in the scene

### 1. Robot links

During `RobotScene` construction:

1. iterate `mMech.EnumTree()`
2. get `m.CMesh`
3. if missing, fall back to `m.Mesh`
4. build `OBBTree.From(...)`
5. store in `mLinkOBBs`

This creates a collision tree per mechanism node.

### 2. Box obstacle

The box is generated procedurally:

1. create rectangle `(-100,-100)` to `(100,100)`
2. extrude height `200`
3. translate by `(0,0,-100)`
4. build `mBoxOBB = OBBTree.From(mBoxMesh)`

World placement is controlled by `BoxWorldXfm`.

### 3. Custom collision triangles

Each `CollisionTri`:

1. builds a one-triangle mesh
2. builds `OBB = OBBTree.From(mesh)`
3. creates a translucent `Mesh3VN`

Triangles are stored in world coordinates.

---

## Top-level collision pass

`RobotScene.CheckCollisions()` is the main scene collision routine.

### Step 1: Prepare obstacle state

- `boxOBBW = mBoxOBB.With(BoxWorldXfm)`

### Step 2: Borrow pooled collider

- `using var bc = OBBCollider.Borrow()`

### Step 3: Initialize group hit map

- `"Box"` starts as `false`
- every triangle group is added with `false`

### Step 4: Per-link checks

For each `(Mechanism m, OBBTree linkOBB)`:

1. `wLink = linkOBB.With(m.Xfm)`
2. test `bc.Check(wLink, boxOBBW)`
3. for every custom triangle:
   - test `bc.Check(wLink, tri.OBB.With(Matrix3.Identity))`
4. mark:
   - `m.IsColliding = linkHit`
   - group hit flags

### Step 5: Apply display colors

- `mBoxVN.Color = red or blue`
- every triangle in a hit group turns red
- robot links render in collision color via `MechanismVN`

---

## Group collision behavior

Collision triangles carry:

- `Name`
- `Group`
- `OBB`
- `VN`

Groups are visual collision sets.

### Behavior

If any triangle in a group is hit:

- every triangle in that group is shown as colliding

### Box group

The box uses group key:

- `"Box"`

### Default triangle group

`TriangleDialog` defaults `Group` to `"Group1"` when the group field is left empty.

### Box group tracking

The box obstacle collision state is tracked separately with a dedicated `bool boxHit` variable
inside `CheckCollisions()`, independent of any triangle group named `"Box"`.
This means a triangle in a group called `"Box"` does not interfere with box collision coloring,
and the box does not interfere with triangle group coloring.

---

## `OBBTree` model

`N:/Core/Sim/OBBTree.cs` defines a hierarchical bounding volume structure.

### Structure

An `OBBTree` contains:

- `Pts`
- `Tris`
- `OBBs`

### Important layout rule

- `OBBs[0]` is the root box
- leaf references use negative indices pointing to triangles

### Purpose

This allows:

- fast broad-phase rejection
- gradual refinement
- triangle-level exact checks only when needed

### World transforms

`OBBTree.With(Matrix3 xfm)` returns a transformed view of the same tree data.

That is how robot links and the box are moved without rebuilding the tree.

---

## `OBBCollider` traversal model

`OBBCollider.Check(a, b, oneCrash = true)` performs hierarchical collision checks.

### High-level strategy

1. choose larger tree as `A`
2. transform tree `B` into `A` space
3. root OBB vs root OBB test
4. recurse only if overlap exists

### Traversal pair types

The recursion processes:

- OBB vs OBB
- OBB vs triangle
- triangle vs OBB
- triangle vs triangle

### Exit mode

With `oneCrash = true`:

- returns on first confirmed triangle collision

That is the mode used by `RobotScene`.

---

## `OBBCollider` optimizations

### 1. Transform-on-demand

Instead of transforming the whole second tree up front, the collider lazily transforms only:

- visited points
- visited triangles
- visited OBBs

### 2. Rung counters

Arrays such as:

- `mPtRung`
- `mTriRung`
- `mOBBRung`

avoid re-transforming the same objects within one check cycle.

### 3. Last-collision reuse

The collider stores:

- last A tree UID
- last B tree UID
- last colliding triangle pair

If the next check uses the same trees, it tries that triangle pair first.

This is beneficial for incremental robot motion.

---

## Primitive collision tests

From `N:/Core/Geom/Collision.cs`.

## 1. OBB vs OBB

`Collision.Check(in OBB a, in OBB b)`

Uses SAT with 15 axes:

- 3 axes from A
- 3 axes from B
- 9 cross-product axes

### Role

Broad and mid-phase box rejection.

---

## 2. OBB vs Triangle

`Collision.Check(pts, tri, obb)`

Uses SAT-style tests including:

- edge cross axes
- local AABB overlap
- triangle face normal test

### Role

Intermediate refinement during tree descent.

---

## 3. Triangle vs Triangle

`Collision.TriTri(...)`

Performs full 3D triangle intersection:

- plane-side tests
- canonical reordering
- line overlap checks
- coplanar fallback to 2D projected test

### Role

Final narrow-phase confirmation.

---

## Collision visuals

## Robot links

From `MechanismVN.SetAttributes()`:

- normal color: `mMech.Color`
- collision color: orange-red-like `new Color4(255, 64, 32)`

Collision status comes from `mMech.IsColliding`.

## Box

From `RobotScene.CheckCollisions()`:

- blue when clear
- red when colliding

## Custom triangles

From `CollisionTri.IsColliding`:

- idle color = group color
- red when colliding

---

## What is and is not modeled

## Modeled

- mesh-based link collision proxies
- box obstacle
- user-defined triangle obstacles
- per-link binary collision state
- per-group binary triangle/box collision state

## Not modeled in current scene logic

- self-collision between robot links
- triangle-triangle collisions among custom obstacles
- collision distance / clearance
- contact normals / penetration depth
- continuous collision detection over motion path
- collision-aware IK solution ranking

---

## Practical interpretation

The current collision system is a hierarchical mesh collision pipeline:

1. build an `OBBTree` for each collidable object
2. move trees via transforms, not rebuilds
3. reject quickly with OBB overlap
4. descend only into overlapping regions
5. confirm with exact triangle intersection
6. report simple scene-level booleans for coloring

This is substantially more precise than simple AABB testing while still remaining suitable for interactive updates after every slider move.