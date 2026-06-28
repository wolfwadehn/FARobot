# Scene Objects

Named objects in the FRobot 3D scene.

---

## Obstacle

| Property | Value |
|----------|-------|
| Size     | 200 × 200 × 200 mm |
| Default position (BX, BY, BZ) | 700, 300, 700 mm |
| Color    | Blue (glass shading) |
| Adjustable via | BX / BY / BZ sliders in the sidebar |

The obstacle is a box centred on its BX/BY/BZ origin.
Local Z runs from −100 mm (bottom face) to +100 mm (top face).

---

## Coordinate Systems

Drawn as RGB axes (X = red, Y = green, Z = blue, arrow length 180 mm).
Defined as `AxesVN` instances in `RobotScene.cs`.

| Name     | Position | Notes |
|----------|----------|-------|
| **Dropoff** | (BX, BY, BZ + 100) | Centre of the obstacle's top face. Tracks the obstacle as it moves. |
| **Pickup**  | (700, −200, 700)   | Fixed world position. |

### Adding a new coordinate system

```csharp
new AxesVN { Name = "MyFrame", GetOrigin = () => new Point3 (x, y, z) }
```

Add the line to the `Root` `GroupVN` in `RobotScene.cs` and rebuild.

---

## TCP Frame

Drawn by `TcpVN` — not an `AxesVN` instance and has no `Name`.
Shows the current tool-centre-point position and orientation,
updated every frame from the IK/FK solver.
