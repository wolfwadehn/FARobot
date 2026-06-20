// ╔═╦╗
// ║╬╠╬╦╗ Toolbar.cs
// ║╔╣╠║╣ Implements the toolbar control showing the 'command' tools on the left
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FApp.Widgets;
namespace FApp;

#region class Toolbar ------------------------------------------------------------------------------
/// <summary>The toolbar displays tools on the left</summary>
class Toolbar : StackPanel {
   // Construct the toolbar by getting a list of tools (icons) from the manifest.txt
   public Toolbar () {
      WrapPanel? wrap = new ();
      var (wmap, mmap) = (DwgHub.WidgetMap, DwgHub.MethodMap);
      var sepBrush = Util.MakeBrush (new Color4 (172, 172, 180));
      foreach (var line in Lib.ReadLines ("pix:Modes/manifest.txt")) {
         if (line.StartsWith (';')) continue; // Skip!
         if (line.IsBlank ()) {
            // Blank lines in the manifest become separator lines in the toolbar
            Children.Add (wrap); wrap = new ();
            Children.Add (new Border { MinWidth = 100, Height = 2, Background = sepBrush, Margin = new (0, 2, 0, 2) });
            continue;
         }
         if (line.StartsWith ('>')) {
            // >GroupName:Item1,Item2,... becomes a group button with a popup sub-menu
            int colon = line.IndexOf (':');
            string groupName = line[1..colon];
            string[] items = line[(colon + 1)..].Split (',');
            if (wrap.Children.Count > 0) Children.Add (wrap);
            wrap = new ();
            Children.Add (MakeGroupButton (groupName, items, wmap, mmap));
            continue;
         }

         var emode = Enum.Parse<ECmd> (line, true);
         var border = MakeButton (emode, line, wmap, mmap);
         wrap.Children.Add (border);
         mMap[emode] = border;
      }
      if (wrap.Children.Count > 0 && !Children.Contains (wrap)) {
         Children.Add (wrap);
         Children.Add (new Border { MinWidth = 100, Height = 2, Background = sepBrush, Margin = new (0, 2, 0, 2) });
      }
   }
   Dictionary<ECmd, Border> mMap = [];

   // Creates a single tool button Border for a given command
   Border MakeButton (ECmd emode, string cmdName, Dictionary<ECmd, Type> wmap, Dictionary<ECmd, Action<Dwg2>> mmap) {
      using var stm = Lib.OpenRead ($"pix:Modes/{cmdName}.png");
      var source = Util.LoadBitmapFromStream (stm);
      Image image = new () { Source = source, Width = 16, Height = 16, Stretch = Stretch.Uniform, Margin = new (3), ToolTip = CmdPrompt.TryGet (emode)?.Name ?? cmdName };
      AutomationProperties.SetName (image, cmdName);
      Border border = new () { Child = image, Tag = emode, CornerRadius = new (2) };
      AutomationProperties.SetAutomationId (border, cmdName);
      if (wmap.ContainsKey (emode) || mmap.ContainsKey (emode))
         border.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) OnClicked ((Border)s); };
      else
         image.Opacity = 0.2;
      border.MouseEnter += (s, e) => OnMouseEnter ((Border)s);
      border.MouseLeave += (s, e) => OnMouseLeave ((Border)s);
      return border;
   }

   // Creates a full-width group button that opens a popup sub-menu when clicked
   Border MakeGroupButton (string groupName, string[] items, Dictionary<ECmd, Type> wmap, Dictionary<ECmd, Action<Dwg2>> mmap) {
      List<Border> subBtns = [];
      foreach (var item in items) {
         var name = item.Trim ();
         var emode = Enum.Parse<ECmd> (name, true);
         var subBtn = MakeButton (emode, name, wmap, mmap);
         mMap[emode] = subBtn;
         subBtns.Add (subBtn);
      }

      var subPanel = new WrapPanel { Margin = new (4) };
      subBtns.ForEach (b => subPanel.Children.Add (b));

      var popup = new Popup {
         Child = new Border {
            Child = subPanel,
            Background = Util.MakeBrush (new Color4 (200, 200, 208)),
            BorderBrush = Util.MakeBrush (new Color4 (140, 140, 148)),
            BorderThickness = new (1),
            CornerRadius = new (4),
            Padding = new (2),
         },
         StaysOpen = false,
         Placement = PlacementMode.Right,
      };
      subBtns.ForEach (b => b.MouseDown += (s, e) => popup.IsOpen = false);

      var label = new TextBlock {
         Text = $"{groupName} ▾",
         FontSize = 11,
         HorizontalAlignment = HorizontalAlignment.Center,
         VerticalAlignment = VerticalAlignment.Center,
         Margin = new (4, 2, 4, 2),
      };
      var groupBorder = new Border {
         Child = label, MinWidth = 100,
         Margin = new (0, 2, 0, 0),
         CornerRadius = new (2),
         ToolTip = groupName,
      };
      AutomationProperties.SetAutomationId (groupBorder, groupName);
      popup.PlacementTarget = groupBorder;
      groupBorder.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) popup.IsOpen = !popup.IsOpen; };
      groupBorder.MouseEnter += (s, e) => OnMouseEnter (groupBorder);
      groupBorder.MouseLeave += (s, e) => OnMouseLeave (groupBorder);
      return groupBorder;
   }

   // When we click a button:
   // - If we find a method corresponding to this mode in MethodMap, we simply
   //   invoke the method
   // - If we find a type corresponding to this mode in WidgetMap, we construct
   //   that widget and attach it
   void OnClicked (Border b) {
      if (DwgHub.Dwg == null) return;
      ECmd mode = (ECmd)b.Tag;
      if (DwgHub.MethodMap.TryGetValue (mode, out var func))
         func (DwgHub.Dwg);
      if (DwgHub.WidgetMap.TryGetValue (mode, out var type)) {
         if (Activator.CreateInstance (type) is Widget widget) {
            DwgHub.Widget = widget;
            if (mCurrent != null) mCurrent.Background = null;
            (mCurrent = b).Background = mBrSelected;
         }
      }
   }

   /// <summary>Resets the existing tool selection</summary>
   public void Reset () {
      DwgHub.Widget = null;
      mCurrent?.ClearValue (Border.BackgroundProperty);
      mCurrent = null;
      Lux.CursorVisible = true;
   }

   /// <summary>Simulate the clicking of a particular button</summary>
   public void DoClick (ECmd cmd) {
      if (mMap.TryGetValue (cmd, out var border)) OnClicked (border);
   }

   void OnMouseEnter (Border b) { if (b != mCurrent) b.Background = mBrHover; }
   void OnMouseLeave (Border b) { if (b != mCurrent) b.Background = null; }
   Border? mCurrent;

   // Implementation -----------------------------------------------------------
   Brush mBrHover = Util.MakeBrush (new (228, 228, 236));
   Brush mBrSelected = Util.MakeBrush (new (248, 248, 255));
}
#endregion
