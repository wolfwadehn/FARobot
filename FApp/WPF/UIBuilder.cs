// ╔═╦╗
// ║╬╠╬╦╗ UIBuilder.cs
// ║╔╣╠║╣ UI skin components
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace FApp;

#region TableEdit ----------------------------------------------------------------------------------
/// <summary>UI component to handle tabular grid display</summary>
class TableEdit : UserControl {
   // Interface ----------------------------------------------------------------
   public TableEdit (EditTableVM tableVM) {
      mRowSelectors = new Border[tableVM.Rows];
      mCells = new Border[tableVM.Rows, tableVM.ColNames.Count];
      Content = new ScrollViewer () {
         HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
         VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
         Content = PrepareGrid (TableVM = tableVM)
      };
   }
   public readonly EditTableVM TableVM;

   public void SelectRow (int row) {
      if (mSelectedRow == row) return;
      for (int i = 0, c = TableVM.Rows; i < c; i++) {
         mRowSelectors[i].Background = i == row ? Brushes.LightCyan : Brushes.WhiteSmoke;
      }
      mSelectedRow = row;
      TableVM.SelectedRow = row;
   }
   int mSelectedRow = -1;

   public object GetRowObject () => TableVM.GetBaseObject (mSelectedRow);

   public void Refresh () {
      mSelectedRow = -1; // Resets the stored selected row
      ((ScrollViewer)Content).Content = PrepareGrid (TableVM);
   }

   // Implementation -----------------------------------------------------------
   Grid PrepareGrid (EditTableVM tableVM) {
      var (cRow, cCol) = (tableVM.Rows, tableVM.ColNames.Count);

      Grid g = new ();
      for (int i = 0; i <= cRow; i++) // Accommodate header row
         g.RowDefinitions.Add (new RowDefinition () { Height = GridLength.Auto });
      var colWidths = new GridLength[3] { new (10, GridUnitType.Star), new (2, GridUnitType.Star), new (4, GridUnitType.Star) };
      for (int i = 0; i < cCol; i++)
         g.ColumnDefinitions.Add (new ColumnDefinition () { Width = colWidths[i] });

      // Hydrate the table header
      for (int c = 0; c < cCol; c++) {
         var tb = new TextBlock {
            Text = tableVM.ColNames[c], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch,
         };
         var b = new Border () {
            Padding = new Thickness (4.8, 1.2, 4.8, 1.2), SnapsToDevicePixels = true,
            BorderBrush = Brushes.DarkGray,
            BorderThickness = new Thickness (c == 0 ? 1 : 0, 1, 1, 1),
            Child = tb,
         };
         Grid.SetRow (b, 0); Grid.SetColumn (b, c);
         g.Children.Add (b);
      }

      // Hydrate the table body
      for (int r = 0; r < cRow; r++) {
         var rowSel = new Border () { Background = Brushes.Transparent };
         Grid.SetRow (rowSel, r + 1); Grid.SetColumnSpan (rowSel, cCol);
         g.Children.Add (mRowSelectors[r] = rowSel);
         for (int c = 0; c < cCol; c++) {
            var b = new Border () {
               Padding = new Thickness (4.8, 1.2, 4.8, 1.2), SnapsToDevicePixels = true,
               BorderBrush = Brushes.DarkGray,
               BorderThickness = new Thickness (c == 0 ? 1 : 0, 0, 1, 1),
               Background = Brushes.Transparent
            };
            b.Child = GetCellContent (r, c);
            Grid.SetRow (b, r + 1); Grid.SetColumn (b, c);
            b.Tag = Tuple.Create (r, c);
            AddClickHandler (b, OnClick);
            g.Children.Add (mCells[r, c] = b);
         }
      }
      return g;
   }
   Border[] mRowSelectors;
   Border[,] mCells;

   UIElement GetCellContent (int row, int col) {
      return TableVM.ColSemantics[col] switch {
         EditTableVM.ESemantics.Color => new Border () {
            Margin = new Thickness (4, 2, 4, 2), CornerRadius = new CornerRadius (2),
            Background = Util.MakeBrush ((Color4)TableVM.Get (row, col))
         },
         _ => new TextBlock {
            Text = TableVM.Get (row, col).ToString (),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
         },
      };
   }

   // When the mouse is clicked on a cell, this method is called
   void OnClick (object sender, RoutedEventArgs e) {
      Border b = (Border)sender;
      (int row, int col) = (Tuple<int, int>)b.Tag;
      SelectRow (row);
   }

   void UpdateCell (int row, int col) {
      mCells[row, col].Child = GetCellContent (row, col);
   }

   /// <summary>Handles click-like events on controls which are not exactly buttons or menus</summary>
   /// A click event is generated after a mouse-down followed by mouse-up on the same control. The handler is
   /// called on the mouse-up event.
   static void AddClickHandler (UIElement elem, RoutedEventHandler handler) {
      elem.MouseDown += async (s, e) => {
         elem.MouseUp += UpHandler;
         await Task.Delay (750);
         elem.MouseUp -= UpHandler;
      };
      // Helper ..............................
      void UpHandler (object s, MouseButtonEventArgs e) { handler (s, e); }
   }
}
#endregion
