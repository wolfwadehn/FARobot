// ╔═╦╗
// ║╬╠╬╦╗ GeoReader.cs
// ║╔╣╠║╣ Implements the GEOReader class, reads in a Dwg2 from .GEO files.
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
namespace FApp;

#region class GEOReader ---------------------------------------------------------------------------
public class GEOReader {
   // Constructor -------------------------------------------------------------
   /// <summary>Construct a GeoReader, given a filename</summary>
   public GEOReader (string file) => mReader = new StreamReader (new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

   // Methods -----------------------------------------------------------------
   /// <summary>Parses the GEO file and returns the Dwg2</summary>
   public Dwg2 Build () {
      while (true) {
         string s = GetLine ();
         if (s == "#~EOF") break;
         if (!s.StartsWith ("#~")) continue;
         if (s == "#~KONT_END") { EndContour (); continue; }
         switch (s[2..].ToInt ()) {
            case 1: LoadDrawingData (); break;
            case 11: LoadDrawingInfo (); break;
            case 31: LoadPoints (); break;
            case 32:
            case 331:
            case 332: LoadSegments (); break;
            case 33: StartContour (); break;
            case 37: LoadBendData (); break;
            case 371: LoadBendlines (); break;
         }
      }
      mReader.Dispose ();
      return mDwg;
   }

   // Loads drawing data #~1 section
   void LoadDrawingData () {
      GetLine ();
      mRevision = GetLine ().ToInt ();
      SkipLines (4);
      if (GetLine ().ToInt () == 2) mScale = 25.4;
   }

   // Loads the drawing info from #~11 section
   void LoadDrawingInfo () {
      SkipLines (6);
      mThickness = mScale * GetLine ().ToDouble ();
   }

   // Loads the 'points' from #~31 section
   void LoadPoints () {
      string line; double[] d = new double[3];
      while ((line = GetLine ()) != null && line != "##~~") {
         if (line.Trim () == "P") {
            int idx = GetLine ().ToInt ();
            var coords = GetLine ();
            var pts = coords.Split (' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < 3; i++) d[i] = pts[i].ToDouble ();
            while (mPts.Count <= idx) mPts.Add (Point2.Nil);
            mPts[idx] = new Point2 (d[0], d[1]);
         }
         GetLine (); // Skips "|~" line
      }
   }

   // Loads the bend data from #~37 section
   void LoadBendData () {
      mBendData = GetInts ();
      mBendAngs = GetLine ().Split ();
      mAngles = [.. mBendAngs.Select (a => a.ToDouble ())];
      mRadii = GetDoubles ();
      mDeduction = -GetLine ().ToDouble ();
   }

   // Loads bend lines from the bend data read in #~37 section
   void LoadBendlines () {
      string line; var pts = new List<Point2> ();
      while ((line = GetLine ()) != null && line != "##~~") {
         if (line is "LIN") {
            GetLine (); // As of now skipping color and linetype
            var a = GetInts ();
            pts.Add (mPts[a[0]]); pts.Add (mPts[a[1]]);
         }
      }

      double rawAngle = mAngles[0];
      bool isNegative = rawAngle < 0 || mBendAngs[0][0] == '-';
      double angle = (mBendData[0] == 1) ? (isNegative ? -Lib.PI : Lib.PI) : (180 - Math.Abs (rawAngle)).D2R ();
      if (mAngles[1] < 0) angle = -Math.Abs (angle);
      var bend = new E2Bendline (mDwg, pts, angle, mRadii[0], 0.42, mThickness) {
         Deduction = mDeduction
      };
      Add (bend);
   }

   // Loads contour shapes from #~32, #~331, and #~332 sections
   void LoadSegments () {
      string line;
      int nextPtNum = 0; // Note: 0 is not a valid point number
      while ((line = GetLine ()) != null && line != "##~~") {
         if (!sSupportedEnts.Contains (line)) continue;
         GetLine (); // As of now skipping color and linetype
         var p = GetInts ();
         switch (line) {
            case "PKT":
               Add (new E2Point (mDwg.Layers.Current, mPts[p[0]]));
               break;
            case "ARC":
            case "FIL":
               var flags = (GetLine () == "1" ? Poly.EFlags.CCW : Poly.EFlags.CW) | Poly.EFlags.HasArcs;
               int arcStartNum = p[1];
               if (nextPtNum != 0 && nextPtNum != arcStartNum) throw new Exception ("GEO contour reconstruction fail");
               mPB.Arc (mPts[arcStartNum], mPts[p[0]], flags);
               nextPtNum = p[2];
               break;
            case "LIN":
            case "CHA":
               int lineStartNum = p[0];
               if (nextPtNum != 0 && nextPtNum != lineStartNum) throw new Exception ("GEO contour reconstruction fail");
               mPB.Line (mPts[lineStartNum]);
               nextPtNum = p[1];
               break;
            case "CIR":
               Add (Poly.Circle (mPts[p[0]], GetLine ().ToDouble ()));
               break;
            case "TXT":
               Point2 pt = mPts[p[0]];
               var d1 = GetDoubles ();             // CharHeight, Ratio, CharAngle
               var d2 = GetDoubles ();             // Line spacing, Text angle
               var angle = d2[1].D2R ();           // Text angle
               var i2 = GetInts ();                // Centering, Direction, NoOfLines
               if (!sAlign.TryGetValue (i2[0], out ETextAlign align)) align = ETextAlign.BotLeft;
               string text = ""; for (int i = 0; i < i2[2]; i++) text += "\n" + GetLine ();
               mDwg.Add (new E2Text (mDwg.Layers.Current, Style2.Default, text[1..], pt, d1[0], angle, 0, d1[1], align));
               break;
         }
      }
      if (nextPtNum != 0) mPB.Line (mPts[nextPtNum]);
   }
   static readonly HashSet<string> sSupportedEnts = ["ARC", "PKT", "LIN", "CIR", "FIL", "CHA", "TXT"];
   static readonly Dictionary<int, ETextAlign> sAlign = new () {
      [9] = ETextAlign.BaseLeft, [17] = ETextAlign.BaseCenter, [33] = ETextAlign.BaseRight,
      [10] = ETextAlign.MidLeft, [18] = ETextAlign.MidCenter, [34] = ETextAlign.MidRight,
      [12] = ETextAlign.TopLeft, [20] = ETextAlign.TopCenter, [36] = ETextAlign.TopRight
   };

   void StartContour () {
      GetLine ();
      mIsClosed = GetInts ()[1] is 24;
   }

   void EndContour () {
      if (!mPB.IsNull) {
         if (mIsClosed) mDwg.Add (mPB.Close ().Build ());
         else mDwg.Add (mPB.Build ());
      }
   }

   void Add (Poly poly) => Add (new E2Poly (mDwg.Layers.Current, poly));

   void Add (Ent2 ent) => mDwg.Add (ent);

   public static Dwg2 Load (string name) => new GEOReader (name).Build ();

   // Helpers ------------------------------------------------------------------
   void SkipLines (int mn) {
      for (int i = 0; i < mn; i++) GetLine ();
   }

   string GetLine () {
      var s = mReader.ReadLine () ?? throw new ParseException ("Unexpected end of file");
      return s.Trim ();
   }

   // Returns the integers from the current line
   int[] GetInts () => [.. GetLine ().Split ().Select (a => a.ToInt ())];

   // Returns the doubles from the current line
   double[] GetDoubles () => [.. GetLine ().Split ().Select (a => a.ToDouble ())];


   // Private data -------------------------------------------------------------
   int mRevision;
   bool mIsClosed;
   double mScale = 1, mThickness, mDeduction;
   int[] mBendData = [];
   string[] mBendAngs = [];
   double[] mAngles = [], mRadii = [];
   readonly PolyBuilder mPB = new ();
   readonly Dwg2 mDwg = new ();
   readonly List<Point2> mPts = [];
   readonly StreamReader mReader;
}
#endregion
