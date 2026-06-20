// ╔═╦╗
// ║╬╠╬╦╗ PolyDiff.cs
// ║╔╣╠║╣ Coverage/difference b/w two Poly/s
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Poly coverage computation interface</summary>
/// Primarily used for edited-Poly change feedback computation
static class PolyCoverage {
   public static FreePoly ComputeFeedback (Poly oldPoly, Poly[] newPolys) {
      FreePoly oldSlicer = new (oldPoly);
      if (newPolys.Length == 0) return oldSlicer;
      FreePoly[] newSlicers = [.. newPolys.Select (p => new FreePoly (p))];
      // Compute all the coverages
      foreach (var newSlicer in newSlicers) ComputeCoverages (oldSlicer, newSlicer);
      // Now comes the tricky part. Note: All Polys may either be fully or partially covered.
      return newSlicers.FirstOrDefault (slicer => !slicer.IsFullyCovered) ?? oldSlicer;
   }

   static void ComputeCoverages (FreePoly cvgA, FreePoly cvgB) {
      if (!cvgA.GetFirst (out FreeSegSlice sA)
         || !cvgB.GetFirst (out FreeSegSlice sB)) return;
      while (true) {
         if (sA.ComputeOverlap (sB, out Bound1 ovrA, out Bound1 ovrB)) {
            cvgA.AddOverlap (sA.Poly, sA.ToPolyLies (ovrA));
            cvgB.AddOverlap (sB.Poly, sB.ToPolyLies (ovrB));
            if (!cvgA.GetNext (ref sA))
               return;
            // Reset the other slicer
            if (!cvgB.GetFirst (out sB)) return;
            continue;
         }
         if (!cvgB.GetNext (ref sB)) {
            if (!cvgA.GetNext (ref sA))
               return;
            if (!cvgB.GetFirst (out sB))
               return;
         }
      }
   }
}

/// <summary>Poly coverage tracker</summary>
/// All Polys are treated as OPEN for this purpose
/// Usage (design):
///   .GetFirst - gets the first "uncovered" seg slice
///      Note: A given seg may have multiple "uncovered" seg slices
///   .GetNext - gets next subsequent "uncovered" seg slices
class FreePoly {
   // Constructor --------------------------------------------------------------
   public FreePoly (Poly poly) => Poly = poly;

   public readonly Poly Poly;

   // Interface ----------------------------------------------------------------
   /// <summary>Gets the first "uncovered" seg slice</summary>
   public bool GetFirst (out FreeSegSlice ss) => NextSegSlice (start: 0, out ss);

   /// <summary>Gets next subsequent "uncovered" seg slice</summary>
   /// <param name="ss">Last "uncovered seg slice, for reference"</param>
   public bool GetNext (ref FreeSegSlice ss) {
      // See if the reference slice is still uncovered, or is now fully covered.
      // If partially covered, then next slice should be the still uncovered section!
      var bound = ss.PolyExtent;
      if (!NextSegSlice (bound.Min, out FreeSegSlice ss2)) return false;
      // If ss == ss2, the reference slice range is still uncovered, then we can move to next slice
      var bound2 = ss2.PolyExtent;
      if (!bound.Min.EQ (bound2.Min) || !bound.Max.EQ (bound2.Max)) {
         ss = ss2;
         return true;
      }
      return NextSegSlice (bound.Max, out ss);
   }

   /// <summary>Indicates the Poly is fully covered</summary>
   public bool IsFullyCovered => mOverlaps.Count == 1
      && mOverlaps[0].Min.IsZero () && mOverlaps[0].Max.EQ (Poly.Count);

   public void AddOverlap (Poly p, Bound1 newOvr) {
      Debug.Assert (p == Poly);
      if (mOverlaps.Count == 0) {
         // For a Circle, the Max range may be > 1
         // If so, we need to roll-over and create two overlap entries!
         if (Poly.IsCircle && newOvr.Max > 1) {
            mOverlaps.Add (new Bound1 (0, newOvr.Max - 1));
            mOverlaps.Add (new Bound1 (newOvr.Min, 1));
            return;
         }
         mOverlaps.Add (newOvr);
         return;
      }
      var newOvrInf = newOvr.InflatedL (Lib.Epsilon);
      for (int i = 0; i < mOverlaps.Count; i++) {
         var ovr = mOverlaps[i];
         if (!(ovr * newOvrInf).IsEmpty) {
            mOverlaps[i] = ovr + newOvr; // Merged
            if (i + 1 < mOverlaps.Count) {
               // If it overlaps with the next overlap, merge and compact the list
               if (!(mOverlaps[i].InflatedL (Lib.Epsilon) * mOverlaps[i + 1]).IsEmpty) {
                  mOverlaps[i] += mOverlaps[i + 1];
                  mOverlaps.RemoveAt (i + 1);
               }
            }
            return;
         }
      }
      // Could not merge, so find appropriate index to locate it
      var idx = mOverlaps.FindIndex (o => o.Min > newOvr.Max); // Can Add?
      if (idx != -1) mOverlaps.Insert (idx, newOvr);
      else mOverlaps.Add (newOvr);

      // Sanity check
      var lastOvr = mOverlaps[0];
      for (int i = 1; i < mOverlaps.Count; i++) {
         var ovr = mOverlaps[i];
         Debug.Assert (lastOvr.Max < ovr.Min);
         lastOvr = ovr;
      }
   }

   // Internal -----------------------------------------------------------------
   bool NextSegSlice (double start, out FreeSegSlice ss) {
      var freeBd = GetNonOverlapSlot (start);
      if (freeBd == null) {
         ss = new FreeSegSlice (Poly[0], 0, 0); // Empty slice!
         return false;
      }
      // Got free slot
      int N = (int)start;
      ss = new FreeSegSlice (Poly[N], (freeBd.Value.Min - N).Clamp (), (freeBd.Value.Max - N).Clamp ());
      return true;
   }

   Bound1? GetNonOverlapSlot (double start) {
      foreach (var freeBd in GetFreeSlots ()) {
         if (start > freeBd.Max - Lib.Epsilon) continue;
         return (start, freeBd.Max);
      }
      return null;
   }

   public IEnumerable<Bound1> GetFreeSlots () {
      if (mOverlaps.Count == 0) { // No coverage claims!
         yield return (0, Poly.Count);
         yield break;
      }
      Bound1 lastOvr = mOverlaps[0];
      if (!lastOvr.Min.IsZero ())
         yield return (0, lastOvr.Min);
      foreach (var ovr in mOverlaps.Skip (1)) {
         yield return (lastOvr.Max, ovr.Min);
         lastOvr = ovr;
      }
      if (!lastOvr.Max.EQ (Poly.Count))
         yield return (lastOvr.Max, Poly.Count);
   }

   public IEnumerable<Poly> GetFreeSlicePolys () => GetFreeSlots ().Select (r => Poly.Sliced (r.Min, r.Max));

   // Poly coverage (sorted) ranges
   readonly List<Bound1> mOverlaps = [];
}

/// <summary>As yet "uncovered" seg section (available for further coverage computations)</summary>
readonly ref struct FreeSegSlice {
   // Constructor --------------------------------------------------------------
   public FreeSegSlice () => throw new NotSupportedException ();
   /// <summary>Construct a seg slice</summary>
   /// <param name="seg">Actual seg being sliced</param>
   /// <param name="startLie">Slice start lie, along the underlying seg</param>
   /// <param name="endLie">Slice end lie, along the underlying seg</param>
   public FreeSegSlice (Seg seg, double startLie = 0, double endLie = 1)
      => (mSeg, Extent) = (seg, (startLie.Clamp (), endLie.Clamp ()));

   readonly Seg mSeg;
   readonly Bound1 Extent; // Seg-level lie range

   readonly static double Fuzz = Lib.Epsilon;

   // Interface ----------------------------------------------------------------
   public Poly Poly => mSeg.Poly;
   /// <summary>Poly-level lie range</summary>
   public Bound1 PolyExtent => ToPolyLies (Extent);
   /// <summary>Translate given seg-level lie range (along this seg) to Poly-level lie range</summary>
   public Bound1 ToPolyLies (Bound1 segLies) => (mSeg.N + segLies.Min, mSeg.N + segLies.Max);

   /// <summary>Computes and returns the overlap b/w the two given "uncovered" seg slices</summary>
   public bool ComputeOverlap (FreeSegSlice o, out Bound1 ovrA, out Bound1 ovrB) {
      if (!IsAlignedWith (o, out bool opposing) || opposing) { ovrA = ovrB = new Bound1 (); return false; }
      if (mSeg.IsCircle && o.mSeg.IsCircle) { ovrA = ovrB = new Bound1 (0, 1); return true; }
      if (mSeg.IsCircle || o.mSeg.IsCircle) { // Circle-Arc overlap
         var arc = mSeg.IsCircle ? o.mSeg : mSeg;
         var (sArc, eArc) = arc.GetStartAndEndAngles ();
         if (arc.IsCCW) {
            if (sArc < 0)
               (sArc, eArc) = (sArc + Lib.TwoPI, eArc + Lib.TwoPI);
         } else {
            if (sArc >= 0)
               (sArc, eArc) = (sArc - Lib.TwoPI, eArc - Lib.TwoPI);
         }
         // Circle overlap range
         var extent = (sArc / Lib.TwoPI, eArc / Lib.TwoPI);
         if (mSeg.IsCircle) { ovrA = extent; ovrB = (0, 1); return true; }
         ovrA = (0, 1); ovrB = extent;
         return true;
      }

      var (segA, segB) = (mSeg, o.mSeg);
      if (mSeg.IsLine) {
         var (sLie, eLie) = (segA.GetLie (segB.A), segA.GetLie (segB.B)); // Lie of segB, in terms of segA
         if (sLie > (1 - Lib.Epsilon) || eLie < Lib.Epsilon) { ovrA = ovrB = new Bound1 (); return false; }
         // Got overlap!
         var (sLie2, eLie2) = (segB.GetLie (segA.A), segB.GetLie (segA.B)); // Lie of segA, in terms of segB
         (ovrA, ovrB) = CreatePairedSpan (this, (sLie, eLie), o, (sLie2, eLie2));
         return !ovrA.IsEmpty && !ovrB.IsEmpty;
      }

      var (sa, ea) = segA.GetStartAndEndAngles ();
      var (sb, eb) = segB.GetStartAndEndAngles ();

      // Arc overlaps are a bit more involved
      if (!segA.IsCCW) { // Reduce to CCW case
         (sa, ea) = (ea, sa);
         (sb, eb) = (eb, sb);
      }
      // Works for both CCW and CW case. [Note: Both spans arc either CCW or CW]
      bool overlap = sa < eb && ea > sb;
      if (!overlap) {
         // Note: Cross-extent of both the spans must not exceed 360. [Else we need to shift some span upwards/downwards]
         if (Math.Abs (eb - sa) > Lib.TwoPI) { // End of segB too far from Start of segA (lift segA)
            (sa, ea) = (sa + Lib.TwoPI, ea + Lib.TwoPI);
            overlap = sa < eb && ea > sb;
         } else if (Math.Abs (ea - sb) > Lib.TwoPI) { // End of segA too far from Start of segB (lift segB)
            (sb, eb) = (sb + Lib.TwoPI, eb + Lib.TwoPI);
            overlap = sa < eb && ea > sb;
         }
         if (!overlap) { ovrA = ovrB = new Bound1 (); return false; }
      }
      var (spanA, spanB) = (ea - sa, eb - sb);
      var (os, oe) = (Math.Max (sa, sb), Math.Min (ea, eb));
      var extentA = ((os - sa) / spanA, (oe - sa) / spanA);
      var extentB = ((os - sb) / spanB, (oe - sb) / spanB);
      (ovrA, ovrB) = CreatePairedSpan (this, extentA, o, extentB);
      return !ovrA.IsEmpty && !ovrB.IsEmpty;
   }

   // Internal -----------------------------------------------------------------
   static (Bound1 OvrA, Bound1 OvrB) CreatePairedSpan (FreeSegSlice sliceA, Bound1 extentA, FreeSegSlice sliceB, Bound1 extentB) {
      // Delimit given extents to lie within corresponding slice's extents
      Bound1 bA = sliceA.Extent * extentA, bB = sliceB.Extent * extentB; // Intersection of bounds, to determine overlapping range
      if (bA.IsEmpty || bA.Length < Lib.Epsilon || bB.IsEmpty || bB.Length < Lib.Epsilon) return (new Bound1 (), new Bound1 ());
      return (bA, bB);
   }

   // Alignment check: Common line def | common circle def
   bool IsAlignedWith (FreeSegSlice o, out bool opposing) { // Handle opposing alignment
      opposing = false;
      if (mSeg.IsLine ^ o.mSeg.IsLine) return false;
      var (sa, sb) = (mSeg, o.mSeg);
      if (mSeg.IsLine) {
         if (sa.A.Side (sb.A, sb.B) != 0 || sa.B.Side (sb.A, sb.B) != 0) return false;
         opposing = Math.Abs (sb.Slope - sa.Slope) > Lib.HalfPI;
         return true;
      }
      if (!sa.Center.EQ (sb.Center, Fuzz) || !sa.Radius.EQ (sb.Radius, Fuzz)) return false;
      opposing = sa.IsCCW != sb.IsCCW;
      return true;
   }
}
