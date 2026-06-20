// ╔═╦╗
// ║╬╠╬╦╗ DwgScene.cs
// ║╔╣╠║╣ Implements DwgScene (scene to host a drawing, and some auxiliary widgets for drawing)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Widgets;
namespace FApp;

using static Util;

#region class DwgScene -----------------------------------------------------------------------------
/// <summary>A 2D scene that displays a 2D drawing</summary>
class DwgScene : Scene2 {
   public DwgScene (Dwg2 dwg) {
      BgrdColor = new (216, 216, 224);
      Bound = (mDwg = dwg).Bound.InflatedF (1.05);
      Root = new GroupVN ([new Dwg2VN (mDwg), WidgetVN.It, ActiveDraggersVN.It,
                           new DwgFillVN (mDwg),
                           new DwgGridVN (mDwg, false), new DwgGridVN (mDwg, true), DwgSnapVN.It,
                           DwgConsLineVN.It, TraceVN.It]);
   }
   readonly Dwg2 mDwg;

   public void ShowFoldPreview (bool iShow) {
      if (!iShow)
         Lux.RemoveSubScene (mFoldScene!);
      else
         Lux.AddSubScene (mFoldScene = new FoldedPartScene (mDwg), new (0.75, 0.75, 0.95, 0.95));
   }
   FoldedPartScene? mFoldScene;

   public void RefreshFoldPreview () => Lib.Post (() => mFoldScene!.Refresh ());

   public override bool CursorVisible => false;

   public override void ZoomExtents () {
      Bound = mDwg.Bound.InflatedF (1.05);
      base.ZoomExtents ();
   }
}
#endregion

#region class DwgGridVN ----------------------------------------------------------------------------
/// <summary>VNode that draws the grid underneath the drawing</summary>
/// This uses Dwg.Grid to get some properties of the grid like Visibility,
/// Pitch, Subdivisions. The main grid lines (at a spacing of Grid.Pitch)
/// are drawn with a darker gray pen, and the subdivision lines are drawn with
/// a lighter gray pen (so simulate a typical graph-paper). Note that we
/// actually instantiate two DwgGridVN objects - one to draw the darker main lines
/// and the other to draw the lighter subdivision lines (that's the meaning of
/// the subdiv parameter in the constructor)
[RedrawOnZoom]
class DwgGridVN : VNode {
   // Constructor --------------------------------------------------------------
   // Makes a DwgGridVN to draw either the main grid lines at spacing of
   // Grid.Pitch (if subdiv=false) or the lighter subdivision lines between them
   // (if subdiv=true)
   public DwgGridVN (Dwg2 dwg, bool subdiv) : base (dwg)
      => (mDwg, mSub, mPitch) = (dwg, subdiv, dwg.Grid.Pitch);

   // Overrides ----------------------------------------------------------------
   // Draw is called whenever the view is zoomed or panned (because we have the
   // [RedrawOnZoom] attribute attached to this class). At that point, SetAttributes
   // will be called first, and that calls Compute, which computes the visible
   // bound of the drawing in world space coordinates. That's the part of the drawing
   // that's visible, and we draw the grid only in that space.
   //
   // As we zoom out, the grid lines get closer and closer together. The Compute()
   // routine uses this spacing to start 'dimming' the lines (by reducing the
   // mAlpha down from 1.0 all the way to 0.0 if the lines are less than about 10
   // pixels apart). So if we are sufficiently zoomed out, nothing will be drawn
   // (to avoid an excessively dense grid).
   public override void Draw () {
      // First check if we can exit without drawing anything. That's the case
      // if we are so zoomed out that the inter-line spacing is less than 10 pixels
      // or if the grid has simply been turned off.
      mPts.Clear ();
      var grid = mDwg.Grid;
      if (mAlpha.IsZero () || !grid.Visible) return;

      // Prepare to draw the vertical lines, by figuring out a starting X value, an
      // X step and the number of lines to iterate. Note that the da value below is
      // the spacing between the minor lines (Pitch / SubDiv).
      var (a0, da, n) = Get (mBound.X);
      var (b0, b1) = mBound.Y;
      for (int i = 0; i <= n; i++) {
         // da is the spacing between minor lines. So, if we assume the SubDiv is
         // set to 5, then:
         // - when we are drawing major lines, draw only every 5th line
         // - when we are drawing minor lines, draw everything EXCEPT every 5th line
         // and that's what the if statement below is checking
         double a = a0 + i * da;
         if (mSub ^ (i % grid.Subdivs != 0)) continue;
         // Draw vertical lines, where ymin and ymax are set based on the visible bounds
         // bottom and top edges.
         mPts.Add (new (a, b0)); mPts.Add (new (a, b1));
      }
      // Do a similar loop, now with the horizonal lines
      (a0, da, n) = Get (mBound.Y);
      (b0, b1) = mBound.X;
      for (int i = 0; i <= n; i++) {
         double a = a0 + i * da;
         if (mSub ^ (i % grid.Subdivs != 0)) continue;
         mPts.Add (new (b0, a)); mPts.Add (new (b1, a));
      }
      // We got all the lines to be drawn, issue a Lux.Lines call
      Lux.Lines (mPts.AsSpan ());

      // Helper ..................................
      // Given an extent (either X or Y), this computes a starting line ordinate,
      // the step between lines, and the number of lines to cover the entire extent.
      (double Start, double Step, int Count) Get (Bound1 bound) {
         double f0 = mPitch * Math.Floor (bound.Min / mPitch);
         double f1 = mPitch * Math.Ceiling (bound.Max / mPitch);
         double pitch = mPitch / grid.Subdivs;
         return (f0, pitch, (int)((f1 - f0) / pitch + 0.001));
      }
   }
   List<Vec2F> mPts = [];

   // If we see an EProp.Grid change notitication, the grid settings of the drawing
   // have changed, and we do a redraw
   protected override void OnChanged (EProp prop) {
      if (prop == EProp.Grid) Redraw ();
      base.OnChanged (prop);
   }

   // Each time the geometry is tagged dirty (which happens when the grid settings change,
   // or the view bounds change), we call Compute to figure out the new view bounds in
   // model space, and also the opacity of the lines (which depends on the spacing)
   public override void SetAttributes () {
      if (mGeometryDirty) Compute ();
      // Pick the shade of gray based on whether we are drawing the main grid lines
      // or the subdivisions - the alpha value (opacity) of those lines has been set up
      // already by the Compute routine
      int r = mSub ? 200 : 184;
      int alpha = ((int)(mAlpha * 255)).Clamp (0, 255);
      Lux.Color = new (alpha, r, r, r + 8);
      Lux.ZLevel = mSub ? -6 : -5;
   }

   // Implementation -----------------------------------------------------------
   void Compute () {
      // Compute the bound (in model space) of the area being displayed in the viewport
      Point2 p0 = ToWorld (Vec2S.Zero), p1 = ToWorld (Lux.PanelSize);
      mBound = new Bound2 ([(Point2)p0, (Point2)p1]).InflatedF (1.01);
      // Compute the pitch between the lines
      double pitch = mPitch / (mSub ? mDwg.Grid.Subdivs : 1);
      // Convert that pitch into pixels
      pitch *= Lux.PanelSize.Y / (p0.Y - p1.Y);
      // Compute an opacity that smoothly changes as we zoom in our out
      // - If the spacing between lines is less than 10 pixels, the lines are invisible
      //   (opacity of 0)
      // - If the spacing between lines is more than 50 pixels, the line is fully opaque
      //   (opacity of 1)
      // Interpolate smoothly between these values
      mAlpha = ((pitch - 10) / 40).Clamp ();
   }
   Bound2 mBound;
   double mAlpha;

   // Private data -------------------------------------------------------------
   readonly Dwg2 mDwg;         // Drawing we're working with
   readonly double mPitch;    // The pitch of the grid (main lines)
   readonly bool mSub;        // If set, we're drawing the subdivision lines
}
#endregion

#region class DwgConsLineVN ------------------------------------------------------------------------
/// <summary>DwgConsLineVN draws the construction lines in the current drawing (generated by DwgSnap)</summary>
[Singleton, RedrawOnZoom]
partial class DwgConsLineVN : VNode {
   public override void Draw () {
      if (!DwgHub.MouseInside || DwgHub.HideSimCursor) return;
      if (DwgHub.Snap is not { } snapper) return;

      // We want to construction lines to stretch across the entire viewport. Rather than
      // finding the endpoints where it intersects the viewport, we just 'overdraw' and use
      // the default OpenGL scissor functionality to display only the pixels inside the viewport
      // (So use viewport diagonal to ensure the lines are long enough)
      double diag = mBound.Diagonal;
      Span<Vec2F> pts = stackalloc Vec2F[2];
      foreach (var (node, angle) in snapper.Lines) {
         pts[0] = node.Polar (diag, angle); pts[1] = node.Polar (-diag, angle);
         Lux.Lines (pts);
      }
   }

   public override void SetAttributes () {
      if (mGeometryDirty) {
         // Compute the bound (in model space) of the area being displayed in the viewport
         Point2 p0 = ToWorld (Vec2S.Zero), p1 = ToWorld (Lux.PanelSize);
         mBound = new Bound2 ([p0, p1]).InflatedF (1.01);
      }
      Lux.LineType = ELineType.Dot;
      Lux.Color = Color4.DarkGreen;
      Lux.LineWidth = 1.5f;
   }
   Bound2 mBound;
}
#endregion

#region class DwgSnapVN ----------------------------------------------------------------------------
/// <summary>DwgSnapVN is a singleton that draws snap indicators, construction lines for the current drawing</summary>
/// Here's what we want to draw:
/// 1. A + (plus) at the original input point (raw point)
/// 2. A x (cross) at the snapped point
/// 3. If the two are different from each other, a 'square' at the snap point to indicate that
///    we are 'snapped'
/// 4. If there is a valid snap, then a snap annotation text like 'endpoint', 'midpoint' etc.
///    This is drawn slightly _below_ the snap point
/// 5. If we have any construction lines active (mouse is within aperture of these lines), they
///    are drawn in dotted lines
/// 6. For each construction line, we draw the original source point (anchor) with a square, and
///    just _above_ that anchor point, we draw an annotatin like 'align:h', 'align:v' or
///    'align 35.2' (where 35.2 degrees is the slope of the alignment line)
/// This class draws 1, 2, 3, 4, 6 while the actual construction lines are drawn by the DwgConsLineVN
/// class (since that requires a different LineType, it is simplest to hive off that responsibility to
/// a separate VN)
[Singleton, RedrawOnZoom]
partial class DwgSnapVN : VNode {
   public override void Draw () {
      if (!DwgHub.MouseInside || DwgHub.HideSimCursor) return;
      if (DwgHub.Snap is not { } snapper) return;

      // Draw a + at the raw mouse input point (unsnapped)
      var (pt, dx) = (DwgHub.RawMousePos, DwgHub.PickAperture);
      var (x, y) = pt;
      // First, a '+' at the exact mouse position
      Span<Vec2F> pts = stackalloc Vec2F[4];
      pts[0] = new (x - dx, y); pts[1] = new (x + dx, y);
      pts[2] = new (x, y - dx); pts[3] = new (x, y + dx);
      Lux.Lines (pts);

      // Then, a x at the snapped mouse position
      (x, y) = (pt = DwgHub.MousePos); dx *= 0.7071;
      double x1 = x - dx, x2 = x + dx, y1 = y - dx, y2 = y + dx;
      pts[0] = new (x1, y1); pts[1] = new (x2, y2);
      pts[2] = new (x2, y1); pts[3] = new (x1, y2);
      Lux.Lines (pts);

      // If a snap is in effect, draw a small square at the snapped position
      if (snapper.ESnap != ESnap.None)
         DrawSquare (DwgHub.MousePos);

      int shift = (int)(2 * Lux.DPIScale);
      foreach (var (esnap, tpt, above) in snapper.Labels) {
         DrawSquare (tpt);
         string text = esnap.ToString ().ToLower ();
         var align = above ? ETextAlign.BotLeft : ETextAlign.TopLeft;
         var offset = new Vec2S ((int)(3.5 * Lux.DPIScale), above ? shift : -shift);
         Lux.Text2D (esnap.ToString ().ToLower (), tpt, align, offset);
      }

      // Helper ............................................
      static void DrawSquare (Point2 pt) {
         var (x, y) = pt;
         Span<Vec2F> pts = stackalloc Vec2F[8];
         double a = DwgHub.PickAperture;
         double x1 = x - a, x2 = x + a, y1 = y - a, y2 = y + a;
         pts[0] = pts[7] = new (x1, y1); pts[1] = pts[2] = new (x2, y1);
         pts[3] = pts[4] = new (x2, y2); pts[5] = pts[6] = new (x1, y2);
         Lux.Lines (pts);
      }
   }

   public override void SetAttributes () {
      Lux.Color = Color4.DarkGreen;
      Lux.LineWidth = 1.5f;
   }
}
#endregion

#region class WidgetVN -----------------------------------------------------------------------------
/// <summary>WidgetVN is a singleton that draws feedback for the current Widget</summary>
[Singleton, RedrawOnZoom]
partial class WidgetVN : VNode {
   public void SetWidget (Widget? widget) { mWidget = widget; Redraw (); }
   Widget? mWidget;

   public override void SetAttributes () { Lux.Color = Color4.Red; Lux.PointSize = 9f; Lux.ZLevel = 10; }
   public override void Draw () {
      if (mWidget is null) return;
      if (DwgHub.MouseInside || DwgHub.KeyboardMode || mWidget.AlwaysShowFeedback) 
         mWidget.DrawFeedback ();
   }
}
#endregion

#region class ModelSubScene ------------------------------------------------------------------------
class FoldedPartScene : Scene3 {
   public FoldedPartScene (Dwg2 dwg) {
      mDwg = dwg;
      Refresh ();
   }
   Dwg2 mDwg;

   public void Refresh () {
      var pf = new PaperFolder (mDwg);
      if (pf.Process (out mModel)) {
         Bound = mModel.Bound;
         BgrdColor = new Color4 (216, 252, 224);
         Root = new Model3VN (mModel);
      } else Root = null;
   }
   Model3? mModel;
}
#endregion
