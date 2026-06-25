// ╔═╦╗
// ║╬╠╬╦╗ MainWindow.xaml.cs
// ║╔╣╠║╣ Main window of the FApp application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
namespace FApp;

public partial class MainWindow : Window {
   public MainWindow () {
      Lib.Init ();
      var file = Lib.GetLocalFile ("FApp.wad");
      Lib.Register (File.Exists (file) ? new ZipStmLocator ("pix:", file) : new FileStmLocator ("pix:", "F:/Wad/"));
      Lib.AddNamespace ("FApp");
      InitializeComponent ();
      mContent.Child = WPFHost.Init (this, OnLuxReady);
      mToolbarHost.Child = new Toolbar ();
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
      Loaded += delegate { Lib.Post (App.CheckExpired); };
   }

   void OnLuxReady () {
      new SceneManipulator ();
      mRobotScene = new RobotScene ();
      Lux.UIScene = mRobotScene;
      mRobotPanel.SetScene (mRobotScene);
      if (mToolbarHost.Child is Toolbar tb)
         tb.AddButton ("⚙", "TCP Offset", ShowTcpOffsetDlg);
   }

   void OpenCell (object s, RoutedEventArgs e) {
      if (mRobotScene == null) return;
      var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Cell files|*.cell;*.json|All files|*.*", Title = "Open Cell" };
      if (ofd.ShowDialog () is true) mRobotScene.LoadCell (ofd.FileName);
   }

   void SaveCell (object s, RoutedEventArgs e) {
      if (mRobotScene == null) return;
      var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "Cell files|*.cell|JSON|*.json", Title = "Save Cell", FileName = "cell.cell" };
      if (sfd.ShowDialog () is true) mRobotScene.SaveCell (sfd.FileName);
   }

   void Exit      (object s, RoutedEventArgs e) => Close ();
   void ZoomExtents (object s, RoutedEventArgs e) => Lux.UIScene?.ZoomExtents ();
   void RobotHome (object s, RoutedEventArgs e) => mRobotScene?.GoHome ();

   // Toggles the docked controls panel.
   void OpenRobot (object s, RoutedEventArgs e) =>
      mRobotPanel.Visibility = mRobotPanel.Visibility == Visibility.Visible
                               ? Visibility.Collapsed : Visibility.Visible;

   void ShowTcpOffsetDlg () {
      if (mRobotScene == null) return;
      var dlg = new TcpOffsetDialog (mRobotScene.TcpOffset) { Owner = this };
      if (dlg.ShowDialog () is true) mRobotScene.TcpOffset = dlg.Offset;
   }

   void TcpLegend (object s, RoutedEventArgs e) {
      var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness (20, 16, 20, 20) };
      panel.Children.Add (new System.Windows.Controls.TextBlock {
         Text = "TCP Coordinate Frame", FontSize = 14, FontWeight = FontWeights.Bold,
         Margin = new Thickness (0, 0, 0, 14)
      });
      foreach (var (hex, axis) in new[] { (0xFF2020u, "X"), (0x20CC20u, "Y"), (0x2060FFu, "Z") }) {
         var row = new System.Windows.Controls.StackPanel {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness (0, 4, 0, 4)
         };
         byte r = (byte)(hex >> 16), g = (byte)(hex >> 8), b = (byte)hex;
         row.Children.Add (new System.Windows.Controls.Border {
            Width = 18, Height = 18, Margin = new Thickness (0, 0, 12, 0),
            CornerRadius = new CornerRadius (3),
            Background = new System.Windows.Media.SolidColorBrush (
               System.Windows.Media.Color.FromRgb (r, g, b))
         });
         row.Children.Add (new System.Windows.Controls.TextBlock {
            Text = $"{axis} axis", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
         });
         panel.Children.Add (row);
      }
      new Window {
         Title = "TCP Legend", Content = panel,
         SizeToContent = SizeToContent.WidthAndHeight,
         ResizeMode = ResizeMode.NoResize,
         WindowStartupLocation = WindowStartupLocation.CenterOwner,
         Owner = this
      }.ShowDialog ();
   }

   void About (object s, RoutedEventArgs e) {
      var (year, month, rel, exMonth) = App.AssemblyVersion ();
      string[] mnames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
      var sb = new StringBuilder ();
      sb.Append ($"FApp {rel}\n\n");
      sb.Append ($"Released: {year} {mnames[month - 1]}\n");
      if (exMonth >= 12) { exMonth -= 12; year++; }
      sb.Append ($"Expiry: {year} {mnames[exMonth - 1]}");
      MessageBox.Show (this, sb.ToString (), "About FApp", MessageBoxButton.OK);
   }

   RobotScene? mRobotScene;
}
