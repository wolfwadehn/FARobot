// ╔═╦╗
// ║╬╠╬╦╗ Widget.cs
// ║╔╣╠║╣ <<TODO>>
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
namespace FApp.Widgets;

#region class Widget -------------------------------------------------------------------------------
/// <summary>This is the base class for all Widgets</summary>
abstract class Widget {
   // Constructor --------------------------------------------------------------
   protected Widget () {
      var type = GetType ();
      var cmd = type.GetCustomAttribute<DwgCmdAttribute> ()!.Mode;
      CanRepeat = type.HasAttribute<CanRepeatAttribute> ();
      NoKeyboardMode = GetType ().HasAttribute<NoKeyboardModeAttribute> ();
      NoMouseInput = GetType ().HasAttribute<NoMouseInputAttribute> ();
      AlwaysShowFeedback = GetType ().HasAttribute<AlwaysShowFeedbackAttribute> ();
      NoSimMouse = GetType ().HasAttribute<NoSimMouseAttribute> ();
      Prompt = CmdPrompt.Get (cmd);
   }

   // Properties ---------------------------------------------------------------
   /// <summary>If set, this widget can 'repeat' by Ctrl+Click on the first click</summary>
   public readonly bool CanRepeat;

   /// <summary>The set of fields in this Widget</summary>
   /// Some fields have [Input(N)] attributes to connect them to an input box,
   /// some have [Click(N)] attributes to connect them to a mouse-click etc
   public IReadOnlyList<WField> Fields => mFields;
   List<WField> mFields = [];

   /// <summary>If set, pressing Enter in the input bar has no effect</summary>
   public readonly bool NoKeyboardMode;
   /// <summary>If set, mouse Click is ignored</summary>
   public readonly bool NoMouseInput;
   /// <summary>For mark overlaps, we are "rendering" the overlap marks (rather than placing them in the drawing!)</summary>
   public readonly bool AlwaysShowFeedback;
   /// <summary>No simulated mouse! Instead usual "arrow" cursor is shown</summary>
   public readonly bool NoSimMouse;

   /// <summary>The current phase we're in</summary>
   public int Phase { get; private set; }

   /// <summary>Total number of 'phases' in this widget (number of clicks needed to complete)</summary>
   public int Phases => mPhases;
   int mPhases;

   /// <summary>The set of clicks we used durnig the 'previous' cycle of this widget</summary>
   public List<Point2> PrevClick => mPrevClick;
   protected List<Point2> mPrevClick = [];

   /// <summary>This provides the input boxes and prompts for this command</summary>
   public readonly CmdPrompt Prompt;

   // Methods ------------------------------------------------------------------
   /// <summary>This is called when this widget is 'activated'</summary>
   /// At that point, we gather all the fields in this widget that are decorated
   /// with an [Input] or [Click] field, and we then create all the UI (edit boxes,
   /// check boxes, combo boxes)
   public void Activate (bool iActivate = true) {
      if (!iActivate) {
         OnDeactivated ();
         return;
      }
      foreach (var mi in GetType ().GetMembers (mBF)) {
         var member = new WField (this, mi);
         if (member.NClick >= 0 || member.NInput >= 0) {
            mFields.Add (member);
            mPhases = Math.Max (Phases, member.NClick + 1);
         }
      }

      if (DwgHub.IBar is { } ib) {
         ib.Clear (); ib.SetWidget (this);
         foreach (var field in mFields.OrderBy (a => a.NInput))
            if (field.NInput >= 0 && field.IsVariantActive)
               ib.CreateUI (field);
      }

      OnActivated ();
   }
   static BindingFlags mBF = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

   // Phase management ---------------------------------------------------------
   public void ResetPhase () => SetPhase (0);

   /// <summary>Increment and move to the next Phase</summary>
   public void IncrPhase () {
      if (mHoldPhase) { mHoldPhase = false; return; }
      SetPhase (Phase + 1);
   }

   /// <summary>Ensures minimum specified phase! [Handles Phase correction due to direct command parameter input]</summary>
   public void EnsurePhase (int phase) => SetPhase (Math.Max (phase, Phase));

   void SetPhase (int phase) {
      if (phase == Phase) return;
      if (phase > Phases) // Technically, phase < Phases, but phase == Phases is also meaningful.
         Lib.Check (false, "Coding Error: Widget.Phase out of range");
      Phase = phase;
      if (DwgHub.IBar is { } ib) ib.SetPhase (phase);
   }

   /// <summary>Hold on to the current Phase. [And not allow it to increment]</summary>
   /// Useful for Pick ops, where we want to Pick something,
   /// and until Picked we want to stay put in that Phase
   protected void HoldPhase () => mHoldPhase = true;
   bool mHoldPhase = false;

   void SoftPhaseReset () => SetPhase (mSoftResetPhase); // Support for tandem linear dimension command variants
   protected int mSoftResetPhase = 0;

   // Overrides ----------------------------------------------------------------
   /// <summary>Called when the widget has completed</summary>
   public virtual void Completed () {
      mPrevClick.Clear ();
      for (int i = 0; i < Phases; i++)
         mPrevClick.Add ((Point2)Fields.First (a => a.NClick == i).GetValue ()!);
      SoftPhaseReset ();
   }

   /// <summary>Override this to draw feedback for the widget</summary>
   /// The basic implementation just draws dots at each of the points
   public virtual void DrawFeedback () {
      for (int i = 0; i < Phase; i++) {
         var field = Fields.FirstOrDefault (a => a.NClick == i);
         if (field != null) {
            var point = (Vec2F)(Point2)field.GetValue ()!;
            Lux.Points ([point]);
         }
      }
   }

   /// <summary>Override this to do something when the widget is activated</summary>
   public virtual void OnActivated () { }

   /// <summary>Override this to do something when the widget is de-activated</summary>
   /// Note: This is added later, and only for Bend command widget which needs to pull-down fold preview!
   public virtual void OnDeactivated () { }

   /// <summary>Called when Ctrl+Click is done, to 'repeat' the last shape</summary>
   public virtual void Repeat (Point2 pt) { }

   /// <summary>Override to intercept key presses before keyboard-mode logic runs. Return true if handled.</summary>
   public virtual bool OnKey (KeyInfo info) => false;

   protected bool mRepeating;
}
#endregion

#region class WField -------------------------------------------------------------------------------
class WField {
   // Constructor --------------------------------------------------------------
   /// <summary>Create a WField wrapper given a widget type and a FieldInfo or PropertyInfo</summary>
   public WField (Widget owner, MemberInfo mi) {
      (Owner, MI, NInput, NClick) = (owner, mi, -1, -1);
      if (mi.GetCustomAttribute<InputAttribute> () is { } input)
         (NInput, UIType) = (input.Index, input.UIType);
      if (mi.GetCustomAttribute<ClickAttribute> () is { } click)
         NClick = click.Index;
      if (mi.GetCustomAttribute<PhaseAttribute> () is { } phase)
         NPhase = phase.Index;
      if (mi.GetCustomAttribute<VariantAttribute> () is { } ifs)
         Variant = ifs.Value;
      if (mi.GetCustomAttribute<HotKeyAttribute> () is { } hk)
         HotKey = hk.Key;
      Angle = mi.HasAttribute<AngleAttribute> ();
      Unsnapped = mi.HasAttribute<UnsnappedAttribute> ();
      NoMouseMove = mi.HasAttribute<NoMouseMoveAttribute> ();
   }

   // Properties ---------------------------------------------------------------
   /// <summary>Returns true if this input field is currently 'active'</summary>
   public bool IsVariantActive {
      get {
         if (Variant == null) return true;
         foreach (var pi in Owner.GetType ().GetProperties (mBF))
            if (pi.PropertyType == Variant.GetType ())
               if (Equals (pi.GetValue (Owner), Variant)) return true;
         foreach (var fi in Owner.GetType ().GetFields (mBF))
            if (fi.FieldType == Variant.GetType ())
               if (Equals (fi.GetValue (Owner), Variant)) return true;
         return false;
      }
   }
   static BindingFlags mBF = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

   /// <summary>The label for this input box (read from the cmd-prompt.txt file)</summary>
   public string Label
      => Owner.Prompt.Labels.SafeGet (NInput) ?? $"Input{NInput}";

   // Methods ------------------------------------------------------------------
   /// <summary>Gets the underlying type of this field or property</summary>
   public Type GetFieldType () {
      if (MI is PropertyInfo pi) return pi.PropertyType;
      if (MI is FieldInfo fi) return fi.FieldType;
      throw new NotImplementedException ();
   }

   /// <summary>Fetches the value of this field or property</summary>
   public object? GetValue () {
      if (MI is PropertyInfo pi) return pi.GetValue (Owner);
      if (MI is FieldInfo fi) return fi.GetValue (Owner);
      throw new NotImplementedException ();
   }

   /// <summary>Gets the value of this field to use for the UI</summary>
   /// This is different from GetValue() in the following ways:
   /// - If the value is a Point2, we return a string like "10,20"
   /// - If the value is an integer, we return a string like "12"
   /// - If the value is a double, we return a string like "45.0",
   ///   remembering to convert radians to degrees
   /// - If the value is a bool, we return an integer: 0 or 1
   /// - If the value is an Enum, we get the internal value of that
   ///   enum (0,1,2...)
   public object GetUIValue () {
      object obj = GetValue ()!;
      if (obj is Point2 pt) return $"{pt.X.R6 ()}, {pt.Y.R6 ()}";
      if (obj is int n) return n.ToString ();
      if (obj is double d) {
         if (Angle) d = d.R2D ();
         return d.S6 ();
      }
      if (obj is bool b) return b ? 1 : 0;
      if (obj.GetType ().IsEnum) {
         Array arr = Enum.GetValues (obj.GetType ());
         for (int i = 0; i < arr.Length; i++)
            if (Equals (arr.GetValue (i), obj)) return i;
         return -1;
      }
      return obj;
   }

   /// <summary>Sets this field or property to the given value</summary>
   public void SetValue (object value) {
      if (MI is PropertyInfo pi) pi.SetValue (Owner, value);
      if (MI is FieldInfo fi) fi.SetValue (Owner, value);
   }

   // Fields -------------------------------------------------------------------
   public readonly Widget Owner;       // Widget this belongs to
   public readonly MemberInfo MI;      // The FieldInfo or PropertyInfo
   public readonly EInput UIType;      // If this is bound to a UI, the UI type (edit/checkbox/...)
   public readonly int? NPhase;        // If set, specifies the nth (ordered) click this field value is derived from (reverse click level)
   public readonly int NInput;         // If >= 0, this is tied to an input box
   public readonly int NClick;         // If >= 0, this is tied to a particular click
   public readonly EKey HotKey;        // If set, this checkbox / combobox is tied to a Hotkey
   public readonly object? Variant;    // If set, the variant ENUM this is connected to
   public readonly bool Angle;         // If set, this field is editing an angle
   public readonly bool NoMouseMove;   // Don't forward mouse-move messages to this
   public readonly bool Unsnapped;     // Unsnapped (raw) points
   public object? UIElement;           // The UI element (TextBox / CheckBox / ComboBox)
}
#endregion
