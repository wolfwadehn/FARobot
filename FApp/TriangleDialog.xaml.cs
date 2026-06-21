// ╔═╦╗
// ║╬╠╬╦╗ TriangleDialog.xaml.cs
// ║╔╣╠║╣ Dialog for entering a collision triangle name, group, and vertex coordinates
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;
namespace FApp;

#region class TriangleDialog -----------------------------------------------------------------------
public partial class TriangleDialog : Window {
   // Constructor --------------------------------------------------------------
   public TriangleDialog () { InitializeComponent (); }

   // Properties ---------------------------------------------------------------
   public string TriName => mName.Text.Trim ();
   public string Group   => mGroup.Text.Trim () is { Length: > 0 } g ? g : "Group1";
   public Point3 P1 => Pt (mP1X, mP1Y, mP1Z);
   public Point3 P2 => Pt (mP2X, mP2Y, mP2Z);
   public Point3 P3 => Pt (mP3X, mP3Y, mP3Z);

   // Implementation -----------------------------------------------------------
   void OnOK (object sender, RoutedEventArgs e) {
      if (string.IsNullOrWhiteSpace (mName.Text)) { mName.Focus (); return; }
      foreach (var tb in new[] { mP1X, mP1Y, mP1Z, mP2X, mP2Y, mP2Z, mP3X, mP3Y, mP3Z }) {
         if (!TryParse (tb.Text, out _)) { tb.Focus (); tb.SelectAll (); return; }
      }
      DialogResult = true;
   }

   // CSV format: Name,Group,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z
   // Legacy (10-col) format without Group is also accepted on import.
   void OnImport (object sender, RoutedEventArgs e) {
      var ofd = new OpenFileDialog { Filter = "CSV files|*.csv|All files|*.*", Title = "Import Triangle CSV" };
      if (ofd.ShowDialog () is not true) return;
      try {
         foreach (var line in File.ReadLines (ofd.FileName)) {
            var p = line.Split (',');
            if (p[0].Trim ().Equals ("Name", StringComparison.OrdinalIgnoreCase)) continue;
            if (p.Length >= 11) {                 // current format: Name,Group,9×coords
               mName.Text  = p[0].Trim ();
               mGroup.Text = p[1].Trim ();
               SetCoords (p, 2);
            } else if (p.Length >= 10) {          // legacy format: Name,9×coords
               mName.Text  = p[0].Trim ();
               mGroup.Text = "Box";
               SetCoords (p, 1);
            } else continue;
            break;
         }
      } catch (Exception ex) { MessageBox.Show (this, ex.Message, "Import failed"); }

      void SetCoords (string[] p, int offset) {
         mP1X.Text = p[offset].Trim ();     mP1Y.Text = p[offset+1].Trim (); mP1Z.Text = p[offset+2].Trim ();
         mP2X.Text = p[offset+3].Trim ();   mP2Y.Text = p[offset+4].Trim (); mP2Z.Text = p[offset+5].Trim ();
         mP3X.Text = p[offset+6].Trim ();   mP3Y.Text = p[offset+7].Trim (); mP3Z.Text = p[offset+8].Trim ();
      }
   }

   void OnExport (object sender, RoutedEventArgs e) {
      var sfd = new SaveFileDialog { Filter = "CSV files|*.csv|All files|*.*",
                                     Title = "Export Triangle CSV",
                                     FileName = mName.Text.Trim () };
      if (sfd.ShowDialog () is not true) return;
      try {
         using var w = new StreamWriter (sfd.FileName);
         w.WriteLine ("Name,Group,P1X,P1Y,P1Z,P2X,P2Y,P2Z,P3X,P3Y,P3Z");
         w.WriteLine ($"{mName.Text.Trim ()},{Group}," +
                      $"{mP1X.Text},{mP1Y.Text},{mP1Z.Text}," +
                      $"{mP2X.Text},{mP2Y.Text},{mP2Z.Text}," +
                      $"{mP3X.Text},{mP3Y.Text},{mP3Z.Text}");
      } catch (Exception ex) { MessageBox.Show (this, ex.Message, "Export failed"); }
   }

   static Point3 Pt (TextBox x, TextBox y, TextBox z) =>
      new (Parse (x.Text), Parse (y.Text), Parse (z.Text));

   static double Parse (string s) { TryParse (s, out double v); return v; }

   static bool TryParse (string s, out double v) =>
      double.TryParse (s.Replace (',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
#endregion
