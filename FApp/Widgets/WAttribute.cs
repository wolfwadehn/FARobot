// ╔═╦╗
// ║╬╠╬╦╗ WAttribute.cs
// ║╔╣╠║╣ <<TODO>>
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;

/// <summary>Base class for [Textbox], [Checkbox], [Variant]</summary>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
abstract class InputAttribute (int n) : Attribute {
   public readonly int Index = n;
   public abstract EInput UIType { get; }
}

/// <summary>[Textbox(N)] is attached to a field/property to create an input box</summary>
/// The field should be of type Point2, double or int
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class TextboxAttribute (int n) : InputAttribute (n) {
   public override EInput UIType => EInput.Edit;
}

/// <summary>Indicates ordered Click (index) using which this non-point parameter was computed</summary>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class PhaseAttribute (int n) : Attribute {
   public readonly int Index = n;
}

/// <summary>[Checkbox(N)] is attached to a field/property to create a checkbox</summary>
/// The field should be a boolean
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class CheckboxAttribute (int n) : InputAttribute (n) {
   public override EInput UIType => EInput.Checkbox;
}

/// <summary>[Variant(N)] is attached to a field/property to create a conditional input</summary>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class VariantAttribute (object value) : Attribute {
   public readonly object Value = value;
}

/// <summary>[Choice(N)] is attached to a field/property to create a combobox</summary>
/// The field should be of an Enum type, and the checkbox is populated
/// with the different values of that enum
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class ChoiceAttribute (int n) : InputAttribute (n) {
   public override EInput UIType => EInput.Combobox;
}

/// <summary>[Click(N)] means the field / property is set by the Nth click</summary>
/// The field should be of type Point2
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class ClickAttribute (int n) : Attribute {
   public readonly int Index = n;
}

[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class AngleAttribute () : Attribute { }

/// <summary>[Unsnapped] means the field / property gets the raw (unsnapped) mouse position</summary>
/// The field should be of type Point2
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class UnsnappedAttribute () : Attribute { }

/// <summary>Mouse moves do not update this field/property</summary>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class NoMouseMoveAttribute () : Attribute { }

[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field)]
class HotKeyAttribute (EKey key) : Attribute {
   public readonly EKey Key = key;
}

[AttributeUsage (AttributeTargets.Class | AttributeTargets.Method)]
class DwgCmdAttribute (ECmd mode) : Attribute {
   public readonly ECmd Mode = mode;
}

[AttributeUsage (AttributeTargets.Class)]
class CanRepeatAttribute : Attribute { }

[AttributeUsage (AttributeTargets.Class)]
class NoKeyboardModeAttribute : Attribute { }

/// <summary>Widget does not need any mouse (move/click) input</summary>
[AttributeUsage (AttributeTargets.Class)]
class NoMouseInputAttribute : Attribute { }

/// <summary>No simulated mouse! Instead usual "arrow" cursor is shown</summary>
[AttributeUsage (AttributeTargets.Class)]
class NoSimMouseAttribute : Attribute { }

/// <summary>For mark overlaps, we are "rendering" the overlap marks (rather than placing them in the drawing!)</summary>
[AttributeUsage (AttributeTargets.Class)]
class AlwaysShowFeedbackAttribute : Attribute { }
