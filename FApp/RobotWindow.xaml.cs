// ╔═╦╗
// ║╬╠╬╦╗ RobotWindow.xaml.cs
// ║╔╣╠║╣ Floating controls panel for robot FK/IK and collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp;

#region class RobotWindow --------------------------------------------------------------------------
/// <summary>Floating controls palette; the GL scene renders in the main window viewport</summary>
public partial class RobotWindow : Window {
   // Constructor --------------------------------------------------------------
   public RobotWindow () { InitializeComponent (); }

   // Methods ------------------------------------------------------------------
   /// <summary>Wires the window to the robot scene: sets DataContext and stores the scene
   /// reference needed for the Add Triangle dialog.</summary>
   internal void SetScene (RobotScene scene) {
      mScene    = scene;
      DataContext = scene.ViewModel;
   }

   // Event handlers -----------------------------------------------------------
   // Opens the triangle dialog and tells the scene to add the result.
   // The "Add Triangle…" button uses Click="OnAddTriangle" in the XAML.
   void OnAddTriangle (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var dlg = new TriangleDialog ();
      if (dlg.ShowDialog () is true)
         mScene.AddTri (dlg.TriName, dlg.Group, dlg.P1, dlg.P2, dlg.P3);
   }

   // Opens the 3-point calibration dialog (prefilled with the current frame, if any)
   // and applies the resulting pallet frame to the scene.
   void OnCalibrateFrame (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var dlg = new PalletFrameDialog (mScene.FramePoints) { Owner = this };
      if (dlg.ShowDialog () is true)
         mScene.SetPalletFrame (dlg.Origin, dlg.XDir, dlg.PlanePt);
   }

   void OnClearFrame (object sender, RoutedEventArgs e) => mScene?.ClearPalletFrame ();

   // ── Pallet pick & path ────────────────────────────────────────────────────
   void OnImportPallet (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var ofd = new Microsoft.Win32.OpenFileDialog {
         Filter = "Mesh files|*.stl;*.obj|STL files|*.stl|OBJ files|*.obj|All files|*.*",
         Title  = "Import Pallet Geometry"
      };
      if (ofd.ShowDialog () is true) mScene.ImportPallet (ofd.FileName);
   }

   void OnSetHome (object sender, RoutedEventArgs e) => mScene?.SetHome ();

   void OnPickCorner       (object sender, RoutedEventArgs e) => mScene?.BeginPickCorner ();
   void OnPickPickup       (object sender, RoutedEventArgs e) => mScene?.BeginPickPickup ();
   void OnGenerateWaypoints (object sender, RoutedEventArgs e) => mScene?.GenerateWaypoints ();

   // Fields -------------------------------------------------------------------
   RobotScene? mScene;
}
#endregion
