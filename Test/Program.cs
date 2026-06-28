// ╔═╦╗
// ║╬╠╬╦╗ Program.cs
// ║╔╣╠║╣ Entry point into the FRobot.Test application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FRobot.Testing;

#region class Program ------------------------------------------------------------------------------
/// <summary>Entry point of the Nori.Test application</summary>
class Program {
   // Entry point into Nori.Test.exe
   [STAThread]
   public static void Main (string[] args)
      => new Program (args).Run ();

   // Implementation -----------------------------------------------------------
   // The constructor gathers all the tests, and also parses the command line arguments
   Program (string[] args) {
      Lib.Init ();
      GLFWHost.Init (() => { });
      mWindow = new Window (500, 500, "FRobot Tests", Window.EFlags.None);
      Lib.Register (new FileStmLocator ("pix:", "F:/Wad/"));
      Lib.AddNamespace ("FRobot");
      TestRunner.RunDiff = true;
      TestRunner.ParseArgs (args);
      foreach (var arg in args) {
         if (int.TryParse (arg, out int n)) {
            if (n >= 0) mTestID.Add (n);
            else mFixtureID.Add (-n);
         }
      }
   }
   readonly List<int> mTestID = [];       // If non-empty, run only these tests
   readonly List<int> mFixtureID = [];    // If non-empty, run only these fixtures
   readonly Window mWindow;

   // This runs the tests in this assembly
   void Run () {
      var assembly = typeof (Program).Assembly;
      TestRunner.GatherAndRun ([assembly], Filter, ConsoleTestCallback.It);
   }

   // This is the filter used to run specific tests or fixtures
   TestRunner.ETest Filter (Test t) {
      if (mTestID.Count > 0 || mFixtureID.Count > 0)
         return mTestID.Contains (t.Id) || mFixtureID.Contains (t.Fixture.Id) ? TestRunner.ETest.Run : TestRunner.ETest.Hide;
      return TestRunner.ETest.Run;
   }
}
#endregion
