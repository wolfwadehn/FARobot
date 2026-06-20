// ╔═╦╗
// ║╬╠╬╦╗ DwgCmds.cs
// ║╔╣╠║╣ Various 'commands' for Dwg (most invoke a modal dialog to edit settings or do operations)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Widgets;
namespace FApp;

#region class CmdPrompt ----------------------------------------------------------------------------
/// <summary>Contains information about a drawing command (Name, Input box Labels, Step prompts etc)</summary>
class CmdPrompt {
   // Properties ---------------------------------------------------------------
   /// <summary>The Cmd that this prompt is for</summary>
   public readonly ECmd Cmd;
   /// <summary>The name of the command</summary>
   public readonly string Name;
   /// <summary>The labels for the input boxes (text labels for input boxes, text of checkboxes)</summary>
   public readonly string[] Labels;
   /// <summary>Stepwise prompts for the labels (includes basic markup)</summary>
   public readonly string[] Prompts;

   // Methods ------------------------------------------------------------------
   /// <summary>Tries to get the prompt for a Cmd, or gives null if it can't</summary>
   public static CmdPrompt? TryGet (ECmd command) {
      if (sDict == null) {
         // First time we get this, we load the commands
         sDict = [];
         var lines = Lib.ReadLines ("pix:Modes/cmd-prompt.txt");
         for (int i = 0; i < lines.Length; i++) {
            ECmd cmd = Enum.Parse<ECmd> (lines[i++].Trim ());
            if (cmd == ECmd.Nil) break;
            var w = lines[i++].Split ('|', StringSplitOptions.TrimEntries);
            string[] labels = [.. w.Skip (1)];
            List<string> prompts = [];
            for (; ; ) {
               string line = lines[i++];
               if (line.IsBlank ()) { i--; break; }
               prompts.Add (line.Trim ());
            }
            sDict.Add (cmd, new (cmd, w[0], labels, [.. prompts]));
         }
      }
      return sDict.GetValueOrDefault (command);
   }
   static Dictionary<ECmd, CmdPrompt>? sDict;

   /// <summary>Fetches the prompt for a given Cmd</summary>
   public static CmdPrompt Get (ECmd command) =>
      TryGet (command) ?? throw new Exception ($"No entry for {command} in cmd-prompt.txt");

   CmdPrompt (ECmd cmd, string name, string[] labels, string[] prompts)
      => (Cmd, Name, Labels, Prompts) = (cmd, name, labels, prompts);
}
#endregion

#region class DwgCmds ------------------------------------------------------------------------------
/// <summary>Implements handlers for some drawing commands that don't need mouse/keyboard interaction</summary>
/// These would be commands like GridSetup, LayerSetup etc
public static class DwgCmds { // Made "public" for testing
   [DwgCmd (ECmd.Grid)]
   static void GridSettings (Dwg2 dwg) {
      var grid = dwg.Grid;
      dwg.Grid = grid.WithVisible (!grid.Visible);
   }

   [DwgCmd (ECmd.MarkOpenPlines)]
   public static void DoMarkOpenPolys (Dwg2 dwg) {
      List<Point2> pts = [];
      foreach (var e2p in dwg.Ents.OfType<E2Poly> ().Where (a => a.Layer.Name == "0")) {
         var poly = e2p.Poly;
         if (poly.IsOpen) { pts.Add (poly.A); pts.Add (poly.B); }
      }
      dwg.ResetCleanupmarkers (CmdPrompt.Get (ECmd.MarkOpenPlines).Name, pts);
   }

   [DwgCmd (ECmd.LayerSetup)]
   static void LaunchLayersDlg (Dwg2 dwg) => LayersDlg.Launch (dwg);

   [DwgCmd (ECmd.DeleteHelperLines)]
   static void DoDeleteHelperLines (Dwg2 dwg) {
      var layer = dwg.GetHelperLayer ();
      List<Ent2> rmv = [.. dwg.Ents.Where (a => a.Layer == layer)];
      if (rmv.Count > 0) new ModifyDwgEnts (dwg, "Delete Helper Lines", [], rmv).Push ();
   }

   [DwgCmd (ECmd.HelperLayer)]
   static void GotoHelperLayer (Dwg2 dwg) {
      var layer = dwg.GetHelperLayer ();
      dwg.Layers.Current = layer;
   }

   [DwgCmd (ECmd.Robo)]
   static void ShowRobot (Dwg2 _) => DwgHub.OpenRobotFn?.Invoke ();

   [DwgCmd (ECmd.Cleanup)]
   public static void DoPurgeMarkers (Dwg2 dwg) {
      // Simply purge the "CleanupMarker" layer
      var layer = dwg.GetCleanupLayer ();
      List<Ent2> rmv = [.. dwg.Ents.Where (a => a.Layer == layer)];
      if (rmv.Count > 0)
         new ModifyDwgEnts (dwg, "Cleanup", [], rmv).Push ();

   }
}
#endregion

#region class PixDwgExtns --------------------------------------------------------------------------
/// <summary>Localizes FApp related expectations to avoid polluting Nori.Dwg2 interface</summary>
static class PixDwgExtns {
   public static void ResetCleanupmarkers (this Dwg2 dwg, string description, IList<Point2> pts) {
      try {
         new ClubbedStep (dwg, description).Push ();
         var layer = dwg.GetCleanupLayer ();
         List<Ent2> rmv = [.. dwg.Ents.Where (a => a.Layer == layer)], add = [];
         double radius = dwg.Bound.Diagonal / 100;
         foreach (var pt in pts) {
            add.Add (new E2Poly (layer, Poly.Circle (pt, radius)));
            add.Add (new E2Point (layer, pt));
         }
         if (add.Count > 0 || rmv.Count > 0)
            new ModifyDwgEnts (dwg, description, add, rmv).Push ();
      } finally {
         UndoStack.Current?.ClubSteps ();
      }
   }

   public static void ResetCleanupmarkers (this Dwg2 dwg, string description, IList<Poly> polys) {
      try {
         new ClubbedStep (dwg, description).Push ();
         var layer = dwg.GetCleanupLayer ();
         List<Ent2> rmv = [.. dwg.Ents.Where (a => a.Layer == layer)], add = [];
         add.AddRange (polys.Select (p => new E2Poly (layer, p)));
         if (add.Count > 0 || rmv.Count > 0)
            new ModifyDwgEnts (dwg, description, add, rmv).Push ();
      } finally {
         UndoStack.Current?.ClubSteps ();
      }
   }

   /// <summary>Move selected entities to this layer</summary>
   public static void MoveSelectedTo (this Dwg2 dwg, Layer2 layer) { 
      var moveEnts = dwg.Ents.Where (a => a.IsSelected);
      new ModifyEntLayer (dwg, "Layer changed", moveEnts, layer).Push ();
   }

   /// <summary>Select all entities in this layer</summary>
   public static void SelectLayerEnts (this Dwg2 dwg, Layer2 layer) {
      dwg.Select (null, deselectOthers: true);
      dwg.Ents.Where (a => a.Layer == layer).ForEach (a => a.IsSelected = true);
   }

   public static void PurgeUnusedLayers (this Dwg2 dwg) {
      List<Layer2> rmv = [.. dwg.Layers.Where (a => a.Name != "0" && !dwg.Ents.Any (ent => ent.Layer == a))];
      if (rmv.Count == 0) return;
      try {
         new ClubbedStep (dwg, "Purge Unused Layers").Push ();
         rmv.ForEach (l2 => dwg.Layers.Remove (l2));
      } finally {
         UndoStack.Current?.ClubSteps ();
      }
   }

   /// <summary>Gets "CleanupMarker" layer; Creates it if it does not exist already</summary>
   public static Layer2 GetCleanupLayer (this Dwg2 dwg)
      => dwg.GetLayer ("CleanupMarker", Color4.Red, ELineType.Continuous);

   public static Layer2 GetDimLayer (this Dwg2 dwg)
      => dwg.GetLayer ("Dimension", Color4.DarkGreen, ELineType.Continuous);

   public static Layer2 GetHelperLayer (this Dwg2 dwg)
      => dwg.GetLayer ("helper", Color4.Yellow, ELineType.Continuous);

   public static Layer2 GetDefaultLayer (this Dwg2 dwg) {
      _ = dwg.Layers.Current; // Force creation of layer "0"
      var layer = dwg.Layers.FirstOrDefault (a => a.Name == "0");
      if (layer == null) {
         Lib.Trace ("Missing layer \"0\"");
         layer = new Layer2 ("0", Color4.Black, ELineType.Continuous);
         dwg.Layers.Add (layer);
      }
      return layer;
   }

   static Layer2 GetLayer (this Dwg2 dwg, string name, Color4 clr, ELineType lineStyle) { // Plumbing!
      _ = dwg.Layers.Current; // Force creation of layer "0"
      var layer = dwg.Layers.FirstOrDefault (a => a.Name == name);
      if (layer == null) {
         layer = new Layer2 (name, clr, lineStyle);
         dwg.Layers.Add (layer);
      }
      return layer;
   }
}
#endregion

#region enum EDwgCmd -------------------------------------------------------------------------------
/// <summary>Enumerates the various drawing commands we support</summary>
/// Some of these commands (like Arc, Line, Circle) require keyboard and mouse interaction and
/// are implemented via Widget classes (like LineMaker, ArcMaker). When the command is invoked,
/// the corresponding widget is created and starts handling mouse / keyboard input to get the
/// command completed.
/// Other commands (like GridSetup, LayerSetup) are implemented as methods in the DwgCmds class.
/// These typically invoke a dialog to get input from the user to get the command completed
public enum ECmd {
   Arc = 1, Arc2P, Arc3P, ArcTangent, AutoDim, Chamfer, Circle, Circle2P, Circle3P, CircleTTR,
   CircleTTT, Cleanup, CommonShapes, CopyArcNotch, CopyCornerNotch, CopyEdgeNotch, CopyToOtherEdge,
   CornerStep, CreateBlock, CreateSpline, Decurve, DeleteHelperLines, DeleteNotch, Dim2P, DimAngle, DimBaseline, DimAngle3P,
   DimCallout, DimDiameter, DimContinue, DimHorzOrdinate, DimRadius, DimSegment, DimSettings,
   DimVertOrdinate, Divide, EdgeRecess, EdgeU, EdgeV, EditBend, EditSpline, ExplodeBlock, Extend,
   Fillet, Fillet3T, FitArc, Grid, HelperLayer, HelperLineH, HelperLineV, HighlightOverlaps, InFillet, Info, InsertBlock, Intersect,
   IrregularArray, Join, KeySlot, LayerSetup, Line, LineFillet, LineParallel, LinePerp, Lines,
   LineTangent, MarkLargeRadius, MarkNonTangent, MarkOpenPlines, MarkSmallSegs, MarkText, Measure,
   Mirror, MirrorNotch, Move, Offset, OffsetSeg, ParametricDucts, Pick, PlineToSpline, Point,
   PolarArray, PolyCircumscribe, PolyEdge, PolyInscribe, Rect, RectArray, RectCenter,
   RemoveSmallSegs, Robo, Rotate, RoughTrim, Scale, ShiftOrigin, Subtract, Trim, TruetypeText, Union,

   Nil
}
#endregion

#region ModifyEntLayer ----------------------------------------------------------------------------=
/// <summary>Undoable entity layer switch step</summary>
class ModifyEntLayer : UndoStep {
   public ModifyEntLayer (Dwg2 dwg, string desc, IEnumerable<Ent2> ents, Layer2 toLayer) : base (dwg, desc)
      => (mEnts, mOldLayers, mNewLayer) = ([.. ents], [.. ents.Select (ent => ent.Layer)], toLayer);

   public override void Step (EUndoDir dir) {
      if (dir == EUndoDir.Undo) { // Restore old layer assignments
         for (int i = 0; i < mEnts.Count; i++)
            mEnts[i].Layer = mOldLayers[i];
      } else
         mEnts.ForEach (ent => ent.Layer = mNewLayer);
   }

   readonly List<Ent2> mEnts;
   readonly List<Layer2> mOldLayers;
   readonly Layer2 mNewLayer;
}
#endregion
