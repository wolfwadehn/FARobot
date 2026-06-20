// ╔═╦╗
// ║╬╠╬╦╗ DwgCleanup.cs
// ║╔╣╠║╣ Various 'commands' for Dwg cleanup
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;

#region CmdMarkSmallSegs ---------------------------------------------------------------------------
/// <summary>Highlights all the small segments within specified threshold length value</summary>
[DwgCmd (ECmd.MarkSmallSegs), NoMouseInput]
class CmdMarkSmallSegs : DwgWidget {
   [Textbox (0)] static double MaxLength = 0.5;

   public override void OnActivated () {
      mDwg.Ents.ForEach (e => e.IsSelected = false);
      Execute ();
   }

   public override void Completed () {
      base.Completed ();
      Execute ();
   }

   void Execute () {
      var pts = mDwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")
                    .SelectMany (p => p.Poly.Segs).Where (s => s.Length < MaxLength + Lib.Epsilon)
                    .Select (s => s.Midpoint).ToList ();
      mDwg.ResetCleanupmarkers (Prompt.Name, pts);
   }
}
#endregion

#region CmdMarkNonTangentCorners -------------------------------------------------------------------
[DwgCmd (ECmd.MarkNonTangent), NoMouseInput]
class CmdMarkNonTangentCorners : DwgWidget {
   [Textbox (0), Angle] static double MinAngle = 0.1.D2R ();
   [Textbox (1), Angle] static double MaxAngle = 5.0.D2R ();

   public override void OnActivated () {
      mDwg.Ents.ForEach (e => e.IsSelected = false);
      Execute ();
   }

   public override void Completed () {
      base.Completed ();
      Execute ();
   }

   void Execute () {
      // Grab all the polys (open/closed), evaluate all the corners and check
      // For open polys, 1st and last nodes are open, and must be ignored
      List<Point2> pts = [];
      foreach (var e2p in mDwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")) {
         var p = e2p.Poly;
         for (int i = p.Count - 1, first = p.IsOpen ? 1 : 0; i >= first; i--) {
            double angle = Math.Abs (p.GetTurnAngle (i));
            if (angle > MinAngle - Lib.Epsilon && angle < MaxAngle + Lib.Epsilon) pts.Add (p.Pts[i]);
         }
      }
      mDwg.ResetCleanupmarkers (Prompt.Name, pts);
   }
}
#endregion

#region CmdMarkLargeRadius -------------------------------------------------------------------------
[DwgCmd (ECmd.MarkLargeRadius), NoMouseInput]
class MarkLargeRadius : DwgWidget {
   [Textbox (0)] static double MinRadius = 2000;

   public override void OnActivated () {
      mDwg.Ents.ForEach (e => e.IsSelected = false);
      Execute ();
   }

   public override void Completed () {
      base.Completed ();
      Execute ();
   }

   void Execute () {
      var pts = mDwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")
                    .SelectMany (a => a.Poly.Segs)
                    .Where (a => a.IsArc && !a.IsCircle && a.Radius > MinRadius + Lib.Epsilon)
                    .Select (a => a.Midpoint).ToList ();
      mDwg.ResetCleanupmarkers (Prompt.Name, pts);
   }
}
#endregion

#region RemoveSmallSegs ----------------------------------------------------------------------------
[DwgCmd (ECmd.RemoveSmallSegs)]
class RemoveSmallSegs : DwgWidget {
   [Textbox (0)] static double MaxLength = 0.5;
   [Click (0)] Point2 CornerA = Point2.Nil;
   [Click (1)] Point2 CornerB = Point2.Nil;

   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phase == 0) return;
      Lux.Poly (Poly.Rectangle (new Bound2 ([CornerA, CornerB])));
   }

   public override void Completed () {
      base.Completed ();
      DoRemoveSmallSegs (mDwg, new Bound2 ([CornerA, CornerB]), MaxLength, DwgHub.CtrlPressed);
   }

   void DoRemoveSmallSegs (Dwg2 dwg, Bound2 b, double threshold, bool iLeaveGaps) {
      threshold += Lib.Epsilon;
      E2Poly[] e2ps = [.. dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")];

      List<Ent2> add = [], rmv = [];
      foreach (var e2p in e2ps) {
         List<Poly> frags = [.. SmallSegsPurged (e2p.Poly, b, threshold)];
         if (frags.Count > 0) {
            rmv.Add (e2p);
            add.AddRange (frags.Select (e2p.With));
         }
      }
      Add (Prompt.Name, add, rmv);
   }

   // Gets remnant fragments after removing small segments
   static IEnumerable<Poly> SmallSegsPurged (Poly p, Bound2 b, double threshold) {
      PolyBuilder pb = new ();
      (int sIdx, int cSeg) = (0, p.Count);
      foreach (var last in p.Segs.Where (a => a.Length < threshold && b.Contains (a.A) && b.Contains (a.B))) {
         if (sIdx == last.N) {
            if (++sIdx == cSeg) yield break;
            continue;
         }
         for (; sIdx <= last.N; sIdx++) {
            Seg s = p[sIdx];
            if (s.IsArc) pb.Arc (s.A, s.Center, s.Flags);
            else pb.Line (s.A);
         }
         yield return pb.Build ();
         sIdx = last.N + 1;
      }
      // Final (trailing) fragment
      if (sIdx > 0 && sIdx != cSeg) {
         for (; sIdx < cSeg; sIdx++) {
            Seg s = p[sIdx];
            if (s.IsArc) pb.Arc (s.A, s.Center, s.Flags);
            else pb.Line (s.A);
         }
         pb.Line (p.B);
         yield return pb.Build ();
      }
   }
}
#endregion

#region MarkOverlapSegs ----------------------------------------------------------------------------
[DwgCmd (ECmd.HighlightOverlaps), NoSimMouse, AlwaysShowFeedback]
class MarkOverlapSegs : DwgWidget {
#pragma warning disable 0169
   [Click (0), NoMouseMove] Point2 _Unused;
#pragma warning restore 0169

   public override void OnActivated () {
      base.OnActivated ();
      RecomputeOverlaps ();
   }

   public override void Completed () {
      base.Completed ();
      // Or, simply attempt to cleanup all the affected polys! [Let user press Ctrl+Click anywhere...]
      if (!DwgHub.CtrlPressed) return;
      DoCleanup ();
      RecomputeOverlaps ();
   }

   public override void DrawFeedback () {
      base.DrawFeedback ();
      Lux.Polys ([.. SilhouetteMarker (mOverlapPolys, DwgHub.PickAperture)]);
   }

   void RecomputeOverlaps () {
      mOverlapPolys.Clear ();
      mDwgOverlap = new DwgOverlap (mDwg);
      if (!mDwgOverlap.GotOverlap) return;
      mOverlapPolys.AddRange (mDwgOverlap.GetOverlapSectionRepPoly ());
   }

   void DoCleanup () {
      if (!(mDwgOverlap ??= new DwgOverlap (mDwg)).GotOverlap) return;
      // Get the affected polys.
      // Find the owner entity for each poly.
      // Cleanup each poly, and gather clean polys and form new poly entity
      List<E2Poly> rmv = [];
      List<Poly> addPolys = [];
      foreach (var p in mDwgOverlap.GetOverlappingPolys ()) {
         var e2p = mDwg.Ents.OfType<E2Poly> ().First (e2p => e2p.Poly == p);
         rmv.Add (e2p);
         foreach (var p2 in mDwgOverlap.ComputeOverlapFreePolys (p)) {
            addPolys.Add (p2);
         }
      }
      // Add cleaned up poly section polys.
      addPolys.AddRange (mDwgOverlap.GetOverlapSectionRepPoly ());

      // Stitch: The likely fragmented but stitchable newly added polys!
      addPolys = StitchReduce (addPolys);

      var tmplEnt = rmv[0];
      Add ("Overlap cleanup", addPolys.Select (tmplEnt.With), rmv);
   }

   static List<Poly> StitchReduce (List<Poly> polys) {
      if (polys.Count == 0) return polys;
      List<Poly> stitched = [];
      // Single pass: Poly may get stitched about any/both ends!
      foreach (var poly in polys) {
         bool gotStitched = false;
         // See if this poly can stitch with existing polys
         for (int i = stitched.Count - 1; i >= 0; i--) {
            var poly2 = stitched [i];
            if (poly2.IsClosed) continue;
            if (poly.TryAppend (poly2, out var poly3)) {
               gotStitched = true;
               if (poly3.A.EQ (poly3.B))
                  poly3 = poly3.Close ();
               stitched[i] = poly3;
               if (poly3.IsClosed) continue;
               // Poly3 may still hook up with yet another existing poly!
               for (int j = i - 1; j >= 0; j--) {
                  var poly4 = stitched [j];
                  if (poly4.IsClosed) continue;
                  if (poly3.TryAppend (poly4, out var poly5)) {
                     if (poly5.A.EQ (poly5.B))
                        poly5 = poly5.Close ();
                     stitched[j] = poly5;
                     stitched.RemoveAt (i);
                  }
               }
            }
         }
         if (!gotStitched) stitched.Add (poly);
      }
      return stitched;
   }

   // Computes silhouette poly for each given poly, with the specified thickness
   static IEnumerable<Poly> SilhouetteMarker (IEnumerable<Poly> polys, double thick) {
      foreach (var poly in polys) {
         if (poly.IsCircle) {
            var seg = poly[0];
            yield return Poly.Circle (seg.Center, seg.Radius - thick);
            yield return Poly.Circle (seg.Center, seg.Radius + thick);
            continue;
         }
         foreach (var seg in poly.Segs) {
            if (seg.IsArc) {
               var (slopeA, slopeB) = (seg.GetSlopeAt (0), seg.GetSlopeAt (1));
               var (radialA, radialB) = (slopeA + Lib.HalfPI, slopeB + Lib.HalfPI);
               yield return Poly.Arc (seg.A.Polar (thick, radialA), slopeA, seg.B.Polar (thick, radialB));
               yield return Poly.Arc (seg.A.Polar (-thick, radialA), slopeA, seg.B.Polar (-thick, radialB));
            } else {
               var perp = seg.Slope + Lib.HalfPI;
               yield return Poly.Line (seg.A.Polar (thick, perp), seg.B.Polar (thick, perp));
               yield return Poly.Line (seg.A.Polar (-thick, perp), seg.B.Polar (-thick, perp));
            }
            yield return Poly.Circle (seg.A, thick);
         }
         yield return Poly.Circle (poly.B, thick);
      }
   }

   DwgOverlap? mDwgOverlap;
   readonly List<Poly> mOverlapPolys = [];
}
#endregion
