// ╔═╦╗
// ║╬╠╬╦╗ MainWindow.xaml.cs
// ║╔╣╠║╣ Main window of the FApp application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using Microsoft.Win32;
using FApp.WPF;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reflection;
namespace FApp;

#region class MainWindow ---------------------------------------------------------------------------
/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window {
   // Constructor --------------------------------------------------------------
   public MainWindow () {
      Lib.Init ();
      var file = Lib.GetLocalFile ("FApp.wad");
      Lib.Register (File.Exists (file) ? new ZipStmLocator ("pix:", file) : new FileStmLocator ("pix:", "F:/Wad/"));
      Lib.AddNamespace ("FApp");
      InitializeComponent ();
      mMRU = new MRUList (this, Lib.GetLocalFile ("RecentFiles.txt"), 9, OpenFromMRU);
      mContent.Child = WPFHost.Init (this, OnLuxReady);
      DwgHub.IBar = new InputBar (mStack, mStatus);
      mToolbarHost.Child = new Toolbar ();
      VNode.RegisterAssembly (Assembly.GetExecutingAssembly ());
      Closing += OnClosing;
      Loaded += delegate { Lib.Post (App.CheckExpired); };
   }

   // Implementation -----------------------------------------------------------
   void OnLuxReady () {
      new SceneManipulator ();
      SetDwg (mDwg);
      Lib.Tracer = TracePrinter;
      new CmdRouter (this);
      DwgHub.OpenRobotFn = OpenRobot;
      var args = Environment.GetCommandLineArgs ();
      if (args.Length > 1) SetDwg (args[1]);
      OpenRobot ();
   }

   // Trace printer limiting trace output to Developer machines
   static void TracePrinter (string msg) {
      if (!DwgHub.DeveloperMC) return;
      TraceVN.Print (msg);
   }

   /// <summary>Handles when files are dragged and dropped into FApp</summary>
   protected override void OnDrop (DragEventArgs e) {
      if (!e.Data.GetDataPresent (DataFormats.FileDrop)) return;
      string fname = ((string[])e.Data.GetData (DataFormats.FileDrop))[0];
      // Check the extension - DXF, GEO, etc
      var ext = Path.GetExtension (fname).ToLower ();
      if (ext != ".dxf" && ext != ".geo") return;
      if (IsCancelled) return;
      Lib.Post (() => SetDwg (fname));
   }

   void OnClosing (object? sender, CancelEventArgs e) {
      if (IsCancelled) e.Cancel = true;
      mMRU.Save ();
   }

   MessageBoxResult PromptToSave () {
      if (!DwgHub.IsDwgDirty) return MessageBoxResult.No;
      var result = MessageBox.Show (this, "The file has been changed. Do you want to save it?",
                                   "FApp", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
      if (result is MessageBoxResult.Yes) Save ();
      return result;
   }

   // When the edit menu is opening, set up the names for the UNDO and REDO actions
   void OnEditMenuOpened (object sender, RoutedEventArgs e) {
      string? undo = null, redo = null;
      if (UndoStack.Current is { } stack) {
         undo = stack.NextUndo?.Description;
         redo = stack.NextRedo?.Description;
      }
      mUndo.IsEnabled = undo != null; mUndo.Header = $"_Undo {undo}";
      mRedo.IsEnabled = redo != null; mRedo.Header = $"_Redo {redo}";
   }

   // Set up the current drawing
   void SetDwg (Dwg2 dwg) {
      DwgHub.Dwg = mDwg = dwg;
      DwgHub.IBar?.Clear ();
      _ = dwg.Layers.Current; // Force creation of layer "0"
      Lux.UIScene = new DwgScene (dwg);
      if (mToolbarHost.Child is Toolbar t) {
         t.Reset ();
         t.DoClick (ECmd.Pick);
      }
      UndoStack.Current = new ();
      if (mTcpOffsetBtn != null) mTcpOffsetBtn.Visibility = Visibility.Collapsed;
   }
   Dwg2 mDwg = new ();

   // Opens a drawing from the recent files list if it exists
   void OpenFromMRU (string filename) {
      if (!File.Exists (filename)) {
         MessageBox.Show (this, "File not found: " + filename);
         mMRU.RemoveFile (filename);
         return;
      }
      if (IsCancelled) return;
      SetDwg (filename);
   }

   // Loads a drawing from file, updates the title, and adds it to the recent list
   void SetDwg (string filename) {
      if (!File.Exists (filename)) return;
      DwgHub.FileName = Title = filename;
      string ext = Path.GetExtension (filename).ToLower ();
      switch (ext) {
         case DXF:
            var dr = new DXFReader (filename) { WhiteToBlack = true, DarkenColors = true, StitchThreshold = 0.0001 };
            mDwg = dr.Load ();
            break;
         case GEO: mDwg = GEOReader.Load (filename); break;
         case CURL: mDwg = (Dwg2)CurlReader.Load (filename); break;
      }
      SetDwg (mDwg);
      mMRU.AddFile (filename);
   }

   // Saves a drawing to a file
   void SaveDwg (string filename) {
      DwgHub.ResetDirty ();
      var ext = Path.GetExtension (filename).ToLower ();
      switch (ext) {
         case DXF: DXFWriter.Save (mDwg, filename); break;
         case GEO: GEOWriter.Save (mDwg, filename); break;
         case CURL: CurlWriter.Save (mDwg, filename); break;
      }
   }

   // Command sinks ------------------------------------------------------------
   [CmdSink ("cmd:Exit")]
   void Exit () => Close ();

   [CmdSink ("cmd:New")]
   void New () {
      if (IsCancelled) return;
      Dwg2 dwg = new ();
      SetDwg (dwg);
      DwgHub.FileName = string.Empty;
      Title = "FApp";
   }

   [CmdSink ("cmd:Open")]
   void Open () {
      if (IsCancelled) return;
      var filter = "2D files|*.dxf;*.geo|DXF files|*.dxf|GEO files|*.geo" +
                   (DwgHub.DeveloperMC ? "|FApp CURL files|*.curl" : "");
      var ofd = new OpenFileDialog { Filter = filter };
      if (ofd.ShowDialog () is true) SetDwg (ofd.FileName);
   }

   [CmdSink ("cmd:Save")]
   void Save () {
      if (string.IsNullOrWhiteSpace (DwgHub.FileName)) SaveAs ();
      else SaveDwg (DwgHub.FileName);
   }

   [CmdSink ("cmd:SaveAs")]
   void SaveAs () {
      var sfd = new SaveFileDialog { Filter = mSaveFilter };
      if (sfd.ShowDialog () is true) {
         var fileName = DwgHub.FileName = sfd.FileName;
         SaveDwg (fileName);
         mMRU.AddFile (fileName);
         Title = fileName;
      }
   }

   [CmdSink ("cmd:SaveSelectedAs")]
   void SaveSelectedAs () {
      Span<Ent2> selEnts = [.. mDwg.Ents.Where (a => a.IsSelected)];
      if (selEnts.Length == 0) return;
      var sfd = new SaveFileDialog { Filter = mSaveFilter };
      if (sfd.ShowDialog () is true) {
         var filename = sfd.FileName;
         mMRU.AddFile (filename);
         var dwg = SelectedEntsDwg (mDwg);
         var ext = Path.GetExtension (filename).ToLower ();
         switch (ext) {
            case DXF: DXFWriter.Save (dwg, filename); break;
            case GEO: GEOWriter.Save (dwg, filename); break;
            case CURL: CurlWriter.Save (dwg, filename); break;
         }
      }

      static Dwg2 SelectedEntsDwg (Dwg2 srcDwg) {
         var dwg = new Dwg2 ();
         // Copy all the blocks!
         foreach (var b in srcDwg.Blocks)
            dwg.Add (b);
         // Copy the entities and recreate the layers used.
         HashSet<Layer2> layers = [];
         foreach (var ent in srcDwg.Ents.Where (a => a.IsSelected)) {
            dwg.Add (ent);
            if (layers.Add (ent.Layer))
               dwg.Layers.Add (ent.Layer);
         }
         return dwg;
      }
   }
   static string mSaveFilter = "DXF files (*.dxf)|*.dxf|GEO files (*.geo)|*.geo" +
                                (DwgHub.DeveloperMC ? "|FApp CURL files|*.curl" : "");

   [CmdSink ("cmd:Cut")]
   void Cut () {
      if (DwgHub.Dwg is { } dwg)
         new ModifyDwgEnts (mDwg, "Delete", [], dwg.Ents.Where (a => a.IsSelected)).Push ();
   }

   [CmdSink ("cmd:SelectAll")]
   void SelectAll () => mDwg.Ents.ForEach (e => e.IsSelected = true);

   [CmdSink ("cmd:DeselectAll")]
   void DeselectAll () => mDwg.Ents.ForEach (e => e.IsSelected = false);

   [CmdSink ("cmd:InvertSelection")]
   void InvertSelection () => mDwg.Ents.ForEach (e => e.IsSelected ^= true);

   [CmdSink ("cmd:Undo")]
   void Undo () => UndoStack.Current?.Undo ();

   [CmdSink ("cmd:Redo")]
   void Redo () => UndoStack.Current?.Redo ();

   [CmdSink ("cmd:ZoomExtents")]
   void ZoomExtents () => Lux.UIScene?.ZoomExtents ();

   [CmdSink ("cmd:Robot")]
   void OpenRobot () {
      if (mRobotWin is { IsVisible: true }) { mRobotWin.Activate (); return; }
      mRobotScene ??= new RobotScene ();
      Lux.UIScene  = mRobotScene;
      mRobotWin    = new RobotWindow ();
      var wa       = SystemParameters.WorkArea;
      mRobotWin.Left   = wa.Right - mRobotWin.Width - 10;
      mRobotWin.Top    = wa.Top;
      mRobotWin.Closed += (_, _) => SetDwg (mDwg);
      mRobotWin.Show ();
      mRobotWin.SetScene (mRobotScene);
      if (mTcpOffsetBtn == null && mToolbarHost.Child is Toolbar tb)
         mTcpOffsetBtn = tb.AddButton ("TCP Offset", ShowTcpOffsetDlg);
      if (mTcpOffsetBtn != null) mTcpOffsetBtn.Visibility = Visibility.Visible;
   }

   void ShowTcpOffsetDlg () {
      if (mRobotScene == null) return;
      var dlg = new TcpOffsetDialog (mRobotScene.TcpOffset) { Owner = this };
      if (dlg.ShowDialog () is true) mRobotScene.TcpOffset = dlg.Offset;
   }

   RobotScene?  mRobotScene;
   RobotWindow? mRobotWin;
   System.Windows.Controls.Border? mTcpOffsetBtn;

   [CmdSink ("cmd:RobotHome")]
   void RobotHome () => mRobotScene?.GoHome ();

   [CmdSink ("cmd:TcpLegend")]
   void TcpLegend () {
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

   [CmdSink ("cmd:About")]
   void About () {
      var (year, month, rel, exMonth) = App.AssemblyVersion ();
      string[] mnames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

      StringBuilder sb = new ();
      sb.Append ($"FApp {rel}\n\n");
      sb.Append ($"Released: {year} {mnames[month - 1]}\n");
      if (exMonth >= 12) { exMonth -= 12; year++; }
      sb.Append ($"Expiry: {year} {mnames[exMonth - 1]}");

      MessageBox.Show (this, sb.ToString (), "About FApp", MessageBoxButton.OK);
   }

   // Fields -------------------------------------------------------------------
   bool IsCancelled => PromptToSave () is MessageBoxResult.Cancel;
   MRUList mMRU;

   // Constants ----------------------------------------------------------------
   const string DXF = ".dxf", GEO = ".geo", CURL = ".curl";
}
#endregion
