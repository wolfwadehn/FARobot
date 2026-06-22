// ╔═╦╗
// ║╬╠╬╦╗ Toolbar.cs
// ║╔╣╠║╣ Minimal toolbar container for robot-specific buttons
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace FApp;

#region class Toolbar ------------------------------------------------------------------------------
class Toolbar : StackPanel {
   /// <summary>Appends a button below any existing toolbar items</summary>
   public System.Windows.Controls.Border AddButton (string iconText, string tooltip, Action onClick) {
      var icon = new TextBlock {
         Text                = iconText,
         FontSize            = 12,
         Width = 16, Height  = 16,
         TextAlignment       = System.Windows.TextAlignment.Center,
         VerticalAlignment   = VerticalAlignment.Center,
         HorizontalAlignment = HorizontalAlignment.Center
      };
      var border = new System.Windows.Controls.Border { Child = icon, CornerRadius = new (2), Margin = new (3), ToolTip = tooltip };
      border.MouseDown  += (s, e) => { if (e.ChangedButton == MouseButton.Left) onClick (); };
      border.MouseEnter += (s, e) => border.Background = mBrHover;
      border.MouseLeave += (s, e) => border.Background = null;
      var wrap = new WrapPanel ();
      wrap.Children.Add (border);
      Children.Add (wrap);
      return border;
   }

   readonly Brush mBrHover = Util.MakeBrush (new (228, 228, 236));
}
#endregion
