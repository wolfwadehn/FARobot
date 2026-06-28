// ╔═╦╗
// ║╬╠╬╦╗ Util.cs
// ║╔╣╠║╣ Some utility functions and properties
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.Windows.Media;
namespace FRobot;

static class Util {
   /// <summary>Create a solid color brush, given a Nori.Color4</summary>
   public static Brush MakeBrush (Color4 color) {
      var brush = new SolidColorBrush (Color.FromArgb (color.A, color.R, color.G, color.B));
      brush.Freeze ();
      return brush;
   }

   /// <summary>Parse a double that may use either '.' or ',' as the decimal separator.</summary>
   public static bool TryParseDouble (string s, out double v) =>
      double.TryParse (s.Replace (',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

   /// <summary>Returns the parsed value, or 0 if the string is not a valid number.</summary>
   public static double ParseDouble (string s) { TryParseDouble (s, out double v); return v; }
}
