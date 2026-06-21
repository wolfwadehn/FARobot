# MainWindow UI

**Window**: `FApp.MainWindow`  
Background: `#9898A0` | Font size: 13 | Maximized on launch | Drag-and-drop enabled

---

## Layout (DockPanel)

### Menu Bar (Top)

Background: `#C8C8D0`

| Menu | Items | Shortcut |
|------|-------|----------|
| **File** | New | Ctrl+N |
| | Open… | Ctrl+O |
| | Save | Ctrl+S |
| | Save As… | Ctrl+Alt+S |
| | Save Selected As… | — |
| | *(separator)* | |
| | Exit | Alt+F4 |
| **Edit** | Undo (`mUndo`) | Ctrl+Z |
| | Redo (`mRedo`) | Ctrl+Y |
| | *(separator)* | |
| | Cut | Del |
| | Copy | Ctrl+C |
| | Paste | Ctrl+V |
| | *(separator)* | |
| | Select All | Ctrl+A |
| | Deselect All | Ctrl+D |
| | Invert Selection | Ctrl+I |
| **View** | Zoom Extents | Ctrl+E |
| | *(separator)* | |
| | Robot… | — |
| | TCP Legend… | — |
| **About!** | *(direct command)* | — |

> `Edit` menu fires `OnEditMenuOpened` on open to update Undo/Redo state.

---

### Toolbar Host (Left)

| Property | Value |
|----------|-------|
| Name | `mToolbarHost` |
| Width | 114 px |
| Padding | `2,4,0,0` |
| Background | `#C8C8D0` |

Populated at runtime by `DwgHub` via the toolbar manifest.

---

### Input Bar (Bottom)

Background: `#C8C8D0`

| Element | Name | Description |
|---------|------|-------------|
| `TextBlock` | `mStatus` | Status/prompt text; default value `"OK"` |
| `StackPanel` | `mStack` | Horizontal row of input widgets (textboxes, checkboxes, choices) injected when a widget activates |

---

### Drawing Canvas (Center)

| Property | Value |
|----------|-------|
| Name | `mContent` |
| Type | `Border` |
| Background | `#C8C8D0` |

Hosts the WPF drawing surface. Fills all remaining space in the `DockPanel`.
