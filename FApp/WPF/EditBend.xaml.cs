namespace FApp.WPF {
   /// <summary>Interaction logic for EditBend.xaml</summary>
   public partial class EditBend : Window {
      EditBend (Dwg2 dwg, E2Bendline bend) {
         (mDwg, mBend) = (dwg, bend);
         InitializeComponent ();

         Loaded += delegate {
            mAngleTB.Text = bend.Angle.R2D ().ToString ();
            mRadiusTB.Text = bend.Radius.ToString ();
            mKFactorTB.Text = bend.KFactor.ToString ();
            iModified = false;
         };
         mAngleTB.TextChanged += delegate { iModified = true; };
         mRadiusTB.TextChanged += delegate { iModified = true; };
         mKFactorTB.TextChanged += delegate { iModified = true; };
         mFlipBtn.Click += delegate {
            if (double.TryParse (mAngleTB.Text, System.Globalization.CultureInfo.InvariantCulture, out double angle)) {
               mAngleTB.Text = (-angle).ToString ();
            }
         };
         mDeleteBtn.Click += delegate {
            new ModifyDwgEnts (mDwg, "Delete Bend", [], [mBend]).Push ();
            Close ();
         };
         Closed += delegate {
            if (!iModified) return;
            if (double.TryParse (mAngleTB.Text, System.Globalization.CultureInfo.InvariantCulture, out double angle)
            && double.TryParse (mRadiusTB.Text, System.Globalization.CultureInfo.InvariantCulture, out double radius)
            && double.TryParse (mKFactorTB.Text, System.Globalization.CultureInfo.InvariantCulture, out double kfactor)
            && (!angle.EQ (mBend.Angle.R2D ()) || !radius.EQ (mBend.Radius) || !kfactor.EQ (mBend.KFactor))) {
               var e2b = new E2Bendline (mDwg, [.. mBend.Pts], angle.D2R (), radius, kfactor);
               new ModifyDwgEnts (mDwg, "Edit Bend", [e2b], [mBend]).Push ();
            }
            ((DwgScene?)Lux.UIScene)?.RefreshFoldPreview ();
         };
         KeyDown += (sender, keyEvent) => {
            if (keyEvent.Key == System.Windows.Input.Key.Escape)
               Close ();
         };
      }

      public static void Show (Dwg2 dwg, E2Bendline bend) {
         mDlg?.Close ();
         mDlg = new EditBend (dwg, bend) { Owner = App.Current.MainWindow };
         mDlg.Show ();
         mDlg.Closed += delegate { mDlg = null; };
      }
      static EditBend? mDlg;

      bool iModified = false;
      readonly Dwg2 mDwg;
      readonly E2Bendline mBend;
   }
}

