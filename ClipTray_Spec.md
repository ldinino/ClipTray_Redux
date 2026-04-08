# ClipTray — Build Specification

## Overview

ClipTray is a Windows system tray application that manages canned text responses. Users right-click the tray icon to see a menu of named entries. Clicking an entry copies its text to the clipboard. The app reads/writes a simple plain-text file format.

## Platform & Constraints

- **Language:** C# WinForms
- **Target:** .NET Framework 4.8 (ships with Windows 10/11)
- **Output:** Single portable `.exe`, no installer, no dependencies
- **No feature creep.** Replicate original ClipTray functionality only.

---

## Data File Format

The app reads and writes `.txt` files using this exact format. The format must remain backward-compatible with the original ClipTray files.

```
End:

Title:NEW EMAIL
Hi there,

Thank you for contacting Microsoft Support.
End:

Title:Reroute
I've gone ahead and changed the support path of this case to <TEAM>.
End:
```

### Rules

- File MUST begin with `End:` followed by a blank line.
- Each entry starts with `Title:` immediately followed by the title (no space after colon).
- Entry text begins on the next line and can be multiline.
- Each entry MUST end with `End:` on its own line followed by a newline.
- Parsing is case-sensitive for `Title:` and `End:`.

---

## Features

### 1. System Tray Icon

- App starts minimized to system tray. No main window on launch.
- Tray icon tooltip shows the name of the currently loaded file.
- **Right-click** tray icon: opens context menu.
- **Double-click** tray icon: opens Add New Entry dialog.

### 2. Tray Context Menu

Top-level menu structure (top to bottom):

```
Add...
Options        >  Preview Mode (checkable toggle)
                  Edit...
                  File         >  Open/Create...
                                  [recent file path]
                  Help         >  About ClipTray
---separator---
More...
---separator---
[Entry 1]
[Entry 2]
...
[Entry N]
---separator---
Exit ClipTray
```

- Entry items shown in the menu are limited by a configurable **Menu Size** value (default: 20). All entries are always accessible via the More... dialog.
- Clicking an entry copies its text to the clipboard.
- If Preview Mode is enabled, clicking an entry also shows a read-only dialog displaying the copied text.

### 3. Add New Entry Dialog

- **Title bar:** "Add New ClipTray Entry"
- **Fields:** "Name of ClipTray Entry" (single-line textbox), "Entry Text" (multiline textbox)
- **Buttons:** Add (disabled until title is non-empty), Paste (pastes clipboard into Entry Text), Cancel
- After clicking Add, the entry is appended to the file and the menu refreshes. The dialog remains open for adding more entries.

### 4. ClipTray Editor Dialog

- **Title bar:** "ClipTray Editor"
- **Fields:** "Name of ClipTray Entry" (dropdown/combobox listing all entries), "Entry text" (multiline textbox, read-only in this view)
- **Buttons:** Delete, New... (opens Add dialog), Edit Current... (opens Edit dialog), OK (closes)
- Selecting an entry in the combobox displays its text in the textbox below.
- Delete prompts for confirmation via MessageBox.

### 5. Edit Entry Dialog

- **Title bar:** `Edit - "[ENTRY NAME]"`
- **Fields:** "Name of ClipTray Entry" (editable textbox, pre-filled), "Entry text" (editable multiline textbox, pre-filled)
- **Buttons:** Save, Cancel
- Save writes changes to the file and returns to the Editor dialog.

### 6. More ClipTray Entries Dialog

- **Title bar:** "More ClipTray Entries"
- **Listbox** showing all entries as `N: Title` (1-indexed).
- **Buttons:** Move Up, Move Down, Edit... (opens Editor), Copy (copies selected entry text to clipboard), Close
- **Menu Size** control in lower-left: a numeric up/down (labeled "Menu Size", displays "N Items") controlling how many entries appear on the tray menu.
- Move Up/Move Down disabled when selection is at top/bottom respectively.

### 7. Open/Create File

- Standard Windows Open File dialog filtered to `.txt` files.
- If the user types a filename that does not exist, prompt: "Do you wish to create it?" — if Yes, create the file with the initial `End:\n\n` content.
- After opening/creating, reload the menu from the new file.
- The most recently opened file path appears in the File submenu for quick switching.

### 8. Preview Mode

- Toggle via Options > Preview Mode (checkmark when active).
- When active, clicking any entry in the tray menu or More dialog pops up a read-only dialog showing the entry text. The text is still copied to clipboard.
- State does not need to persist between sessions (original behavior).

### 9. About Dialog

- **Title bar:** "About ClipTray"
- Show app name, version, and author info.

---

## Behavior Notes

- The app should load `ClipTray.txt` from its own directory on startup. If the file does not exist, create it with the initial `End:\n\n` content.
- All file writes should be immediate (no deferred save).
- The app must handle malformed files gracefully (skip unparseable entries, do not crash).
- Only one instance should run at a time. If a second instance is launched, activate the existing one.
- Minimize memory footprint. No background threads or timers needed.

---

## Out of Scope

- Installer / setup wizard
- Auto-update
- Rich text or HTML in entries
- Hotkey / global shortcut support
- Cloud sync
- Search/filter
- Import/export beyond the native `.txt` format
