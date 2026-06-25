# Relevant File Tree

Only files that exist today are listed.
Files under `obj/` (build artefacts) are omitted.

```
F:\
├── FApp\                              Application project
│   │
│   ├── App.xaml                       WPF application definition
│   ├── App.xaml.cs                    Entry point; global error handlers; CheckExpired()
│   ├── Globals.cs                     Global `using` directives for the whole project
│   ├── Util.cs                        Small WPF helpers: MakeBrush, ParseDouble
│   │
│   ├── MainWindow.xaml                Root window XAML: menu, toolbar host, GL viewport, docked panel
│   ├── MainWindow.xaml.cs             Menu handlers; OpenRobot() toggles panel; Open/Save Cell; toolbar
│   │
│   ├── RobotScene.cs                  ★ 3-D scene: FK/IK, collision, frames, pick&place, planner, cell I/O
│   ├── RobotViewModel.cs              ★ Bindable state (INotifyPropertyChanged); DelegateCommand;
│   │                                    JointSliderModel; CollisionTriVM; WaypointVM; ObjectItemVM; DoubleConverter
│   ├── RobotPanel.xaml                Embedded sidebar (UserControl): sliders, objects, frames, pick&place, waypoints
│   ├── RobotPanel.xaml.cs             Thin code-behind: SetScene(); import/teach/plan button handlers
│   │
│   ├── TcpOffsetDialog.xaml           TCP offset editor layout
│   ├── TcpOffsetDialog.xaml.cs        Reads/writes Vector3 offset; validates input
│   │
│   ├── TriangleDialog.xaml            Add-triangle dialog layout
│   ├── TriangleDialog.xaml.cs         Name/Group/P1-P3 input; CSV import/export
│   │
│   ├── PalletFrameDialog.xaml         3-point frame calibration dialog layout
│   ├── PalletFrameDialog.xaml.cs      P1/P2/P3 input; CSV import/export; collinear check
│   │
│   ├── FApp.csproj                    SDK-style project; targets net10.0-windows
│   │
│   └── Widgets\
│       └── Toolbar.cs                 StackPanel subclass; AddButton() helper
│
├── Wad\                               Assets loaded at runtime via Lib/ZipStmLocator
│   └── FanucX\
│       └── mechanism.curl             Robot arm definition (loaded by RobotScene)
│
├── Bin\                               Build output (FApp.dll, FApp.exe, freetype.dll)
├── FApp.slnx                          Solution file
└── Docu\                              ← you are here
    ├── window-layout.md
    ├── file-tree.md
    ├── sequences.md
    ├── beginner-guide.md
    ├── nori-guide.md
    ├── expert-reference.md
    └── cell-pick-place.md             ← geometry, frames, pick&place, collision, planner, cell I/O
```

## Role summary

| File | What it owns |
|------|-------------|
| `RobotScene.cs` | All 3-D logic: mechanism, IK/FK, collision, imported objects, per-object frames, pick&place, the carried part, the RRT planner, and cell save/load |
| `RobotViewModel.cs` | All bindable data: IK pose, joints, objects + selection + 6-DOF move + frame, pickup/place/part status, waypoint list, obstacle; helpers `JointSliderModel`, `CollisionTriVM`, `WaypointVM`, `ObjectItemVM`, `DelegateCommand`, `DoubleConverter` |
| `RobotPanel.xaml` | The entire embedded sidebar UI layout; no logic |
| `RobotPanel.xaml.cs` | `SetScene()` plus thin click handlers (import, teach, generate, plan) |
| `MainWindow.xaml.cs` | Application shell: starts the robot, hosts the panel, Open/Save Cell, dialogs, menu |
| `Toolbar.cs` | One method: `AddButton()` |

## External dependencies (Nori)

These assemblies are referenced from `N:\` and provide the rendering and geometry
infrastructure.  You do not need to read their source to work on FApp, but you
will call their types frequently:

| Assembly | Key types used |
|----------|---------------|
| `Nori.Core` | `Lib`, `Mechanism`, `RBRSolver`, `Poly`, `Mesh3`, `STLReader`, `OBBTree`, `OBBCollider`, `CoordSystem`, `Matrix3`, `Vector3`, `Point3`, `Color4`, `Bound3` |
| `Nori.Lux` | `Lux`, `Scene3`, `VNode`, `GroupVN`, `XfmVN`, `Mesh3VN`, `MechanismVN`, `TraceVN`, `ETess`, `EShadeMode` |
| `Nori.Host.WPF` | `WPFHost`, `SceneManipulator`, `Hub` (mouse/keyboard observables) |
