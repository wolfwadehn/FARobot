// ╔═╦╗
// ║╬╠╬╦╗ Console.cs
// ║╔╣╠║╣ Entry point into the FRobot.Console application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Diagnostics.CodeAnalysis;
using static System.Reflection.BindingFlags;
using Nori;
using System.Text;
using System.ComponentModel.DataAnnotations;
namespace FRobot.Con;

#region class Program ------------------------------------------------------------------------------
static class Program {
   /// <summary>Entry point into the FRobot.Con program</summary>
   static void Main (string[] args) {
      if (args.Length == 0) Help ();
      else {
         var mi = typeof (Program).GetMethods (Static | NonPublic | Public)
            .FirstOrDefault (mi => mi.HasAttribute<ConsoleCommandAttribute> () && mi.Name.EqIC (args[0]));
         if (mi == null) Help ();
         else mi.Invoke (null, null);
      }
   }

   /// <summary>Displays usage help</summary>
   [ConsoleCommand]
   static void Help () {
      Console.WriteLine ($$"""
         FRobot.Con: FRobot console utility for developers.
         Build {{Build}}.

         CLEAN           - Do basic cleanup on all the FRobot source files
         COUNT           - Do a line-count on FRobot source files
         COVERAGE        - Compute coverage % for FRobot.Test
         HELP            - Display this help message
         NEXTID          - Gets the next available test Id
         OPTIMIZE 0/1    - Turns optimization on / off for all FRobot projects
         XMLDOC 0/1      - Turns XML documentation on / off for all FRobot projects
         PNGCRUSH folder - Optimize all PNG files in the given folder
         """);
      Environment.Exit (0);
   }

   [ConsoleCommand] static void Clean () => SrcClean.Run ();
   [ConsoleCommand] static void Coverage () => ComputeCoverage.Run ();
   [ConsoleCommand] static void Count () => LineCount.Run ();
   [ConsoleCommand] static void NextId () => GetNextId.Run ();

   [ConsoleCommand]
   static void PNGCrush () {
      string[] args = Environment.GetCommandLineArgs ();
      if (args.Length != 3) Help ();
      string folder = Path.GetFullPath (args[2]);
      if (!Directory.Exists (folder))
         Fatal ($"Folder {folder} does not exist");
      global::PNGCrush.Run (folder);
   }

   [ConsoleCommand]
   static void Optimize () {
      string[] args = Environment.GetCommandLineArgs ();
      if (args.Length != 3) Help ();
      if (!int.TryParse (args[2], out int n)) Help ();
      if (n is < 0 or > 1) Help ();
      SetOptimize.Run (n == 1);
   }

   [DoesNotReturn]
   public static void Fatal (string s) {
      Console.WriteLine ();
      Console.ForegroundColor = ConsoleColor.Yellow;
      Console.WriteLine (s);
      Console.ResetColor ();
      Environment.Exit (-1);
   }

   [ConsoleCommand]
   static void XmlDoc () {
      string[] args = Environment.GetCommandLineArgs ();
      if (args.Length != 3) Help ();
      if (!int.TryParse (args[2], out int n)) Help ();
      if (n is < 0 or > 1) Help ();
      SetXmlDoc.Run (n == 1);
   }

   [ConsoleCommand]
   static void TestHook () {
      int n = 0;
      HashSet<string> encodings = [];
      foreach (var file in Directory.GetFiles ("W:/DXF", "*.dxf")) {
         n++;
         Console.Title = n.ToString ();
         var lines = File.ReadAllLines (file);
         for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Trim () == "$DWGCODEPAGE") {
               if (encodings.Add (lines[i + 2].Trim ().ToUpper ()))
                  Console.WriteLine (lines[i + 2].ToUpper ());
               break;
            }
         }
      }
   }

   static int Build = 1;
}
#endregion

#region [ConsoleCommand] attribute -----------------------------------------------------------------
/// <summary>[ConsoleCommand] attribute is used to decorate methods that should be exposed as commands</summary>
[AttributeUsage (AttributeTargets.Method)]
class ConsoleCommandAttribute : Attribute { }
#endregion
