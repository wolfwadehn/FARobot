# Practical Mental Model: How to Use Nori

Nori is the internal rendering and geometry library that FRobot is built on.
You interact with it through three assemblies:

| Assembly | What it gives you |
|----------|-------------------|
| `Nori.Core` | Math types, file I/O, geometry |
| `Nori.Lux` | 3-D rendering (scenes, VNodes, draw calls) |
| `Nori.Host.WPF` | WPF integration (GL host control, input observables) |

---

## The Lux render loop

You never write a render loop yourself.  Nori runs it internally.

Your job is to:
1. Set `Lux.UIScene` to the scene you want rendered.
2. Build a tree of `VNode` objects and assign it to `scene.Root`.
3. Implement `Draw()` and `SetAttributes()` on each VNode.

Lux calls your nodes every frame (or on demand for non-streaming nodes).

### `SetAttributes()` vs `Draw()`

```csharp
public override void SetAttributes () {
    Lux.Color     = Color4.Red;
    Lux.LineWidth = 2f;
    Lux.ZLevel    = 10;     // draw-order layer; higher = on top
}

public override void Draw () {
    Span<Vec3F> pts = ...;
    Lux.Lines (pts);        // or Lux.Text(), Lux.Points(), etc.
}
```

`SetAttributes` is called first each frame to configure OpenGL state.  
`Draw` is called immediately after to emit geometry.  
Never do heavy computation in `Draw` — precompute in `SetAttributes` or in response
to data changes.

### When does `Draw` get called?

- **`Streaming = true`** — every frame, unconditionally (use for animated content
  like `TcpVN` and `InfoVN`).
- **Default** — only when the node is marked dirty (`Redraw()` is called, or the
  scene refreshes).
- **`[RedrawOnZoom]` attribute** — also re-draws when the user pans or zooms
  (needed for any geometry expressed in screen-space units).

Call `node.Redraw()` to request a redraw of a specific node.

---

## Key Lux draw calls

```csharp
// Lines: each consecutive pair of points is one line segment
Lux.Lines (Span<Vec2F> pts);   // 2-D
Lux.Lines (Span<Vec3F> pts);   // 3-D

// Points
Lux.Points (Span<Vec3F> pts);

// Text (2-D, screen-space)
Lux.Text (string text, Vec2S pixelPos);
Lux.Text2D (string text, Point2 worldPos, ETextAlign align, Vec2S pixelOffset);

// 3-D mesh (typically via Mesh3VN, not direct calls)
```

Set drawing properties *before* the draw call — they are not parameters:

```csharp
Lux.Color     = Color4.Green;   // RGBA as bytes
Lux.LineWidth = 1.5f;
Lux.PointSize = 9f;
Lux.TypeFace  = myFace;
Lux.ZLevel    = 70;             // 0–100; default 0
```

---

## Scenes

```csharp
// Activate a scene (replaces whatever was shown before)
Lux.UIScene = new RobotScene();

// Scene3 base class — you fill in:
BgrdColor = Color4.Gray(64);
Bound     = new Bound3(-1200, -1200, 0, 1200, 1200, 1500);
Root      = new GroupVN([nodeA, nodeB, ...]);
```

`Bound` controls the initial camera fit when `ZoomExtents()` is called.

### Sub-scenes (picture-in-picture)

```csharp
// Show a second scene in a rectangle (0–1 normalized coords)
Lux.AddSubScene(anotherScene, new Rect2(0.75, 0.75, 0.95, 0.95));
Lux.RemoveSubScene(anotherScene);
```

FRobot no longer uses sub-scenes (the fold-preview was removed with the drawing
editor).

---

## VNode lifetime

VNodes live as long as you hold a reference.  Add them to a `GroupVN` to make
them visible; remove them to hide them:

```csharp
GroupVN group = new GroupVN([]);
group.Add(myNode);      // now visible
group.Remove(myNode);   // now hidden
```

`GroupVN` is just a container — it has no visual output of its own.

`XfmVN` applies a `Matrix3` transform to its single child:

```csharp
var mBoxXfm = new XfmVN(Matrix3.Translation(x, y, z), mBoxVN);
// Later, to move the box:
mBoxXfm.Xfm = Matrix3.Translation(newX, newY, newZ);
```

---

## Mechanism (robot kinematics)

```csharp
// Load the arm description
Mechanism root = Mechanism.Load("path/to/mechanism.curl");

// Navigate the tree
Mechanism tip = root.FindChild("Tip")!;
Mechanism[] joints = "SLURBT".Select(c => root.FindChild(c.ToString())!).ToArray();

// Set a joint angle (radians internally, degrees for display)
joints[0].JValue = 45.0.D2R();   // D2R() = degrees to radians

// Read the world transform of any node (updated by the mechanism after JValue set)
Matrix3 tipWorld = tip.Xfm;

// Iterate all nodes
foreach (Mechanism m in root.EnumTree()) { ... }
```

The mechanism computes forward kinematics automatically when `JValue` changes.
You just read `m.Xfm` to get the result.

---

## OBB collision detection

OBB = Oriented Bounding Box.  `OBBTree` is a hierarchy of OBBs for fast
overlap queries.

```csharp
// Build once from a mesh
OBBTree obb = OBBTree.From(myMesh);

// Transform to world space (does not modify the original)
OBBTree obbWorld = obb.With(worldMatrix);

// Check collision — borrow a collider to avoid allocation
using var bc = OBBCollider.Borrow();
bool hit = bc.Check(obbA, obbB);
```

Borrow / return pattern avoids per-frame allocation.  Always use `using`.

---

## Useful math types

| Type | Description |
|------|-------------|
| `Point3(x,y,z)` | A position in 3-D space |
| `Vector3(x,y,z)` | A direction / displacement |
| `Matrix3` | 4×4 homogeneous transform; `Matrix3.Translation(x,y,z)`, `.Rotation(EAxis.X, rad)` |
| `CoordSystem` | A position + three axes; `CoordSystem.World` = identity |
| `Bound3` | Axis-aligned bounding box in 3-D |
| `Color4(a,r,g,b)` | ARGB color as bytes; `Color4.Red`, `.Blue`, `.Gray(n)` |
| `Vec3F(x,y,z)` | Single-precision 3-D vector used in draw calls |
| `Vec2S(x,y)` | Integer 2-D vector (pixel coordinates) |

Extension methods you will see everywhere:

```csharp
45.0.D2R()   // degrees → radians
1.5.R2D()    // radians → degrees
x.Clamp()    // clamp to [0,1]
x.Clamp(lo, hi)
x.EQ(y)      // double equality with epsilon
```

---

## `Lib` — the global toolkit

```csharp
Lib.Init()                         // must be called once at startup
Lib.Register(locator)              // register an asset source (files or zip)
Lib.ReadLines("pix:path/to.txt")   // read text asset
Lib.ReadBytes("pix:path/to.ttf")   // read binary asset
Lib.Trace("debug message")         // prints via Lib.Tracer delegate
Lib.Post(action)                   // queue an action on the UI thread
Lib.GetLocalFile("name.ext")       // path in the app's local data folder
```

The `"pix:"` prefix is a registered asset namespace.  In dev mode it maps to
`F:/Wad/`; in release it maps to `FRobot.wad` (a zip file).
