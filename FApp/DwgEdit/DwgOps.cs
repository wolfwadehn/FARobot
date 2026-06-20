// ╔═╦╗
// ║╬╠╬╦╗ DwgOps.cs
// ║╔╣╠║╣ Operations performed on Dwg Elements
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Widgets;
using System.Reactive.Linq;
namespace FApp;

#region PolarArray ---------------------------------------------------------------------------------
[DwgCmd (ECmd.PolarArray)]
class PolarArray : DwgLayouter {
   [Textbox (0)] static int Count = 6;
   [Textbox (1), Click (0)] static Point2 Center = Point2.Zero;
   [Textbox (2), Angle] static double StepAng = 30;

   [Click (1)] Point2 StartPt = (100, 0);
   [Click (2)] Point2 EndPt = (-100, 0);

   public override IEnumerable<Ent2> MakeCopies () {
      if (Count < 1) return [];
      if (DwgHub.KeyboardMode) {
         if (StepAng.IsZero ()) return [];
         return MakeCopies (Count, Center, StepAng, [.. SelectedEntities]);
      }
      double stepAng = (StartPt - Center).AngleTo (EndPt - Center); // Gives the angular span only!
      stepAng *= EndPt.Side (Center, StartPt);
      if (stepAng.IsZero ()) return [];
      return MakeCopies (Count, Center, stepAng, [.. SelectedEntities]);
   }

   static IEnumerable<Ent2> MakeCopies (int count, Point2 center, double stepAng, List<Ent2> ents) {
      for (int i = 1; i < count; i++) {
         Matrix2 xfm = Matrix2.Rotation (center, i * stepAng);
         for (int j = 0; j < ents.Count; j++)
            yield return ents[j] * xfm;
      }
   }
}
#endregion

#region class RectArray ----------------------------------------------------------------------------
[DwgCmd (ECmd.RectArray)]
class RectArray : DwgLayouter {
   [Textbox (0)] static int C = 2;
   [Textbox (1)] static int R = 2;
   [Textbox (2)] static double CP = 10;
   [Textbox (3)] static double RP = 10;
   [Textbox (4)] static double angle = 0;

   [Click (0)] Point2 Start = Point2.Zero;
   [Click (1)] Point2 End = Point2.Zero;

   public override IEnumerable<Ent2> MakeCopies () {
      if (C == 0 || R == 0) return [];
      var (dx, dy) = (CP, RP);
      if (!DwgHub.KeyboardMode) (dx, dy) = End - Start;
      return Copies (SelectedEntities, dx, dy, C, R, angle.D2R ());
   }

   static IEnumerable<Ent2> Copies (IEnumerable<Ent2> ents, double dx, double dy, int col, int row, double angle) {
      var (offX, offY) = (Vector2.UnitVec (angle) * dx, Vector2.UnitVec (angle + Lib.HalfPI) * dy);
      for (var i = 0; i < col; i++) {
         for (var j = 0; j < row; j++) {
            if (i == 0 && j == 0) continue;
            Matrix2 mat = Matrix2.Translation (offX * i + offY * j);
            foreach (var entity in ents)
               yield return entity * mat;
         }
      }
   }
}
#endregion

#region class MoveEntities -------------------------------------------------------------------------
[DwgCmd (ECmd.Move)]
class MoveEntities : DwgArranger {
   [Textbox (0)] static double DX = 0;
   [Textbox (1)] static double DY = 0;

   [Click (0)] Point2 Start = Point2.Zero;
   [Click (1)] Point2 End = Point2.Zero;

   public override IEnumerable<Ent2> Make () {
      var (dx, dy) = (DX, DY);
      if (!DwgHub.KeyboardMode)
         (End - Start).Deconstruct (out dx, out dy);
      var trans = Matrix2.Translation (dx, dy);
      return SelectedEntities.Select (ent => ent * trans);
   }
}
#endregion

#region class ScaleEntities ------------------------------------------------------------------------
[DwgCmd (ECmd.Scale)]
class ScaleEntities : DwgArranger {
   [Textbox (0), Click (0)] static Point2 BasePoint = Point2.Zero;
   [Textbox (1)] static double ScaleFactor = 1.0;
   [Click (1)] Point2 RefPoint = Point2.Zero;
   [Click (2)] Point2 TargetPoint = Point2.Zero;

   public override IEnumerable<Ent2> Make () {
      double sf = ScaleFactor;
      // If not in keyboard mode, compute scale factor from picked points
      if (!DwgHub.KeyboardMode) {
         double refLen = (RefPoint - BasePoint).Length;
         sf = refLen > Lib.Epsilon ? (TargetPoint - BasePoint).Length / refLen : 1.0;
      }
      if (sf.EQ (1)) return [];
      var mat = Matrix2.Scaling (BasePoint, sf, sf);
      return SelectedEntities.Select (ent => ent * mat);
   }
}
#endregion

#region class RotateEntities -----------------------------------------------------------------------
[DwgCmd (ECmd.Rotate)]
class RotateEntities : DwgArranger {
   [Textbox (0), Click (0)] static Point2 Center = Point2.Zero;
   [Textbox (1), Angle] static double Angle = 0;

   [Click (1)] Point2 Start = Point2.Zero;
   [Click (2)] Point2 End = Point2.Zero;

   public override IEnumerable<Ent2> Make () {
      var angle = DwgHub.KeyboardMode ? Angle : Center.AngleTo (End) - Center.AngleTo (Start);
      var rot = Matrix2.Rotation (Center, angle);
      return SelectedEntities.Select (ent => ent * rot);
   }
}
#endregion

#region class MirrorEntities -----------------------------------------------------------------------
[DwgCmd (ECmd.Mirror)]
class MirrorEntities : DwgArranger {
   [Textbox (0), Click (0)] static Point2 Start = Point2.Zero;
   [Textbox (1), Click (1)] static Point2 End = (1, 0);

   public override IEnumerable<Ent2> Make ()
      => SelectedEntities.Select (ent => ent * Matrix2.Mirror (Start, End));
}
#endregion
