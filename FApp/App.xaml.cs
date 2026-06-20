// ╔═╦╗
// ║╬╠╬╦╗ App.xaml.cs
// ║╔╣╠║╣ WPF Application entry point
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

[assembly: ThemeInfo (ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: InternalsVisibleTo ("FApp.Test")]

namespace FApp;

public partial class App : Application {
   public App () {
      Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
      AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException; // (Final)Notification before dying!
      DispatcherUnhandledException += OnUnhandledException; // Handled/noted option...
   }

   /// <summary>Gets Assemby Version Attribute</summary>
   public static (int Year, int Month, int Release, int ExMonth) AssemblyVersion () {
      var ver = Assembly.GetEntryAssembly ()!.GetCustomAttributes<AssemblyFileVersionAttribute> ().First ();
      int[] ints = [.. ver.Version.Split ('.').Select (s => int.Parse (s, CultureInfo.InvariantCulture))];
      return (ints[0], ints[1], ints[2], ints[1] + 1);
   }

   public static void CheckExpired () {
      var (year, _, _, exMonth) = AssemblyVersion ();
      var now = DateTime.Now;
      if (now.Year * 12 + now.Month > year * 12 + exMonth)
         MessageBox.Show (App.Current.MainWindow, "This version is old.\n\nPlease get the latest version for more stable features.", "FApp", MessageBoxButton.OK);
   }

   /// <summary>This is called when there is some exception we haven't handled.</summary>
   /// We display the exception message and continue, for now.
   static void OnUnhandledException (object sender, DispatcherUnhandledExceptionEventArgs e) {
      MessageBox.Show (App.Current.MainWindow, e.Exception.ToString (), "FApp encountered a problem (Dispatcher)", MessageBoxButton.OK);
      e.Handled = true;
   }

   /// <summary>Handles when there is an unhandled exception in this current domain</summary>
   static void OnDomainUnhandledException (object sender, UnhandledExceptionEventArgs e) {
      MessageBox.Show (App.Current.MainWindow, ((Exception)e.ExceptionObject).ToString (), "FApp encountered a problem", MessageBoxButton.OK);
   }
}
