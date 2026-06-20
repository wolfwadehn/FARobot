// ╔═╦╗
// ║╬╠╬╦╗ BaseWidget.cs
// ║╔╣╠║╣ Some widget base classes (like DwgWidget)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Widgets;

#region class DwgWidget ----------------------------------------------------------------------------
/// <summary>DwgWidget is the base class for all drawing related widgets</summary>
/// Construcing a DwgWiget requires that DwgHub.Dwg be set (the 'current' drawing)
abstract class DwgWidget : Widget {
   public DwgWidget ()
      => mDwg = DwgHub.Dwg ?? throw new Exception ("DwgHub.Dwg not set");
   protected readonly Dwg2 mDwg;

   // Helper to draw Ent2 feedback (Note: Draw attributes are not important)
   protected static void DrawFeedback (IEnumerable<Ent2> ents) {
      Lux.Polys ([.. ents.OfType<E2Poly> ().Select (e2p => e2p.Poly),
         .. ents.OfType<E2Text> ().SelectMany (e2t => e2t.Polys)]);
      Lux.Points ([.. ents.OfType<E2Point> ().Select (e2pt => e2pt.Pt)]);
      ents.OfType<E2Spline> ().ForEach (e2s => Lux.LineStrip (e2s.Pts));
      ents.OfType<E2Bendline> ().ForEach (DrawBendline);
      ents.OfType<E2Dim> ().ForEach (e2d => DrawFeedback (e2d.Ents));
      ents.OfType<E2Leader> ().ForEach (e2l => DrawFeedback (e2l.Ents));
      ents.OfType<E2Insert> ().Select (e2i => (e2i.Block.Ents, e2i.Xfm))
         .ForEach (t => DrawFeedback (t.Ents.Select (e => e * t.Xfm)));
      ents.OfType<E2Solid> ().ForEach (e2 => Lux.Quads (SolidPts (e2)));

      static void DrawBendline (E2Bendline eb) {
         Lux.Lines ([.. eb.Pts.Select (a => (Vec2F)a)]);
         string text = Math.Round (eb.Angle.R2D (), 2).ToString (System.Globalization.CultureInfo.InvariantCulture);
         if (text == "-0") text = "0";
         text = eb.Angle > 0 ? $"+{text}\u00b0" : $"{text}\u00b0";
         for (int i = 0; i < eb.Pts.Length; i += 2) {
            Point2 pt = eb.Pts[i].Midpoint (eb.Pts[i + 1]);
            Lux.Text2D (text, (Vec2F)pt, ETextAlign.MidCenter, Vec2S.Zero);
         }
      }

      static ReadOnlySpan<Vec2F> SolidPts (E2Solid e2) { // See E2SolidVN
         Vec2F[] mPoints = [.. e2.Pts.Select (pt => (Vec2F)pt)];
         (mPoints[2], mPoints[3]) = (mPoints[3], mPoints[2]);
         return mPoints;
      }
   }

   // Helper used to compose dimension hint text
   protected static string? ComposeDimText (string text, string tolerance) {
      var (textEmpty, tolEmpty) = (text.IsWhiteSpace (), tolerance.IsWhiteSpace ());
      if (textEmpty && tolEmpty) return null;
      if (tolEmpty) return text;
      return new StringBuilder ().Append (text).Append ('±').Append (tolerance).ToString ();
   }

   // Helper miscellaneous tasks
   protected static Seg? PickLineSeg (Dwg2 dwg, Point2 pt) {
      if (dwg.PickPoly (pt, DwgHub.PickAperture, out int nSeg, out double _) is { } poly) {
         var seg = poly.Poly[nSeg];
         return seg.IsLine ? seg : null;
      }
      return null;
   }

   protected static Seg? PickArcSeg (Dwg2 dwg, Point2 pt) {
      if (dwg.PickPoly (pt, DwgHub.PickAperture, out int nSeg, out double _) is { } poly) {
         var seg = poly.Poly[nSeg];
         return seg.IsArc ? seg : null;
      }
      return null;
   }

   protected static Ent2? SnapEnt (Dwg2 dwg, Point2 pt) {
      double threshold = DwgHub.PickAperture;
      Ent2? picked = null;
      foreach (var ent in dwg.Ents)
         if (ent.IsCloser (pt, ref threshold)) picked = ent;
      return picked;
   }

   // Helper used to add one entity to the drawing (with Undo)
   // Example: adding a Circle
   protected void Add (string description, Ent2 ent)
      => new ModifyDwgEnts (mDwg, description, [ent], []).Push ();

   // Helper used to add multiple entities into the drawing (with Undo)
   // Example: adding an array of entities
   protected void Add (string description, IEnumerable<Ent2> addEnts, IEnumerable<Ent2>? rmvEnts = null)
      => new ModifyDwgEnts (mDwg, description, addEnts, rmvEnts ?? []).Push ();
   
   // Helper used to replace one entity with another. 
   // For example: adding a Fillet to a Poly
   protected void Replace (string description, Ent2 e0, Ent2 e1)
      => new ModifyDwgEnts (mDwg, description, [e1], [e0]).Push ();
}
#endregion

#region class PolyMaker ----------------------------------------------------------------------------
/// <summary>PolyMaker is the base class for all widgets that create a single Poly</summary>
abstract class PolyMaker : DwgWidget {
   // Called when the command has been completed (all clicks are done, and we
   // have enough information to make the Poly)
   public override void Completed () {
      base.Completed ();
      if (Make (Phases - 1) is Poly p) Add (Prompt.Name, new E2Poly (mDwg.Layers.Current, p));
   }

   // We first call base.DrawFeedback to draw the general feedback to all the widgets.
   // Then, we draw the poly that is being constructed (this is not done when we are in mouse
   // mode and this is the first click)
   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phase > 0 && Make (Phase) is Poly p)
         Lux.Poly (p);
   }

   // Override this to actually make the Poly
   // Note that based on the phase, we can make some 'intermediate' poly that is only
   // used to draw feedback, but not the final poly that is added into the drawing
   abstract public Poly? Make (int phase);

   // This is called when we Ctrl+Click during phase 0. This means we want to
   // 'repeat' the last entity drawn, but just with a different 'starting point'.
   public override void Repeat (Point2 pt) {
      try {
         mRepeating = true;
         if (mPrevClick.Count > 0) {
            for (int i = 0; i < mPrevClick.Count; i++)
               Fields.First (a => a.NClick == i).SetValue (pt + (mPrevClick[i] - mPrevClick[0]));
            Completed ();
         }
      } finally {
         mRepeating = false;
      }
   }
}
#endregion

#region class DwgArranger --------------------------------------------------------------------------
/// <summary>Base class for all widgets which work with selected entities</summary>
/// These are:
///   Selected entity transformers: Move, Rotate, Scale, Mirror
abstract class DwgArranger : DwgWidget {
   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phases != Phase + 1) return;
      DrawFeedback ([.. Make ()]);
   }

   public override void Completed () {
      base.Completed ();
      List<Ent2> newEnts = [.. Make ()];
      if (newEnts.Count == 0) return;

      bool retain = DwgHub.CtrlPressed;
      if (retain) SelectedEntities.ForEach (a => a.IsSelected = false);

      // Add and select new entities
      Add (Prompt.Name, newEnts, retain ? [] : SelectedEntities);
      newEnts.ForEach (ent => ent.IsSelected = true);
      _selectedEntities = null;
   }

   /// <summary>Selected entities for derived widgets to work with</summary>
   protected IList<Ent2> SelectedEntities
      => _selectedEntities ??= [.. mDwg.Ents.Where (ent => ent.IsSelected)];
   List<Ent2>? _selectedEntities;

   abstract public IEnumerable<Ent2> Make ();
}
#endregion

#region class PointMaker ---------------------------------------------------------------------------
/// <summary>Implements the Point command</summary>
[DwgCmd (ECmd.Point)]
class PointMaker : DwgWidget {
   // Fields -------------------------------------------------------------------
   [Textbox (0), Click (0)] static Point2 Point = (0, 0);

   // Methods ------------------------------------------------------------------
   public override void Completed () {
      base.Completed ();
      Add (Prompt.Name, new E2Point (mDwg.Layers.Current, Point));
   }
}
#endregion

#region class PolyMaker2 --------------------------------------------------------------------------
/// <summary>Base class for editors that create parallel or perpendicular lines</summary>
/// Workflow:
///   Phase 1: User clicks a segment
///   Phase 2: User defines an endpoint (EndPoint) to set length/direction
abstract class PolyMaker2 : DwgWidget {
   /// <summary>First click input point used to select a polyline segment</summary>
   [Click (0), NoMouseMove]
   protected Point2 Pick {
      get => mPick;
      set {
         mPickedPoly = mDwg.PickPoly (mPick = value, DwgHub.PickAperture, out mPickedSeg, out _);
         if (mPickedPoly == null) HoldPhase ();
      }
   }
   Point2 mPick;

   /// <summary>Create new geometry from the picked polyline segment</summary>
   protected abstract Poly[]? Make (E2Poly poly, int seg);

   /// <summary>Called repeatedly while the command is in progress to show preview geometry</summary>
   /// Overrides PolyEditor's feedback to show new geometry instead of modified poly
   /// Don't call base.DrawFeedback() as we want different preview behavior
   public override void DrawFeedback () {
      if (mPickedPoly is null || Phase == 0) return;
      Make (mPickedPoly, mPickedSeg)?.ForEach (Lux.Poly);
   }

   /// <summary>Called when the command completes</summary>
   /// Overrides PolyEditor's completion to add new geometry instead of replacing
   /// Don't call base.Completed() as we want different completion behavior
   public override void Completed () {
      base.Completed ();
      var set = Make (mPickedPoly!, mPickedSeg);
      if (set != null) Add (Prompt.Name, set.Select (a => new E2Poly (mDwg.Layers.Current, a)));
   }

   // Fields -------------------------------------------------------------------
   E2Poly? mPickedPoly; // Polyline entity that was picked
   int mPickedSeg; // Segment index within that poly
}
#endregion

#region class DwgLayouter --------------------------------------------------------------------------
/// <summary>Base class for all widgets which _layout_ selected entities</summary>
/// There are two such widgets: RectArray and PolarArray
abstract class DwgLayouter : DwgWidget {
   public override void DrawFeedback () {
      base.DrawFeedback ();
      if (Phases != Phase + 1) return;
      DrawFeedback ([.. MakeCopies ()]);
   }

   public override void Completed () {
      base.Completed ();
      List<Ent2> newEnts = [.. MakeCopies ()];
      if (newEnts.Count == 0) return;

      // The targetted entities are already "selected".
      // Add and "also" select new entities
      Add (Prompt.Name, newEnts);
      newEnts.ForEach (ent => ent.IsSelected = true);
      _selectedEntities = null;
   }

   /// <summary>Selected entities for derived widgets to work with</summary>
   protected IEnumerable<Ent2> SelectedEntities
      => _selectedEntities ??= [.. mDwg.Ents.Where (ent => ent.IsSelected)];
   List<Ent2>? _selectedEntities;

   /// <summary>Creates transformed copies of the currently selected entities</summary>
   abstract public IEnumerable<Ent2> MakeCopies ();
}
#endregion
