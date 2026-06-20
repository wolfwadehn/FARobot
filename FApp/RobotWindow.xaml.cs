// ╔═╦╗
// ║╬╠╬╦╗ RobotWindow.xaml.cs
// ║╔╣╠║╣ Floating controls panel for robot FK/IK and collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
namespace FApp;

#region class RobotWindow --------------------------------------------------------------------------
/// <summary>Floating controls palette; the GL scene renders in the main window viewport</summary>
public partial class RobotWindow : Window {
   // Constructor --------------------------------------------------------------
   public RobotWindow () {
      InitializeComponent ();
   }

   // Properties ---------------------------------------------------------------
   public UIElementCollection Panel => mPanel.Children;
}
#endregion
