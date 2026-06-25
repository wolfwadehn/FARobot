// ╔═╦╗
// ║╬╠╬╦╗ RobotPanel.xaml.cs
// ║╔╣╠║╣ Embedded controls panel (FK/IK, objects, frames, pick & place) docked in MainWindow
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
namespace FApp;

#region class RobotPanel ---------------------------------------------------------------------------
/// <summary>Robot controls palette, hosted inside the main window beside the GL viewport.</summary>
public partial class RobotPanel : UserControl {
   // Constructor --------------------------------------------------------------
   public RobotPanel () { InitializeComponent (); }

   // Methods ------------------------------------------------------------------
   /// <summary>Wires the panel to the robot scene: sets DataContext and stores the scene
   /// reference needed by the dialogs.</summary>
   internal void SetScene (RobotScene scene) {
      mScene      = scene;
      DataContext = scene.ViewModel;
   }

   // Event handlers -----------------------------------------------------------
   void OnAddTriangle (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var dlg = new TriangleDialog ();
      if (dlg.ShowDialog () is true)
         mScene.AddTri (dlg.TriName, dlg.Group, dlg.P1, dlg.P2, dlg.P3);
   }

   // Opens the 3-point calibration dialog (prefilled with the selected object's frame)
   // and applies the resulting frame to that object.
   void OnCalibrateFrame (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var dlg = new PalletFrameDialog (mScene.FramePoints) { Owner = Window.GetWindow (this) };
      if (dlg.ShowDialog () is true)
         mScene.SetPalletFrame (dlg.Origin, dlg.XDir, dlg.PlanePt);
   }

   void OnClearFrame (object sender, RoutedEventArgs e) => mScene?.ClearPalletFrame ();

   void OnImportPallet (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var ofd = new Microsoft.Win32.OpenFileDialog {
         Filter = "Mesh files|*.stl;*.obj|STL files|*.stl|OBJ files|*.obj|All files|*.*",
         Title  = "Import Geometry", Multiselect = true
      };
      if (ofd.ShowDialog () is true)
         foreach (var f in ofd.FileNames) mScene.ImportGeometry (f);
   }

   void OnSetHome (object sender, RoutedEventArgs e) => mScene?.SetHome ();

   void OnPickCorner        (object sender, RoutedEventArgs e) => mScene?.BeginPickCorner ();
   void OnPickPickup        (object sender, RoutedEventArgs e) => mScene?.BeginPickPickup ();
   void OnPickPlace         (object sender, RoutedEventArgs e) => mScene?.BeginPickPlace ();
   void OnClearPlaces       (object sender, RoutedEventArgs e) => mScene?.ClearPlaces ();
   void OnGenerateWaypoints (object sender, RoutedEventArgs e) => mScene?.GenerateWaypoints ();
   void OnAutoAvoid         (object sender, RoutedEventArgs e) => mScene?.PlanCollisionFree ();

   void OnImportPart (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var ofd = new Microsoft.Win32.OpenFileDialog {
         Filter = "Mesh files|*.stl;*.obj|STL files|*.stl|OBJ files|*.obj|All files|*.*",
         Title  = "Import Sheet Metal Part"
      };
      if (ofd.ShowDialog () is true) mScene.ImportPart (ofd.FileName);
   }

   // Fields -------------------------------------------------------------------
   RobotScene? mScene;
}
#endregion
