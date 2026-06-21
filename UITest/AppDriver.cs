// ╔═╦╗
// ║╬╠╬╦╗ AppDriver.cs
// ║╔╣╠║╣ Helpers for launching and interacting with the FApp process
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
#if FLAUI
using FlaUI.Core.Conditions;
#endif
namespace FApp.UITesting;

#region class UIFixtureAttribute -------------------------------------------------------------------
/// <summary>Marks a class as a UI test fixture</summary>
[AttributeUsage (AttributeTargets.Class)]
sealed class UIFixtureAttribute (int id, string name) : Attribute {
   public int Id { get; } = id;
   public string Name { get; } = name;
}
#endregion

#region class UITestAttribute ----------------------------------------------------------------------
/// <summary>Marks a method as a UI test</summary>
[AttributeUsage (AttributeTargets.Method)]
sealed class UITestAttribute (int id, string name) : Attribute {
   public int Id { get; } = id;
   public string Name { get; } = name;
}
#endregion

#if FLAUI
#region class AppDriver ----------------------------------------------------------------------------
/// <summary>Launches FApp and provides access to its main window for UI automation</summary>
sealed class AppDriver : IDisposable {
   public AppDriver () {
      mAutomation = new UIA3Automation ();
      var exe = Path.Combine (AppContext.BaseDirectory, "FApp.exe");
      mApp = Application.Launch (exe);
      MainWindow = mApp.GetMainWindow (mAutomation, TimeSpan.FromSeconds (10));
   }

   public Window MainWindow { get; }
   public ConditionFactory CF => mAutomation.ConditionFactory;

   public void Dispose () {
      mApp.Close ();
      mAutomation.Dispose ();
   }

   // Implementation -----------------------------------------------------------
   readonly UIA3Automation mAutomation;
   readonly Application mApp;
}
#endregion
#endif
