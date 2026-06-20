// ╔═╦╗
// ║╬╠╬╦╗ DwgHub.cs
// ║╔╣╠║╣ Central coordinator for Dwg editing (tracks current Widget, current Dwg etc)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Widgets;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
namespace FApp;

using static Util;

#region class DwgHub -------------------------------------------------------------------------------
/// <summary>DwgHub is a central coordinator for drawing editing (holds current Dwg, current Widget etc)</summary>
/// Here are some key 'whiteboard-like' properties it manages
/// - Dwg : the current drawing being edited (if any)
/// - Widget : the currently active widget (if any)
/// - Phase : which 'phase' of this widget is currently running
/// - IBar : the input-bar used to house the input boxes for the widget, and the prompt text
/// - KeyboardMode : a global bool that tells us if we are in 'keyboard-mode' or 'mouse-mode'
static class DwgHub {
   // Properties ---------------------------------------------------------------
   /// <summary>The current drawing we're working with</summary>
   public static Dwg2? Dwg {
      get => mDwg;
      set {
         mDwg = value; Widget = null;
         if (mDwg == null) {
            mHooks?.Dispose (); mHooks = null; Snap = null;
         } else {
            SanitizeDwg (mDwg);
            var mouse = Hub.Mouse;
            // The first time a drawing is set up, we create the hooks with which
            // we listen to mouse-clicks, mouse-moves etc
            mHooks ??= new MultiDispose (
               Hub.Keyboard.Keys.Subscribe (OnKey),
               mouse.Clicks.Where (a => a.IsLeftPress).Subscribe (a => DoMouse (a.Position, true)),
               mouse.Moves.Subscribe (a => DoMouse (a, false)),
               mouse.Enter.Subscribe (DoMouseInside),
               mouse.Clicks.Subscribe (a => MouseEventSink (a.Position, a.Button, a.State)),
               mouse.Moves.Subscribe (a => MouseEventSink (a))
            );
            Snap = new (mDwg);
         }
      }
   }

   static MultiDispose? mHooks;
   static Dwg2? mDwg;

   // FApp.#351 Infinite bound due to extra large poly!
   static void SanitizeDwg (Dwg2 dwg) {
      List<E2Poly> rmv = [.. dwg.Ents.OfType<E2Poly> ().Where (a => {
         var b = a.Bound;
         return !double.IsFinite (b.Width) || !double.IsFinite (b.Height);
      })];
      rmv.ForEach (dwg.Remove);
   }

   /// <summary>Widgets call this to know if the CTRL key is pressed</summary>
   /// This is implemented like this so we can simulate (during testing) the pressing of
   /// the CTRL key by setting mCtrlPressed
   public static bool CtrlPressed => mCtrlPressed ?? Hub.Keyboard.IsCtrlDown;
   static bool? mCtrlPressed;

   /// <summary>Widgets call this to know if the SHIFT key is pressed</summary>
   /// This is implemented like this so we can simulate (during testing) the pressing of
   /// the SHIFT key by setting mShiftPressed;
   public static bool ShiftPressed => mShiftPressed ?? Hub.Keyboard.IsShiftDown;
   static bool? mShiftPressed;

   public static double PickAperture => 3.5 * Lux.DPIScale * PxScale;

   /// <summary>The current InputBar (used to house input-boxes, and the prompt bar)</summary>
   public static InputBar? IBar;
   public static Action?   OpenRobotFn;

   /// <summary>The DwgSnap handler for the current drawing</summary>
   public static DwgSnap? Snap;

   /// <summary>Is the current widget running keyboard mode, or in mouse mode?</summary>
   /// When the user starts typing in any number, we switch to keyboard mode, turn on the focus
   /// to the widget input box that corresponds to the current phase. In keyboard mode,
   /// mouse-moves no longer update the 'current point' and are not sent to the widget, so
   /// just moving the mouse will not update the screen.
   /// In mouse mode, the mouse-moves update the current point (the point field in the current
   /// widget that is marked with NClick(N) where N is the current phase).
   ///
   /// We switch TO keyboard mode by
   /// - Clicking on an input box
   /// - Start typing in a number
   /// We switch OUT of keyboard mode by
   /// - Pressing the Esc key
   /// - Click somewhere with the mouse
   public static bool KeyboardMode {
      get => mKeyboardMode;
      set {
         if (!Lib.Set (ref mKeyboardMode, value) || Widget == null) return;
         if (value) {
            if (Widget.Fields.FirstOrDefault (a => a.NInput == mNInput)?.UIElement is TextBox tb) tb.Focus ();
         } else
            Keyboard.ClearFocus ();
         IBar?.SetKeyboardMode (value);
         Redraw ();
      }
   }
   static bool mKeyboardMode;
   static int mNInput = 0; // Last edited input box

   /// <summary>Redraw the non-drawing (essentially editor) related VNodes</summary>
   static void Redraw () {
      WidgetVN.It.Redraw ();
      DwgSnapVN.It.Redraw ();
      DwgConsLineVN.It.Redraw ();
   }

   /// <summary>Maps drawing commands (EDwgCmd) to methods that implement that command</summary>
   /// Some commands (like LayerSetup, GridSetup) are implemented just using dialog boxes,
   /// or require no interactive input. Such commands are modeled as Action(Dwg) delegates
   public static Dictionary<ECmd, Action<Dwg2>> MethodMap {
      get {
         if (mMethodMap == null) {
            mMethodMap = [];
            var bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var mi in typeof (DwgCmds).GetMethods (bf)) {
               // Go through methods of the DwgCmds class, and look for methods decorated with the EDwgCmd attribute.
               // That method should take a Dwg as input (that will be checked when we try to create a delegate)
               DwgCmdAttribute? attr = mi.GetCustomAttribute<DwgCmdAttribute> ();
               if (attr != null)
                  mMethodMap.Add (attr.Mode, mi.CreateDelegate<Action<Dwg2>> (null));
            }
         }
         return mMethodMap;
      }
   }
   static Dictionary<ECmd, Action<Dwg2>>? mMethodMap;

   /// <summary>The widget that's currently in use</summary>
   public static Widget? Widget {
      get => mWidget;
      set {
         mWidget?.Activate (iActivate: false);
         (mWidget = value)?.Activate ();
         if (mWidget != null) IBar?.SetWidget (mWidget);
         WidgetVN.It.SetWidget (mWidget);
         KeyboardMode = false;
         mNInput = 0;
      }
   }
   static Widget? mWidget;

   /// <summary>Maps drawing commands (EDwgCmd) to widgets that implement them</summary>
   /// Some commands (like Line, Circle etc) are interactive and require mouse / keyboard input
   /// from the user. These commands are mapped to Widgets that implement the needed interaction.
   /// This map only handles such commands.
   public static Dictionary<ECmd, Type> WidgetMap {
      get {
         if (mWidgetMap == null) {
            mWidgetMap = [];
            var allTypes = Assembly.GetExecutingAssembly ().GetTypes ();
            foreach (var type in allTypes.Where (a => a.IsAssignableTo (typeof (Widget)))) {
               if (type.IsAbstract) continue;
               // Every concrete widget type must have a [DwgCmd] attribute that tells us which
               // drawing command it implements
               DwgCmdAttribute attr = type.GetCustomAttribute<DwgCmdAttribute> () ?? throw new Exception ($"[DwgCmd] attribute missing for {type.Name}");
               mWidgetMap.Add (attr.Mode, type);
            }
         }
         return mWidgetMap;
      }
   }
   static Dictionary<ECmd, Type>? mWidgetMap;

   /// <summary>Filename of the current drawing</summary>
   public static string FileName = string.Empty;

   /// <summary>Any changes in the drawing to save? </summary>
   public static bool IsDwgDirty => UndoStack.Current?.NextUndo != mSavedNextUndo;

   static UndoStep? mSavedNextUndo = null;

   /// <summary>Is this a developer machine?</summary>
   /// This reads the PIXDEVELOPER environment variable on developer machines
   public static bool DeveloperMC => mDeveloperMC ??= Environment.GetEnvironmentVariable ("PIXDEVELOPER") == "1";
   static bool? mDeveloperMC;

   /// <summary>Resets the saved undo step</summary>
   public static void ResetDirty () => mSavedNextUndo = UndoStack.Current?.NextUndo;

   // Methods ------------------------------------------------------------------
   /// <summary>Called by the input-bar when ENTER is pressed (completes the current command)</summary>
   public static void Enter (bool iCtrl = false, bool iShift = false) {
      if (!KeyboardMode || Widget == null || Widget.NoKeyboardMode) return;
      (mCtrlPressed, mShiftPressed) = (iCtrl, iShift);
      Widget.Completed ();
      (mCtrlPressed, mShiftPressed) = (null, null);
      if (Widget.Fields.FirstOrDefault (a => a.NInput == 0)?.UIElement is TextBox tb) tb.Focus ();
      Redraw ();
   }

   /// <summary>Called when the mouse is clicked on a particular location</summary>
   /// This is implemented as a separate routine, so that it can be used also for testing
   /// widgets with fake mouse-clicks from the test runner
   public static void OnClick (Point2 pt, bool iCtrl = false, bool iShift = false) {
      if (Widget is null || Widget.NoMouseInput) return;
      Lib.Check (mCtrlPressed == null && mShiftPressed == null, "Coding Error");
      mCtrlPressed = iCtrl; mShiftPressed = iShift;
      var currPhase = Widget.Phase;

      // Note: Phase may already be == Widget.Phases, due to user editing the appropriate cmd parameter, if any!
      if (currPhase == Widget.Phases) { // Use this Click to complete the Command
         Widget.Completed ();
         mCtrlPressed = mShiftPressed = null;
         return;
      }

      Snap?.LastClickedPt = pt; // Consider click point for snap

      // Handle Repeatable commands
      if (iCtrl && currPhase == 0 && Widget.CanRepeat) {
         Widget.Repeat (pt);
         mCtrlPressed = mShiftPressed = null;
         return;
      }

      foreach (var field in Widget.Fields.Where (a => a.IsVariantActive))
         if (field.NClick == currPhase || field.NClick == currPhase + 1) // Prime the next Click receiver also!
            field.SetValue (pt);
      foreach (var field in Widget.Fields.Where (a => a.IsVariantActive))
         if (field.NPhase == currPhase) IBar?.DataToUI (field);

      if (Widget.Phase == Widget.Phases - 1) Widget.Completed ();
      else Widget.IncrPhase ();

      mCtrlPressed = mShiftPressed = null;
   }

   /// <summary>Called when the user types in a value into one of the input boxes</summary>
   /// This is implemented as a separate routine, so that it can also be sued for
   /// testing widget with fake typed-in input from the test runner
   public static bool OnTypeIn (int n, string text) {
      mCtrlPressed = mShiftPressed = null; // Not relevant during input processing (Meaningful only during ENTER handling)
      if (Widget is null) return false;
      text = text.Trim ();
      object? value = null;
      var field = Widget.Fields.Single (a => a.NInput == n);
      Type type = field.GetFieldType ();
      if (type == typeof (double)) {
         if (!double.TryParse (text, out double d)) return false;
         value = field.Angle ? d.D2R () : d;
      } else if (type == typeof (int)) {
         if (!int.TryParse (text, out int i)) return false;
         value = i;
      } else if (type == typeof (Point2)) {
         bool relative = text.StartsWith ('@'), polar = text.Contains ('<');
         text = text.Replace ('@', ' ').Replace ('<', ' ').Replace (',', ' ');
         var w = text.Split (' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
         if (w.Length != 2 || !double.TryParse (w[0], out double x) || !double.TryParse (w[1], out double y))
            return false;
         Point2 pt = new (x, y);
         if (polar) pt = (Point2)(Vector2.UnitVec (pt.Y.D2R ()) * pt.X);
         if (relative) {
            // If we're doing relative coordinates get the 'previous' point.
            Point2 ptPrev = (0, 0);
            if (n > 0) {
               var prevField = Widget.Fields.Single (a => a.NClick == n - 1);
               ptPrev = (Point2)prevField.GetValue ()!;
            } else {
               if (Widget.PrevClick.Count > 0) ptPrev = Widget.PrevClick[0];
            }
            pt += (Vector2)ptPrev;
         }
         value = pt;
      } else if (type == typeof (string)) {
         value = text;
      } else
         throw new NotImplementedException ($"Unhandled input type {type}");

      field.SetValue (value);
      Redraw ();
      return true;
   }

   // Implementation -----------------------------------------------------------
   // Internal subscriber to the HW.Keys observable
   static void OnKey (KeyInfo info) {
      if (!info.IsPress () || Widget == null) return;
      if (Widget.OnKey (info)) { Redraw (); return; }
      if (info.Key is >= EKey.Space and <= EKey.N9)
         KeyboardMode = true;
      if (info.Key == EKey.Escape) {
         Widget.ResetPhase ();
         Redraw ();
      }
   }

   // Internal subscriber to the HW.MouseClick and HW.MouseMoves observables
   static void DoMouse (Vec2S pix, bool leftPressed) {
      MouseInside = true; // Only way to capture Mouse_Enter!
      if (Dwg == null || Widget == null || IBar == null) return;
      if (sDragDriver is not null) return;
      // Determine if we are in DwgScene (or perhaps in bend preview scene!)
      if (Lux.PickScene (pix) is not DwgScene) {
         DwgHub.HideSimCursor = true; // Show default Windows cursor (in bend preview scene!)
         return;
      } else DwgHub.HideSimCursor = Widget.NoSimMouse;
      RawMousePos = ToWorld (pix);
      Point2 pt = MousePos = RawMousePos;
      if (!Widget.NoSimMouse && Snap is { } snap) {
         pt = MousePos = snap.Snap (RawMousePos, PickAperture);
      }

      // Demux - widget v/s dragger (Only one being active at any given time)

      if (sDragDriver is not null) {
         sDragDriver ((RawMousePos, leftPressed));
      } else {
         if (KeyboardMode) {
            if (leftPressed) KeyboardMode = false;
            if (!Widget.NoKeyboardMode) return;
         }
         if (Widget.Fields.Any (a => a.Unsnapped && a.IsVariantActive && a.NClick == Widget.Phase))
            pt = RawMousePos;
         if (leftPressed) OnClick (pt, Hub.Keyboard.IsCtrlDown, Hub.Keyboard.IsShiftDown);
         else if (Widget.Phase < Widget.Phases) { // Note: Phase may get updated when user edits cmd parameter
            foreach (var field in Widget.Fields.Where (a => a.IsVariantActive && !a.NoMouseMove))
               if (field.NClick == Widget.Phase) { field.SetValue (pt); break; }
            foreach (var field in Widget.Fields.Where (a => a.IsVariantActive && !a.NoMouseMove))
               IBar.DataToUI (field);
         }
      }
      Redraw ();
   }
   public static Action<(Point2 RawPt, bool LeftPressed)>? sDragDriver;

   // Internal subscriber to the HW.MouseClick and HW.MouseMoves observables
   static void MouseEventSink (Vec2S pix, EMouseButton? button = null, EKeyState? state = null) {
      if (sDragDriver is null) return;
      MouseInside = true;
      RawMousePos = ToWorld (pix);
      Point2 pt = MousePos = Snap switch {
         DwgSnap snap => snap.Snap (RawMousePos, PickAperture),
         _ => RawMousePos
      };
      if (Dwg == null || Widget == null || IBar == null) return;
      bool leftPressed = true;
      if (button != null && state != null && button == EMouseButton.Left && state == EKeyState.Released)
         leftPressed = false;
      sDragDriver ((RawMousePos, leftPressed));
   }

   static void DoMouseInside (bool inside) { MouseInside = inside; Redraw (); }
   /// <summary>Indicates whether the mouse is within the pane, or not</summary>
   public static bool MouseInside;

   /// <summary>Projected mouse point. [It may be raw mouse-point, or snapped one]</summary>
   public static Point2 MousePos {
      get => mMousePos;
      set { mMousePos = value; Redraw (); }
   }
   static Point2 mMousePos;

   /// <summary>The 'raw mouse position' is the mouse position before any snaps are applied to it</summary>
   public static Point2 RawMousePos;

   public static bool HideSimCursor {
      get => _HideSimCursor;
      set {
         if (Lib.Set (ref _HideSimCursor, value))
            Lux.CursorVisible = value;
      }
   }
   static bool _HideSimCursor;
}
#endregion
