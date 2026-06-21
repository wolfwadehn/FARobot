// ╔═╦╗
// ║╬╠╬╦╗ TcpOffsetDialog.xaml.cs
// ║╔╣╠║╣ Dialog for editing the wrist-to-TCP offset in the wrist frame
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
namespace FApp;

#region class TcpOffsetDialog ----------------------------------------------------------------------
public partial class TcpOffsetDialog : Window {
   public TcpOffsetDialog (Vector3 current) {
      InitializeComponent ();
      mX.Text = current.X.ToString ("F1", CultureInfo.InvariantCulture);
      mY.Text = current.Y.ToString ("F1", CultureInfo.InvariantCulture);
      mZ.Text = current.Z.ToString ("F1", CultureInfo.InvariantCulture);
   }

   public Vector3 Offset => new (Parse (mX.Text), Parse (mY.Text), Parse (mZ.Text));

   void OnOK (object sender, RoutedEventArgs e) {
      foreach (var tb in new[] { mX, mY, mZ })
         if (!TryParse (tb.Text, out _)) { tb.Focus (); tb.SelectAll (); return; }
      DialogResult = true;
   }

   static double Parse (string s) { TryParse (s, out double v); return v; }

   static bool TryParse (string s, out double v) =>
      double.TryParse (s.Replace (',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
#endregion
