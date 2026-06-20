// ╔═╦╗
// ║╬╠╬╦╗ MRUList.cs
// ║╔╣╠║╣ Manages and displays a Most Recently Used (MRU) file list in a WPF application
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;

namespace FApp.WPF;

#region class MRUList -----------------------------------------------------------------------------
/// <summary>Maintains a Most Recently Used (MRU) file list and integrates it with a WPF window's menu</summary>
public class MRUList {
   /// <summary> Initializes a new instance of the <see cref="MRUList"/> class </summary>
   /// <param name="window">Window containing the File menu to update</param>
   /// <param name="memoFile">The path to the file used to store the recent files list</param>
   /// <param name="maxFiles">Maximum number of recent files to keep</param>
   /// <param name="opener">Action to execute when a recent file is selected</param>
   /// <exception cref="ArgumentException">Thrown if <paramref name="memoFile"/> is null or empty</exception>
   /// <exception cref="InvalidOperationException">Thrown if the File menu cannot be found in the given window</exception>
   public MRUList (Window window, string memoFile, int maxFiles, Action<string> opener) {
      string arg = nameof (memoFile);
      if (string.IsNullOrWhiteSpace (memoFile))
         throw new ArgumentException ($"{arg} cannot be null or empty.", arg);

      mOpener = opener;
      mMemoFile = memoFile;
      mMaxFiles = Math.Max (1, maxFiles);

      LoadMemoFile ();

      mFileMenu = VisualTreeHelper.FindLogicalChild<Menu> (window)?.Items.OfType<MenuItem> ().FirstOrDefault ()
           ?? throw new InvalidOperationException ("File menu not found.");
      mFileMenu.SubmenuOpened += delegate { UpdateFileMenu (); };
   }

   /// <summary>Adds a file to the MRU list, moving it to the top if it already exists</summary>
   public void AddFile (string file) {
      if (!File.Exists (file)) return;
      string fullPath = Path.GetFullPath (file);
      if (mFiles.Count > 0 && mFiles[0] == fullPath) return; // Already most recent, skip moving
      // Remove the file from the list if it already exists, then add it to the top (most recent)
      mFiles.Remove (fullPath);
      mFiles.Insert (0, fullPath);

      // If the list exceeds the maximum allowed size, remove the oldest file
      if (mFiles.Count > mMaxFiles) mFiles.RemoveRange (mMaxFiles, mFiles.Count - mMaxFiles);
   }

   /// <summary>Removes a file from the MRU list, if present</summary>
   public void RemoveFile (string file) => mFiles.Remove (file);

   /// <summary>Saves the MRU list to the memo file</summary>
   public void Save () {
      if (string.IsNullOrWhiteSpace (mMemoFile) || mFiles.Count == 0) return;
      Directory.CreateDirectory (Path.GetDirectoryName (mMemoFile) ?? ".");
      File.WriteAllLines (mMemoFile, mFiles);
   }

   // Loads the MRU list from the memo file
   void LoadMemoFile () {
      if (!File.Exists (mMemoFile)) return;
      foreach (var line in File.ReadAllLines (mMemoFile)) {
         if (mFiles.Count >= mMaxFiles) break;
         if (!string.IsNullOrWhiteSpace (line)) mFiles.Add (line.Trim ());
      }
   }

   // Updates the file menu with the current MRU list. Called automatically when the menu is about to open
   void UpdateFileMenu () {
      // Remove any existing MRU separator and items first
      if (mSeparator != null) {
         int index = mFileMenu.Items.IndexOf (mSeparator);
         if (index >= 0)
            while (mFileMenu.Items.Count > index) mFileMenu.Items.RemoveAt (index);
      }
      if (mFiles.Count == 0) {
         if (mSeparator != null) {
            mFileMenu.Items.Remove (mSeparator);
            mSeparator = null;
         }
         return;
      }

      // Add the separator
      mSeparator = new Separator ();
      mFileMenu.Items.Add (mSeparator);
      int i = 1;
      // Add the MRU items
      foreach (var file in mFiles) {
         // Use _1 to _9 for accelerators, 1_0 for 10, no accelerator after that
         var menuItem = new MenuItem {
            Header = i switch {
               10 => $"1_0 {file}",
               <= 9 => $"_{i} {file}",
               _ => $"{i} {file}"
            }
         };
         menuItem.Click += (mitem, e) => mOpener (file);
         mFileMenu.Items.Add (menuItem);
         i++;
      }
   }

   // Path to the memo file for saving and loading the MRU list.
   readonly string mMemoFile;
   // Maximum number of MRU files to store.
   readonly int mMaxFiles;
   // Callback to open a file from the MRU list.
   readonly Action<string> mOpener;
   // List of the most recently used files.
   readonly List<string> mFiles = [];
   // MenuItem representing the "File" menu.
   readonly MenuItem mFileMenu;
   // Separator UI element between "Exit" and MRU files in the File menu.
   Separator? mSeparator;
}
#endregion

#region class VisualTreeHelper --------------------------------------------------------------------
/// <summary>To find a logical child of a specified type in the visual tree</summary>
static class VisualTreeHelper {
   /// <summary>Searches the logical tree for the first descendant of type <typeparamref name="T"/> using BFS</summary>
   public static T? FindLogicalChild<T> (this DependencyObject parent) where T : DependencyObject {
      Queue<DependencyObject> queue = [];
      queue.Enqueue (parent);
      while (queue.Count > 0)
         foreach (var child in LogicalTreeHelper.GetChildren (queue.Dequeue ())) {
            if (child is T t) return t;
            if (child is DependencyObject d) queue.Enqueue (d);
         }
      return null;
   }
}
#endregion
