// ╔═╦╗
// ║╬╠╬╦╗ Program.cs
// ║╔╣╠║╣ Entry point into the FApp.UITest application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Reflection;
namespace FApp.UITesting;

#region class Program ------------------------------------------------------------------------------
class Program {
   [STAThread]
   static int Main (string[] args) {
      Console.WriteLine ("FApp UI Tests");
      Console.WriteLine (new string ('-', 60));
      var fixtures = Assembly.GetExecutingAssembly ().GetTypes ()
         .Where (t => t.GetCustomAttribute<UIFixtureAttribute> () is not null)
         .OrderBy (t => t.GetCustomAttribute<UIFixtureAttribute> ()!.Id);
      int passed = 0, failed = 0;
      foreach (var fixtureType in fixtures) {
         var attr = fixtureType.GetCustomAttribute<UIFixtureAttribute> ()!;
         Console.WriteLine ($"\n[{attr.Id}] {attr.Name}");
         var methods = fixtureType.GetMethods (BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where (m => m.GetCustomAttribute<UITestAttribute> () is not null)
            .OrderBy (m => m.GetCustomAttribute<UITestAttribute> ()!.Id);
         foreach (var method in methods) {
            var testAttr = method.GetCustomAttribute<UITestAttribute> ()!;
            Console.Write ($"  [{testAttr.Id:D3}] {testAttr.Name} ... ");
            try {
               var instance = Activator.CreateInstance (fixtureType)!;
               method.Invoke (instance, null);
               Console.ForegroundColor = ConsoleColor.Green;
               Console.WriteLine ("PASS");
               Console.ResetColor ();
               passed++;
            } catch (Exception ex) {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine ("FAIL");
               Console.ResetColor ();
               Console.WriteLine ($"    {ex.InnerException?.Message ?? ex.Message}");
               failed++;
            }
         }
      }
      Console.WriteLine ($"\n{new string ('-', 60)}");
      Console.WriteLine ($"Passed: {passed}   Failed: {failed}   Total: {passed + failed}");
      return failed > 0 ? 1 : 0;
   }
}
#endregion
