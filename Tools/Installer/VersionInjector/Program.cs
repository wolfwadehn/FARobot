// ╔═╦╗
// ║╬╠╬╦╗ VersionInjector
// ║╔╣╠║╣ Utility tool which extracts build number from FApp and updates the ISS file
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Text.RegularExpressions;
namespace VersionInjector;

class Program {
   static void Main () {
      if (!GetPixVersion ()) {
         Console.ForegroundColor = ConsoleColor.Red;
         Console.WriteLine ("\nERROR: Extracting AssemblyVersion\n");
         Console.ResetColor ();
      }
      UpdateISS ();
   }

   // Fetches the build number from AssemblyInfo.cs in FApp
   static bool GetPixVersion () { // Match format: AssemblyVersion ("2025.11.12.0")
      var match = Regex.Match (File.ReadAllText (@"F:\FApp\Properties\AssemblyInfo.cs"),
                        @"AssemblyVersion\s*\(""\d{4}.\d{2}\.(\d+)\.(\d+)""\)");
      if (match.Success) {
         sBuild = int.Parse (match.Groups[1].Value);
         sRevision = int.Parse (match.Groups[2].Value);
      }
      return match.Success;
   }
   static int sBuild, sRevision;

   // Regenerates the pix.iss file with the build number
   static void UpdateISS () {
      var lines = File.ReadAllText (@"F:\Tools\Installer\pix.template.iss")
                      .Replace ("{Version}", $"{sBuild}{(sRevision > 0 ? $".{sRevision}" : "")}");
      File.WriteAllText (@"F:\Tools\Installer\pix.iss", lines);
   }
}

