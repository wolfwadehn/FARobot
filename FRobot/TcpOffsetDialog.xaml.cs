// ╔═╦╗
// ║╬╠╬╦╗ TcpOffsetDialog.xaml.cs
// ║╔╣╠║╣ Dialog for editing the wrist-to-TCP offset in the wrist frame
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
namespace FRobot;

#region class TcpOffsetDialog ----------------------------------------------------------------------
public partial class TcpOffsetDialog : Window {
   public TcpOffsetDialog (Vector3 current) {
      InitializeComponent ();
      mX.Text = current.X.ToString ("F1", CultureInfo.InvariantCulture);
      mY.Text = current.Y.ToString ("F1", CultureInfo.InvariantCulture);
      mZ.Text = current.Z.ToString ("F1", CultureInfo.InvariantCulture);
   }

   public Vector3 Offset => new (Util.ParseDouble (mX.Text), Util.ParseDouble (mY.Text), Util.ParseDouble (mZ.Text));

   void OnOK (object sender, RoutedEventArgs e) {
      foreach (var tb in new[] { mX, mY, mZ })
         if (!Util.TryParseDouble (tb.Text, out _)) { tb.Focus (); tb.SelectAll (); return; }
      DialogResult = true;
   }
}
#endregion
