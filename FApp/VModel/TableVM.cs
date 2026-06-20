// ╔═╦╗
// ║╬╠╬╦╗ TableVM.cs
// ║╔╣╠║╣ Table edit control backing view model
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp;

#region EditTableVM --------------------------------------------------------------------------------
/// <summary>Table editor server</summary>
public abstract class EditTableVM { // Generic tabular data VM - View will interact with this interface to show and edit tabular presentation
   public enum ESemantics { Name, Color, SelectOne, SelectOneCustom }
   public abstract int Rows { get; }
   public abstract IReadOnlyList<ESemantics> ColSemantics { get; }
   public abstract IReadOnlyList<string> ColNames { get; }
   public abstract object Get (int row, int col);
   public abstract int SelectedRow { get; set; }
   public abstract void SetRowItem (int row, int col, object value);
   public abstract object GetBaseObject (int row); // Underlying object being displayed in the table row!
   public abstract IReadOnlyList<CmdInfo> Cmds { get; }
}
#endregion

public class CmdInfo (string name, Action act) {
   public string Name => name;
   public void Execute () => act ();
}

#region LayersTableVM ------------------------------------------------------------------------------
// Exposes Nori.Dwg2 layers for editing!
class LayersTableVM : EditTableVM {
   public LayersTableVM (Dwg2 dwg) => mDwg = dwg;
   readonly Dwg2 mDwg;

   public override int Rows => mDwg.Layers.Count;

   public override IReadOnlyList<ESemantics> ColSemantics => [ESemantics.Name, ESemantics.Color, ESemantics.SelectOneCustom];

   public override IReadOnlyList<string> ColNames => ["Name", "Color", "Line style"];

   public override object Get (int row, int col) {
      Layer2 layer = mDwg.Layers[row];
      return col switch {
         0 => layer.Name,
         1 => layer.Color,
         2 => layer.Linetype,
         _ => throw new BadCaseException (string.Empty),
      };
   }

   public override int SelectedRow {
      get => mSelectedRow;
      set { mSelectedRow = value; mDwg.Layers.Current = mDwg.Layers[value]; }
   }
   int mSelectedRow;

   public override void SetRowItem (int row, int col, object value) {
      Layer2 layer = mDwg.Layers[row];
      var (name, color, linetype) = (layer.Name, layer.Color, layer.Linetype);
      switch (col) {
         case 0: name = (string)value; break;
         case 1: color = (Color4)value; break;
         case 2: linetype = (ELineType)value; break;
         default: throw new BadCaseException (string.Empty);
      }
      mDwg.Layers[row] = new Layer2 (name, color, linetype);
   }

   public override object GetBaseObject (int row) => mDwg.Layers[row];

   public override IReadOnlyList<CmdInfo> Cmds {
      get {
         return [
            new ("Move this layer entities to layer \"0\"", () => {
               var srcLayer = (Layer2)GetBaseObject (SelectedRow);
               if (srcLayer.Name == "0") return;
               mDwg.SelectLayerEnts (srcLayer);
               // Get layer "0"
               var destLayer = mDwg.GetDefaultLayer ();
               mDwg.MoveSelectedTo (destLayer);
               mDwg.Select (null, deselectOthers: true);
            }),
            new ("Select all entities in this layer",
               () => mDwg.SelectLayerEnts ((Layer2)GetBaseObject (SelectedRow))),
            new ("Move selected entities to this layer",
               () => mDwg.MoveSelectedTo ((Layer2)GetBaseObject (SelectedRow))),
         ];
      }
   }
}
#endregion
