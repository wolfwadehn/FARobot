// ╔═╦╗
// ║╬╠╬╦╗ PickMode.cs
// ║╔╣╠║╣ <<TODO>>
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;

#region class PickWidget ---------------------------------------------------------------------------
[DwgCmd (ECmd.Pick), NoSimMouse]
class PickWidget : DwgWidget {
   [Click (0), NoMouseMove]
   Point2 Pick {
      get => mPick;
      set {
         mPick = value;
         Ent2? closest = null;
         double threshold = DwgHub.PickAperture;
         foreach (var ent in mDwg.Ents)
            if (ent.Layer.Name != "CleanupMarker" && ent.IsCloser (mPick, ref threshold)) closest = ent;
         if (closest is null) new Ent2Selector (mDwg);
         else {
            mDwg.Select (closest, !DwgHub.ShiftPressed);
            if (!DwgHub.CtrlPressed) return;

            // Special case: Ctrl pressed, then selected all entities bounded by picked e2p, if any.
            if (closest is E2Poly e2p) {
               // Grab all the entities lying within the bounds of this poly
               var b = closest.Bound.InflatedL (Lib.Delta);
               var subEnts = mDwg.Ents.Where (a => a != closest && b.Contains (a.Bound));
               // Simply selected/unselect all contained sub entities, based on selection state of "e2p"
               subEnts.ForEach (a => a.IsSelected = e2p.IsSelected);
            }
         }
      }
   }
   Point2 mPick;
}
#endregion

#region class MeasureWidget ------------------------------------------------------------------------
/// <summary> A widget that provides interactive measurements. </summary>
/// First click sets the measurement start point.
/// While moving the mouse displays the current distance from the start point.
/// When not measuring, hovering over geometry shows segment specific information like length, angle, radius, etc.
[DwgCmd (ECmd.Measure), NoKeyboardMode]
class MeasureWidget : DwgWidget {
   static MeasureWidget () {
      var face = Lux.TypeFace ?? TypeFace.Default;
      var cellM = face.Measure ("M", true);
      (int tw, int th) = (cellM.Width, (int)(cellM.Height * 1.67)); // MAGIC: Some "reasonable" line gap
      LineOffset = new (tw, th); LineOffset2 = new (tw, 2 * th); LineOffset3 = new (tw, 3 * th);
   }

   [Click (0)] Point2 StartPt = Point2.Zero;
   [Click (1)] Point2 EndPt = Point2.Zero;

   // Text-line placement offsets
   static readonly Vec2S LineOffset, LineOffset2, LineOffset3;

   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phase == 0) { // Only StartPt is valid
         if (mDwg.PickPoly (StartPt, DwgHub.PickAperture, out int nSeg, out double lie) is E2Poly { } e2p) {
            var seg = e2p.Poly[nSeg];
            var txt = seg switch {
               { IsLine: true } => LineInfo (seg, StartPt, lie, LineOffset3),
               { IsCircle: true } => $"Diameter {seg.Radius.Round (3) * 2}",
               { IsArc: true } => $"Radius {seg.Radius.Round (3)}, Span {seg.AngSpan.R2D ().Round (3)}",
               _ => ""
            };
            if (txt.Length > 0)
               Lux.Text2D (txt, StartPt, ETextAlign.TopLeft, LineOffset2);
         }
      } else { // Both StartPt and EndPt are valid
         Vector2 v = EndPt - StartPt;
         Lux.Text2D ($"Distance {v.Length.Round (3)}", EndPt, ETextAlign.TopLeft, LineOffset3);
         Lux.Text2D ($"DX {v.X.Round (3)}, DY {v.Y.Round (3)}", EndPt, ETextAlign.TopLeft, LineOffset2);
         Lux.Poly (Poly.Line (StartPt, EndPt));
      }

      // Always print the current hover position
      var pt = Phase == 0 ? StartPt : EndPt;
      Lux.Text2D ($"X {pt.X.Round (3)} , Y {pt.Y.Round (3)}", pt, ETextAlign.TopLeft, LineOffset);

      // Get line segment info and draw angle indicator
      static string LineInfo (Seg seg, Point2 pos, double lie, Vec2S offset) {
         Point2 p1 = seg.A, p2 = seg.B;
         if (!(p1.X.EQ (p2.X) || p1.Y.EQ (p2.Y))) { // Not horizontal/vertical...
            if (lie > 0.5) (p1, p2) = (p2, p1);
            double slope = p1.AngleTo (p2);
            Lux.Text2D ($"Angle {slope.R2D ().Round (3)}", pos, ETextAlign.TopLeft, offset);
            var line = Poly.Line (p1, p1.Moved (15, 0));
            var startTangentAngle = slope < 0 ? -Lib.HalfPI : Lib.HalfPI;
            var arc = Poly.Arc (p1.Moved (10, 0), startTangentAngle, p1.Polar (10, slope));
            Lux.Polys ([line, arc]);
         }
         Lux.Polys ([Poly.Circle (p1, 1), Poly.Circle (p2, 1)]);
         return $"Length {seg.Length.Round (3)}";
      }
   }
}
#endregion

#region class DwgInfoWidget ------------------------------------------------------------------------
[DwgCmd (ECmd.Info), NoKeyboardMode, NoSimMouse]
class DwgInfoCmd : DwgWidget {
   public DwgInfoCmd () {
      var face = Lux.TypeFace ?? TypeFace.Default;
      var cellM = face.Measure ("M", true);
      (int tw, int th) = (cellM.Width, (int)(cellM.Height * 1.67)); // MAGIC: Some "reasonable" line gap
      LineOffset = new (tw, 2 * th); LineOffset2 = new (tw, 3 * th);
   }

   [Click (0)]
   Point2 Pick {
      get => mPick;
      set => mPicked = SnapEnt (mDwg, mPick = value);
   }
   Point2 mPick;
   Ent2? mPicked;

   // Text-line placement offsets
   readonly Vec2S LineOffset, LineOffset2;

   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (mPicked == null) return;
      // Note: Not all entities (like bendline) may have "valid" layer!
      if (mPicked is not E2Bendline)
         Lux.Text2D ($"Layer: {mPicked.Layer.Name}", Pick, ETextAlign.TopLeft, LineOffset2);
      // Entity type
      Lux.Text2D ($"Type: {GetE2TypeName (mPicked)}", Pick, ETextAlign.TopLeft, LineOffset);
   }

   static string GetE2TypeName (Ent2 e2) {
      return e2 switch {
         E2Poly { } e2p => e2p.Poly.IsClosed ? "Poly (Closed)" : "Poly (Open)",
         E2Point { } => "Point",
         E2Bendline { } b => $"Bend line ({b.Angle.R2D ().Round (2)})",
         E2Insert { } => "Insert",
         E2Solid { } => "Solid",
         E2Text { } => "Text",
         E2Dim { } => "Dimension",
         E2Spline { } => "Spline",
         _ => "Unknown!",
      };
   }
}
#endregion

#region class BendWidget ---------------------------------------------------------------------------
[DwgCmd (ECmd.EditBend), NoKeyboardMode, NoSimMouse]
class BendWidget : DwgWidget {
   [Click (0), NoMouseMove, Unsnapped]
   Point2 Pick {
      get => mPick;
      set {
         mPick = value;
         var threshold = DwgHub.PickAperture;
         if (SnapEnt (mDwg, mPick = value) is { } e2) {
            if (e2 is E2Bendline e2b) FApp.WPF.EditBend.Show (mDwg, e2b);
            else if (e2 is E2Poly e2p) {
               var (_, nSeg) = e2p.Poly.GetDistance (mPick);
               Seg s = e2p.Poly[nSeg];
               if (!s.IsLine) return;
               var bend = CreateBend (mDwg, s);
               List<Ent2> toAdd = [bend];
               if (s.Poly.Count > 1) {
                  var layer = e2p.Layer;
                  // One of the segments got converted to bend line - which may split the poly.
                  toAdd.AddRange (s.Poly.Trimmed ((double)nSeg, (double)(nSeg + 1)).Select (a => new E2Poly (layer, a)));
               }
               Add ("Create Bend", toAdd, [e2p]);
               ((DwgScene?)Lux.UIScene)?.RefreshFoldPreview ();
            }
         }
      }
   }
   Point2 mPick;

   // Overrides ----------------------------------------------------------------
   public override void OnActivated () => ((DwgScene?)Lux.UIScene)?.ShowFoldPreview (true);
   public override void OnDeactivated () => ((DwgScene?)Lux.UIScene)?.ShowFoldPreview (false);

   // Implementation -----------------------------------------------------------
   // Creates a bend with default parameters, given a line seg
   static E2Bendline CreateBend (Dwg2 dwg, Seg line)
      => new (dwg, [line.A, line.B], angle: 90.D2R (), radius: 1.5, kfactor: 0.4, thickness: 1);
}
#endregion