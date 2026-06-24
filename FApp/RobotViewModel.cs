// ╔═╦╗
// ║╬╠╬╦╗ RobotViewModel.cs
// ║╔╣╠║╣ Bindable robot state — WPF sliders/textboxes stay in sync automatically
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
namespace FApp;

// ── RobotViewModel ────────────────────────────────────────────────────────────────────────────────
// Single source of truth for everything the robot sidebar needs to display or modify.
// Implements INotifyPropertyChanged so WPF sliders and textboxes update automatically
// when code changes a value — no manual SyncIKSliders() or mSyncingUI guard needed.
#region class RobotViewModel -----------------------------------------------------------------------
class RobotViewModel : INotifyPropertyChanged {
   public event PropertyChangedEventHandler? PropertyChanged;
   void Notify ([CallerMemberName] string? n = null) =>
      PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (n));

   // ── IK pose (TCP world position + orientation) ────────────────────────────
   public double X  { get => mX;  set { mX  = value; Notify (); IKChanged?.Invoke (); } }
   public double Y  { get => mY;  set { mY  = value; Notify (); IKChanged?.Invoke (); } }
   public double Z  { get => mZ;  set { mZ  = value; Notify (); IKChanged?.Invoke (); } }
   public double Rx { get => mRx; set { mRx = value; Notify (); IKChanged?.Invoke (); } }
   public double Ry { get => mRy; set { mRy = value; Notify (); IKChanged?.Invoke (); } }
   public double Rz { get => mRz; set { mRz = value; Notify (); IKChanged?.Invoke (); } }

   // Sets all 6 IK values at once and fires IKChanged only once.
   // Used by SnapToFace/SnapToTriNode/TickScript to avoid one ComputeIK per field.
   public void SetIKPose (double x, double y, double z, double rx, double ry, double rz) {
      (mX, mY, mZ, mRx, mRy, mRz) = (x, y, z, rx, ry, rz);
      Notify (nameof (X));  Notify (nameof (Y));  Notify (nameof (Z));
      Notify (nameof (Rx)); Notify (nameof (Ry)); Notify (nameof (Rz));
      IKChanged?.Invoke ();
   }

   // Updates the IK display fields without triggering a new IK solve.
   // Called by OnFK so the IK sliders reflect the TCP position after a joint drag.
   public void SetIKDisplay (double x, double y, double z, double rx, double ry, double rz) {
      (mX, mY, mZ, mRx, mRy, mRz) = (x, y, z, rx, ry, rz);
      Notify (nameof (X));  Notify (nameof (Y));  Notify (nameof (Z));
      Notify (nameof (Rx)); Notify (nameof (Ry)); Notify (nameof (Rz));
   }

   // ── Obstacle box position ─────────────────────────────────────────────────
   public double BX { get => mBX; set { mBX = value; Notify (); BoxChanged?.Invoke (); } }
   public double BY { get => mBY; set { mBY = value; Notify (); BoxChanged?.Invoke (); } }
   public double BZ { get => mBZ; set { mBZ = value; Notify (); BoxChanged?.Invoke (); } }

   // ── Pallet frame ──────────────────────────────────────────────────────────
   // When UseFrame is on, the IK X/Y/Z/Rx/Ry/Rz above are interpreted relative to
   // the calibrated pallet frame instead of the robot world.  RobotScene owns the
   // actual frame; toggling here re-runs the IK solve via FrameToggled.
   public bool UseFrame { get => mUseFrame; set { mUseFrame = value; Notify (); FrameToggled?.Invoke (); } }
   public string FrameStatus { get => mFrameStatus; set { mFrameStatus = value; Notify (); } }

   // ── Pallet geometry / pickup teach ────────────────────────────────────────
   public string PalletStatus { get => mPalletStatus; set { mPalletStatus = value; Notify (); } }
   public string PickupStatus { get => mPickupStatus; set { mPickupStatus = value; Notify (); } }
   public string PlaceStatus  { get => mPlaceStatus;  set { mPlaceStatus  = value; Notify (); } }
   public string PartStatus   { get => mPartStatus;   set { mPartStatus   = value; Notify (); } }

   // ── Script playback ───────────────────────────────────────────────────────
   public string ScriptPath { get => mScriptPath; set { mScriptPath = value; Notify (); } }
   public string PlayLabel  { get => mPlayLabel;  set { mPlayLabel  = value; Notify (); } }

   // Continuous scrub position over the loaded waypoints: integer part = waypoint
   // index, fractional part = interpolation toward the next one.  Max is count−1.
   // Dragging the slider fires WaypointScrubbed; Play animates this value one cycle.
   public double WaypointPos { get => mWaypointPos; set { mWaypointPos = value; Notify (); WaypointScrubbed?.Invoke (); } }
   public double WaypointMax { get => mWaypointMax; set { mWaypointMax = value; Notify (); } }
   public event Action? WaypointScrubbed;

   // Commands bound to buttons in XAML.
   public ICommand HomeCommand { get; }
   public ICommand LoadCommand { get; }
   public ICommand AddCommand  { get; }
   public ICommand PlayCommand { get; }

   // ── FK joints — set by RobotScene once the mechanism is loaded ────────────
   public JointSliderModel[] Joints { get; internal set; } = [];

   // ── Collision triangle list shown in the sidebar ──────────────────────────
   public ObservableCollection<CollisionTriVM> Triangles { get; } = [];

   // ── Waypoint list (one row per script waypoint) ───────────────────────────
   public ObservableCollection<WaypointVM> Waypoints { get; } = [];

   // ── Imported objects: selection, 6-DOF placement, and 6-param user frame ──
   public ObservableCollection<ObjectItemVM> Objects { get; } = [];
   public int SelectedObject { get => mSelObj; set { mSelObj = value; Notify (); SelectedObjectChanged?.Invoke (); } }

   // Selected object's placement in world (mm / degrees).  UI edits fire ObjMoved.
   public double ObjX  { get => mObjX;  set { mObjX  = value; Notify (); ObjMoved?.Invoke (); } }
   public double ObjY  { get => mObjY;  set { mObjY  = value; Notify (); ObjMoved?.Invoke (); } }
   public double ObjZ  { get => mObjZ;  set { mObjZ  = value; Notify (); ObjMoved?.Invoke (); } }
   public double ObjRx { get => mObjRx; set { mObjRx = value; Notify (); ObjMoved?.Invoke (); } }
   public double ObjRy { get => mObjRy; set { mObjRy = value; Notify (); ObjMoved?.Invoke (); } }
   public double ObjRz { get => mObjRz; set { mObjRz = value; Notify (); ObjMoved?.Invoke (); } }

   // Selected object's user frame, 6 parameters (world mm / degrees).  UI edits fire FrameEdited.
   public double FrX  { get => mFrX;  set { mFrX  = value; Notify (); FrameEdited?.Invoke (); } }
   public double FrY  { get => mFrY;  set { mFrY  = value; Notify (); FrameEdited?.Invoke (); } }
   public double FrZ  { get => mFrZ;  set { mFrZ  = value; Notify (); FrameEdited?.Invoke (); } }
   public double FrRx { get => mFrRx; set { mFrRx = value; Notify (); FrameEdited?.Invoke (); } }
   public double FrRy { get => mFrRy; set { mFrRy = value; Notify (); FrameEdited?.Invoke (); } }
   public double FrRz { get => mFrRz; set { mFrRz = value; Notify (); FrameEdited?.Invoke (); } }

   public event Action? SelectedObjectChanged, ObjMoved, FrameEdited;

   // Code-side setters that update the fields/UI WITHOUT firing the edit events (used by
   // RobotScene to push the selected object's values into the panel on selection).
   public void SetObjMove (double x, double y, double z, double rx, double ry, double rz) {
      (mObjX, mObjY, mObjZ, mObjRx, mObjRy, mObjRz) = (x, y, z, rx, ry, rz);
      foreach (var n in new[] { nameof (ObjX), nameof (ObjY), nameof (ObjZ), nameof (ObjRx), nameof (ObjRy), nameof (ObjRz) }) Notify (n);
   }
   public void SetObjFrame (double x, double y, double z, double rx, double ry, double rz) {
      (mFrX, mFrY, mFrZ, mFrRx, mFrRy, mFrRz) = (x, y, z, rx, ry, rz);
      foreach (var n in new[] { nameof (FrX), nameof (FrY), nameof (FrZ), nameof (FrRx), nameof (FrRy), nameof (FrRz) }) Notify (n);
   }

   // ── Events subscribed by RobotScene ──────────────────────────────────────
   public event Action?         IKChanged;
   public event Action?         BoxChanged;
   public event Action?         HomeRequested;
   public event Action<string>? LoadScriptRequested;
   public event Action?         AddRequested;
   public event Action?         PlayRequested;
   public event Action?         FrameToggled;

   public RobotViewModel () {
      HomeCommand = new DelegateCommand (() => HomeRequested?.Invoke ());
      LoadCommand = new DelegateCommand (() => LoadScriptRequested?.Invoke (ScriptPath));
      AddCommand  = new DelegateCommand (() => AddRequested?.Invoke ());
      PlayCommand = new DelegateCommand (() => PlayRequested?.Invoke ());
   }

   double mX, mY, mZ, mRx = -90, mRy, mRz;
   double mBX = 700, mBY, mBZ = 700;
   bool   mUseFrame;
   string mFrameStatus  = "(not calibrated)";
   string mPalletStatus = "(no geometry)";
   string mPickupStatus = "(not set)";
   string mPlaceStatus  = "(not set)";
   string mPartStatus   = "(no part)";
   string mScriptPath = Path.Combine (AppContext.BaseDirectory, "robot_script.txt");
   string mPlayLabel  = "Play";
   double mWaypointPos, mWaypointMax;
   int    mSelObj = -1;
   double mObjX, mObjY, mObjZ, mObjRx, mObjRy, mObjRz;
   double mFrX, mFrY, mFrZ, mFrRx, mFrRy, mFrRz;
}
#endregion

// ── JointSliderModel ──────────────────────────────────────────────────────────────────────────────
// One entry per robot joint (S, L, U, R, B, T).  The FK ItemsControl in RobotWindow.xaml
// displays one slider row per entry using a DataTemplate.
#region class JointSliderModel ---------------------------------------------------------------------
class JointSliderModel : INotifyPropertyChanged {
   public JointSliderModel (Mechanism m, Action onChanged) { mMech = m; mOnChanged = onChanged; }

   public string Name => mMech.Name;
   public double Min  => mMech.JMin;
   public double Max  => mMech.JMax;
   public double Value {
      get => mMech.JValue;
      set { mMech.JValue = value; Notify (); mOnChanged (); }
   }

   // Called by ComputeIK after an IK solve to push the updated angle to the FK slider.
   public void Refresh () => Notify (nameof (Value));

   public event PropertyChangedEventHandler? PropertyChanged;
   void Notify ([CallerMemberName] string? n = null) =>
      PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (n));

   readonly Mechanism mMech;
   readonly Action    mOnChanged;
}
#endregion

// ── CollisionTriVM ────────────────────────────────────────────────────────────────────────────────
// One entry per triangle in the collision triangle list.
#region class CollisionTriVM -----------------------------------------------------------------------
class CollisionTriVM {
   public CollisionTriVM (string name, string group, Brush groupBrush, Action onRemove) {
      Name          = name;
      Group         = group;
      GroupBrush    = groupBrush;
      RemoveCommand = new DelegateCommand (onRemove);
   }

   public string  Name          { get; }
   public string  Group         { get; }
   public Brush   GroupBrush    { get; }
   public ICommand RemoveCommand { get; }
}
#endregion

// ── ObjectItemVM ──────────────────────────────────────────────────────────────────────────────────
// One entry in the imported-objects combo box.
#region class ObjectItemVM -------------------------------------------------------------------------
class ObjectItemVM (string name) {
   public string Name { get; } = name;
}
#endregion

// ── WaypointVM ────────────────────────────────────────────────────────────────────────────────────
// One row in the waypoint list.  GoCommand scrubs the robot to this waypoint; ActionCommand
// cycles its action Move→Pick→Place; RemoveCommand deletes it.  Rebuilt on every change.
#region class WaypointVM ---------------------------------------------------------------------------
class WaypointVM {
   public WaypointVM (int number, string action, Brush actionBrush,
                      Action go, Action cycle, Action remove) {
      Name          = $"WP {number}";
      Action        = action;
      ActionBrush   = actionBrush;
      GoCommand     = new DelegateCommand (go);
      ActionCommand = new DelegateCommand (cycle);
      RemoveCommand = new DelegateCommand (remove);
   }

   public string   Name          { get; }
   public string   Action        { get; }
   public Brush    ActionBrush   { get; }
   public ICommand GoCommand     { get; }
   public ICommand ActionCommand { get; }
   public ICommand RemoveCommand { get; }
}
#endregion

// ── DelegateCommand ───────────────────────────────────────────────────────────────────────────────
// Minimal ICommand that wraps a plain Action.  Used for Load/Play/Remove buttons.
#region class DelegateCommand ----------------------------------------------------------------------
class DelegateCommand (Action execute) : ICommand {
   public bool CanExecute (object? _) => true;
   public void Execute    (object? _) => execute ();
   public event EventHandler? CanExecuteChanged { add { } remove { } }
}
#endregion

// ── DoubleConverter ───────────────────────────────────────────────────────────────────────────────
// WPF value converter used by every TextBox that binds to a double property on the ViewModel.
// Accepts both '.' and ',' as decimal separator so the input works across locales.
// Returns DependencyProperty.UnsetValue for invalid input (WPF then skips the update).
#region class DoubleConverter ----------------------------------------------------------------------
[ValueConversion (typeof (double), typeof (string))]
class DoubleConverter : IValueConverter {
   public object Convert (object v, Type t, object p, CultureInfo c) =>
      v is double d ? d.ToString ("F1", CultureInfo.InvariantCulture) : "0.0";

   public object ConvertBack (object v, Type t, object p, CultureInfo c) =>
      Util.TryParseDouble (v as string ?? "", out double d) ? d : DependencyProperty.UnsetValue;
}
#endregion
