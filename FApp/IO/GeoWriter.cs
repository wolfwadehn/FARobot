// ╔═╦╗
// ║╬╠╬╦╗ GEOWriter.cs
// ║╔╣╠║╣ Implements GEOWriter: writes out a Dwg2 to a GEO file
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Reactive.Linq;
namespace FApp;

public class GEOWriter (Dwg2 dwg) {
   public string Write () {
      S.Clear ();
      ExplodeBlock ();
      OutDrawingData ();    // Write the #~1 section
      OutDrawingInfo ();    // Write the #~11 section
      OutPoints ();         // Write the #~31 section
      OutPointsAndTexts (); // Write the #~32 section
      OutPlines ();         // Write the #~33 & #~331 sections
      OutBendLineData ();   // Write the #~37 & #~371 sections
      OutFinal ();          // Write the epilogue
      return S.ToString ();
   }

   public static void Save (Dwg2 dwg, string file)
      => File.WriteAllText (mFile = file, new GEOWriter (dwg).Write ());

   void Out (string s) => S.Append (s);

   // Writes the bunch of passed doubles separated by a space
   void OutDs (params double[] vals) {
      string fmt = Lib.Testing ? "F3" : "F9";
      S.AppendJoin (' ', vals.Select (a => a.ToString (fmt))).AppendLine ();
   }

   // For some data we don't have now, leave empty lines
   void OutEmptyLines (int n) => S.Append ('\n', n);

   void EndElement () => S.AppendLine ("|~");

   void EndSection () => S.AppendLine ("##~~");

   void EndBlock () => S.AppendLine ("#~END");

   // Explodes all block in the drawing if any.
   void ExplodeBlock () {
      foreach (var insert in mDwg.Ents.OfType<E2Insert> ()) {
         var xfm = insert.Xfm;
         foreach (var ent in insert.Block.Ents)
            if (ent is not (E2Dim or E2Solid)) mExplodedEnts.Add (ent * xfm);
      }
   }

   // Writes the section #~1: Drawing data
   // Contains file version, date of file modified, min and max points, bounded area, etc.,
   void OutDrawingData () {
      var date = Lib.Testing ? new DateTime (2025, 1, 1) : DateTime.Today;
      Out ($"#~1\n1.03\n1\n{date:dd.MM.yyyy}\n"); // Version: 1.03, Revision: 1
      Bound2 polyBound = new (0, 0); mDwg.Polys.ForEach (a => polyBound += a.GetBound ());
      if (polyBound.IsEmpty) polyBound = new Bound2 (100, 100);
      OutDs (polyBound.X.Min, polyBound.Y.Min, 0); OutDs (polyBound.X.Max, polyBound.Y.Max, 0);
      // Area enclosed, mm(1)|inch(2), data precision, 2D(0)|3D(1), a flag related to part
      Out (polyBound.Area.ToString ("F3")); Out ("\n1\n0.001\n0\n1\n");
      EndSection ();
   }

   // Writes the section #~11: Drawing info
   // Contains the meta data of the drawing.
   void OutDrawingInfo () {
      Out ($"#~11\n\n{Path.GetFileNameWithoutExtension (mFile)}\n");
      OutEmptyLines (3); Out ("NONE\n"); // For customer name, author, job no and material name
      var thickness = mDwg.Ents.OfType<E2Bendline> () is var b && b.Any () ? b.First ().Thickness : 0;
      OutDs (thickness); OutEmptyLines (3); // For rule name, table name and target machine
      for (int i = 0; i < 9; i++) Out ($"{(i == 4 ? 1 : 0)}\n"); // Set some necessary default flags
      OutEmptyLines (1); EndSection (); EndBlock ();
   }

   // Writes the section #~31: Points data
   // Contains all the unique points of all entities in the drawing sorted w.r.t X, then by Y.
   void OutPoints () {
      List<Point2> pts = [];
      // Firstly, gather all the points
      foreach (var ent in mDwg.Ents.Concat (mExplodedEnts)) {
         switch (ent) {
            case E2Point point: mPoints.Add (point); pts.Add (point.Pt); break;
            case E2Text text: mTexts.Add (text); pts.Add (text.Pt); break;
            case E2Poly poly:
               mPolys.Add (poly);
               var pline = poly.Poly;
               foreach (var seg in pline.Segs) {
                  if (seg.IsArc) pts.Add (seg.Center);
                  if (!seg.IsCircle) pts.Add (seg.A);
               }
               if (pline.IsOpen) pts.Add (pline.B);
               break;
            case E2Bendline bend:
               mBends.Add (bend);
               bend.Pts.ForEach (pts.Add);
               break;
         }
      }

      // Then, take the unique points and sort them
      pts = [.. pts.Distinct ().OrderBy (p => p.X).ThenBy (p => p.Y)];
      // Finally, write the points
      Out ("#~31\n");
      for (int i = 0; i < pts.Count; i++) {
         Point2 pt = pts[i]; int idx = i + 1;
         mPts.TryAdd (pt, idx); // Store the points for later use
         Out ($"P\n{idx}\n"); OutDs (pt.X, pt.Y, 0); EndElement ();
      }
      EndSection ();
   }

   // Writes the section #~32: Points and text entities data
   void OutPointsAndTexts () {
      if (mPoints.Count == 0 && mTexts.Count == 0) return;
      Out ("#~32\n");
      // Write the point entities
      foreach (var pt in mPoints) {
         int color = 1;
         Out ($"PKT\n{color} 2\n{mPts[pt.Pt]}\n");
         EndElement ();
      }

      // Write the text entities
      foreach (var txt in mTexts) {
         // Geo file supports only ISO (1), ISO prop.(130) and Bold (131) font types.
         int color = 1, fontType = 1; // Assign ISO type as default
         Out ($"TXT\n{color} {fontType}\n{mPts[txt.Pt]}\n");
         // Width to height ratio and angle of the character are assumed as 1 and 0 respectively below.
         Out ($"{txt.Height} 1 0\n{txt.DYLine} {txt.Angle.R2D ()}\n");

         if (!sAlign.TryGetValue (txt.Alignment, out var align)) align = 9;
         var strings = txt.Text.Split ('\n');
         Out ($"{align} 1 {strings.Length}\n"); // The flag 1 says it's a Left-to-Right text
         strings.ForEach (s => Out (s + '\n'));
         EndElement ();
      }
      EndSection ();
   }
   static readonly Dictionary<ETextAlign, int> sAlign = new () {
      [ETextAlign.BotLeft] = 9, [ETextAlign.BotCenter] = 17, [ETextAlign.BotRight] = 33,
      [ETextAlign.BaseLeft] = 9, [ETextAlign.BaseCenter] = 17, [ETextAlign.BaseRight] = 33,
      [ETextAlign.MidLeft] = 10, [ETextAlign.MidCenter] = 18, [ETextAlign.MidRight] = 34,
      [ETextAlign.TopLeft] = 12, [ETextAlign.TopCenter] = 20, [ETextAlign.TopRight] = 36
   };

   // Writes the plines in the drawing, each one under a separate #~33 section
   void OutPlines () {
      // Get the outer plines first
      foreach (var outer in mPolys) {
         if (mPolys.Where (p => p != outer).All (p => !p.Bound.Contains (outer.Bound)))
            mOuter.Add (outer);
      }

      // Then, write each outer plines followed by its inner plines
      int idx = 0; // unique index of a pline
      foreach (var outer in mOuter) {
         int parentIdx = ++idx;
         var inners = mPolys.Where (p => p != outer && outer.Bound.Contains (p.Bound));
         OutThisPline (outer, idx, inners.Count (), 0);
         inners.ForEach (pline => OutThisPline (pline, ++idx, 0, parentIdx));
      }

      // Writes the sections #~33: Pline info and #~331: Segments data under the pline
      void OutThisPline (E2Poly ep, int idx, int innersCount, int parentIdx) {
         // Firstly, write the general info of the polyline
         Poly p = ep.Poly;
         var (isClosed, isOuter) = (p.IsClosed, mOuter.Contains (ep));
         Out ($"#~33\n\n{idx} {(isClosed ? 24 : 25)} {(isOuter ? 0 : 1)}\n{innersCount}\n");
         OutDs (0, 0, p.GetWinding () == Poly.EWinding.CCW ? 1 : -1);
         Bound2 bound = p.GetBound ();
         OutDs (bound.X.Min, bound.Y.Min, 0); OutDs (bound.X.Max, bound.Y.Max, 0);
         if (isClosed) { // Centroid and area data
            OutDs (bound.X.Length / 2, bound.Y.Length / 2, 0); OutDs (p.GetBound ().Area);
         } else { OutDs (double.NaN, double.NaN, 0); OutDs (0); }
         Out ($"{parentIdx}\n");
         EndSection ();

         // Then, write the segments (line & arc) data of that pline
         Out ($"#~331\n");
         int color = 1, lineType = 0; // Represents continuous lines
         foreach (var seg in p.Segs) {
            if (seg.IsArc) {
               if (seg.IsCircle) {
                  Out ($"CIR\n{color} {lineType}\n{mPts[seg.Center]}\n");
                  OutDs (seg.Radius);
               } else {
                  Out ($"ARC\n{color} {lineType}\n{mPts[seg.Center]} {mPts[seg.A]} {mPts[seg.B]}\n");
                  Out ((seg.IsCCW ? "1" : "-1") + '\n');
               }
            } else {
               Out ($"LIN\n{color} {lineType}\n{mPts[seg.A]} {mPts[seg.B]}\n");
            }
            EndElement ();
         }
         EndSection ();
         Out ("#~KONT_END\n"); // Contour End
      }
   }

   // Writes the sections #~37: Bend parameters and #~371: Bend line data
   // foreach bend lines present in the drawing.
   void OutBendLineData () {
      foreach (var bendline in mBends) {
         // Bending type, method and the technique adopted are the necessary bend parameters to be written.
         // As of now, assume the followings:
         int type = 0,  // V-bend
           method = 0,  // Air bending
         technique = 0; // No special treatment is done for the edges

         double angle = (Lib.PI - Math.Abs (bendline.Angle).Clamp (0, Lib.PI)).R2D ();
         if (angle.IsZero ()) {
            type = 1;      // Fold
            method = 2;    // hemming is performed
            technique = 1; // bending with preliminary bend
         }
         if (bendline.Angle < 0) angle = -angle;
         if (bendline.Angle.EQ (-Lib.PI)) angle = 0;
         double preBendAngle = 0; // Assuming no pre-bending is done
         double deduction = -bendline.Deduction; if (deduction.IsNan) deduction = 0;

         // Now, write the acquired data of this bend
         Out ($"#~37\n{type} {method} {technique}\n");
         OutDs (angle, preBendAngle); OutDs (bendline.Radius, bendline.Radius); OutDs (deduction);
         OutEmptyLines (2); // For punch name and die name
         EndSection ();

         // Then write the points of this bend line
         Out ("#~371\n");
         for (int i = 0; i < bendline.Pts.Length; i += 2) {
            Out ($"LIN\n4 {(bendline.Angle > 0 ? 0 : 1)}\n{mPts[bendline.Pts[i]]} {mPts[bendline.Pts[i + 1]]}\n");
            EndElement ();
         }
         EndSection (); Out ("#~BIEG_END\n"); // Bend End
      }
   }

   void OutFinal () { EndBlock (); Out ("#~EOF\n"); }

   static string mFile = "";
   readonly Dwg2 mDwg = dwg;
   readonly StringBuilder S = new ();
   Dictionary<Point2, int> mPts = []; // Stores all the points in the drawing along with its indices
   List<E2Point> mPoints = [];        // Stores all the point entities
   List<E2Text> mTexts = [];          // Stores all the text entities
   HashSet<E2Poly> mOuter = [];       // Stores only the outer contours
   List<E2Poly> mPolys = [];          // Stores all the contours (both outer and outer)
   List<E2Bendline> mBends = [];      // Stores all the bend lines
   List<Ent2> mExplodedEnts = [];     // Stores the exploded entities from blocks if any
}