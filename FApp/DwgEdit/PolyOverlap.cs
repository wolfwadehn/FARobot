// ╔═╦╗
// ║╬╠╬╦╗ PolyOverlap.cs
// ║╔╣╠║╣ Overlapping poly sections in the drawing
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region DwgOverlap ---------------------------------------------------------------------------------
/// <summary>Drawing overlap (line, arc, circle) overlap detection and ranging computer</summary>
class DwgOverlap {
   public DwgOverlap (Dwg2 dwg) {
      var ents = dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0").Select (e2p => e2p.Poly);
      List<Seg> segs = [.. ents.SelectMany (p => p.Segs)];

      // QnDirty: Pit each seg against every other
      for (int i = segs.Count - 1; i >= 0; i--) {
         var seg = segs[i];
         for (int j = i - 1; j >= 0; j--) {
            var seg2 = segs[j];
            CheckOverlap (seg, seg2);
         }
      }

      // Collate the result and generate resultant overlap polys
      ConsolidateOverlaps (PolyOverlapMap);
   }

   public bool GotOverlap => PolyOverlapMap.Count > 0;

   /// <summary>Gets overlapping sections as Polys for highlighing those sections</summary>
   public IEnumerable<Poly> GetOverlapSectionRepPoly () => ComputeResultPolys (PolyOverlapMap);

   public IEnumerable<Poly> GetOverlappingPolys () => PolyOverlapMap.Keys;

   // Remnant Polys after stripping given overlapping poly off of the overlapping sections!
   public IEnumerable<Poly> ComputeOverlapFreePolys (Poly p) {
      if (PolyOverlapMap.TryGetValue (p, out var overlaps)) {
         foreach (var poly in ComputeOverlapFreePoly (p, overlaps))
            yield return poly;
      }
   }

   // Final overlap section representative polys, for feedback display!
   // Note: These are "clean" enough to be used for healing polys after they have been stripped off of overlapping sections!
   static IEnumerable<Poly> ComputeResultPolys (Dictionary<Poly, List<(double SLie, double ELie)>> overlaps) {
      // Per Poly coverage is useful if we need to trim-out overlapping sections
      // Here, we simply wish to consolidate all the overlapping sections (across all overlapping Polys) into "highlight" Polys
      var ovrInfo = new OverlapInfo ();
      foreach (var (poly, ranges) in overlaps) {
         foreach (var r in ranges) {
            int N = (int)double.Floor (r.SLie);
            var seg = poly[N];
            if (seg.IsArc) {
               var (sa, ea) = seg.GetStartAndEndAngles ();
               var span = ea - sa;
               ovrInfo.AddOvr (seg.Center, seg.Radius, sa + span * (r.SLie - N), sa + span * (r.ELie - N), seg.IsCCW);
            } else ovrInfo.AddOvr (seg.GetPointAt (r.SLie - N), seg.GetPointAt (r.ELie - N));
         }
      }
      return ovrInfo.GenerateOverlapPolys ();
   }

   // Remnant Polys after stripping given overlapping poly off of the overlapping sections!
   // Note: The ranges are ordered and non-overlapping themselves.
   static IEnumerable<Poly> ComputeOverlapFreePoly (Poly p, List<(double SLie, double ELie)> overlaps) {
      // Incoming polys could be open or closed, but remnant poly will all be open.
      double sLie = 0.0;
      foreach (var r in overlaps) {
         if (!sLie.EQ (r.SLie))
            yield return p.Sliced (sLie, r.SLie);
         sLie = r.ELie;
      }
      double eLie = p.Count;
      if (!sLie.EQ (eLie))
         yield return p.Sliced (sLie, eLie);
   }

   // Seg level - compacts overlapping overlaps
   static void ConsolidateOverlaps (Dictionary<Poly, List<(double SLie, double ELie)>> overlaps) {
      foreach (var list in overlaps.Values)
         MergeRanges (list);
   }

   internal static void MergeRanges (List<(double SLie, double ELie)> list) {
      if (list.Count == 1) return;
      list.Sort ((a, b) => {
         if (a.SLie.EQ (b.SLie))
            return a.ELie.EQ (b.ELie) ? 0 : (a.ELie < b.ELie ? -1 : 1);
         return a.SLie < b.SLie ? -1 : 1;
      });
      for (int i = 0; i < list.Count; i++) {
         var r = list[i];
         for (int j = i + 1; j < list.Count; j++) {
            var r2 = list[j]; // Note: r2.SLie >= r.SLie
            if (r2.SLie > r.ELie - Lib.Epsilon) break;
            list[i] = r = (Math.Min (r.SLie, r2.SLie), Math.Max (r.ELie, r2.ELie));
            list.RemoveAt (j--);
         }
      }
   }

   // Attempts to determine the overlap range, if the given segs overlap
   void CheckOverlap (Seg sa, Seg sb) {
      Lib.Check (sa.Poly != sb.Poly || sa.N != sb.N, "Coding error");
      if (sa.IsArc != sb.IsArc) return;
      if (sa.IsArc) {
         if (!sa.Center.EQ (sb.Center) || !sa.Radius.EQ (sb.Radius)) return; // Concentric check

         if (sa.IsCircle && sb.IsCircle) { // Circle v/s Circle
            RegisterOverlap (sa.Poly, (0, 1));
            RegisterOverlap (sb.Poly, (0, 1));
            return;
         }

         if (sa.IsCircle || sb.IsCircle) { // Circle v/s Arc
            var (circ, arc) = sa.IsCircle ? (sa, sb) : (sb, sa);
            RegisterOverlap (arc.Poly, (0, 1)); // Arc (always) has full overlap, against Circle
            // Note: All Arcs are treated as CCW arcs!
            var (sAng, eAng) = arc.GetStartAndEndAngles ();
            if (!arc.IsCCW) (sAng, eAng) = ToCCW ((sAng, eAng));
            // If the Arc crosses over 0 degree, we need to handle it differently!
            if (eAng > 0 && sAng < 0) {
               RegisterOverlap (circ.Poly, (0, eAng / Lib.TwoPI));
               RegisterOverlap (circ.Poly, ((sAng + Lib.TwoPI) / Lib.TwoPI, 1));
               return;
            }
            if (eAng < 0) (sAng, eAng) = (sAng + Lib.TwoPI, eAng + Lib.TwoPI);
            RegisterOverlap (circ.Poly, (sAng / Lib.TwoPI, eAng / Lib.TwoPI));
            return;
         }

         // Arc v/s Arc
         // Note: All Arcs are treated as CCW arcs!
         var (saAngStart, saAngEnd) = sa.GetStartAndEndAngles ();
         if (!sa.IsCCW) (saAngStart, saAngEnd) = ToCCW ((saAngStart, saAngEnd));

         var (sbAngStart, sbAngEnd) = sb.GetStartAndEndAngles ();
         if (!sb.IsCCW) (sbAngStart, sbAngEnd) = ToCCW ((sbAngStart, sbAngEnd));

         if ((saAngStart.EQ (sbAngStart) && saAngEnd.EQ (sbAngEnd)) || (saAngStart.EQ (sbAngEnd) && saAngEnd.EQ (sbAngStart))) { // Exact overlap
            RegisterOverlap (sa.Poly, (sa.N, sa.N + 1));
            RegisterOverlap (sb.Poly, (sb.N, sb.N + 1));
            return;
         }

         // Partial-overlap case! [Adjust the end angles to bring them closer]
         if ((sbAngEnd - saAngStart) > Lib.TwoPI + Lib.Epsilon) {
            saAngStart += Lib.TwoPI; saAngEnd += Lib.TwoPI;
         } else if ((saAngEnd - sbAngStart) > Lib.TwoPI + Lib.Epsilon) {
            sbAngStart += Lib.TwoPI; sbAngEnd += Lib.TwoPI;
         }
         if (saAngStart > sbAngEnd - Lib.Epsilon || sbAngStart > saAngEnd - Lib.Epsilon) return; // There is NO "significant" overlap!

         {
            var span = saAngEnd - saAngStart;
            var (s, e) = ((sbAngStart - saAngStart) / span, (sbAngEnd - saAngStart) / span);
            (s, e) = (Math.Max (s, 0), Math.Min (e, 1)); // Clamp to range!
            if (!sa.IsCCW) (s, e) = (1 - e, 1 - s);
            RegisterOverlap (sa.Poly, (sa.N + s, sa.N + e));
         }
         {
            var span = sbAngEnd - sbAngStart;
            var (s, e) = ((saAngStart - sbAngStart) / span, (saAngEnd - sbAngStart) / span);
            (s, e) = (Math.Max (s, 0), Math.Min (e, 1)); // Clamp to range!
            if (!sb.IsCCW) (s, e) = (1 - e, 1 - s);
            RegisterOverlap (sb.Poly, (sb.N + s, sb.N + e));
         }
      } else {
         if (!sa.A.DistToLineSq (sb.A, sb.B).IsZero () || !sa.B.DistToLineSq (sb.A, sb.B).IsZero ()) return; // Collinear check

         if ((sa.A.EQ (sb.A) && sa.B.EQ (sb.B)) || (sa.A.EQ (sb.B) && sa.B.EQ (sb.A))) { // Both overlap exactly [Quite a common case!]
            RegisterOverlap (sa.Poly, (sa.N, sa.N + 1));
            RegisterOverlap (sb.Poly, (sb.N, sb.N + 1));
            return;
         }

         // The segments might be collinear but no overlap. Check for this and return.
         bool segSwapped = sb.Length > sa.Length;
         var (longr, shortr) = segSwapped ? (sb, sa) : (sa, sb); // Take longest seg as measurement base
         (double ss, double se) = (longr.GetLie (shortr.A), longr.GetLie (shortr.B));
         bool opposing = se < ss;
         if (opposing) (ss, se) = (se, ss); // Simply ordering the lies is sufficient to align the two (since the base is the same)
         if (ss > 1 - Lib.Epsilon || se < Lib.Epsilon) return;

         var (shortrA, shortrB) = opposing ? (shortr.B, shortr.A) : (shortr.A, shortr.B); // Aligned end point of shortr
         // Get end points delimiting the overlapping section
         var (pt, pt2) = (ss < 0 ? longr.A : shortrA, se > 1 ? longr.B : shortrB);


         // Note: The following logic would also capture self-overlapping Poly
         {
            var (s, e) = (longr.GetLie (pt), longr.GetLie (pt2));
            Lib.Check (s is > -Lib.Epsilon and < 1 + Lib.Epsilon, "");
            Lib.Check (e is > -Lib.Epsilon and < 1 + Lib.Epsilon, "");
            Lib.Check (s < e, "");
            int N = longr.N;
            RegisterOverlap (longr.Poly, (N + s, N + e));
         }
         {
            var (s, e) = (shortr.GetLie (pt), shortr.GetLie (pt2));
            Lib.Check (s is > -Lib.Epsilon and < 1 + Lib.Epsilon, "");
            Lib.Check (e is > -Lib.Epsilon and < 1 + Lib.Epsilon, "");
            Lib.Check (s < e == !opposing, "");
            if (opposing) (s, e) = (e, s);
            Lib.Check (s < e, "");
            int N = shortr.N;
            RegisterOverlap (shortr.Poly, (N + s, N + e));
         }
      }

      // Local
      void RegisterOverlap (Poly p, (double, double) r) {
         if (PolyOverlapMap.TryGetValue (p, out var ranges)) ranges.Add (r);
         else PolyOverlapMap.Add (p, [r]);
      }
   }

   /// <summary>Transforms CW (start & end) angles to CCW (start & end) angles</summary>
   public static (double start, double end) ToCCW ((double start, double end) cw) {
      var span = cw.start - cw.end;
      Lib.Check (span > 0, "Coding error");
      var newStart = Lib.NormalizeAngle (cw.end);
      return (newStart, newStart + span);
   }

   readonly Dictionary<Poly, List<(double SLie, double ELie)>> PolyOverlapMap = []; // Overlapping Polys & overlap coverages
}
#endregion

#region OverlapInfo --------------------------------------------------------------------------------
/// <summary>Consolidates all the overlapping sections info</summary>
/// This structure should help in resolving heavily overlapping drawings!
/// Something seen with Vulcan Steel "dirty" drawing
class OverlapInfo {
   // Interface ----------------------------------------------------------------
   /// <summary>Process line overlap info</summary>
   public void AddOvr (Point2 a, Point2 b) => Lines.Add (new DirectedLine (a, b));

   /// <summary>Process arc overlap info</summary>
   public void AddOvr (Point2 center, double radius, double sAngle, double eAngle, bool isCCW)
      => Arcs.Add (new ArcCCW (center, radius, sAngle, eAngle, isCCW));

   /// <summary>Generates overlap sections as Polys</summary>
   public IEnumerable<Poly> GenerateOverlapPolys () {
      // Simply deploy the dwg-stitcher to stitch up whatever gets stitched.
      Dwg2 dwg = new ();
      MergedLines ().ForEach (dwg.Add);
      MergedArcs ().ForEach (dwg.Add);
      new DwgStitcher (dwg).Process ();
      return dwg.Ents.OfType<E2Poly> ().Select (a => a.Poly);
   }

   // Implementation -----------------------------------------------------------
   IEnumerable<Poly> MergedLines () {
      List<DirectedLine> collinears = [];
      List<(double, double)> ranges = [];
      while (Lines.Count > 0) {
         collinears.Clear ();
         // Take one line, and gather all collinear with that line.
         var l = Lines.RemoveLast ();
         for (int i = Lines.Count - 1; i >= 0; i--) {
            var l2 = Lines[i];
            if (!l.Angle.EQ (l2.Angle) || l2.A.Side (l.A, l.B) != 0) continue;
            collinears.Add (l2);
            Lines.RemoveAt (i);
         }
         if (collinears.Count == 0) {
            yield return Poly.Line (l.A, l.B);
            continue;
         }
         collinears.Add (l);

         // Flatten them on infinite line, and compute lie ranges
         var (pt, pt2) = (l.A, l.A.Polar (1000, l.Angle));
         ranges.Clear ();
         foreach (var dl in collinears)
            ranges.Add ((dl.A.GetLieOn (pt, pt2), dl.B.GetLieOn (pt, pt2)));
         DwgOverlap.MergeRanges (ranges);
         var seg = Poly.Line (pt, pt2)[0];
         foreach (var r in ranges)
            yield return Poly.Line (seg.GetPointAt (r.Item1), seg.GetPointAt (r.Item2));
      }
   }

   IEnumerable<Poly> MergedArcs () {
      List<ArcCCW> concentrics = [];
      List<(double SAng, double EAng)> ranges = [];
      while (Arcs.Count > 0) {
         concentrics.Clear ();
         // Take one line, and gather all collinear with that line.
         var l = Arcs.RemoveLast ();
         for (int i = Arcs.Count - 1; i >= 0; i--) {
            var l2 = Arcs[i];
            if (!l.Radius.EQ (l2.Radius) || !l.Center.EQ (l2.Center)) continue;
            concentrics.Add (l2);
            Arcs.RemoveAt (i);
         }
         if (concentrics.Count == 0) {
            yield return Poly.Arc (l.Center, l.Radius, l.SAng, l.EAng, ccw: true);
            continue;
         }
         concentrics.Add (l);

         // Flatten them on infinite spiral/circle, and compute lie ranges
         var seg = Poly.Circle (l.Center, l.Radius)[0];
         ranges.Clear ();
         foreach (var arc in concentrics)
            ranges.Add ((arc.SAng, arc.EAng));
         MergeCircularSpans (ranges);
         foreach (var r in ranges) { // See if there is any "full" circle! [Which simply subsumes all other spans]
            if (r.EAng.EQ (r.SAng)) continue;
            if ((r.EAng - r.SAng).EQ (Lib.TwoPI))
               yield return Poly.Circle (l.Center, l.Radius);
            else
               yield return Poly.Arc (l.Center, l.Radius, r.SAng, r.EAng, ccw: true);
         }
      }

      static void MergeCircularSpans (List<(double SAng, double EAng)> sranges) {
         for (int i = 0; i < sranges.Count; i++) {
            var r = sranges[i];
            for (int j = i + 1; j < sranges.Count; j++) {
               // We are dealing with angles, which rollover 360 degrees
               // Need to check span overlaps (by shifting them +/-360)
               var r2 = sranges[j];
               if (r.EAng < r2.SAng || r.SAng > r2.EAng) {
                  if ((r.EAng - r2.SAng) > Lib.TwoPI) {
                     (double SAng, double EAng) r3 = (r2.SAng + Lib.TwoPI, r2.EAng + Lib.TwoPI);
                     if (r.EAng < r3.SAng || r.SAng > r3.EAng)
                        continue;
                     r2 = r3;
                  } else if ((r2.EAng - r.SAng) > Lib.TwoPI) {
                     (double SAng, double EAng) r3 = (r2.SAng - Lib.TwoPI, r2.EAng - Lib.TwoPI);
                     if (r.EAng < r3.SAng || r.SAng > r3.EAng)
                        continue;
                     r2 = r3;
                  } else continue;
               }
               sranges[i] = r = (Math.Min (r.SAng, r2.SAng), Math.Max (r.EAng, r2.EAng));
               sranges.RemoveAt (j--);
            }
         }
      }
   }

   readonly List<DirectedLine> Lines = [];
   readonly List<ArcCCW> Arcs = [];

   // Nested
   readonly struct DirectedLine {
      public DirectedLine (Point2 a, Point2 b) {
         double angle = a.AngleTo (b);
         bool isFlipped = false;
         if (angle.EQ (Lib.PI)) { angle = 0; isFlipped = true; }
         if (angle < 0) { angle += Lib.PI; isFlipped = true; }
         if (isFlipped) (a, b) = (b, a);
         (A, B, Angle) = (a, b, angle);
      }

      public readonly Point2 A, B;
      public readonly double Angle; // Normalized180 angle
   }

   readonly struct ArcCCW { // All Arc are captured as CCW Arcs
      public ArcCCW (Point2 center, double radius, double sAngle, double eAngle, bool isCCW) {
         if (!isCCW)
            (sAngle, eAngle) = DwgOverlap.ToCCW ((sAngle, eAngle));
         (Center, Radius, SAng, EAng) = (center, radius, sAngle, eAngle);
      }

      public readonly Point2 Center;
      public readonly double Radius;
      public readonly double SAng;
      public readonly double EAng;
   }
}
#endregion
