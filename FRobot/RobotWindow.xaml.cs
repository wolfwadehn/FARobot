// ╔═╦╗
// ║╬╠╬╦╗ RobotWindow.xaml.cs
// ║╔╣╠║╣ Floating controls panel for robot FK/IK and collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.IO;
using Microsoft.Win32;
namespace FRobot;

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
   void OnAddTriangle (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var dlg = new TriangleDialog ();
      if (dlg.ShowDialog () is true)
         mScene.AddTri (dlg.TriName, dlg.Group, dlg.P1, dlg.P2, dlg.P3);
   }

   void OnImportTriangles (object sender, RoutedEventArgs e) {
      if (mScene == null) return;
      var ofd = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*", Title = "Import Triangle CSV" };
      if (ofd.ShowDialog () is not true) return;
      int count = 0;
      try {
         foreach (var line in File.ReadLines (ofd.FileName)) {
            var p = line.Split (',');
            if (p[0].Trim ().Equals ("Name", StringComparison.OrdinalIgnoreCase)) continue;
            string name, group; int offset;
            if (p.Length >= 11)      { name = p[0].Trim (); group = p[1].Trim (); offset = 2; }
            else if (p.Length >= 10) { name = p[0].Trim (); group = "Box";        offset = 1; }
            else continue;
            var d = new double[9]; bool ok = true;
            for (int i = 0; i < 9 && ok; i++) ok = Util.TryParseDouble (p[offset + i].Trim (), out d[i]);
            if (!ok) continue;
            mScene.AddTri (name, group, new (d[0], d[1], d[2]), new (d[3], d[4], d[5]), new (d[6], d[7], d[8]));
            count++;
         }
         if (count == 0) MessageBox.Show (this, "No valid triangle rows found.", "Import");
      } catch (Exception ex) { MessageBox.Show (this, ex.Message, "Import failed"); }
   }

   // Fields -------------------------------------------------------------------
   RobotScene? mScene;
}
#endregion
