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

   // Fields -------------------------------------------------------------------
   RobotScene? mScene;
}
#endregion
