// ╔═╦╗
// ║╬╠╬╦╗ PolyEditor.cs
// ║╔╣╠║╣ Widgets used to _edit_ polylines (like adding chamfers, fillets, notches etc)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;
using static System.Math;

#region class PolyEditor ---------------------------------------------------------------------------
/// <summary>Base class for all polyon editors that require one click to complete</summary>
/// Many poly-editors (like Chamfer, fillet, EdgeRecess etc) work by typing in
/// some parameters and then clicking on a Poly. This is the base class for
/// such widgets
abstract class PolyEditor : DwgWidget {
   // Properties ---------------------------------------------------------------
   /// <summary>The point we are picking</summary>
   [Click (0), Unsnapped]
   protected Point2 Pick {
      get => mPick;
      set {
         if (mDwg.PickPoly (mPick = value, DwgHub.PickAperture, out Picked))
            mOld = Picked.Ent;
      }
   }
   Point2 mPick;
   E2Poly? mOld;

   /// <summary>Data about the poly closest to that Pick point</summary>
   /// This includes additional data like the closest segment, closest node etc,
   /// and also whether the click was to the 'left' of the closest segment.
   /// Note that TPoly could be null if we are more than the 'pickbox' number
   /// of pixels away from any Poly
   TPolyPick Picked = new ();

   // Overrides  ---------------------------------------------------------------
   // Called when the command is completed, it calls Make to create the new
   // (modified) Poly. It then replaces the previous entity in the drawing with
   // the new one.
   public override void Completed () {
      if (Picked.Ent != null && Make (ref Picked) is { } poly && mOld != null) 
         Replace (Prompt.Name, mOld, new E2Poly (mOld.Layer, poly));
   }

   // To draw feedback, we simply construct the new (modified) Poly and render it.
   // It is possible that the new poly may not be constructible (Make might return
   // null), in which case we display no feedback
   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Picked.Ent != null && Make (ref Picked) is { } p) Lux.Poly (p);
   }

   // Abstracts (to be overridden) ---------------------------------------------
   // Each derived class must override this to create a new modified poly based
   // on the input Poly. To make things easy, this base class already computes
   // this closest poly, the closest seg and the closest node on that. If the
   // modified Poly cannot be computed, Make will return null.
   protected abstract Poly? Make (ref TPolyPick tpp);
}
#endregion

#region class PolyCornerEditor ---------------------------------------------------------------------
/// <summary>Base class for editors that are modifying a 'corner' of a Poly</summary>
/// This is derived from PolyEditor, and adds special functionality to
/// process ALL corners of a poly with the given input.
abstract class PolyCornerEditor : PolyEditor {
   // Overrides ----------------------------------------------------------------
   // This overrides PolyEditor.Make, and implements special code to process
   // all corners if CTRL is pressed. The actual processing of one single corner
   // is delegated to the MakeImp abstract function (which concrete classes like
   // ChamferMaker and FilletMaker will implement).
   // The idea is that such specialized makers will just have the code to process
   // ONE corner of a Poly, and this base class will then leverage that to process
   // all the corners
   protected override Poly? Make (ref TPolyPick tpp) {
      if (tpp.Poly.Count < 2) return null; // There must at least be one corner!
      if (DwgHub.CtrlPressed) {
         Poly poly = tpp.Poly;
         // We basically call MakeImp to do the operation like Chamfer / Fillet
         // etc at each corner. So a lot of intermediate versions of the Poly
         // will be created (1 corner filleted, 2 corners filleted etc). At each
         // stage, we have to handle the possibility that makeImp may not be able
         // to chamfer that particular corner - in that case, we skip that corner and
         // keep going
         var (pts, lie, isRect) = (poly.Pts, poly[tpp.Seg].GetLie (Pick), poly.IsRectangle ());
         foreach (var pt in pts.Reverse ()) {
            // When we started, we snapshotted all the 'corners' of the original
            // Poly. Since the poly is going to be continuously modified, new corners
            // will get added, but we should not end up filleting those new corners,
            // and also the indices of those original corners in the Poly will keep
            // shifting as the poly gets edited. So we call GetClosestNode each time
            int node = poly.GetClosestNode (pt), seg = node;
            if (poly.IsOpen && (node == 0 || node == poly.Count)) continue;
            Seg stmp = poly[node];
            Poly.ECornerOpFlags flags = tpp.Flags;
            if (isRect) {
               // This code below is to create 'symmetrical' chamfers, edge recess etc.
               // For example, suppose we make a chamfer with different Dist1 and
               // Dist2 parameters. If Dist1 is applied to a 'horizontal' segment
               // at one corner, and Dist2 to the 'vertical' segment, we want to keep
               // that same behavior as we loop through the corners. This is not perfect,
               // and will fail with inclined segments, rotated shapes etc. However,
               // for such cases, the user should probably not be doing CTRL=All Corners
               // anyway.
               bool horz = SegHorz (stmp);
               if (horz ^ ((tpp.Flags & Poly.ECornerOpFlags.Horz) != 0)) seg--;
               if (node == seg) flags |= Poly.ECornerOpFlags.NearLeadOut;
               else flags &= ~Poly.ECornerOpFlags.NearLeadOut;
            } else if (lie > 0.5) seg--;

            var wrapper = new E2Poly (tpp.Ent.Layer, poly);
            var tpp2 = new TPolyPick (wrapper, seg, node, flags);
            poly = MakeImp (tpp2) ?? poly;
         }
         return poly;
      } else
         return MakeImp (tpp);

      // Helper ..................................
      static bool SegHorz (Seg seg) {
         var vec = seg.B - seg.A;
         return Abs (vec.X) > Abs (vec.Y);
      }
   }

   protected abstract Poly? MakeImp (TPolyPick tpp);
}
#endregion

#region class ChamferMaker -------------------------------------------------------------------------
/// <summary>Implements the Chamfer command</summary>
[DwgCmd (ECmd.Chamfer), NoKeyboardMode]
class ChamferMaker : PolyCornerEditor {
   [Textbox (0)] static double Distance1 = 10;
   [Textbox (1)] static double Distance2 = 15;

   protected override Poly? MakeImp (TPolyPick tpp) {
      var (dist1, dist2) = (Distance1, Distance2);
      if (tpp.Node == tpp.Seg) (dist1, dist2) = (dist2, dist1);
      return tpp.Poly.Chamfer (tpp.Node, dist1, dist2);
   }
}
#endregion

#region class InFilletMaker ------------------------------------------------------------------------
[DwgCmd (ECmd.InFillet), NoKeyboardMode]
class InFilletMaker : PolyCornerEditor {
   [Textbox (0)] double Radius { get => mRadius; set => mRadius = value.Clamp (0, 1_000.0); }
   static double mRadius = 10;

   protected override Poly? MakeImp (TPolyPick tpp) => tpp.Poly.InFillet (tpp.Node, Radius, (tpp.Flags & Poly.ECornerOpFlags.Left) != 0);
}
#endregion

#region class CornerStepMaker ----------------------------------------------------------------------
/// <summary>Implements the CornerStep command</summary>
[DwgCmd (ECmd.CornerStep), NoKeyboardMode]
class CornerStepMaker : PolyCornerEditor {
   [Textbox (0)] static double Distance1 = 10;
   [Textbox (1)] static double Distance2 = 15;

   protected override Poly? MakeImp (TPolyPick tpp) {
      var (dist1, dist2) = (Distance1, Distance2);
      if (tpp.Node == tpp.Seg) (dist1, dist2) = (dist2, dist1);
      return tpp.Poly.CornerStep (tpp.Node, dist1, dist2, tpp.Flags);
   }
}
#endregion

#region class FilletMaker --------------------------------------------------------------------------
/// <summary>Implements the Fillet command</summary>
[DwgCmd (ECmd.Fillet), NoKeyboardMode]
class FilletMaker : PolyCornerEditor {
   [Textbox (0)] double Radius { get => mRadius; set => mRadius = value.Clamp (0, 1_000); }
   static double mRadius = 10;

   protected override Poly? MakeImp (TPolyPick tpp) => tpp.Poly.Fillet (tpp.Node, Radius);
}
#endregion

#region class EdgeRecessMaker ----------------------------------------------------------------------
[DwgCmd (ECmd.EdgeRecess), NoKeyboardMode]
class EdgeRecessMaker : PolyEditor {
   [Textbox (0)] static double mOffset = 5;
   [Textbox (1)] static double mWidth = 20;
   [Textbox (2)] static double mDepth = 10;

   protected override Poly? Make (ref TPolyPick tpp) {
      double centerOffset = mOffset + mWidth / 2;
      if ((tpp.Flags & Poly.ECornerOpFlags.NearLeadOut) == 0)
         centerOffset = tpp.Poly[tpp.Seg].Length - centerOffset;
      return tpp.Poly.EdgeRecess (tpp.Seg, (tpp.Flags & Poly.ECornerOpFlags.Left) != 0, centerOffset, mWidth, mDepth);
   }
}
#endregion

#region class EdgeVNotchMaker ----------------------------------------------------------------------
[DwgCmd (ECmd.EdgeV), NoKeyboardMode]
class EdgeVNotchMaker : PolyEditor {
   [Textbox (0)] static double mOffset = 5;
   [Textbox (1)] static double mWidth = 20;
   [Textbox (2)] static double mDepth = 10;

   protected override Poly? Make (ref TPolyPick tpp) {
      double centeroffset = mOffset + mWidth / 2;
      if ((tpp.Flags & Poly.ECornerOpFlags.NearLeadOut) == 0)
         centeroffset = tpp.Poly[tpp.Seg].Length - centeroffset;
      return tpp.Poly.VNotch (tpp.Seg, centeroffset, mWidth, (tpp.Flags & Poly.ECornerOpFlags.Left) != 0 ? mDepth : -mDepth);
   }
}
#endregion

#region class EdgeUNotchMaker ----------------------------------------------------------------------
[DwgCmd (ECmd.EdgeU), NoKeyboardMode]
class EdgeUNotchMaker : PolyEditor {
   [Textbox (0)] static double mOffset = 5;
   [Textbox (1)] static double mWidth = 10;
   [Textbox (2)] static double mDepth = 5;
   [Textbox (3)] static double mRadius = 2;

   protected override Poly? Make (ref TPolyPick tpp) {
      double centerOffset = Abs (mOffset) + (Abs (mWidth) / 2);
      if ((tpp.Flags & Poly.ECornerOpFlags.NearLeadOut) == 0)
         centerOffset = tpp.Poly[tpp.Seg].Length - centerOffset;
      return tpp.Poly.UNotch (tpp.Seg, centerOffset, mWidth, (tpp.Flags & Poly.ECornerOpFlags.Left) != 0 ? mDepth : -mDepth, mRadius);
   }
}
#endregion

#region KeySlotMaker -------------------------------------------------------------------------------
[DwgCmd (ECmd.KeySlot), NoKeyboardMode]
class KeySlotMaker : PolyEditor {
   [Textbox (0)] static double mWidth = 20;
   [Textbox (1)] static double mDepth = 10;
   [Textbox (2)] static double mAngle = 90;

   protected override Poly? Make (ref TPolyPick tpp)
      => tpp.Poly.KeySlot (tpp.Seg, (tpp.Flags & Poly.ECornerOpFlags.Left) != 0, Math.Abs (mWidth), Math.Abs (mDepth), mAngle.D2R ());
}
#endregion