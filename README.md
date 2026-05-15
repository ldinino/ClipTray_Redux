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
- **Rich text** — Compose entries with bold, italic, underline, strikethrough, font, size, text and highlight color, bullets, alignment, and hyperlinks. Pasted formatting from browsers and Word is preserved. Plain-text entries stay plain on disk.
- **Add entries** — Use "Add..." from the menu, or double-click the tray icon.
- **Manage entries** — The "Entries..." dialog lets you create, edit, reorder, delete, copy, or preview entries in one place.
- **Dynamic tokens** — Embed placeholders like `{date:yyyy-MM-dd}`, `{time:h:mm tt}`, `{datetime}`, or `{clipboard}` in any entry. They're resolved at paste time, so a single entry can produce fresh content. The composer's **Insert ▾** button makes it easy to add tokens without remembering the syntax. Tokens work inside rich text too.
- **Menu size** — Control how many entries appear in the tray menu (the rest are accessible via "Entries...").
- **Multiple files** — Open different `.txt` files via Options > File > Open. The most recent file is saved for quick switching.
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

## Building from Source

**Requirements:** .NET SDK with .NET Framework 4.8 targeting pack support.

```bash
dotnet build -c Release
```

The output exe is at `ClipTray/bin/Release/net48/ClipTray.exe`.

## Author

Built by Luciano DiNino.
