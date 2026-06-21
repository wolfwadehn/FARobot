// ╔═╦╗
// ║╬╠╬╦╗ Util.cs
// ║╔╣╠║╣ Some utility functions and properties
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FCursor = System.Windows.Forms.Cursor;
namespace FApp;

#region class Util ---------------------------------------------------------------------------------
/// <summary>Various WPF utility functions and extension methods</summary>
static class Util {
   // Properties ---------------------------------------------------------------
   /// <summary>The pixel-scale of the current drawing scene</summary>
   public static double PxScale => Lux.UIScene?.PixelScale ?? 1;

   // Methods ------------------------------------------------------------------
   /// <summary>Loads a bitmap from a stream (various formats like PNG, JPG are supported)</summary>
   /// The resulting BitmapImage could be used as a Source for an Image
   public static BitmapImage LoadBitmapFromStream (Stream stm) {
      if (stm is not MemoryStream) {
         MemoryStream ms = new (); stm.CopyTo (ms);
         ms.Position = 0; stm = ms;
      }

      BitmapImage bmp = new ();
      bmp.BeginInit (); bmp.StreamSource = stm; bmp.EndInit ();
      if (bmp.CanFreeze) bmp.Freeze ();
      else bmp.DownloadCompleted += FreezeBitmap;
      return bmp;

      static void FreezeBitmap (object? sender, EventArgs e) {
         if (sender is BitmapImage bmp) {
            bmp.DownloadCompleted -= FreezeBitmap;
            bmp.StreamSource.Dispose ();
            bmp.Freeze ();
         }
      }
   }

   /// <summary>Create a solid color brush, given a Nori.Color4</summary>
   public static Brush MakeBrush (Color4 color) {
      var brush = new SolidColorBrush (Color.FromArgb (color.A, color.R, color.G, color.B));
      brush.Freeze ();
      return brush;
   }

   /// <summary>Convert pixels to 2D world coordinates</summary>
   public static Point2 ToWorld (Vec2S pix)
      => (Point2)(Lux.UIScene?.PixelToWorld (pix) ?? Point3.Zero);

   /// <summary>Parse a double that may use either '.' or ',' as the decimal separator.</summary>
   public static bool TryParseDouble (string s, out double v) =>
      double.TryParse (s.Replace (',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

   /// <summary>Returns the parsed value, or 0 if the string is not a valid number.</summary>
   public static double ParseDouble (string s) { TryParseDouble (s, out double v); return v; }
}
#endregion
