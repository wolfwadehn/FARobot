// ╔═╦╗
// ║╬╠╬╦╗ DwgHub.cs
// ║╔╣╠║╣ Central coordinator for Dwg editing (tracks current Widget, current Dwg etc)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp;

abstract class DragOp {
   // [x] Source the mouse/keyboard events! [DwgHub.OnMouse & OnKey handler could help here]

   // Overridables -------------------------------------------------------------
   /// <summary>This is overridden in a derived class</summary>
   protected abstract void Action (EAct act);

   public virtual void Draw () { }

   protected void Redraw () => VN?.Redraw ();

   // Nested types -------------------------------------------------------------
   /// <summary>This is passed to Action to signal the phase of mouse-dragging</summary>
   protected enum EAct {
      /// <summary>Start the mouse-drag loop</summary>
      Start,
      /// <summary>Mouse has moved to a new position</summary>
      Move,
      /// <summary>Mouse drag operation has finished (user has released mouse button)</summary>
      Finish,
      /// <summary>Mouse-drag operation got cancelled (element lost focus, typically)</summary>
      Cancel
   }

   // Private ------------------------------------------------------------------
   /// <summary>This is the DragOpVModel (if any) that is handling the drawing for this drag-operation</summary>
   /// this DragOp
   internal VNode? VN { get; set; }
}

class DragOpVN : VNode {
   public DragOpVN (DragOp dragger) => (DragOp, dragger.VN) = (dragger, this);

   public DragOp DragOp { get; private set; }

   public override void SetAttributes () {
      Lux.Color = Color4.DarkGreen;
      Lux.LineWidth = 1.5f;
   }

   public override void Draw () => DragOp.Draw ();
}

/// <summary>Placeholder for current drag-operation in progress</summary>
[Singleton, RedrawOnZoom]
partial class ActiveDraggersVN : VNode {
   // Overrides
   public override VNode? GetChild (int n) => mDraggers.SafeGet (n);

   // Implementation
   public static void Add (DragOp dragOp) {
      mDraggers.Add (new DragOpVN (dragOp));
      It.ChildAdded ();
   }

   public static void Remove (DragOp dragOp) {
      for (int i = mDraggers.Count - 1; i >= 0; i--) {
         var dragger = mDraggers[i];
         if (dragger.DragOp == dragOp) {
            It.ChildRemoved (dragger);
            mDraggers.RemoveAt (i);
         }
      }
   }

   readonly static List<DragOpVN> mDraggers = [];
}

abstract class DragOp2D : DragOp {
   protected DragOp2D () => StartListening ();

   /// <summary>The 2-D point where we started dragging</summary>
   protected Point2 Anchor;
   /// <summary>The 'previous' location of the drag point (the last time the Action virtual function was called)</summary>
   protected Point2 PtPrev { get; private set; }
   /// <summary>The current location of the drag point</summary>
   protected Point2 Pt { get; private set; }

   // Interested in mouse position (in the drawing), and click button type (left/right/middle)
   void OnMouse ((Point2 RawPt, bool LeftPressed) m) {
      Pt = m.RawPt;
      EAct act = EAct.Move;
      if (miFirst) {
         Anchor = PtPrev = m.RawPt;
         miFirst = false;
         act = EAct.Start;
      }
      if (!m.LeftPressed) { // Drag is over!
         act = EAct.Finish;
         StopListening ();
      }
      Action (act);
      PtPrev = Pt;
   }
   bool miFirst = true;

   // Interested only in ESC key being hit
   void OnEsc () {
      StopListening ();
      Action (EAct.Cancel);
   }

   // Start listening to mouse events, redraw, etc
   void StartListening () {
      DwgHub.sDragDriver = OnMouse;
      ActiveDraggersVN.Add (this);
   }

   // Stop listening to mouse events, redraw, etc
   void StopListening () {
      DwgHub.sDragDriver = null;
      ActiveDraggersVN.Remove (this);
   }
}

abstract class RectSelector : DragOp2D {
   protected override void Action (EAct act) {
      Redraw ();
      if (act == EAct.Finish) Select (new Bound2 ([Anchor, Pt]), Anchor.X > Pt.X);
   }

   public override void Draw () {
      Lux.Poly (Poly.Rectangle (new Bound2 ([Anchor, Pt])));
   }

   protected abstract void Select (Bound2 b, bool iCrossing);
}

class Ent2Selector : RectSelector {
   public Ent2Selector (Dwg2 dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   protected override void Select (Bound2 b, bool iCrossing) {
      var shiftPressed = DwgHub.ShiftPressed;
      foreach (var e2 in mDwg.Ents) {
         if (shiftPressed && e2.IsSelected) continue;
         e2.IsSelected = iCrossing
            ? !e2.Bound.Contains (b) && ! (b * e2.Bound).IsEmpty
            : b.Contains (e2.Bound);
      }
   }
}
