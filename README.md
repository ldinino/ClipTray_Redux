# ClipTray

A Windows system tray application for managing canned text responses. Right-click the tray icon, pick an entry, and it's copied to your clipboard.

This is a ground-up rebuild of the original ClipTray, rewritten in C# targeting .NET Framework 4.8. The result is a single portable `.exe` with zero dependencies — no installer, no runtime downloads, just drop it in a folder and run.

## How It Works

ClipTray sits in your system tray. It reads entries from a plain-text file (`ClipTray.txt` in the same directory as the exe) and displays them in a right-click context menu. Click any entry to copy its text to the clipboard.

This is useful for support engineers, customer service reps, or anyone who frequently pastes the same blocks of text.

## Getting Started

1. Download `ClipTray.exe` from the [Releases](../../releases) page.
2. Place it in any folder.
3. Run it — a clipboard icon appears in your system tray.
4. Right-click the icon to see your entries and access all features.

On first launch, ClipTray creates a default `ClipTray.txt` file in the same directory as the exe.

## Features

- **Copy entries** — Right-click the tray icon and click any entry to copy it to the clipboard.
- **ClipBar** — Press **Ctrl+Alt+Space** anywhere in Windows to summon a floating search bar, type a few letters of an insert's name, and press Enter to copy it. Arrow keys move the selection, Esc dismisses, and focus returns to whatever you were working in. It fades in with rounded corners and follows your Windows light/dark theme.
- **ClipBar settings** — Configure it from Options > ClipBar, or the gear button in the editor. Choose the shortcut (with a **Test** button that tells you whether another application already owns it), the backdrop (none, translucent, or Windows 11 system acrylic), transparency, how many results to show, and the theme. Changes apply straight away.
- **ClipBar extras** — Four optional behaviours, all off unless you turn them on: paste automatically after copying, list recently used inserts first, show what tokens will produce instead of the raw `{date}`, and Alt+Enter to open an insert in the editor rather than copying it.
- **Rich text** — Compose inserts with bold, italic, underline, strikethrough, font, size, text and highlight color, bullets, alignment, and titled hyperlinks. Pasted formatting from browsers and Word is preserved; copied links remain clickable in browser-based and native rich-text editors, with clean titles in plain-text apps. Plain-text inserts stay plain on disk.
- **Unified editor** — Browse inserts on the left and edit the selected draft on the right. Create, duplicate, delete, copy, preview, save, or discard without bouncing between dialogs.
- **Direct ordering** — Move the selected insert up or down from the list footer; the tray menu order updates with it.
- **Quick capture** — Use "New insert..." from the tray menu, or double-click the tray icon, to open a fresh draft in the editor.
- **Dynamic tokens** — Embed placeholders like `{date:yyyy-MM-dd}`, `{time:h:mm tt}`, `{datetime}`, or `{clipboard}` in any entry. They're resolved at paste time, so a single entry can produce fresh content. The composer's **Insert ▾** button makes it easy to add tokens without remembering the syntax. Tokens work inside rich text too.
- **Menu size** — Control how many inserts appear in the tray menu (the rest remain available in "Open editor...").
- **Multiple files** — Open different `.txt` files via Options > File > Open. The most recent file is remembered between runs for quick switching.
- **Settings file** — ClipBar's shortcut and appearance, the menu size, and the recent file live in `ClipTray.settings.ini` next to the exe. It's plain text, safe to delete (defaults return), hand-editable, and keeps your inserts file free of configuration. Your `ClipTray.txt` format is unchanged.
- **High-DPI support** — Windows scale and monitor changes use PerMonitorV2 awareness, with every ClipTray window scaling from a 96-DPI layout baseline. ClipBar additionally scales with screen resolution, so it stays readable on a 4K display running at 100%.
- **Start with Windows** — Enable or disable per-user startup from Options > Start with Windows. No administrator access or installer is required.
- **Single instance** — Only one copy of ClipTray runs at a time.

## Tokens

Any entry text can include placeholders that are substituted at paste time:

| Token | Result |
|---|---|
| `{date}` | Current date (`MM/dd/yyyy`) |
| `{date:yyyy-MM-dd}` | Current date with a custom [.NET DateTime format](https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings) |
| `{time}` | Current time (`HH:mm:ss`) |
| `{time:h:mm tt}` | Current time with a custom format |
| `{datetime}` | Current date + time |
| `{clipboard}` | Whatever text is currently on the clipboard |

To insert a literal brace in your text, double it: `{{` → `{` and `}}` → `}`. Unknown tokens (e.g. `{foo}`) pass through unchanged, so JSON and code snippets are safe.

## File Format

ClipTray uses a simple plain-text format that is fully backward-compatible with the original ClipTray:

```
End:

Title:NEW EMAIL
Hi there,

Thank you for contacting Technical Support.
End:

Title:Survey
If you have some spare time, I'd love to find a few minutes to collect a brief phone survey. It goes a long way to help us understand how we can improve.
End:
```

- The file starts with `End:` followed by a blank line.
- Each entry begins with `Title:` immediately followed by the name (no space after the colon).
- The entry text follows on subsequent lines and can span multiple lines.
- Each entry ends with `End:` on its own line.

You can edit this file by hand if you prefer, or use the built-in editor.

Entries with rich formatting also include a series of `Rtf:`-prefixed lines after the body, holding raw RTF for the formatted version. Plain-text entries omit these lines and remain byte-identical to the original ClipTray format.

Original ClipTray files saved as Windows ANSI (Windows-1252) are detected automatically when they are not valid UTF-8. New and updated files are written as UTF-8 without a BOM.

## Building from Source

**Requirements:** .NET SDK with .NET Framework 4.8 targeting pack support.

```bash
dotnet build -c Release
```

The output exe is at `ClipTray/bin/Release/net48/ClipTray.exe`.

## Author

Built by Luciano DiNino.
