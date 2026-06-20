// ╔═╦╗
// ║╬╠╬╦╗ PolyEditor2.cs
// ║╔╣╠║╣ Widgets used to _edit_ polys (based on selected poly/seg)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;

#region class PolyEditor2 --------------------------------------------------------------------------
/// <summary>Base class for all poly editors that require 1+ poly/seg</summary>
/// Variant of PolyEditor, which does not use bulky TPolyPick structure.
abstract class PolyEditor2 : DwgWidget {
   // Properties ---------------------------------------------------------------
   /// <summary>The point we are picking</summary>
   [Click (0)]
   protected Point2 Pick {
      get => mPick;
      set => mOld = mDwg.PickPoly (mPick = value, DwgHub.PickAperture, out nSeg, out mLie);
   }
   Point2 mPick;
   int nSeg;
   double mLie;
   E2Poly? mOld;

   // Overrides  ---------------------------------------------------------------
   // Called when the command is completed, it calls Make to create the new 
   // (modified) Poly. It then replaces the previous entity in the drawing with 
   // the new one. 
   public override void Completed () {
      if (mOld == null) return;
      if (!Make (mOld.Poly, nSeg, mLie, out ReadOnlySpan<Poly> poly)) return;
      List<Ent2> add = [], rmv = [mOld];
      mDwg.Ents.Remove (mOld);
      foreach (var p in poly) add.Add (new E2Poly (mOld.Layer, p));
      Add (Prompt.Name, add, rmv);
   }

   // To draw feedback, we simply construct the new (modified) Poly and render it.
   // It is possible that the new poly may not be constructible (Make might return
   // null), in which case we display no feedback
   public override void DrawFeedback () {
      // Display only the difference b/w target poly, and result poly(s)
      base.DrawFeedback ();
      if (mOld == null || !Make (mOld.Poly, nSeg, mLie, out ReadOnlySpan<Poly> newPolys)) return;
      var cvg = PolyCoverage.ComputeFeedback (mOld.Poly, [.. newPolys]);
      Lux.Polys ([.. cvg.GetFreeSlicePolys ()]);
   }

   // Abstracts (to be overridden) ---------------------------------------------
   protected abstract bool Make (Poly poly, int nSeg, double lie, out ReadOnlySpan<Poly> res);
}
#endregion

#region SegExtender --------------------------------------------------------------------------------
[DwgCmd (ECmd.Extend), NoKeyboardMode]
class SegExtender : PolyEditor2 {
   [Textbox (0)] static double Dist = 0;

   protected override bool Make (Poly poly, int nSeg, double lie, out ReadOnlySpan<Poly> res) { 
      res = new ReadOnlySpan<Poly> ([.. poly.ExtendedSeg (nSeg, lie, Dist, mDwg.Polys)]);
      return res.Length != 0;
   }
}
#endregion

#region SegTrimmer ---------------------------------------------------------------------------------
[DwgCmd (ECmd.Trim), NoKeyboardMode]
class SegTrimmer : PolyEditor2 {
   protected override bool Make (Poly poly, int nSeg, double lie, out ReadOnlySpan<Poly> res) { 
      res = new ReadOnlySpan<Poly> ([.. poly.TrimmedSeg (nSeg, lie, mDwg.Polys)]);
      return true; // Always. For single seg polys, there is no result poly. But that is the result!
   }
}
#endregion
