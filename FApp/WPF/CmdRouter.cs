// ╔═╦╗
// ║╬╠╬╦╗ CmdRouter.cs
// ║╔╣╠║╣ <<TODO>>
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
namespace FApp;

#region [CmdSink] attribute ------------------------------------------------------------------------
/// <summary>Attach this to a method to make it a command sink</summary>
[AttributeUsage (AttributeTargets.Method)]
class CmdSinkAttribute (string tag) : Attribute {
   public readonly string Tag = tag;
}
#endregion

#region class CmdRouter ----------------------------------------------------------------------------
/// <summary>CmdRouter connects menu commands to corresponding handlers in MainWindow</summary>
class CmdRouter {
   // Constructors -------------------------------------------------------------
   /// <summary>Constuct a CmdRouter, given a window</summary>
   /// 1. This finds all the command sinks (methods in the Window tagged with the
   ///    CmdSink attribute)
   /// 2. This finds all the command source in the Window (MenuItems with Tags)
   /// 3. It also maintains a list of specific hotkeys associated with MenuItem, and
   ///    when those keys are pressed, issues those commands
   ///    
   /// The tag name (which is a string) is what connected particular command sources
   /// with the sinks
   public CmdRouter (Window win) {
      mConv = TypeDescriptor.GetConverter (typeof (KeyGesture));
      var bf = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
      foreach (var mi in win.GetType ().GetMethods (bf))
         if (mi.GetCustomAttribute<CmdSinkAttribute> () is CmdSinkAttribute ca) 
            mSinks.Add (ca.Tag, mi);
      win.PreviewKeyDown += OnKeyDown;
      Recurse (mWin = win);
   }

   // Methods ------------------------------------------------------------------   
   public void Execute (string tag) {
      if (mSinks.TryGetValue (tag, out var mi))
         mi.Invoke (mWin, []);
   }

   // Implementation -----------------------------------------------------------
   void OnKeyDown (object s, KeyEventArgs e) {
      if (mGestures.TryGetValue ((e.Key, e.KeyboardDevice.Modifiers), out var cmd)) 
         Execute (cmd);
   }

   void Recurse (FrameworkElement elem) {
      if (elem is MenuItem mi && mi.Tag is string tag) {
         mi.Click += (s, e) => Execute (tag);
         if (mConv.ConvertFromString (mi.InputGestureText) is KeyGesture kg && (kg.Key != Key.None || kg.Modifiers != ModifierKeys.None)) 
            mGestures.Add ((kg.Key, kg.Modifiers), tag);
         if (!mSinks.ContainsKey (tag)) mi.IsEnabled = false;
      }
      foreach (var child in LogicalTreeHelper.GetChildren (elem).OfType<FrameworkElement> ())
         Recurse (child);
   }

   // Private data -------------------------------------------------------------
   readonly Window mWin;
   readonly Dictionary<string, MethodInfo> mSinks = [];
   readonly Dictionary<(Key, ModifierKeys), string> mGestures = [];
   readonly TypeConverter mConv;
}
#endregion
