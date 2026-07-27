# ClipBar — Feasibility Study & Implementation Plan

**Status:** Phases 0–4 complete.
**Target versions:** 2.1.0 → 2.4.0
**Date:** 2026-07-27

A Spotlight-style floating search bar for ClipTray inserts, summoned by a global
hotkey from anywhere in Windows.

---

## 1. Verdict

**Feasible, and it fits the "small and lightweight" constraint.** Everything needed is
already in .NET Framework 4.8 + Win32. **Zero new NuGet packages, zero new assembly
references.** The whole feature is WinForms plus a handful of P/Invoke declarations
into `user32.dll` and `dwmapi.dll`.

Estimated size impact: **+25–35 KB IL** on top of the current 252 KB `ClipTray.exe`
(~12%). Nothing is added to the deployed file set — still `ClipTray.exe` +
`ClipTray.exe.config` in the zip.

---

## 2. Decisions locked

| # | Decision | Choice |
|---|---|---|
| 1 | Name | **ClipBar** |
| 2 | Default hotkey | **`Ctrl+Alt+Space`** (verified available; `Ctrl+Win+Space` is not — see §3.1) |
| 3 | Settings format | **INI** — `ClipTray.settings.ini` next to the exe |
| 4 | Phasing | **1 → 2 → 3** as written, each independently shippable |
| 5 | Phase 4 extras | Build them, but **every one is an opt-in checkbox**, off by default |
| 6 | Persist Menu size + recent file | **Open** — deferred, see §5.1 || 7 | Backdrop | **Acrylic** — `Form.Opacity` 85% + `ACCENT_ENABLE_ACRYLICBLURBEHIND` (Phase 0 winner) |
| 8 | Sizing | **Automatic** (§4.3). Width 740 logical, 5 rows, multiplier 1.00 |
| 9 | Size tunables | **INI-only, not in the settings dialog** — see §6.2 |
---

## 3. What I actually verified (not assumed)

I wrote throwaway probes and ran them on this machine before writing this plan.

### 3.1 Hotkey availability — the originally proposed default doesn't work

I called `RegisterHotKey` for real and checked the result:

| Combo | Result |
|---|---|
| **Ctrl+Win+Space** | ❌ **TAKEN** (`ERROR_HOTKEY_ALREADY_REGISTERED`, 1409) |
| Win+Space | ❌ TAKEN (1409) |
| Ctrl+Shift+Space | ❌ TAKEN (1409) |
| Shift+Win+Space | ❌ TAKEN (1409) |
| Win+`.` | ❌ TAKEN (1409) — emoji picker |
| **Ctrl+Alt+Space** | ✅ **AVAILABLE — chosen default** |
| Alt+Win+Space | ✅ AVAILABLE |
| Ctrl+Win+A | ✅ AVAILABLE |
| Ctrl+Win+J | ✅ AVAILABLE |
| Ctrl+Alt+V / Ctrl+Shift+V | ✅ AVAILABLE |
| Alt+Space | ✅ available, but **don't** — it's the window system menu |

**Why Ctrl+Win+Space appears "free" but isn't:** every `Space` combo in that list is
claimed by the Windows text-input stack (`ctfmon.exe` / `TextInputHost.exe`, both
running here) for language/IME switching. With a single keyboard layout installed they
*visibly* do nothing, but the hotkey is registered and `RegisterHotKey` fails for us.
The Win *key* isn't the problem — `Ctrl+Win+A` and `Ctrl+Win+J` register fine.

Availability is machine-specific, so the picker must test the combo live and the app
must degrade gracefully when registration fails (see §7.1).

### 3.2 Translucency & blur — all techniques work on this OS

This machine is **Windows 11 25H2, build 26200**. I compiled a net48 WinForms probe
and checked every API's return value:

| Technique | API | Result |
|---|---|---|
| Plain translucency | `Form.Opacity` | works everywhere, no P/Invoke |
| Blur behind | `SetWindowCompositionAttribute` / `ACCENT_ENABLE_BLURBEHIND` | ✅ success |
| Acrylic blur | `SetWindowCompositionAttribute` / `ACCENT_ENABLE_ACRYLICBLURBEHIND` | ✅ success |
| Win11 system backdrop | `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE = DWMSBT_TRANSIENTWINDOW)` | ✅ `S_OK` |
| Rounded corners | `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE = ROUND)` | ✅ `S_OK` |
| Glass client area | `DwmExtendFrameIntoClientArea(-1,-1,-1,-1)` | ✅ `S_OK` |

**Blur is on the table, for free.** I could not screen-capture the result —
`Graphics.CopyFromScreen` fails with `ERROR_INVALID_HANDLE` from the agent's terminal
session — which is exactly why Phase 0 is a hands-on visual spike (§8).

### 3.3 Dependencies

Current project references: `System.Windows.Forms`, `System.Drawing`, and a
build-time-only reference-assemblies package. **Nothing new is required.** Even the
settings file is done without a serializer (§6).

---

## 4. Proposed behaviour

1. User presses **`Ctrl+Alt+Space`** anywhere, in any app.
2. ClipBar fades in — horizontally centred on the monitor under the mouse cursor, about
   28% down from the top.
3. Focus lands in the query box. Typing filters inserts live, ranked by match quality.
4. Results appear as a list under the box (title + one-line preview, same look as the
   editor's insert list). Capped at ~8 visible, scrollable.
5. **Enter** copies the highlighted insert to the clipboard (tokens resolved, RTF
   preserved — the exact same code path as the tray menu), hides ClipBar, and returns
   focus to whatever app the user was in.
6. **Esc**, clicking away, or losing focus dismisses without copying.
7. **↑/↓** move the selection. Empty query shows the first N inserts.

The window is created once, then hidden/shown — so the second invocation is instant.

### 4.1 Where the settings live in the UI

- **Editor** — a `ClipBar…` button in the insert-pane footer, next to *Menu size*
  (that footer is already the "app preferences" corner of the UI).
- **Tray** — `Options ▸ ClipBar…` for people who never open the editor.

Both open the same modal `ClipBarSettingsDialog`:

| Setting | Control | Default |
|---|---|---|
| Enable ClipBar | checkbox | on |
| Hotkey | 4 modifier checkboxes (Ctrl/Alt/Shift/Win) + key dropdown + **Test** button showing ✅ Available / ⚠ In use by another app | `Ctrl+Alt+Space` |
| Backdrop | dropdown: None / Translucent / Blur / Acrylic / System acrylic | Acrylic |
| Transparency | slider 50–100% | 100% (opaque) |
| Results shown | numeric 3–15 | 5 |
| Theme | Dark / Light / Follow system | Follow system |

Sizing is deliberately **absent** from this dialog — it is automatic (§4.3), with an
escape hatch in the INI for the rare case it guesses wrong (§6.2).

Changes apply live on OK (re-register hotkey, rebuild the window).

> **Why checkboxes instead of a "press your shortcut here" box:** a press-to-capture
> box can't reliably see the Win key in WinForms without a low-level keyboard hook, and
> tapping Win alone pops the Start menu. Checkboxes + dropdown are boring but 100%
> reliable and testable. Easy to swap for the fancy capture box in Phase 3 if the feel
> matters more than the edge cases.

### 4.2 Dimensions

Proportions follow macOS Spotlight: a wide window, a tall input row, and a query font
large enough to feel like a system surface rather than a dialog. All values are
**logical units at 96 DPI**, scaled at runtime by a single factor.

| Metric | Value | Notes |
|---|---|---|
| Window width | **740** | ~50% of a 1440-wide work area, matching Spotlight's proportion |
| Input row height | **68** | Spotlight's is 64pt |
| Query font | **26 px** | the single biggest driver of "prominent" |
| Result row height | **56** | title + one preview line |
| Result title font | **15 px** bold | |
| Result preview font | **12 px** | 72% white |
| Rows visible | **5** | user-configurable 3–15 |
| Left edge inset | **22** | magnifier glyph sits here |
| Query text inset | **58** | clears the magnifier |

Total at 5 rows: **740 × 348** logical. Verified on screen and approved in Phase 0.

### 4.3 Adaptive scaling — DPI alone is not enough

A 4K panel run at **100% scaling reports 96 DPI**. A purely DPI-driven layout therefore
renders at 1:1 physical pixels and occupies ~19% of a 3840-wide screen — unusably small.
macOS never has this problem because Retina is always a 2× logical surface; Windows at
100% gives no such signal.

So ClipBar derives its scale from **both** signals and takes the larger:

```
dpiScale        = DeviceDpi / 96
resolutionScale = workArea.Height / 1080
scale           = clamp(max(dpiScale, resolutionScale), 1.0, 3.0) * userMultiplier
width           = clamp(740 * scale, 480, workArea.Width * 0.55)
```

**Height** drives the resolution factor, not width, so ultrawide monitors are treated as
the 1440p panels they actually are rather than being over-scaled.

| Monitor | DPI factor | Res factor | Scale | Bar width | % of screen |
|---|---|---|---|---|---|
| 1920×1080 @ 100% | 1.00 | 1.00 | 1.00 | 740 | 39% |
| 1920×1080 @ 150% | 1.50 | 1.00 | 1.50 | 1056 (capped) | 55% |
| 2560×1440 @ 100% | 1.00 | 1.33 | 1.33 | 984 | 38% |
| 3440×1440 @ 100% | 1.00 | 1.33 | 1.33 | 984 | 29% |
| 3840×2160 @ 100% | 1.00 | 2.00 | 2.00 | 1480 | 38% |
| **3240×2160 @ 200% (measured RDP session)** | **2.00** | 1.91 | **2.00** | **1474** | **45%** |

The `userMultiplier` defaults to **1.00** and is adjustable only through the INI
(§6.2) — the automatic result was approved as-is during Phase 0, so surfacing a slider
would be clutter for a control almost nobody needs.

> Physical monitor dimensions would be the theoretically correct input here, but Windows
> only exposes them through EDID via WMI, which is unreliable and often absent on
> laptops and virtual displays. Vertical resolution is the pragmatic proxy.

### 4.4 Remote Desktop

Development and testing happen over RDP, which is worth calling out because it behaves
differently from a local session. Measured from the live session:

```
TerminalServerSession : True     (rdp-sxs260519600#0)
GetDpiForSystem       : 192 (2x)
Monitor EFFECTIVE DPI : 192 (2x)      ANGULAR: 192      RAW: 259 (2.7x)
Screen                : 3240x2160, work area 3240x2064
```

Two consequences:

1. **The session really is 2× DPI**, so the DPI signal is the one that matters here — the
   resolution fallback (§4.3) is what covers the *other* case, a 4K panel at 100%.
2. **Reconnecting from a different client resizes the desktop mid-session.** That fires
   `DisplaySettingsChanged` but does not necessarily fire `DpiChanged`, so a window that
   only listens for the latter keeps a stale size. `ClipBarWindow` must subscribe to
   `SystemEvents.DisplaySettingsChanged` and re-run its layout pass — and unsubscribe on
   dispose, since that's a static event and would otherwise leak the window.

---

## 5. Architecture

New files, all under `ClipTray/`:

```
ClipBar/
  HotKeyDefinition.cs      pure logic: parse/format "Ctrl+Alt+Space", map to Win32 modifiers
  GlobalHotKey.cs          NativeWindow + RegisterHotKey/UnregisterHotKey, fires an event
  InsertSearch.cs          pure logic: rank entries against a query
  ClipBarWindow.cs         the borderless bar (query box + results list)
  WindowBackdrop.cs        P/Invoke: opacity / blur / acrylic / rounded corners + fallback chain
Settings/
  AppSettings.cs           the settings model
  SettingsStore.cs         load/save, atomic write, corrupt-file tolerance
UI/
  ClipBarSettingsDialog.cs the config dialog (subclasses ClipTrayForm)
```

Changes to existing files:

- **`TrayApplicationContext`** — owns the `GlobalHotKey` and the `ClipBarWindow`
  lifetime; loads settings at startup; adds the `Options ▸ ClipBar…` item.
- **`EntriesDialog`** — one new button in the insert-pane footer.
- **One small justified refactor:** `TrayApplicationContext.CopyToClipboard(ClipEntry)`
  becomes a shared `ClipboardWriter.Copy(entry)` so the tray menu and ClipBar use
  literally the same token-resolution + RTF path. No behaviour change, and it makes
  that logic unit-testable for the first time.

### 5.1 How the hotkey reaches us

`TrayApplicationContext` is an `ApplicationContext`, not a window, so there's no HWND to
receive `WM_HOTKEY`. `GlobalHotKey` creates a **message-only `NativeWindow`**
(`CreateHandle` with `HWND_MESSAGE` as parent), registers the hotkey against it, and
raises a .NET event from `WndProc` on `WM_HOTKEY (0x0312)`. `MOD_NOREPEAT` is set so
holding the keys doesn't machine-gun the window open.

### 5.2 Focus handling

Capture `GetForegroundWindow()` **before** showing ClipBar; call `SetForegroundWindow`
on it when dismissing. A process that just received a hotkey is granted
foreground-activation rights by Windows, so activating our own window works without the
usual `AttachThreadInput` hack — but that stays as a fallback since it occasionally
matters.

### 5.3 Search ranking (pure, testable)

`InsertSearch.Rank(entries, query, limit)` — no UI, no I/O, so it gets real unit tests.
Scoring, best to worst: exact title match → title prefix → word-start match in title →
subsequence match in title → body-text match. Case- and diacritic-insensitive, ties
broken by original list order.

---

## 6. Settings file

**Location:** `ClipTray.settings.ini`, next to the exe (same folder as `ClipTray.txt`,
consistent with the portable model).

**Format: INI-style `key=value`.** ~50 lines of parser, no assembly references,
human-editable, trivially unit-testable, and it matches the plain-text ethos of
`ClipTray.txt`.

```ini
# ClipTray settings. Safe to delete - defaults will be restored.
[ClipBar]
Enabled=true
Hotkey=Ctrl+Alt+Space
Backdrop=Acrylic
Transparency=100
MaxResults=5
Theme=System
```

Rules: unknown keys are preserved on rewrite (forward-compatible), malformed values
fall back to defaults silently, a corrupt file is never fatal, and writes are atomic
(temp file + `File.Replace`) so a crash mid-save can't lose settings.

### 6.2 Hidden sizing keys

Sizing is automatic (§4.3) and has no UI. For the rare display the heuristic gets wrong,
two keys are readable from the INI but never written by the settings dialog and never
shown in it:

```ini
[ClipBar]
# Advanced - normally absent. Only add these if automatic sizing gets it wrong.
SizeMultiplier=1.00    ; 0.50-3.00, multiplies the automatic scale
Width=740              ; logical width at 96 DPI, before scaling
```

Because unknown keys are preserved and missing keys fall back to defaults, these can be
added by hand and will survive a settings-dialog save untouched.

### 6.1 Open question — persisting Menu size and recent file

Two settings are currently **lost on every restart**: *Menu size* (`_menuSize`) and the
*recent file* path (`_recentFilePath`). Note the README already claims "The most recent
file is saved for quick switching", which is only true within a session — so this is
arguably a latent bug, not just a feature.

Once `SettingsStore` exists, fixing both is roughly 20 lines:

```ini
[General]
MenuSize=20
RecentFile=C:\Users\...\ClipTray-CCETemplates.txt
```

**Deferred by your call.** Cheapest moment to add it is Phase 1, since the store is
being written anyway; adding it later means a second settings-schema change. Flag it
whenever you decide — no rush, and it doesn't block anything.

---

## 7. Risks & gotchas

### 7.1 Hotkey registration can fail — must be graceful
Another app may own the combo (as proven above). Plan: try to register at startup; on
failure show a **one-time tray balloon** ("ClipBar shortcut Ctrl+Alt+Space is in use by
another app — pick a different one in Options ▸ ClipBar") and mark the state in the
settings dialog. Never nag, never block startup, never show a modal error box on boot.

### 7.2 Elevated windows (UIPI)
ClipTray runs `asInvoker`. The hotkey **will not fire** while an elevated window (Task
Manager, an admin console) has focus. This is a Windows security boundary, not a bug —
the only workaround is running ClipTray elevated, which is not appropriate for a
portable clipboard tool. Document in the README.

### 7.3 Text legibility on translucent windows
`Form.Opacity` alpha-blends the *whole* window, text included, so a 70% bar has 70%
text — it looks soft. The Win11 `DwmExtendFrameIntoClientArea` + system-backdrop route
keeps child controls fully opaque and crisp while the background is real acrylic. This
is the single biggest reason for the Phase 0 spike.

### 7.4 Per-monitor DPI — **confirmed, bit us in the spike**
The app is `PerMonitorV2`. The first build of the spike rendered with **overlapping
text** on a 150%-scaled monitor. Root cause, worth writing down because the production
code will hit it too:

1. `Control.DeviceDpi` read **in the constructor** returns **96**, because under
   PerMonitorV2 the real DPI isn't known until the handle is created on a monitor.
2. GDI+ fonts specified in **points** auto-scale to the true device DPI regardless.
3. Result: text drawn at 144 DPI inside geometry laid out for 96 DPI → collisions.

**Rules adopted for `ClipBarWindow`:**

- Set `Location` in the constructor (picks the monitor), but do **all sizing, font
  creation and control placement in `OnHandleCreated`**, never the constructor.
- Specify fonts in **`GraphicsUnit.Pixel`** and multiply by the same `_scale` factor
  used for geometry, so exactly one scaling factor governs the whole layout.
- Set **`AutoScaleMode = AutoScaleMode.None`**. The window is fully owner-drawn, so
  WinForms must not apply a second, competing scaling pass.
- Re-run the layout pass on `OnDpiChanged` **and** on
  `SystemEvents.DisplaySettingsChanged` (§4.4), unsubscribing on dispose.
- Derive `_scale` from `Screen.FromHandle(Handle)` — the monitor the window is actually
  on — never `Screen.PrimaryScreen`.
- Clamp the final position back inside the work area after any resize.

This is stricter than the existing `ClipTrayForm.ConfigureDpiScaling` /
`ScaleLogical` approach, which relies on WinForms' `AutoScaleMode.Dpi` for laid-out
controls. ClipBar is fully owner-drawn, so it needs the manual discipline above.

**And DPI is only half the story** — see §4.3 for why resolution has to feed into the
scale as well.

### 7.7 WinForms high-DPI needs *three* things, not one

The spike spent a round reporting `DeviceDpi = 96` inside a session that was genuinely
192 DPI. On .NET Framework 4.8, WinForms only honours per-monitor DPI when **all three**
of these are present:

| Requirement | Where | ClipTray status |
|---|---|---|
| `dpiAware` / `dpiAwareness` | [app.manifest](ClipTray/app.manifest) | ✅ present |
| `DpiAwareness = PerMonitorV2` | [App.config](ClipTray/App.config) | ✅ present |
| `TargetFrameworkAttribute` on the entry assembly | emitted by the SDK | ✅ verified `.NETFramework,Version=v4.8` |

Miss any one and `Control.DeviceDpi` silently returns 96 forever — no error, no warning.
The probe was missing the second and third, which is why it under-scaled.

Worth knowing because `ClipTray.csproj` sets `GenerateAssemblyInfo=false`, which *looks*
like it would suppress the attribute. It doesn't — `GenerateTargetFrameworkAttribute` is
separate and still defaults to true. I verified this by reflecting over the shipped
`ClipTray.exe` rather than assuming. **This also means the `.exe.config` is not optional
cosmetics** — shipping the exe without it silently downgrades DPI handling, which is
exactly why `CLAUDE.md` insists on zipping the pair.

### 7.8 Applying the lesson to the rest of the app

The v2.0.0 DPI pass was verified by eye at 200%. Re-auditing the existing dialogs by
measurement found two defects that a visual check had missed, both in `EntriesDialog`:

| Defect at 200% | Root cause | Fix |
|---|---|---|
| Menu-size spinner pushed 26px off the insert footer | Four `AutoSize` columns; text width does **not** scale exactly linearly with DPI, so accumulated label growth displaced the fixed-width spinner | Label column is now `SizeType.Percent`, so it absorbs slack and truncates first |
| Draft-header action buttons clipped by 16px | Header pinned to `Height = 48`; its content needs more than 2× that at 2× | Header is now `AutoSize` with `GrowAndShrink` |

Both are the same mistake: **a fixed-size container holding text that scales
non-linearly.** The other four dialogs measured clean — they use logical 96-DPI
coordinates plus `AutoScaleMode.Dpi`, which is the correct WinForms pattern, and the
`ToolStrip` combo boxes do scale (110→216, 45→86), contrary to my initial suspicion.

**The audit could not be done from the test suite.** `testhost.exe` has none of the
three DPI prerequisites from §7.7, so it always reports 96 DPI — every dialog measures
"fine" there. The audit had to run in a purpose-built process carrying the same
manifest, `app.config` and `TargetFrameworkAttribute` as ClipTray itself.

Simulating with `Control.Scale(2.0)` inside the test host was tried and **rejected**: it
produced a 1624×1061 `EntriesDialog` where the real 192-DPI window is 2160×1300,
invented three phantom `SplitContainer` overflows, and missed both genuine defects. It
would have been worse than no test at all.

What is left behind instead is `EntriesDialogLayoutTests`, which asserts the two
structural invariants (a flexible footer column, an auto-sizing header) rather than
pixel values, so it holds at any DPI.

> **Note for future UI work:** resolution-derived scaling (§4.3) is deliberately
> *not* applied to the ordinary dialogs. ClipBar is a summoned system surface that
> should feel prominent; dialogs should follow Windows conventions and stay the size
> the user's DPI setting asks for.

### 7.5 Order of operations for layering
`Form.Opacity` and the accent-blur API both manipulate `WS_EX_LAYERED`. Opacity must be
set *before* applying the accent policy or the blur is dropped. Noted so it doesn't
become a mystery bug.

### 7.6 Clipboard contention
Already handled in the existing code (`ExternalException` swallowed when another process
holds the clipboard). Reusing `ClipboardWriter` means ClipBar inherits it.

---

## 8. Phases

### Phase 0 — Visual spike *(throwaway, no repo changes)* — ✅ **COMPLETE**

A realistic ClipBar mockup cycling five backdrop treatments, draggable over any
background, with live tunables for scale, width and row count.

| Mode | Treatment | Verdict |
|---|---|---|
| 0 | Opaque — baseline, no compositing | |
| 1 | `Form.Opacity` 85% only | |
| 2 | Opacity 85% + `ACCENT_ENABLE_BLURBEHIND` | |
| **3** | **Opacity 85% + `ACCENT_ENABLE_ACRYLICBLURBEHIND`** | ✅ **chosen** |
| 4 | Win11 DWM acrylic backdrop + glass client area | |

**Outputs, measured and approved:**

```
mode=3  DeviceDpi=192  screen=3240x2064
dpiScale=2.00  resScale=1.91  auto=2.00  mult=1.00  scale=2.00
widthLogical=740  rows=5  clientPx=1480x804  pctOfScreen=46
```

The production window drops the three-line debug footer, so the real height is
`68 + 5×56 = 348` logical → **740×348 logical**, 1480×696 px at 2×.

*Bugs caught before writing any production code:* PerMonitorV2 constructor-DPI trap
(§7.4), 4K-at-100% undersizing (§4.3), the three-part WinForms DPI requirement (§7.7),
and RDP session-resize staleness (§4.4). Probe deleted.

### Phase 1 — Foundation → **v2.1.0**
Settings store + hotkey engine + a *functional but plain* ClipBar (solid dark
background, no blur). Search, ↑/↓, Enter copies, Esc dismisses, focus restores. Hotkey
read from the settings file (hand-editable for now). Ships genuinely useful.

*Includes the `ClipboardWriter` extraction and its tests.*

### Phase 2 — The look → **v2.2.0** — ✅ **COMPLETE**
The Phase 0 winner (`Form.Opacity` 85% + `ACCENT_ENABLE_ACRYLICBLURBEHIND`), the
automatic fallback chain, rounded corners, light/dark/system theming, and a ~105ms
fade-in.

**Fallback chain**, resolved from `Environment.OSVersion` and verified by unit tests
rather than by owning those Windows versions:

| Requested | Win11 22H2+ | Win11 21H2 / Win10 1803+ | Win10 1709 | Older |
|---|---|---|---|---|
| **SystemAcrylic** (default) | SystemAcrylic | Acrylic | Blur | Translucent |
| Acrylic | Acrylic | Acrylic | Blur | Translucent |
| Blur | Blur | Blur | Blur | Translucent |
| Translucent | Translucent | Translucent | Translucent | Translucent |
| None | None | None | None | None |

If a call fails at runtime despite the version check, it degrades one tier rather than
leaving an opaque box.

#### Why the accent implementation barely blurred

The first cut used `ACCENT_ENABLE_ACRYLICBLURBEHIND` together with `Form.Opacity = 0.85`,
which looked translucent but showed almost no blur. The arithmetic explains it: the
accent blur is composited *behind* the window, and the window then paints its own
opaque background over it. All that reaches the eye is the 15% the layered opacity lets
through, so the blur contributes about a seventh of the final image. Lowering the
opacity to reveal more blur would have washed out the text by the same amount.

#### Why the DWM path was tried and then set aside

`DWMSBT_TRANSIENTWINDOW` avoids that trade-off in principle — DWM owns the blur and the
window stays opaque — but WinForms fights it on three fronts, each found by measurement:

1. `Form.Opacity < 1` sets `WS_EX_LAYERED`, and **a layered window cannot display a DWM
   system backdrop**. The fade-in was silently disabling the very effect it was meant to
   reveal, which is why the two acrylic modes looked identical.
2. Without `DWMWA_USE_IMMERSIVE_DARK_MODE`, DWM draws its **light** acrylic, leaving
   dark-theme white text on a pale panel.
3. `DwmExtendFrameIntoClientArea(-1,-1,-1,-1)` makes the whole client area glass, and
   **GDI child controls carry no alpha**, so the query `TextBox` rendered as a
   see-through hole. Confining the glass to the results area fixes that, but only
   because nothing else lives down there.

All three are fixed and `Backdrop=SystemAcrylic` is available, but the remaining ceiling
is structural: a genuinely correct glass ClipBar needs per-pixel alpha via
`UpdateLayeredWindow`, or the query text owner-drawn instead of hosted in a `TextBox`.
That is a rendering rewrite, not a tweak.

**Settled position:** the default is an opaque bar (`Backdrop=Acrylic`,
`Transparency=100`). Turning `Transparency` down re-enables translucency and the accent
blur for anyone who wants it. Chasing a stronger blur was judged not worth a rendering
rewrite.

### Phase 3 — Config UI → **v2.3.0** — ✅ **COMPLETE**
`Options ▸ ClipBar...` in the tray, plus a compact gear button in the editor's
insert-pane footer. The footer is only ~260 logical pixels wide and already carried
four controls, so a captioned button would not have fitted — hence the glyph plus
tooltip.

The editor does not own the hotkey or the ClipBar window, so its button raises
`ClipBarSettingsRequested` and the tray handles the dialog. On OK the tray saves the
file, re-registers the shortcut, and discards the ClipBar window so appearance
settings — which are applied when the handle is created — take effect next summon.

The **Test** button probes a candidate shortcut by briefly claiming and releasing it on
a throwaway message-only window. The probe is injected, so the tray can report its own
already-registered shortcut as available rather than as a conflict, and tests never
touch real system hotkeys.

A conflicting shortcut is still accepted: registration simply fails and the tray says
so in a balloon. Blocking OK would strand anyone whose conflict is intermittent.

Audited at 192 DPI in all three states (default, conflict, disabled): 0 problems.

### Phase 4 — Extras → **v2.4.0** — ✅ **COMPLETE**
All four shipped as **opt-in checkboxes in the ClipBar settings dialog, off by default**:

| Extra | Behaviour | Notes |
|---|---|---|
| **Paste automatically after copying** | `SendInput` sends Ctrl+V once focus is restored | Types into whatever window is in front, so it stays off unless asked for. A 120ms delay lets the restored window actually take focus first |
| **List recently used inserts first** | Recency orders the results | Only separates entries that scored **equally**, so it can reorder peers but never promotes a weak match above a strong one |
| **Show what tokens will produce** | Previews resolve `{date}` and friends | Resolved once per query, not per repaint: `{clipboard}` reads the real clipboard and would be far too expensive on every paint |
| **Alt+Enter opens the editor** | Jumps to that insert instead of copying | The editor is opened by the tray, which owns it |

The recently-used list lives in a `[Recent]` section of the settings file under
**numbered keys** — insert titles are arbitrary user text and would not survive being
used as INI keys (`=`, `#` and `[` all appear in real titles). The writer emits the
titles plus a single empty terminator, so a shorter list cleanly supersedes a longer
one without padding the file with fifty blank keys.

Usage is recorded for tray-menu copies too, so switching the option on later already
has history to work with.

Audited at 192 DPI in four states including every extra enabled: 0 problems.

---

## 9. Testing plan

Following the existing MSTest + reflection pattern in `ClipTray.Tests`
(`InternalsVisibleTo` is already set up):

| Test file | Covers |
|---|---|
| `HotKeyDefinitionTests` | parse/format round-trip, Win32 modifier mapping, invalid input, unknown key names |
| `InsertSearchTests` | ranking order, case-insensitivity, subsequence matching, empty query, limit, tie-breaking |
| `SettingsStoreTests` | round-trip, missing file → defaults, corrupt file → defaults, unknown keys preserved, atomic write |
| `ClipboardWriterTests` | token resolution + RTF selection logic (the non-clipboard parts) |
| `ClipBarSettingsDialogTests` | control names/wiring present, mirroring the existing `TrayMenuTests` approach |

Plus a headless smoke run that constructs `ClipBarWindow`, drives the key handlers
directly, and asserts the right entry was selected — no manual clicking required.

---

## 10. Release impact

Per `CLAUDE.md`: additive feature → **minor bump per phase** (2.1.0, 2.2.0, 2.3.0,
2.4.0), both `AssemblyVersion` and `AssemblyFileVersion` bumped in the same commit as
the feature. README **Features** section updated in the same commit for each
user-visible change. Packaging is unchanged — `ClipTray.exe` + `ClipTray.exe.config`
zipped at the root.

The settings file is **new and optional**: ClipTray runs identically without it, and
older versions ignore it entirely. `ClipTray.txt` is untouched — no file-format change,
so §"Compatibility" in the release notes can say so plainly.
