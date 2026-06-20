// ╔═╦╗
// ║╬╠╬╦╗ LayersDlg.xaml.cs
// ║╔╣╠║╣ FApp drawing layers dialog
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
namespace FApp;

/// <summary>Interaction logic for LayersDlg.xaml</summary>
public partial class LayersDlg : Window {
   public LayersDlg (Dwg2 dwg) {
      InitializeComponent ();
      TableEdit = new TableEdit (new LayersTableVM (mDwg = dwg));
      Loaded += delegate {
         mContent.Content = TableEdit;
         TableEdit.SelectRow (0);
      };
      KeyDown += (sender, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close (); };
      mPurgeBtn.Click += delegate { dwg.PurgeUnusedLayers (); TableEdit.Refresh (); TableEdit.SelectRow (0); };
   }

   readonly Dwg2 mDwg;
   readonly TableEdit TableEdit;

   public static void Launch (Dwg2 dwg) =>
      new LayersDlg (dwg) { Owner = App.Current.MainWindow }.Show ();

   void Hyperlink_Click (object sender, RoutedEventArgs e) {
      var cmds = TableEdit.TableVM.Cmds;
      if (cmds.Count == 0) return;
      var cm = new ContextMenu ();
      foreach (var cmd in cmds) {
         var mi = new MenuItem () { Header = cmd.Name };
         mi.Click += delegate { cmd.Execute (); };
         cm.Items.Add (mi);
      }
      cm.IsOpen = true;
   }
}
