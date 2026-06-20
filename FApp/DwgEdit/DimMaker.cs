// ╔═╦╗
// ║╬╠╬╦╗ DimMaker.cs
// ║╔╣╠║╣ Implements all the dimension command widgets
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp;
using FApp.Widgets;
#pragma warning disable 0649

#region DimMaker -----------------------------------------------------------------------------------
/// <summary>Base class for creating dimension maker widgets</summary>
abstract class DimMaker : DwgWidget {
   protected DimMaker (string actionDesc) => mActionDesc = actionDesc;

   protected abstract Ent2? Make ();

   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phase != Phases - 1) return; // Feedback only in the "final" phase
      if (Make () is { } dim)
         DrawFeedback ([dim]);
   }

   public override void Completed () {
      base.Completed ();
      if (Make () is { } dim)
         Add (mActionDesc, dim);
   }

   readonly string mActionDesc; // Undoable action description
}
#endregion

#region DimSegMaker --------------------------------------------------------------------------------
/// <summary>(Line) Segment dimension</summary>
/// Needs 2 clicks
///  1st click - (must) pick (line) segment
///  2nd click - placement
[DwgCmd (ECmd.DimSegment)]
class DimSegMaker : DimMaker {
   public DimSegMaker () : base ("Segment Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;

   [Click (0), NoMouseMove]
   Point2 Pt1 {
      get => mPt1;
      set {
         if (PickLineSeg (mDwg, value) is { } seg) { mSeg = seg; mPt1 = value; return; }
         // No segment picked. Do not allow phase to increment yet,
         //  since we want the user to click again and pick some segment.
         HoldPhase ();
      }
   }
   Point2 mPt1;
   Seg mSeg;

   [Click (1)] Point2 Pt2;

   protected override E2Dim? Make () {
      if (Pt2.DistToLine (mSeg.A, mSeg.B).IsZero ()) return null;
      return new E2DimAligned (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, [mSeg.A, mSeg.B, Pt2], ComposeDimText (Text, Tolerance));
   }
}
#endregion

#region DimAngleMaker ------------------------------------------------------------------------------
/// <summary>Angle dimension b/w two specified line segments</summary>
/// Needs 3 clicks.
///  1st click - (must) pick (first) line segment
///  2nd click - (must) pick (second) line segment
///  3rd click - construct dimension b/w the two picked segments
[DwgCmd (ECmd.DimAngle)]
class DimAngleMaker : DimMaker {
   public DimAngleMaker () : base ("Angle Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;

   [Click (0), NoMouseMove]
   Point2 Pt1 {
      get => mPt1;
      set {
         if (PickLineSeg (mDwg, value) is { } seg) { mSeg1 = seg; mPt1 = value; return; }
         // No segment picked. Do not allow phase to increment yet,
         //  since we want the user to click again and pick some segment.
         HoldPhase ();
      }
   }

   [Click (1), NoMouseMove]
   Point2 Pt2 {
      get => mPt2;
      set {
         if (Phase == 0) return; // This is some wierdness! [Note: The first click/move also triggers this setter - by design!]
         // If the same segment is picked, then ignore the pick.
         if (PickLineSeg (mDwg, value) is { } seg && (mSeg1.Poly != seg.Poly || mSeg1.N != seg.N)) {
            mSeg2 = seg; mPt2 = value;
            return;
         }
         // No segment picked. Do not allow phase to increment yet,
         //  since we want the user to click again and pick some segment.
         HoldPhase ();
      }
   }

   [Click (2)] Point2 Pt3;

   Point2 mPt1, mPt2;
   Seg mSeg1, mSeg2;

   protected override E2DimAngle? Make ()
      => new (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, [mSeg1.A, mSeg1.B, mSeg2.A, mSeg2.B, Pt3], ComposeDimText (Text, Tolerance));
}
#endregion

#region LinearDimMaker -----------------------------------------------------------------------------
/// <summary>Linear dimension maker</summary>
/// Needs 3 clicks
///  1. Pick first dimension point [*Ctrl*=Pick aligned reference line, *Shift*=Pick perpendicular reference line]
///  2. Pick second dimension point [*Shift*=Set Angle]
///  3. Place dimension
[DwgCmd (ECmd.Dim2P)]
class LinearDimMaker : DimMaker {
   public LinearDimMaker () : base ("Linear Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;
   [Textbox (2), Angle] static double Angle = 0.0;

   [Click (0), NoMouseMove]
   Point2 Pt1 {
      get => mPt1;
      set {
         if (DwgHub.ShiftPressed || DwgHub.CtrlPressed) { // In these cases, we expect user to click on a line!
            // User wishes to override the measurement axis.
            if (PickLineSeg (mDwg, value) is { } seg) {
               var angle = seg.GetSlopeAt (0.5); // Measurement axis parallel to this line.
               if (DwgHub.ShiftPressed)
                  angle += Lib.HalfPI; // Measurement axis perpendicular to this line.
               Angle = angle;
            }

            // So, this point got used up in specifying the measurement direction angle!
            // But we still need the start measurement point. So we stay put in this Phase!
            HoldPhase ();
            return;
         }
         mPt1 = value;
      }

   }
   Point2 mPt1;

   [Click (1), NoMouseMove]
   Point2 Pt2 {
      get => mPt2;
      set {
         if (Phase == 0) return; // This is some wierdness! [Note: The first click/move also triggers this setter - by design!]
         mPt2 = value;
         if (DwgHub.ShiftPressed) // User wishes to override the measurement direction angle, using (mPt1, mPt2).
            Angle = Pt1.AngleTo (mPt2);
      }
   }
   Point2 mPt2;

   [Click (2)] Point2 Pt3;

   public override void DrawFeedback () {
      base.DrawFeedback ();
      // Need to indicate the applicable measurement direction.
      double lenPx = 10; // Length of the line indicating the measurement direction.
      var (pt, ang, dist) = (DwgHub.MousePos, Angle, lenPx * Util.PxScale * Lux.DPIScale);
      Lux.Poly (Poly.Line (pt.Polar (dist, ang), pt.Polar (-dist, ang)));
   }

   protected override E2Dim? Make ()
      => new E2DimLinear (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, Angle, [mPt1, mPt2, Pt3], ComposeDimText (Text, Tolerance));
}
#endregion

/// <summary>Linear (tandem) dimension maker base</summary>
/// Needs 3 clicks, to start with
///  1. Pick first dimension point [*Ctrl*=Pick aligned reference line, *Shift*=Pick perpendicular reference line]
///  2. Pick second dimension point [*Shift*=Set Angle]
///  3. Place dimension
abstract class LinearTandemDimMakerBase : DimMaker {
   protected LinearTandemDimMakerBase (string actionDesc, bool fixedAnchor) : base (actionDesc)
      => (mFixedAnchor, mSoftResetPhase) = (fixedAnchor, 1);

   protected string Text = string.Empty;
   protected string Tolerance = string.Empty;
   protected double Angle = 0.0;

   protected Point2 Pt1 {
      get => mPt1;
      set {
         if (DwgHub.ShiftPressed || DwgHub.CtrlPressed) { // In these cases, we expect user to click on a line!
            // User wishes to override the measurement axis.
            if (PickLineSeg (mDwg, value) is { } seg) {
               var angle = seg.GetSlopeAt (0.5); // Measurement axis parallel to this line.
               if (DwgHub.ShiftPressed)
                  angle += Lib.HalfPI; // Measurement axis perpendicular to this line.
               Angle = angle;
            }

            // So, this point got used up in specifying the measurement direction angle!
            // But we still need the start measurement point. So we stay put in this Phase!
            HoldPhase ();
            return;
         }
         mPt1 = value;
      }

   }
   Point2 mPt1;

   protected Point2 Pt2 {
      get => mPt2;
      set {
         if (Phase == 0) return; // This is some wierdness! [Note: The first click/move also triggers this setter - by design!]
         mPt2 = value;
         if (DwgHub.ShiftPressed) // User wishes to override the measurement direction angle, using (mPt1, mPt2).
            Angle = Pt1.AngleTo (mPt2);
      }
   }
   Point2 mPt2;

   protected Point2 Pt3;

   readonly bool mFixedAnchor; // Differentiates b/w baseline and continue linear dim variants

   public override void Completed () {
      base.Completed (); // Completed core handling is done here!

      // We are clear to prep for the next consecutive dimension placement.
      if (!mFixedAnchor) mPt1 = Pt2;
   }

   public override void DrawFeedback () {
      base.DrawFeedback ();
      // Need to indicate the applicable measurement direction.
      double lenPx = 10; // Length of the line indicating the measurement direction.
      var (pt, ang, dist) = (DwgHub.MousePos, Angle, lenPx * Util.PxScale * Lux.DPIScale);
      Lux.Poly (Poly.Line (pt.Polar (dist, ang), pt.Polar (-dist, ang)));
   }

   protected override E2Dim? Make ()
      => new E2DimLinear (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, Angle, [mPt1, mPt2, Pt3], ComposeDimText (Text, Tolerance));
}

#region BaselineDimMaker ---------------------------------------------------------------------------
/// <summary>Baseline linear dimension — all dims share a common first anchor.</summary>
/// Needs 3 clicks, to start with
///  1. Pick first dimension point [*Ctrl*=Pick aligned reference line, *Shift*=Pick perpendicular reference line]
///  2. Pick second dimension point [*Shift*=Set Angle]
///  3. Place dimension
/// Loops back to phase 1
[DwgCmd (ECmd.DimBaseline)]
class BaselineDimMaker : LinearTandemDimMakerBase {
   public BaselineDimMaker () : base ("Baseline Dimension", fixedAnchor: true) { }

   [Textbox (0)] new string Text { get => base.Text; set => base.Text = value; }
   [Textbox (1)] new string Tolerance { get => base.Tolerance; set => base.Tolerance = value; }
   [Textbox (2), Angle] new double Angle { get => base.Angle; set => base.Angle = value; }

   [Click (0), NoMouseMove] new Point2 Pt1 { get => base.Pt1; set => base.Pt1 = value; }
   [Click (1), NoMouseMove] new Point2 Pt2 { get => base.Pt2; set => base.Pt2 = value; }
   [Click (2)] new Point2 Pt3 { get => base.Pt3; set => base.Pt3 = value; }
}
#endregion

#region ContinueDimMaker ---------------------------------------------------------------------------
/// <summary>Continue (chain) linear dimension — each new dim starts where the previous ended.</summary>
/// Needs 3 clicks, to start with
///  1. Pick first dimension point [*Ctrl*=Pick aligned reference line, *Shift*=Pick perpendicular reference line]
///  2. Pick second dimension point [*Shift*=Set Angle]
///  3. Place dimension
/// Advances chain tip, loops back to phase 1
[DwgCmd (ECmd.DimContinue)]
class ContinueDimMaker : LinearTandemDimMakerBase {
   public ContinueDimMaker () : base ("Continue Dimension", fixedAnchor: false) { }

   [Textbox (0)] new string Text { get => base.Text; set => base.Text = value; }
   [Textbox (1)] new string Tolerance { get => base.Tolerance; set => base.Tolerance = value; }
   [Textbox (2), Angle] new double Angle { get => base.Angle; set => base.Angle = value; }

   [Click (0), NoMouseMove] new Point2 Pt1 { get => base.Pt1; set => base.Pt1 = value; }
   [Click (1), NoMouseMove] new Point2 Pt2 { get => base.Pt2; set => base.Pt2 = value; }
   [Click (2)] new Point2 Pt3 { get => base.Pt3; set => base.Pt3 = value; }
}
#endregion

#region DimDiaMaker --------------------------------------------------------------------------------
/// <summary>Makes diameter dimension (only for a Circle)</summary>
/// Needs 2 clicks:
///   - 1st click - (must) pick a Circle
///   - 2nd click - placement location
/// Modifiers
///   Shift - Show full diameter dimension line passing through the center
[DwgCmd (ECmd.DimDiameter)]
class DimDiaMaker : DimMaker {
   public DimDiaMaker () : base ("Diameter Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;

   [Click (0), NoMouseMove]
   Point2 Pt1 {
      get => mPt1;
      set {
         if (PickArcSeg (mDwg, value) is { IsCircle: true } seg) {
            mCircle = seg; mPt1 = value;
            return;
         }
         // No segment picked. Do not allow phase to increment yet,
         //  since we want the user to click again and pick some segment.
         HoldPhase ();
      }
   }
   Point2 mPt1;
   Seg mCircle;

   [Click (1)] Point2 Pt2;

   protected override E2Dim? Make () {
      if (mCircle.Center.DistTo (Pt2).EQ (mCircle.Radius)) return null; // Show no dimension, if location is snapped onto the circle!
      return new E2DimDia (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, mCircle.Radius, tofl: DwgHub.ShiftPressed, [mCircle.Center, Pt2], ComposeDimText (Text, Tolerance));
   }
}
#endregion

#region DimAngle3P ---------------------------------------------------------------------------------
/// <summary>3P Angle dimension, given 3 points</summary>
/// Needs 4 clicks
///  1. Click on point at which angle is measured
///  2. Click on point defining base of angle
///  3. Click on point defining angle
///  4. Place dimension
[DwgCmd (ECmd.DimAngle3P)]
class DimAngle3PMaker : DimMaker {
   public DimAngle3PMaker () : base ("3P Angle Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;

   [Click (0)] Point2 Pt1;
   [Click (1)] Point2 Pt2;
   [Click (2)] Point2 Pt3;
   [Click (3)] Point2 Pt4;

   protected override E2Dim? Make () {
      if (Pt3.Side (Pt1, Pt2) == 0) return null; // If collinear, return!
      return new E2Dim3PAngle (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, [Pt1, Pt2, Pt3, Pt4], ComposeDimText (Text, Tolerance));
   }
}
#endregion

#region DimRadMaker --------------------------------------------------------------------------------
/// <summary>Makes arc dimension, for any Arc segment (including Circle)</summary>
/// Needs 2 clicks:
///   - 1st click - (must) pick an Arc
///   - 2nd click - placement location
/// Modifiers
///   Shift - Show radial dimension starting at the center
[DwgCmd (ECmd.DimRadius)]
class DimRadMaker : DimMaker {
   public DimRadMaker () : base ("Radius Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;

   [Click (0), NoMouseMove]
   Point2 Pt1 {
      get => mPt1;
      set {
         if (PickArcSeg (mDwg, value) is { } seg) {
            mSeg = seg; mPt1 = value;
            return;
         }
         // No segment picked. Do not allow phase to increment yet,
         //  since we want the user to click again and pick some segment.
         HoldPhase ();
      }
   }
   Point2 mPt1;
   Seg mSeg;

   [Click (1)] Point2 Pt2;

   protected override E2Dim? Make () {
      if (mSeg.Center.DistTo (Pt2).EQ (mSeg.Radius)) return null; // Show no dimension, if location is ON the arc!

      // To make the arc dimension, clamp the second point (positioning the arc dimension)
      // to lie within the span of the arc (but we shouldn't snap it to lie ON the arc itself,
      // - we need to preserve the distance from the center as supplied by the user input)
      // Ensure the dimension line is within the arc span.
      Point2 pt = mSeg.Center.Polar (mSeg.Radius, mSeg.Center.AngleTo (Pt2));
      double lie = mSeg.GetLie (pt).Clamp (); pt = mSeg.GetPointAt (lie);
      pt = mSeg.Center.Polar (mSeg.Center.DistTo (Pt2), mSeg.Center.AngleTo (pt));
      return new E2DimRad (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, mSeg.Radius, tofl: DwgHub.ShiftPressed, [mSeg.Center, pt], ComposeDimText (Text, Tolerance));
   }
}
#endregion

#region DimCalloutMaker ----------------------------------------------------------------------------
/// <summary>Makes Callout dimension </summary>
/// Need 2 clicks:
///   - 1st click - Pick reference point
///   - 2nd click - Placement location
[DwgCmd (ECmd.DimCallout)]
class DimCallOutMaker : DimMaker {
   public DimCallOutMaker () : base ("Callout Dimension") { }

   [Textbox (0)] string Text = string.Empty;
   [Textbox (1)] string Tolerance = string.Empty;
   [Click (0)] Point2 mPt1;
   [Click (1)] Point2 mPt2;

   protected override Ent2? Make () {
      if (string.IsNullOrWhiteSpace (Text)) Text = "ABC";
      return new E2Leader (mDwg.GetDimLayer (), mDwg.CurrentDimStyle, [mPt1, mPt2], ComposeDimText (Text, Tolerance)!);
   }
}
#endregion