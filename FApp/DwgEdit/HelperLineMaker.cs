// ╔═╦╗
// ║╬╠╬╦╗ HelperLineMaker.cs
// ║╔╣╠║╣ Widgets that create horizontal / vertical helper lines
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp;
using FApp.Widgets;
#pragma warning disable 414

#region class HelperLineBase -----------------------------------------------------------------------
/// <summary>Base class for horizontal and vertical helper-line makers</summary>
/// Shared logic: seed base position from a selected helper line, draw live feedback,
/// create the line on completion, and reset the distance input after each Enter.
abstract class HelperLineBase : DwgWidget {
   protected double mBasePos;       // Y (horiz) or X (vert) seeded from a selected helper line
   protected abstract bool IsHorizontal { get; }

   // If a helper line matching this direction is selected, seed mBasePos from its position.
   public override void OnActivated () {
      var layer = mDwg.GetHelperLayer ();
      var sel = mDwg.Ents.OfType<E2Poly> ()
                         .FirstOrDefault (e => e.IsSelected && e.Layer == layer);
      if (sel == null) return;
      var seg = sel.Poly[0];
      if (!seg.IsLine) return;
      if (IsHorizontal && Math.Abs (seg.A.Y - seg.B.Y) < 1e-6)
         mBasePos = seg.A.Y;
      else if (!IsHorizontal && Math.Abs (seg.A.X - seg.B.X) < 1e-6)
         mBasePos = seg.A.X;
   }

   public override void DrawFeedback () {
      base.DrawFeedback ();
      Lux.Poly (MakePoly ());
   }

   // Creates the line, then clears the distance textbox so it is ready for the next input.
   public override void Completed () {
      base.Completed ();
      Add ("Helper Line", new E2Poly (mDwg.GetHelperLayer (), MakePoly ()));
      if (Fields.FirstOrDefault (f => f.NInput == 0) is { } field) {
         field.SetValue (0.0);
         DwgHub.IBar?.DataToUI (field);
      }
   }

   Poly MakePoly () {
      double dist = Fields.FirstOrDefault (f => f.NInput == 0) is { } f2
         ? (double)f2.GetValue ()! : 0.0;
      double pos = mBasePos + dist;
      Bound2 b = mDwg.Bound.IsEmpty
         ? new Bound2 (-1000, -1000, 1000, 1000)
         : mDwg.Bound.InflatedF (2);
      return IsHorizontal
         ? Poly.Line ((b.X.Min, pos), (b.X.Max, pos))
         : Poly.Line ((pos, b.Y.Min), (pos, b.Y.Max));
   }
}
#endregion

#region class HHelperLineMaker ---------------------------------------------------------------------
/// <summary>Creates a horizontal construction line on the helper layer</summary>
[DwgCmd (ECmd.HelperLineH), NoMouseInput, NoSimMouse]
class HHelperLineMaker : HelperLineBase {
   [Textbox (0)] static double Distance = 0;
   protected override bool IsHorizontal => true;
}
#endregion

#region class VHelperLineMaker ---------------------------------------------------------------------
/// <summary>Creates a vertical construction line on the helper layer</summary>
[DwgCmd (ECmd.HelperLineV), NoMouseInput, NoSimMouse]
class VHelperLineMaker : HelperLineBase {
   [Textbox (0)] static double Distance = 0;
   protected override bool IsHorizontal => false;
}
#endregion
