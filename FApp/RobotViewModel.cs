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

   // ── Script playback ───────────────────────────────────────────────────────
   public string ScriptPath { get => mScriptPath; set { mScriptPath = value; Notify (); } }
   public string PlayLabel  { get => mPlayLabel;  set { mPlayLabel  = value; Notify (); } }

   // Commands bound to buttons in XAML.
   public ICommand HomeCommand { get; }
   public ICommand LoadCommand { get; }
   public ICommand AddCommand  { get; }
   public ICommand PlayCommand { get; }

   // ── FK joints — set by RobotScene once the mechanism is loaded ────────────
   public JointSliderModel[] Joints { get; internal set; } = [];

   // ── Collision triangle list shown in the sidebar ──────────────────────────
   public ObservableCollection<CollisionTriVM> Triangles { get; } = [];

   // ── Events subscribed by RobotScene ──────────────────────────────────────
   public event Action?         IKChanged;
   public event Action?         BoxChanged;
   public event Action?         HomeRequested;
   public event Action<string>? LoadScriptRequested;
   public event Action?         AddRequested;
   public event Action?         PlayRequested;

   public RobotViewModel () {
      HomeCommand = new DelegateCommand (() => HomeRequested?.Invoke ());
      LoadCommand = new DelegateCommand (() => LoadScriptRequested?.Invoke (ScriptPath));
      AddCommand  = new DelegateCommand (() => AddRequested?.Invoke ());
      PlayCommand = new DelegateCommand (() => PlayRequested?.Invoke ());
   }

   double mX, mY, mZ, mRx = -90, mRy, mRz;
   double mBX = 700, mBY, mBZ = 700;
   string mScriptPath = Path.Combine (AppContext.BaseDirectory, "robot_script.txt");
   string mPlayLabel  = "Play";
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
