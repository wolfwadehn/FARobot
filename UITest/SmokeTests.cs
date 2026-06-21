// ╔═╦╗
// ║╬╠╬╦╗ SmokeTests.cs
// ║╔╣╠║╣ Basic smoke tests - verify FApp launches and shows its main window
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.UITesting;

#if FLAUI
#region class SmokeTests ---------------------------------------------------------------------------
[UIFixture (1, "Smoke Tests")]
class SmokeTests {
   [UITest (1, "Main window is visible")]
   void MainWindowVisible () {
      using var driver = new AppDriver ();
      if (driver.MainWindow is null)
         throw new Exception ("Main window not found");
   }

   [UITest (2, "Window title is FApp")]
   void WindowTitle () {
      using var driver = new AppDriver ();
      var title = driver.MainWindow.Title;
      if (!title.Contains ("FApp", StringComparison.OrdinalIgnoreCase))
         throw new Exception ($"Unexpected window title: '{title}'");
   }
}
#endregion
#endif
