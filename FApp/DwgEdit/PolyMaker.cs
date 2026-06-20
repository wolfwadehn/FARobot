// ╔═╦╗
// ║╬╠╬╦╗ PolyMaker.cs
// ║╔╣╠║╣ Implements PolyMaker and derived classes (widgets that create a single Poly)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp;
using FApp.Widgets;
#pragma warning disable 414

#region class ArcMaker -----------------------------------------------------------------------------
[DwgCmd (ECmd.Arc), CanRepeat]
class ArcMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Center = Point2.Zero;
   [Textbox (1), Phase (1)] static double Radius = 10;
   [Textbox (2), Phase (1), Angle] static double StartAngle = 0;
   [Textbox (3), Phase (2), Angle] static double EndAngle = Lib.HalfPI;

   // Second click sets the radius and the start-angle of the arc
   [Click (1)] Point2 Pt2 {
      get => Center.Polar (Radius, StartAngle);
      set => (Radius, StartAngle) = (Center.DistTo (value), Center.AngleTo (value));
   }
   [Click (2)] Point2 Pt3 {
      get => Center.Polar (Radius, EndAngle);
      set => EndAngle = Center.AngleTo (value);
   }

   // Methods ------------------------------------------------------------------
   override public Poly? Make (int phase) {
      if (phase == 1) return Poly.Line (Center, Center.Polar (Radius, StartAngle));
      if (!mRepeating) mClockwise = DwgHub.CtrlPressed;
      return Poly.Arc (Center, Radius, StartAngle, EndAngle, !mClockwise);
   }
   bool mClockwise;
}
#endregion

#region class Arc2PMaker ---------------------------------------------------------------------------
[DwgCmd (ECmd.Arc2P), CanRepeat]
class Arc2PMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] static double Radius = 10;
   [Textbox (1), Click (0)] static Point2 Pt1 = Point2.Zero;
   [Textbox (2), Click (1)] static Point2 Pt2 = Point2.Zero;

   // Methods ------------------------------------------------------------------
   public override Poly Make (int phase) {
      // If half the distance between start and end exceeds the radius, use that
      // as radius and set center to the midpoint. Otherwise, use the provided
      // radius and compute the center based on the start point, end point and
      // radius. If SHIFT is pressed, an major arc is drawn. Otherwise, a minor
      // arc is drawn. If CTRL is pressed, the arc is drawn in clockwise
      // direction. Otherwise, it is drawn in counter clockwise direction.
      if (!mRepeating) mClockwise = DwgHub.CtrlPressed;
      mMajorArc = mPrevArcMajor = mRepeating ? mPrevArcMajor : DwgHub.ShiftPressed;
      double d = Pt1.DistTo (Pt2) / 2, r = Math.Max (Radius, d);
      double theta1 = Pt1.AngleTo (Pt2), theta2 = Math.Acos (d / r) * (mClockwise == mMajorArc ? 1 : -1);
      var center = Pt1.Polar (r, theta1 + theta2);
      double sAng = center.AngleTo (Pt1), eAng = center.AngleTo (Pt2);
      return Poly.Arc (center, r, sAng, eAng, !mClockwise);
   }
   bool mClockwise, mMajorArc;
   static bool mPrevArcMajor; // To track if previous arc is a major arc.
}
#endregion

#region class CircleMaker --------------------------------------------------------------------------
/// <summary>Implements the CIRCLE command</summary>
[DwgCmd (ECmd.Circle), CanRepeat]
class CircleMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Center = Point2.Zero;
   [Textbox (1), Phase (1), Variant (EStyle.Radius)] static double Radius = 10;

   // Properties ---------------------------------------------------------------
   // Alternative to use diameter instead of radius
   [Textbox (2), Phase (1), Variant (EStyle.Diameter)] double Diameter { get => Radius * 2; set => Radius = value / 2; }
   [Click (1)] Point2 Pt2 { get => Center.Moved (Radius, 0); set => Radius = Center.DistTo (value); }
   [Checkbox (3), HotKey (EKey.F5)] static EStyle Style = EStyle.Radius; // Are we using radius or diameter
   enum EStyle { Radius, Diameter }

   // Methods ------------------------------------------------------------------
   override public Poly? Make (int _) => Poly.Circle (Center, Radius);
}
#endregion

#region class Circle3PMaker ------------------------------------------------------------------------
/// <summary>Implements the 3-Point Circle command</summary>
[DwgCmd (ECmd.Circle3P), CanRepeat]
class Circle3PMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Point1 = (10, 10);
   [Textbox (1), Click (1)] static Point2 Point2 = (20, 15);
   [Textbox (2), Click (2)] static Point2 Point3 = (20, 15);

   // Methods ------------------------------------------------------------------
   /// <summary>Creates a 3-Point Circle</summary>
   public override Poly? Make (int phase) {
      if (phase == 0) return null;
      if (phase == 1) return Poly.Line (Point1, Point2);
      var center = Geo.Get3PCircle (Point1, Point2, Point3);
      if (center.IsNil) return null;
      var radius = center.DistTo (Point1);
      if (radius > 1e6) return null;
      return Poly.Circle (center, radius);
   }
}
#endregion

#region class LineMaker ----------------------------------------------------------------------------
/// <summary>Implements the LINE command</summary>
[DwgCmd (ECmd.Line), CanRepeat]
class LineMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Start = (10, 10);
   [Textbox (1), Click (1)] static Point2 End = (20, 15);

   // Methods ------------------------------------------------------------------
   override public Poly? Make (int _) => Poly.Line (Start, End);
}
#endregion

#region class RectMaker ----------------------------------------------------------------------------
/// <summary>Implements the RECTANGLE command</summary>
[DwgCmd (ECmd.Rect), CanRepeat]
class RectMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Corner1 = (10, 10);
   [Textbox (1), Click (1)] static Point2 Corner2 = (30, 20);

   // Methods ------------------------------------------------------------------
   public override Poly? Make (int _) => Poly.Rectangle (Corner1.X, Corner1.Y, Corner2.X, Corner2.Y);
}
#endregion

#region class RectCenterMaker ----------------------------------------------------------------------
/// <summary>Implements the Center Rectangle command</summary>
[DwgCmd (ECmd.RectCenter), CanRepeat]
class RectCenterMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Center = (10, 10);
   [Textbox (1), Click (1)] static Point2 Corner = (20, 15);

   // Methods ------------------------------------------------------------------
   public override Poly? Make (int _) => Poly.Rectangle (new Bound2 ([Corner, Center - (Corner - Center)]));
}
#endregion

#region class Arc3PMaker ---------------------------------------------------------------------------
[DwgCmd (ECmd.Arc3P), CanRepeat]
class Arc3PMaker : PolyMaker { // 3 clicks
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Pt1 = Point2.Zero;
   [Textbox (1), Click (1)] static Point2 Pt2 = Point2.Zero;
   [Textbox (2), Click (2)] static Point2 Pt3 = Point2.Zero;

   // Methods ------------------------------------------------------------------
   // In phase 1, returns a straight line from Pt1 to Pt2.
   // In later phases, attempts to create an arc through Pt1, Pt2, and Pt3.
   // If the points are nearly collinear, falls back to a straight line from Pt1 to Pt3.
   public override Poly? Make (int phase) {
      if (phase == 1) return Poly.Line (Pt1, Pt2);
      // Compute direction vectors
      Vector2 d1 = Pt2 - Pt1, d2 = Pt3 - Pt1;
      Point2 center = Geo.Get3PCircle (Pt1, Pt2, Pt3);
      double radius = center.DistTo (Pt1);
      if (radius > 1e6 || center.IsNil) return Poly.Line (Pt1, Pt3);
      Vector3 z1 = new (d1.X, d1.Y, 0), z2 = new (d2.X, d2.Y, 0);
      // Use the cross product to determine the direction of the arc (CCW or CW)
      return Poly.Arc (center, radius, center.AngleTo (Pt1), center.AngleTo (Pt3), (z1 * z2).Z > 0);
   }
}
#endregion

#region class PolyInscribeMaker --------------------------------------------------------------------
/// <summary>Implements the PolyInscribe command</summary>
[DwgCmd (ECmd.PolyInscribe), CanRepeat]
class PolyInscribeMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] int Sides { get => mSides; set => mSides = value.Clamp (3, 360); }
   static int mSides = 6;
   [Textbox (1), Click (0)] static Point2 Center = (0, 0);
   [Textbox (2), Phase (1)] static double Radius = 10;
   [Textbox (3), Phase (1), Angle] static double Angle = 0;
   [Click (1)] Point2 Corner {
      get => Center.Polar (Radius, Angle);
      set => (Radius, Angle) = (Center.DistTo (value), Center.AngleTo (value));
   }

   // Methods ------------------------------------------------------------------
   /// <summary>Creates a polygon using the center and corner points</summary>
   public override Poly Make (int _)
      => Poly.Lines (Enumerable.Range (0, Sides).Select (i => Center.Polar (Radius, Angle + Lib.TwoPI * i / Sides)), true);
}
#endregion

#region class PolyCircumscribeMaker ----------------------------------------------------------------
/// <summary>Implements the POLYCIRCUMSCRIBE command</summary>
[DwgCmd (ECmd.PolyCircumscribe), CanRepeat]
class PolyCircumscribeMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] int Sides { get => mSides; set => mSides = value.Clamp (3, 360); }
   static int mSides = 6;
   [Textbox (1), Click (0)] static Point2 Center = new (0, 0);
   [Textbox (2), Phase (1)] static double Radius = 5;
   [Textbox (3), Phase (1), Angle] static double Angle = 0;
   [Click (1)] Point2 EdgeMidPoint {
      get => Center.Polar (Radius, Angle);
      set => (Radius, Angle) = (Center.DistTo (value), Center.AngleTo (value));
   }

   // Methods ------------------------------------------------------------------
   public override Poly Make (int _) {
      double halfAng = Math.PI / Sides;
      (double r, double a) = (Radius / Math.Cos (halfAng), Angle + halfAng);
      return Poly.Lines (Enumerable.Range (0, Sides).Select (i => Center.Polar (r, a + Lib.TwoPI * i / Sides)), true);
   }
}
#endregion

#region class Circle2PMaker ------------------------------------------------------------------------
/// <summary>Implements the Circle2P command</summary>
[DwgCmd (ECmd.Circle2P), CanRepeat]
class Circle2PMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] static double Radius = 0;
   [Textbox (1), Click (0)] static Point2 Start = (0, 0);
   [Textbox (2), Click (1)] static Point2 End = (0, 0);

   // Methods ------------------------------------------------------------------
   public override Poly Make (int phase) {
      var dist = Start.DistTo (End) / 2;
      var (r, angle) = (Math.Max (dist, Radius), Start.AngleTo (End));
      if (r.EQ (Radius)) {
         double theta = Math.Acos (dist / r); // angle between the start..end line and the center of the circle
         if (!mRepeating) mClockwise = DwgHub.CtrlPressed;
         var center = Start.Polar (r, angle + (mClockwise ? -theta : theta));
         return Poly.Circle (center, r);
      }
      return Poly.Circle (Start.Midpoint (End), r);
   }
   bool mClockwise;
}
#endregion

#region class ArcTangentMaker ----------------------------------------------------------------------
[DwgCmd (ECmd.ArcTangent), CanRepeat]
class ArcTangentMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Start = Point2.Zero;
   [Textbox (1), Phase (1), Angle] double Angle {
      get => mAngle;
      set => (mAngle, mTangentPoint) = (value, Start.Polar (100, value));
   }
   static double mAngle = 0;
   [Textbox (2), Click (2)] static Point2 End = Point2.Zero;

   [Click (1)] Point2 TangentPoint {
      get => mTangentPoint;
      set => (mTangentPoint, mAngle) = (value, Start.AngleTo (value));
   }
   Point2 mTangentPoint = Point2.Nil;

   // Methods ------------------------------------------------------------------
   public override Poly Make (int phase) {
      if (phase == 1) return Poly.Line (Start, TangentPoint);
      return Poly.Arc (Start, mAngle, End);
   }
}
#endregion

#region class PolyEdgeMaker ------------------------------------------------------------------------
/// <summary>Implements the POLY EDGE command</summary>
[DwgCmd (ECmd.PolyEdge), CanRepeat]
class PolyEdgeMaker : PolyMaker {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] int Sides { get => mSides; set => mSides = value.Clamp (3, 360); }
   static int mSides = 6;
   [Textbox (1), Click (0)] static Point2 StartPoint = Point2.Zero;
   [Textbox (2), Click (1)] static Point2 EndPoint = Point2.Zero;

   // Methods ------------------------------------------------------------------
   /// <summary>Creates a polygon using the initial two vertices</summary>
   public override Poly Make (int _) {
      List<Point2> pts = [StartPoint, EndPoint];
      var sides = Sides; var vertexAng = -((sides - 2) * Math.PI) / sides;
      for (int i = 3; i <= sides; i++)
         pts.Add (pts[^1] + (pts[^2] - pts[^1]).Rotated (vertexAng));
      return Poly.Lines (pts, true);
   }
}
#endregion

#region class ParallelLineMaker --------------------------------------------------------------------
/// <summary>Implements the LineParallel command</summary>
[DwgCmd (ECmd.LineParallel)]
class ParallelLineMaker : PolyMaker2 {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] static double Offset = 0;
   [Click (1)] Point2 ThruPt = Point2.Zero;

   // Methods ------------------------------------------------------------------
   protected override Poly[]? Make (E2Poly poly, int segIndex)
      => [.. MakeParallel (poly.Poly[segIndex], ThruPt, Offset, DwgHub.CtrlPressed)];

   static IEnumerable<Poly> MakeParallel (Seg seg, Point2 thruPt, double offset, bool ctrlPressed) {
      offset = Math.Abs (offset);
      if (seg.IsLine) {
         // Determine the side
         int side = thruPt.Side (seg.A, seg.B);
         if (side == 0) yield break; // ThruPt on the line
         if (offset.IsZero ()) offset = thruPt.DistToLine (seg.A, seg.B);
         double slope = seg.Slope + side * Lib.HalfPI;
         yield return Poly.Line (seg.A.Polar (offset, slope), seg.B.Polar (offset, slope));
         if (ctrlPressed)
            yield return Poly.Line (seg.A.Polar (-offset, slope), seg.B.Polar (-offset, slope));
      } else {
         // Determine whether inside or outside
         var (radius, dist) = (seg.Radius, seg.Center.DistTo (thruPt));
         if (dist.EQ (radius)) yield break; // ThruPt on the arc
         if (offset.IsZero ())
            offset = dist - radius; // +ve for outside, -ve for inside
         else if (dist < radius)
            offset = -offset; // Offset is given; switch the polarity if thruPt is inside.
         var (sa, ea) = seg.GetStartAndEndAngles ();
         var newRad = radius + offset;
         if (newRad > Lib.Epsilon)
            yield return seg.IsCircle ? Poly.Circle (seg.Center, newRad)
               : Poly.Arc (seg.Center, newRad, sa, ea, seg.IsCCW);
         newRad = radius - offset;
         if (ctrlPressed && newRad > Lib.Epsilon)
            yield return seg.IsCircle ? Poly.Circle (seg.Center, newRad)
               : Poly.Arc (seg.Center, newRad, sa, ea, seg.IsCCW);
      }
   }
}
#endregion

#region class PerpendicularLineMaker ---------------------------------------------------------------
/// <summary>Implements the LinePerp command</summary>
[DwgCmd (ECmd.LinePerp), NoKeyboardMode]
class PerpendicularLineMaker : PolyMaker2 {
   // Fields -------------------------------------------------------------------
   [Textbox (0)] static double Length = 0;
   [Click (1)] Point2 ThruPt = Point2.Nil;

   // Overrides ----------------------------------------------------------------
   protected override Poly[]? Make (E2Poly poly, int segIndex) =>
       Perpendicular (poly.Poly[segIndex], ThruPt, Length, DwgHub.CtrlPressed) is { } r ? [r] : null;

   // Implementation -----------------------------------------------------------
   /// <summary>Creates a perpendicular line to the given segment at the point nearest to end point.</summary>
   /// <param name="seg">The segment (line or arc) to construct the perpendicular from.</param>
   /// <param name="endPt">The point to determine where the perpendicular touches the segment.</param>
   /// <param name="len">Length of the perpendicular; if zero, uses distance to end point.</param>
   /// <param name="ctrlPressed">If true, the line is drawn symmetrically; otherwise, it extends in one direction</param>
   /// <returns>A new polyline for the perpendicular line, or null if it cannot be created</returns>
   static Poly? Perpendicular (Seg seg, Point2 endPt, double len, bool ctrlPressed) {
      // Compute the foot of the perpendicular.
      Point2 basePt = seg.IsArc ? seg.GetPointAt (seg.GetLie (endPt))
         : endPt.SnappedToLine (seg.A, seg.B);
      var dist = basePt.DistTo (endPt);
      if (dist.IsZero ()) return null;
      if (!len.IsZero ())
         endPt = (len / dist).Along (basePt, endPt);
      if (ctrlPressed)
         basePt = (-1.0).Along (basePt, endPt);
      return Poly.Line (basePt, endPt);
   }
}
#endregion