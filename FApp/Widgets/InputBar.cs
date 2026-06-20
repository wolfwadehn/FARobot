// ╔═╦╗
// ║╬╠╬╦╗ InputBar.cs
// ║╔╣╠║╣ Implements the input-bar (has input boxes for various widget inputs, and prompt bar)
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Media;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using FApp.Widgets;
using System.Windows.Documents;
namespace FApp;

#region enum EInput --------------------------------------------------------------------------------
// The various types of inputs supported by an InputBar
enum EInput { Edit, Checkbox, Combobox };
#endregion

#region class InputBar -----------------------------------------------------------------------------
class InputBar {
   // Constructors -------------------------------------------------------------
   // The 'host' is the horizontal StackPanel where all the input boxes are displayed
   // along with the labels in front of them
   public InputBar (StackPanel host, TextBlock prompt) => (mPanel, mStatus) = (host, prompt);
   StackPanel mPanel;
   TextBlock mStatus;

   // Methods ------------------------------------------------------------------
   // Clear the UI completely
   public void Clear () { mPanel.Children.Clear (); mStatus.Inlines.Clear (); }

   // Called to create the UI for a particualar widget field
   public void CreateUI (WField field) {
      UIElement elem;
      mPanel.Focus ();
      switch (field.UIType) {
         case EInput.Edit:
            AddLabel ();
            int nWidth = field.GetFieldType () == typeof (Point2) ? 100 : 70;
            var tb = new TextBox { Tag = field.NInput, MinWidth = nWidth, Margin = new (0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, IsUndoEnabled = false };
            tb.TextChanged += (s, e) => OnTextChanged ((TextBox)s);
            tb.KeyDown += (s, e) => OnKeyDown (e.Key);
            tb.GotFocus += (s, e) => OnGotFocus ((TextBox)s);
            elem = tb;
            break;
         case EInput.Checkbox:
            var text = field.Label;
            if (field.HotKey != 0) text += $" ({field.HotKey})";
            var cb = new CheckBox { Tag = field.NInput, Content = text, Margin = new (0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            elem = cb;
            break;
         case EInput.Combobox:
            AddLabel ();
            var lb = new ComboBox { Padding = new (4, 1, 4, 1), ItemsSource = Enum.GetValues (field.GetValue ()!.GetType ()), Margin = new (0, 0, 12, 0), Background = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            elem = lb;
            break;
         default: throw new BadCaseException (field.UIType);
      }
      mPanel.Children.Add (elem);
      field.UIElement = elem;
      DataToUI (field);

      // Helper ..................................
      void AddLabel () {
         string text = field.Label;
         if (field.HotKey != 0) text += $" ({field.HotKey})";
         mPanel.Children.Add (new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new (0, 0, 4, 1) });
      }
   }

   // Update the UIElement with some internal data
   public void DataToUI (WField field) {
      if (field.UIElement == null) return;
      switch (field.UIElement) {
         case CheckBox cb: cb.IsChecked = ((int)field.GetUIValue ()) == 1; break;
         case TextBox tb: tb.Text = (string)field.GetUIValue (); break;
         case ComboBox lb: lb.SelectedIndex = (int)field.GetUIValue (); break;
         default: throw new BadCaseException (field.UIElement?.GetType ()!);
      }
   }

   // Called when the system switches between keyboard and mouse mode. 
   // When we are in keyboard mode, the text in input boxes is black, otherwise it is gray
   public void SetKeyboardMode (bool value) {
      foreach (var tb in mPanel.Children.OfType<Control> ())
         tb.Foreground = value ? Brushes.Black : mBrGray;
   }
   Brush mBrGray = Util.MakeBrush (new (128, 128, 136));

   // Called to set to a particular phase
   public void SetPhase (int phase) {
      var runs = mStatus.Inlines; runs.Clear ();
      if (mWidget != null) {
         // Update the status text at the bottom to display the text corresponding to the
         // current phase, like:
         // - "Click Start Point"
         // - "Click End Point" 
         var prompt = mWidget.Prompt;
         string text = phase == DwgHub.Widget!.Phases
            ? $"*{prompt.Name}*: Press *Enter* to execute the command"
            : $"*{prompt.Name}*: {prompt.Prompts[phase]}";
         int start = 0;
         for (; ; ) {
            int n1 = text.IndexOf ('*', start);
            if (n1 == -1) break;
            int n2 = text.IndexOf ('*', n1 + 1);
            if (n1 > start) runs.Add (new Run (text[start..n1]));
            runs.Add (new Run (text[(n1 + 1)..n2]) { FontWeight = FontWeights.Bold });
            start = n2 + 1;
         }
         runs.Add (new Run (text[start..]));

         // Next, find the textbox that matches the current phase, and highlight it
         foreach (var tb in mPanel.Children.OfType<TextBox> ())
            tb.Background = ((int)tb.Tag) == phase ? mBrSelected : Brushes.White;
      }
   }
   Brush mBrSelected = Util.MakeBrush (new Color4 (255, 255, 192));

   // Called to set a particular widget as the 'current widget' we're using
   public void SetWidget (Widget w) { mWidget = w; SetPhase (0); }
   Widget? mWidget;

   // Implementation -----------------------------------------------------------
   // Called when a text box gains focus 
   void OnGotFocus (TextBox tb) {
      tb.SelectAll ();
      DwgHub.KeyboardMode = true;
   }

   // Handle some special keys
   // - ESC switches out of keyboard mode
   // - ENTER commits the current command
   void OnKeyDown (Key key) {
      switch (key) {
         case Key.Escape: DwgHub.KeyboardMode = false; break;
         case Key.Enter: DwgHub.Enter (Hub.Keyboard.IsCtrlDown, Hub.Keyboard.IsShiftDown); break;
      }
   }

   // Called when text is changed in an input box. 
   // At this point, we call DwgHub.OnTypeIn to edit one of the fields of the
   // widget (the one bound to this input box)
   void OnTextChanged (TextBox tb) {
      if (tb.IsFocused) {
         int n = (int)tb.Tag;
         if (DwgHub.OnTypeIn (n, tb.Text) && DwgHub.Widget?.Fields.Single (a => a.NInput == n) is { } field) {
            // Some input field is edited, we need to determine the effective widget phase we _may_ now get in.
            int nomPhase = field.NPhase is int phase ? phase : field.NClick;
            DwgHub.Widget.EnsurePhase (nomPhase + 1);  // Pretend that click/s has occurred, and Phase incremented to suitable Phase value
         }
      }
   }

   // Get a value
   object? GetValue (string name) {
      PropertyInfo? pi = mWidget?.GetType ().GetProperty (name, mBF);
      if (pi != null) return pi.GetValue (mWidget);
      FieldInfo? fi = mWidget?.GetType ().GetField (name, mBF);
      if (fi != null) return fi.GetValue (mWidget);
      return null;
   }
   const BindingFlags mBF = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
}
#endregion
