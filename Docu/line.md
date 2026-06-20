# How Line Creation Works

## For a Beginner

Think of drawing a line as a small back-and-forth conversation between you and the program.
The program asks for two pieces of information — a start point and an end point. Everything
in the application is designed around managing that conversation.

**Step 1 — You click the Line button**

The toolbar reads a text file (`manifest.txt`) to know which buttons to show. When you click
"Line", the toolbar looks up the word "Line" in a dictionary and finds the class responsible
for the Line command. It creates a fresh copy of that class (`LineMaker`) and hands it to the
central coordinator (`DwgHub`).

**Step 2 — The program sets up the input area**

`LineMaker` has two input fields declared: `Start` and `End`. When the class is activated, it
scans itself using reflection (a C# feature that lets code look at its own structure) and
discovers those two fields. It then tells the input bar at the bottom of the screen to create
two text boxes labelled "Start" and "End".

**Step 3 — You move the mouse**

Every time the mouse moves over the drawing area, the program snaps the cursor position to
nearby geometry (endpoints, midpoints, etc.) and updates the `Start` field with the current
position. Nothing is drawn in the canvas yet because you haven't clicked the first point.

**Step 4 — You click the first point**

The program records where you clicked as the `Start` of the line. It advances an internal
counter ("we're now in phase 1") and highlights the "End" text box, telling you what it's
waiting for next.

**Step 5 — You move the mouse again**

Now the `End` field gets updated with every mouse movement. Because you already have a `Start`
and the program has a tentative `End`, it can draw a preview line on screen. This preview is
redrawn every frame — it's not stored in the drawing yet, it's just visual feedback.

**Step 6 — You click the second point**

This is the last piece of information needed. The program calls `Completed()`, which creates a
real line object (`E2Poly` with a `Poly.Line` inside), wraps it in an undo record, and pushes
it into the drawing. The line is now permanently in the drawing and will be saved to file.

---

## For an Expert

### 1. Discovery (startup, one-time)

`DwgHub.WidgetMap` is lazily built on first access. It scans the executing assembly for all
non-abstract types assignable to `Widget` and reads their `[DwgCmd(ECmd.X)]` attribute:

```csharp
// DwgHub.cs:156-172
foreach (var type in allTypes.Where(a => a.IsAssignableTo(typeof(Widget)))) {
    if (type.IsAbstract) continue;
    DwgCmdAttribute attr = type.GetCustomAttribute<DwgCmdAttribute>()!;
    mWidgetMap.Add(attr.Mode, type);
}
// Result: WidgetMap[ECmd.Line] = typeof(LineMaker)
```

### 2. Button click → widget activation

`Toolbar.OnClicked(Border b)` (Toolbar.cs:123-135) reads `b.Tag` (cast to `ECmd`), looks up
`WidgetMap[ECmd.Line]`, calls `Activator.CreateInstance(type)` to produce a `LineMaker`, then
assigns it:

```csharp
DwgHub.Widget = widget;
```

The `DwgHub.Widget` setter (DwgHub.cs:139-149) deactivates the previous widget, then calls
`widget.Activate()`.

### 3. Activation: reflection scan + UI construction

`Widget.Activate()` (Widget.cs:61-82) reflects over the concrete type using
`BindingFlags.Instance | Static | NonPublic`:

```csharp
foreach (var mi in GetType().GetMembers(mBF)) {
    var member = new WField(this, mi);
    if (member.NClick >= 0 || member.NInput >= 0) {
        mFields.Add(member);
        mPhases = Math.Max(Phases, member.NClick + 1);
    }
}
```

For `LineMaker` this finds:

| Member  | Attributes               | NClick | NInput | UIType |
|---------|--------------------------|--------|--------|--------|
| `Start` | `[Textbox(0)][Click(0)]` | 0      | 0      | Edit   |
| `End`   | `[Textbox(1)][Click(1)]` | 1      | 1      | Edit   |

`mPhases` becomes `max(0+1, 1+1) = 2`.

`InputBar.SetWidget(this)` calls `CreateUI(field)` for each field with `NInput >= 0`, creating
a 100 px `TextBox` for each `Point2` field and wiring `TextChanged` →
`DwgHub.OnTypeIn(n, text)`.

### 4. Mouse move pipeline

`Hub.Mouse.Moves` is an `IObservable<Vec2S>` (Nori reactive stream). The subscription wired in
the `DwgHub.Dwg` setter calls `DoMouse(pix, false)`:

```csharp
// DwgHub.cs:298-333
RawMousePos = ToWorld(pix);                              // screen px → world coords
pt = MousePos = snap.Snap(RawMousePos, PickAperture);   // aperture = 3.5 * DPI * pxScale

foreach (var field in Widget.Fields.Where(a => a.IsVariantActive && !a.NoMouseMove))
    if (field.NClick == Widget.Phase) { field.SetValue(pt); break; }
foreach (var field in Widget.Fields)
    IBar.DataToUI(field);   // sync TextBox display

Redraw();   // invalidates WidgetVN, DwgSnapVN, DwgConsLineVN
```

In phase 0 only `Start` (NClick==0) receives updates. `PolyMaker.DrawFeedback()` guards with
`if (Phase > 0 && Make(Phase) is Poly p) Lux.Poly(p)` — so no preview is rendered yet.

### 5. First click — phase transition

`DoMouse(pix, true)` → `OnClick(pt)` (DwgHub.cs:204-236):

```csharp
// "Prime" the current AND next click receiver simultaneously:
foreach (var field in Widget.Fields.Where(a => a.IsVariantActive))
    if (field.NClick == currPhase || field.NClick == currPhase + 1)
        field.SetValue(pt);

// Phase 0, Phases-1 = 1  →  NOT last phase  →  IncrPhase
if (Widget.Phase == Widget.Phases - 1) Widget.Completed();
else Widget.IncrPhase();    // Phase becomes 1
```

After `IncrPhase`, `InputBar.SetPhase(1)` highlights the "End" TextBox and updates the step
prompt text in `mStatus`.

### 6. Mouse move in phase 1 — live preview

Now `Widget.Phase == 1`. The mouse-move loop sets `End` (NClick==1).
`PolyMaker.DrawFeedback()` (BaseWidget.cs:109-113):

```csharp
if (Phase > 0 && Make(Phase) is Poly p)   // Phase=1 satisfies > 0
    Lux.Poly(p);
```

`LineMaker.Make(1)` returns `Poly.Line(Start, End)`. `Lux.Poly` submits this to the GPU
command buffer each frame, producing the rubber-band preview.

### 7. Second click — completion

`OnClick(pt)` with `Phase==1 == Phases-1` → `Widget.Completed()`.

`Widget.Completed()` (base, Widget.cs:116-121) saves each `[Click(n)]` field's current value
into `mPrevClick` for `[CanRepeat]` support.

`PolyMaker.Completed()` (BaseWidget.cs:101-104):

```csharp
public override void Completed() {
    base.Completed();
    if (Make(Phases - 1) is Poly p)     // Make(1) = Poly.Line(Start, End)
        Add(Prompt.Name, new E2Poly(mDwg.Layers.Current, p));
}
```

`DwgWidget.Add(string, Ent2)` (BaseWidget.cs:81-83):

```csharp
protected void Add(string description, Ent2 ent)
    => new ModifyDwgEnts(mDwg, description, [ent], []).Push();
```

`ModifyDwgEnts.Push()` appends the `E2Poly` to `mDwg.Ents`, registers itself on
`UndoStack.Current`, and marks the drawing dirty.

### 8. Key data structures along the path

| Object           | Type                                      | Role                                                          |
|------------------|-------------------------------------------|---------------------------------------------------------------|
| `ECmd.Line`      | `enum`                                    | Canonical command identifier                                  |
| `LineMaker`      | `class : PolyMaker : DwgWidget : Widget`  | Owns the two fields, implements `Make()`                      |
| `WField`         | `class`                                   | Wraps a `FieldInfo`/`PropertyInfo` + attribute metadata       |
| `Poly`           | Nori struct                               | Immutable sequence of `Seg` (line + arc segments)             |
| `E2Poly`         | Nori class                                | Drawing entity wrapping a `Poly` + a `Layer2` reference       |
| `ModifyDwgEnts`  | Nori `UndoStep` subclass                  | Stores add/remove lists; `Push()` applies and registers undo  |
| `CmdPrompt`      | `class`                                   | Lazy-loaded metadata from `cmd-prompt.txt`; provides name "Line" |

### 9. The `static` field trick

`Start` and `End` are declared `static` in `LineMaker` (PolyMaker.cs:115-116):

```csharp
[Textbox(0), Click(0)] static Point2 Start = (10, 10);
[Textbox(1), Click(1)] static Point2 End   = (20, 15);
```

Their values persist across instances — activating Line a second time pre-fills the text boxes
with the last-used coordinates. This gives "memory" across activations with no explicit
persistence layer.

### 10. `[CanRepeat]` path

`LineMaker` is decorated `[CanRepeat]`. If the user Ctrl-clicks during phase 0,
`DwgHub.OnClick` detects `iCtrl && currPhase == 0 && Widget.CanRepeat` and calls
`Widget.Repeat(pt)`, overridden in `PolyMaker` (BaseWidget.cs:122-133):

```csharp
public override void Repeat(Point2 pt) {
    mRepeating = true;
    for (int i = 0; i < mPrevClick.Count; i++)
        Fields.First(a => a.NClick == i).SetValue(pt + (mPrevClick[i] - mPrevClick[0]));
    Completed();   // immediately produces the entity at the translated positions
}
```

This replays the previous click offsets relative to the new anchor point, creating a translated
copy of the last line in a single click.
