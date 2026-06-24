// ╔═╦╗
// ║╬╠╬╦╗ PalletFrameDialog.xaml.cs
// ║╔╣╠║╣ Dialog for entering the 3 calibration points that define a pallet frame
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FApp;

#region class PalletFrameDialog --------------------------------------------------------------------
/// <summary>3-point work-object teach: P1=origin, P2=+X direction, P3=point on the +XY side.</summary>
public partial class PalletFrameDialog : Window {
   // Constructor --------------------------------------------------------------
   public PalletFrameDialog ((Point3 P1, Point3 P2, Point3 P3)? current = null) {
      InitializeComponent ();
      if (current is { } c) {
         Set (mP1X, mP1Y, mP1Z, c.P1); Set (mP2X, mP2Y, mP2Z, c.P2); Set (mP3X, mP3Y, mP3Z, c.P3);
      }
   }

   // Properties ---------------------------------------------------------------
   public Point3 Origin  => Pt (mP1X, mP1Y, mP1Z);
   public Point3 XDir    => Pt (mP2X, mP2Y, mP2Z);
   public Point3 PlanePt => Pt (mP3X, mP3Y, mP3Z);

   // Implementation -----------------------------------------------------------
   void OnOK (object sender, RoutedEventArgs e) {
      foreach (var tb in new[] { mP1X, mP1Y, mP1Z, mP2X, mP2Y, mP2Z, mP3X, mP3Y, mP3Z })
         if (!Util.TryParseDouble (tb.Text, out _)) { tb.Focus (); tb.SelectAll (); return; }
      // Reject collinear / coincident points — they produce a degenerate frame.
      var vx = XDir - Origin;
      var vz = vx * (PlanePt - Origin);   // cross product
      if (vx.Length.IsZero () || vz.Length.IsZero ()) {
         MessageBox.Show (this, "The three points are coincident or collinear; cannot build a frame.",
                          "Invalid calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
         return;
      }
      DialogResult = true;
   }

   // CSV format: P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z (header row optional).
   void OnImport (object sender, RoutedEventArgs e) {
      var ofd = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*", Title = "Import Pallet Frame CSV" };
      if (ofd.ShowDialog () is not true) return;
      try {
         foreach (var line in File.ReadLines (ofd.FileName)) {
            var t = line.Trim ();
            if (t.Length == 0 || t[0] == '#') continue;
            var p = t.Split (',');
            if (p[0].Trim ().StartsWith ('P')) continue;   // header row (P1X,...)
            if (p.Length < 9) continue;
            mP1X.Text = p[0].Trim (); mP1Y.Text = p[1].Trim (); mP1Z.Text = p[2].Trim ();
            mP2X.Text = p[3].Trim (); mP2Y.Text = p[4].Trim (); mP2Z.Text = p[5].Trim ();
            mP3X.Text = p[6].Trim (); mP3Y.Text = p[7].Trim (); mP3Z.Text = p[8].Trim ();
            break;
         }
      } catch (Exception ex) { MessageBox.Show (this, ex.Message, "Import failed"); }
   }

   void OnExport (object sender, RoutedEventArgs e) {
      var sfd = new SaveFileDialog { Filter = "CSV files|*.csv|All files|*.*",
                                     Title = "Export Pallet Frame CSV", FileName = "pallet_frame" };
      if (sfd.ShowDialog () is not true) return;
      try {
         using var w = new StreamWriter (sfd.FileName);
         w.WriteLine ("P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z");
         w.WriteLine ($"{mP1X.Text},{mP1Y.Text},{mP1Z.Text}," +
                      $"{mP2X.Text},{mP2Y.Text},{mP2Z.Text}," +
                      $"{mP3X.Text},{mP3Y.Text},{mP3Z.Text}");
      } catch (Exception ex) { MessageBox.Show (this, ex.Message, "Export failed"); }
   }

   static void Set (TextBox x, TextBox y, TextBox z, Point3 p) {
      var ic = CultureInfo.InvariantCulture;
      x.Text = p.X.ToString ("F1", ic); y.Text = p.Y.ToString ("F1", ic); z.Text = p.Z.ToString ("F1", ic);
   }

   static Point3 Pt (TextBox x, TextBox y, TextBox z) =>
      new (Util.ParseDouble (x.Text), Util.ParseDouble (y.Text), Util.ParseDouble (z.Text));
}
#endregion
