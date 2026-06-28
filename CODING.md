# Code Style

## Class / Region Structure

Each top-level type is wrapped in a `#region` padded with `-` to **column 100**:

```csharp
namespace Flux;

#region class Part --------------------------------------------------------------------------------
/// <summary>Summary for class Part</summary>
/// Extended multi-line documentation if needed.
public class Part {
    ...
}
#endregion
```

Inside the type, each segment is a `#region` padded with `-` to **column 70**. Segments must appear in this exact order (omit any that are empty):

```
#region Constructors ---------------------------------------------
#region Properties -----------------------------------------------
#region Methods --------------------------------------------------
#region Operators ------------------------------------------------
#region Implementation -------------------------------------------
#region Nested Types ---------------------------------------------
#region Private Data ---------------------------------------------
```

- **Constructors** — public/protected constructors and static factory methods (`static T Make(...)`). Private constructors and static constructors go in Implementation.
- **Properties** — all public properties, public fields, and events, in alphabetical order. Where a simple field suffices, prefer a field over a property.
- **Methods** — all public and protected methods, in alphabetical order; overloads sorted by increasing parameter complexity. Overrides of Flux base class methods belong here; overrides of external base classes (`object.ToString()`, WPF overrides) go in Implementation instead.
- **Operators** — public type-conversion and arithmetic operators.
- **Implementation** — private methods, external-base overrides. Also named sub-segments like `#region Object overrides` are used here when overriding multiple `object` methods.
- **Nested Types** — nested classes, structs, enums (public first, then private, alphabetically).
- **Private Data** — private instance fields (alphabetical), then private static fields (alphabetical).

**Backing-field placement exceptions** (do NOT put in Private Data):

```csharp
// Field for a property with logic — place immediately after the property
public DRange[] ACBShadow {
   get => mACBShadow ??= [];
   set => BA.Set (ref mACBShadow, value);
}
DRange[] mACBShadow;

// Field used only inside one method — place immediately after that method
public Rect2 GetBound () {
   if (mBound.IsEmpty) { /* compute */ }
   return mBound;
}
Rect2 mBound;
```

## Naming

| Symbol | Convention | Example |
|--------|-----------|---------|
| Private instance field | `m` + PascalCase | `Point3 mStartPoint;` |
| Private static field | `s` + PascalCase | `static int sCount;` |
| Au-serialized cached field | `_` prefix | `float[] _defaultClampPos;` |
| Enum type | `E` prefix | `enum EKind`, `enum EFlags` |
| Interface | `I` prefix | `interface IDisplayDevice` |
| Class / struct | PascalCase, no prefix | `class PlineIntersector` |
| Singleton accessor | `It` | `public static MCSettings It` |

The `_` prefix (instead of `m`) marks cached fields in classes serialized with the Au metadata system — they are automatically excluded from serialization. Use `m` for everything else.

Never write `private` explicitly — it is the default access modifier.

Don't repeat the namespace or class name in member names: inside `class FChassis`, use `MCSettings` not `FChassisMachineSettings`.

Method names must be proper English words — single-letter abbreviations like `TA` are rejected.

Enum types must not carry a `Type` suffix.

## Spacing and Formatting

- **Indentation**: 3 spaces (no tabs), CRLF line endings.
- **Opening brace**: always on the same line — never Allman style.
- **Space before `(`**: required in control-flow and standalone calls, not after `.`:
  ```csharp
  if (x)          while (x > 0)       MyMethod ()
  list.Add (item) obj.Method ()        new Point2 (x, y)
  ```
- **No consecutive blank lines**: at most one blank line between any two members.
- **Single-statement bodies** for `if`/`while`/`for` with a single statement do not need braces:
  ```csharp
  while (item is not null and not ListBoxItem)
     item = VisualTreeHelper.GetParent (item) as FrameworkElement;
  ```
- **File-scoped namespaces**: `namespace Flux;` not `namespace Flux { }`. Add one blank line after the namespace declaration.

## `using` Directives

System/Microsoft namespaces come before user-defined (Flux) ones. Remove all unused `using` statements before committing. Each project has a `Globals.cs` that holds `global using` directives shared across the project — do not duplicate them in individual files.

Using aliases are fine for disambiguation:

```csharp
using StrMap = System.Collections.Generic.Dictionary<string, string>;
using static Flux.BendMachine.EFlags;
```

## Expression Bodies

Use `=>` for any member whose body is a single expression — properties, getters, setters, constructors, methods, and operators:

```csharp
// Property
public Point3 Start => mStart;

// Lazy property with backing field
E3Plane YNegPlane => mYNegPlane ??= Model.Ents.OfType<E3Plane> ().Single (a => a.ThickVector.Z.EQ (1));
E3Plane mYNegPlane;

// Constructor
public GCodeGenerator (Process process) => mProcess = process;

// Method
public static double D2R (this double degrees) => degrees * Math.PI / 180;

// Getter + setter on one line
public double this[int i, int j] { get => A[i, j]; set => A[i, j] = value; }

// Operator
public static XForm4 operator * (XForm4 a, XForm4 b) => a.MultiplyNew (b);
```

## `readonly` on Struct Methods

In structs, mark methods that do not mutate state as `readonly`:

```csharp
public readonly double DistTo (Point2 b) { ... }
public readonly Point2 Move (double dx, double dy) => new Point2 (X + dx, Y + dy);
```

## C# Idioms

```csharp
// Null-conditional and null-coalescing
agent?.SwitchedMaterial ();
mPlane ??= FindPlane ();
return [.. items.Where (a => a.IsActive)];   // ?? [] for empty default

// Target-typed new()
XForm4 matrix = new ();
Point3 prev = new (x, y, z);

// Collection expressions — always prefer over new List<T>() / new[] {}
List<string> list = [];
List<GCodeSeg>[] mDrawable = [[], []];
mPriority = [EKind.Hole, EKind.Cutout, EKind.Notch];
Lux.Draw (EDraw.Lines, [pt, pt2]);

// Spread operator instead of .ToList()
return [.. Cuts.Where (a => a.Head == 0).OrderBy (a => Priority.IndexOf (a.Kind))];

// String ranges instead of .Substring()
ncname = ncname[..20];      // Substring(0,20)
var suffix = name[5..];     // Substring(5)

// Pattern matching
if (item.Kind is EKind.Hole or EKind.Mark) { }
if (obj is Arc3 arc) { ... }
if (thick is >= 3.5 and < 4.5) { }
while (item is not null and not ListBoxItem) item = GetParent (item);

// switch expression over if-else chains
var result = kind switch {
   EKind.Hole => "hole",
   EKind.Mark => "mark",
   _          => "other",
};

// List.Remove returns bool — don't check Contains first
if (itemsSource.Remove (droppedData)) { ... }

// Path.Join for paths
var path = Path.Join (baseDir, "options.ini");

// ReadOnlySpan<T> for read-only array parameters
void Compute (ReadOnlySpan<double> axes) { ... }

// int sentinel (-1) instead of bool? for lazy flags
static int sIsInstalled = -1;

// stackalloc for temporary point buffers on hot paths
Span<Point2> buffer = stackalloc Point2[2];
```

Direct method references instead of lambdas where signatures match:

```csharp
Dispatcher.Invoke (mOverlay.Redraw);   // not () => mOverlay.Redraw ()
items.ForEach (Process);               // not x => Process (x)
```

## Magic Numbers

Replace all unexplained numeric literals with named constants:

```csharp
// Wrong
if (distance > 0.001) { }
mBuffer = new byte[4096];

// Correct
const double Tolerance = 0.001;
const int BufferSize = 4096;
if (distance > Tolerance) { }
mBuffer = new byte[BufferSize];
```

**Exceptions** (not magic numbers): `-1, 0, 1, 2, 3, 4, 5` in loops; array indexing `[0]`, `[1]`; `360`, `180`, `90` for geometry; `0.5` for halving; `Math.PI`; coordinate origin `(0, 0, 0)`.

## Local Functions

Place local functions at the **end** of the enclosing method, preceded by the `// Helper ...` delimiter:

```csharp
public void Clear (EClear clear) {
   if (Set (EClear.BendTech)) Q.BendTech = null;
   if (Set (EClear.FoldTech)) Q.FoldTech = null;

   // Helper ...............................
   bool Set (EClear bit) => (clear & bit) != 0;
}
```

Prefer `static` local functions when they capture nothing.

## Singleton Pattern

```csharp
// Simple (non-thread-safe)
public static MCSettings It => mIt ??= new ();
static MCSettings mIt;

// Thread-safe
public static MCSettings It => mLazy.Value;
static readonly Lazy<MCSettings> mLazy = new (() => new ());
```

The singleton property must always be named `It`.

## XML Documentation

Every `public` member in a `public` type must have a `<summary>` tag. Use the **single-line** form — never multi-line `<summary>`:

```csharp
/// <summary>Returns the distance to another Point2</summary>
public readonly double DistTo (Point2 b) { ... }
```

Add `<param>` and `<returns>` only when genuinely useful; never commit empty generated nodes. Additional narrative goes on continuation lines without a tag:

```csharp
/// <summary>Returns the lie of this point on the line a-b</summary>
/// The lie is the normalized position: 0 = at a, 1 = at b, 0.5 = midpoint.
/// <param name="a">Start of the reference line</param>
/// <param name="b">End of the reference line</param>
public readonly double LieOn (Point2 a, Point2 b) { ... }
```

Members in `internal`/`private` types do not require XML docs.

## Comments

Comments explain the **why**, not the what. A non-obvious invariant, a workaround, a license gate, or a subtle constraint. Remove dead code rather than commenting it out; use a full-sentence `// TODO:` with context and a case number when something is intentionally temporary:

```csharp
// TODO: Stop positions are hard-coded pending the backgauge calibration API (Case 44801).
```

Vague TODOs (`// TODO fix this`, `// TODO calc coords`) are rejected.

---

## Design Principles

These principles are derived from 1,307 review comments across all closed PRs. They represent how Metamation evaluates design decisions.

## D001 — Prefer 2D Over 3D When Possible

When a problem can be solved in 2D, do not introduce 3D. Before adding `Point3`/`XForm4`, confirm Z is actually needed.

## D002 — Every Cache Field Needs an Explicit Invalidation Strategy

When introducing a cached field (prefixed `_`), immediately define its invalidation trigger. Ask:
- What action invalidates this cache?
- Is there a second code path that modifies related state but doesn't reset the cache?
- Is it safer to compute lazily at the call site instead of caching?

## D003 — A Method Doing Two Things Must Be Split or Renamed

If a method has two distinct responsibilities, either split it into two methods or rename it to make both responsibilities explicit. Prefer returning data and letting the caller decide what to do with it.

## D004 — Design for Testability — Methods Should Return Data, Not Send It

Methods that write files, send HTTP messages, or modify global state cannot be unit tested cleanly. Extract the pure computation into a return value; let the caller decide what to do with it.

## D005 / D014 — Never Encode Machine Names in Logic

Never write `if (machine is B36)` or `if (ePost == EPost.L95BC)` directly in logic. Add a named boolean property to the machine class that encodes the semantic:

```csharp
// Wrong
if (mc.IsB36Series || mc.Model == "LCB") { ... }

// Correct — in BendMachine:
/// <summary>True for machines that support the LCB V2 NC format</summary>
public bool IsLCBV2 => IsB36Series || Model.StartsWith ("LCB");
// then in logic:
if (mc.IsLCBV2) { ... }
```

New machine series will be added — centralizing the condition means you update one property, not 10 scattered `if` blocks.

## D006 — APIs Must Not Expose Temporary or Internal Fields

Do not add public properties to interfaces or base classes for fields that are implementation details or transitional. Expose the semantic intent, not the implementation detail.

## D007 — Compute Once, Apply Transform — Don't Reconvert

Converting between types (e.g. Mesh → Collider) is expensive. Once you have a converted form, keep it and apply transforms instead of re-converting:

```csharp
// Wrong
Collider c = ToCollider (mesh.ApplyTransform (xfm));

// Correct
Collider c = ToCollider (mesh);
c = c.ApplyTransform (xfm); // cheap
```

## D008 — Use `ReadOnlySpan<T>` for Read-Only Array Parameters

When a method takes an array argument and only reads from it, declare the parameter as `ReadOnlySpan<T>`. This prevents accidental mutation and enables stack allocation.

## D009 — Use `int` with `-1` Sentinel for Lazy Boolean Flags

Lazy-initialized boolean flags (tri-state: unknown/true/false) should use `int` with `-1` as uninitialised, not `bool?`. This avoids boxing and has better JIT inlining.

## D010 — Zero Compiler Warnings

Warnings are treated identically to errors. A PR with warnings will be rejected. If marking something `[Obsolete]` introduces warnings in calling code, resolve all call sites first.

## D011 — Never Return Silently on Error

When something goes wrong (file not found, invalid state, null reference), always log a meaningful message. Silent returns make bugs impossible to diagnose:

```csharp
// Wrong
if (!Directory.Exists (path)) return;

// Correct
if (!Directory.Exists (path)) {
    Log.Error ($"Directory not found: {path}");
    return;
}
```

## D012 — Comments Must Explain Non-Obvious Decisions

Add a comment whenever the code does something surprising, license-gated, or where the "why" isn't visible from the code itself.

## D013 — Memory and File Size Awareness

Always question whether an increase in binary/file size is justified. Before adding a large data file: Is the size proportional to the value? Can the data be compressed? Can it be generated at runtime?

## D015 — Validate Bounds After Any Geometric Transform

After applying a transform to a part or mesh, always check that it does not collide with surrounding components. Don't assume the transform is safe.

## D016 — Use `Path.Join` Not String Concatenation for Paths

```csharp
// Wrong
var path = baseDir + "\\" + "options.ini";

// Correct
var path = Path.Join (baseDir, "options.ini");
```

## D017 — Verify Original Bug Is Still Fixed When Reverting

When your PR removes something added by a prior PR to fix a bug, explicitly verify the original bug is still fixed and reference the original FogBugz case.

## D018 — Extract Repeated Copy-Paste Into a Shared Function

Duplicated blocks that differ only by parameter values should become a parameterized helper. If you're tempted to copy-paste and tweak, pause and consider factoring it out.

## D019 — Domain Terminology Must Match Flux Vocabulary

Use the established Flux domain vocabulary, not generic engineering or NC-code terminology. When in doubt, use the same term that appears in the Flux UI and existing codebase.

## D020 — Scoped Singletons Need a Composite Key

When implementing a singleton that may need to be scoped (by name, priority, or configuration), the key should encode all distinguishing attributes.

## D021 — Prefer `IEnumerable<T>` When Caller Decides Materialisation

Return `IEnumerable<T>` from methods when the caller determines whether to materialise the collection. Only return `List<T>` if you must guarantee multiple enumeration or O(1) count.

## D022 — Don't Buffer a Stream Just to Get First/Last

When you only need the first and last element of a large stream, do not buffer the entire stream:

```csharp
// Wrong — buffers everything
var all = stream.ToList ();
var first = all[0]; var last = all[^1];

// Correct
T first = default, last = default; bool any = false;
foreach (var x in stream) { if (!any) { first = x; any = true; } last = x; }
```

## D023 — `switch` Expression Over Long `if-else` Chains

When selecting a value from multiple enum/string cases, use a `switch` expression (already covered in C# Idioms section above).

## D024 — Validate Assumptions Before Reusing Existing Patterns

Before using an API in a new scenario (e.g., a flag designed for one use case applied in another), verify that the original assumptions still hold.

## D025 — Urgent Fix: Minimal PR + Separate Refactor Case

If a PR is needed urgently (e.g., to fix an exception before a build), do the minimal safe fix now and create a FogBugz case for the refactor. Don't block an urgent fix on a refactor.
