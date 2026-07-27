using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.Settings;

namespace ClipTray.ClipBar
{
    /// <summary>
    /// The floating search bar. Summoned by a global hotkey from any application,
    /// it filters inserts as you type and copies the chosen one to the clipboard.
    ///
    /// The window is fully owner-drawn, so WinForms auto-scaling is switched off and
    /// every measurement is derived from one scale factor computed in ApplyLayout.
    /// </summary>
    internal sealed class ClipBarWindow : Form
    {
        // Logical units at 96 DPI, scaled at runtime. Proportions follow macOS
        // Spotlight: wide window, tall input row, large query font.
        private const int InputRowHeight = 68;
        private const int RowHeight = 56;
        private const int QueryFontPx = 26;
        private const int TitleFontPx = 15;
        private const int PreviewFontPx = 12;
        private const int EdgeInset = 22;
        private const int TextInset = 58;
        private const int MagnifierSize = 20;

        private const int FadeIntervalMilliseconds = 15;
        private const int FadeSteps = 7;

        private readonly AppSettings _settings;
        private readonly ClipBarTheme _theme;
        private readonly TextBox _queryBox;
        private readonly List<Font> _retiredFonts = new List<Font>();
        private readonly Timer _fadeTimer;

        private List<ClipEntry> _entries = new List<ClipEntry>();
        private List<ClipEntry> _matches = new List<ClipEntry>();
        private readonly List<string> _previews = new List<string>();
        private int _selectedIndex;

        private float _scale = 1F;
        private float _fontScale;
        private Font _queryFont;
        private Font _titleFont;
        private Font _previewFont;

        private BackdropMode _appliedBackdrop = BackdropMode.None;
        private double _targetOpacity = 1D;
        private IntPtr _previousForegroundWindow;
        private bool _dismissing;

        public ClipBarWindow(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _theme = ClipBarTheme.For(settings.Theme);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            // Layout is entirely manual; WinForms must not apply a second scaling pass.
            AutoScaleMode = AutoScaleMode.None;
            BackColor = _theme.Background;

            _queryBox = new TextBox
            {
                Name = "clipBarQuery",
                BorderStyle = BorderStyle.None,
                ForeColor = _theme.Title,
                BackColor = _theme.InputBand
            };
            _queryBox.TextChanged += QueryBox_TextChanged;
            Controls.Add(_queryBox);

            _fadeTimer = new Timer { Interval = FadeIntervalMilliseconds };
            _fadeTimer.Tick += FadeTimer_Tick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var createParams = base.CreateParams;
                createParams.ExStyle |= NativeMethods.WsExToolWindow; // keep out of Alt+Tab
                return createParams;
            }
        }

        /// <summary>Raised after an insert has been copied, so the tray can react.</summary>
        public event EventHandler<ClipEntry> EntryCopied;

        /// <summary>Raised when the user asks to edit the highlighted insert instead.</summary>
        public event EventHandler<ClipEntry> EditRequested;

        public void ShowFor(IList<ClipEntry> entries)
        {
            _entries = entries != null ? new List<ClipEntry>(entries) : new List<ClipEntry>();
            _previousForegroundWindow = NativeMethods.GetForegroundWindow();
            _dismissing = false;

            _queryBox.Text = string.Empty;
            UpdateMatches();

            // Create the handle while still hidden and lay out before Show(): on a
            // second summon the window still carries its previous bounds, and showing
            // first made it flash at the old size for a frame.
            if (!IsHandleCreated)
            {
                var forceHandleCreation = Handle;
            }

            // Lay out against the monitor the pointer is on. The window is never
            // parked anywhere first - doing that flashed it at the screen corner.
            ApplyLayoutOn(Screen.FromPoint(Cursor.Position));

            // Form.Opacity below 1 turns the window layered, and a layered window
            // cannot show a DWM system backdrop - so that one mode appears instantly.
            // Every other mode fades, including at full opacity.
            bool fade = _appliedBackdrop != BackdropMode.SystemAcrylic;
            Opacity = fade ? 0D : _targetOpacity;

            Show();

            _fadeTimer.Stop();
            if (fade) _fadeTimer.Start();

            NativeMethods.SetForegroundWindow(Handle);
            Activate();
            _queryBox.Focus();
        }

        private void FadeTimer_Tick(object sender, EventArgs e)
        {
            double next = Opacity + _targetOpacity / FadeSteps;
            if (next >= _targetOpacity)
            {
                Opacity = _targetOpacity;
                _fadeTimer.Stop();
                return;
            }
            Opacity = next;
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private void ApplyLayout()
        {
            ApplyLayoutOn(null);
        }

        /// <param name="targetScreen">
        /// Monitor to lay out against, or null to use the one the window is already on.
        /// </param>
        private void ApplyLayoutOn(Screen targetScreen)
        {
            if (!IsHandleCreated) return;

            var screen = targetScreen ?? Screen.FromHandle(Handle);
            var workArea = screen.WorkingArea;

            // DPI alone is not enough: a 4K panel at 100% scaling reports 96 DPI, so a
            // purely DPI-driven layout renders 1:1 and looks tiny. Vertical resolution
            // supplies a second signal; the larger of the two wins. Height is used
            // rather than width so ultrawides are treated as the 1440p panels they are.
            float dpiScale = DeviceDpi / 96F;
            float resolutionScale = workArea.Height / 1080F;
            float autoScale = Math.Min(3F, Math.Max(1F, Math.Max(dpiScale, resolutionScale)));
            _scale = autoScale * _settings.SizeMultiplier;

            // Fonts only need rebuilding when the scale actually moves. Doing it on
            // every keystroke retained a set of fonts per character typed.
            if (_queryFont == null || Math.Abs(_fontScale - _scale) > 0.001F)
            {
                _fontScale = _scale;
                RetireFont(ref _queryFont);
                RetireFont(ref _titleFont);
                RetireFont(ref _previewFont);
                _queryFont = new Font("Segoe UI", QueryFontPx * _scale, GraphicsUnit.Pixel);
                _titleFont = new Font("Segoe UI", TitleFontPx * _scale, FontStyle.Bold, GraphicsUnit.Pixel);
                _previewFont = new Font("Segoe UI", PreviewFontPx * _scale, GraphicsUnit.Pixel);
                _queryBox.Font = _queryFont;
            }

            int width = (int)Math.Round(_settings.Width * _scale);
            width = Math.Max(480, Math.Min((int)(workArea.Width * 0.55F), width));
            int height = S(InputRowHeight) + Math.Max(1, _matches.Count) * S(RowHeight);
            ClientSize = new Size(width, height);

            int queryHeight = _queryBox.Height; // font-driven for a single-line TextBox
            _queryBox.Bounds = new Rectangle(
                S(TextInset),
                (S(InputRowHeight) - queryHeight) / 2,
                width - S(TextInset) - S(EdgeInset),
                queryHeight);

            Location = new Point(
                workArea.Left + (workArea.Width - width) / 2,
                workArea.Top + (int)(workArea.Height * 0.22F));

            // The glass region tracks the results area, which grows and shrinks with
            // the match count.
            if (_appliedBackdrop == BackdropMode.SystemAcrylic)
                WindowBackdrop.ExtendGlassToBottom(Handle, height - S(InputRowHeight));

            Invalidate();
        }

        private void RetireFont(ref Font font)
        {
            if (font == null) return;
            _retiredFonts.Add(font);
            font = null;
        }

        private void QueryBox_TextChanged(object sender, EventArgs e)
        {
            UpdateMatches();
            ApplyLayout(); // the window grows and shrinks with the result count
        }

        private void UpdateMatches()
        {
            _matches = InsertSearch.Rank(
                _entries,
                _queryBox.Text,
                _settings.MaxResults,
                _settings.RankRecentFirst ? _settings.RecentTitles : null);

            // Previews are resolved once per query rather than per paint: resolving
            // {clipboard} reads the real clipboard, which is far too expensive to do
            // on every repaint.
            _previews.Clear();
            foreach (var entry in _matches)
                _previews.Add(BuildPreview(entry.Text, _settings.ResolveTokensInPreview));

            _selectedIndex = 0;
            Invalidate();
        }

        private void MoveSelection(int offset)
        {
            if (_matches.Count == 0) return;

            _selectedIndex = (_selectedIndex + offset + _matches.Count) % _matches.Count;
            Invalidate();
        }

        private void CopySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _matches.Count) return;

            var entry = _matches[_selectedIndex];
            ClipboardWriter.Copy(entry);
            Dismiss();

            var handler = EntryCopied;
            if (handler != null) handler(this, entry);
        }

        private void EditSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _matches.Count) return;

            var entry = _matches[_selectedIndex];
            Dismiss();

            var handler = EditRequested;
            if (handler != null) handler(this, entry);
        }

        /// <summary>Hides the bar and hands focus back to wherever the user was.</summary>
        private void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;

            _fadeTimer.Stop();
            Hide();

            if (_previousForegroundWindow != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(_previousForegroundWindow);

            _previousForegroundWindow = IntPtr.Zero;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    Dismiss();
                    return true;
                case Keys.Enter:
                    CopySelected();
                    return true;
                case Keys.Alt | Keys.Enter:
                    if (_settings.AltEnterOpensEditor) EditSelected();
                    return true;
                case Keys.Up:
                    MoveSelection(-1);
                    return true;
                case Keys.Down:
                    MoveSelection(1);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            int index = RowAt(e.Y);
            if (index < 0) return;

            _selectedIndex = index;
            CopySelected();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = RowAt(e.Y);
            if (index < 0 || index == _selectedIndex) return;

            _selectedIndex = index;
            Invalidate();
        }

        private int RowAt(int y)
        {
            int top = S(InputRowHeight);
            if (y < top || _matches.Count == 0) return -1;

            int index = (y - top) / S(RowHeight);
            return index >= 0 && index < _matches.Count ? index : -1;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible) Dismiss();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyLayout();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            _appliedBackdrop = WindowBackdrop.Apply(
                this, _settings.Backdrop, _settings.Transparency, _theme.IsDark);
            _targetOpacity = WindowBackdrop.OpacityFor(_appliedBackdrop, _settings.Transparency);
            WindowBackdrop.ApplyRoundedCorners(Handle);

            // Under the DWM system backdrop, black client pixels inside the extended
            // frame read as backdrop. Only the results area is extended, so the query
            // box above it keeps painting like an ordinary control.
            if (_appliedBackdrop == BackdropMode.SystemAcrylic)
                BackColor = Color.Black;

            // A Remote Desktop reconnect resizes the desktop without necessarily
            // raising DpiChanged, which would otherwise leave a stale layout.
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        }

        private void DisplaySettingsChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated && !IsDisposed && Visible) ApplyLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // The tray owns this window's lifetime; Alt+F4 should only dismiss it.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Dismiss();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;

            // ClearType needs an opaque surface. On a layered window or over the DWM
            // glass it fringes, so those fall back to greyscale antialiasing - which
            // is why this is chosen per backdrop rather than fixed.
            bool opaqueSurface = _appliedBackdrop != BackdropMode.SystemAcrylic
                && _targetOpacity >= 1D;
            graphics.TextRenderingHint = opaqueSurface
                ? TextRenderingHint.ClearTypeGridFit
                : TextRenderingHint.AntiAliasGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = ClientSize.Width;
            int inputHeight = S(InputRowHeight);

            using (var band = new SolidBrush(_theme.InputBand))
                graphics.FillRectangle(band, 0, 0, width, inputHeight);

            DrawMagnifier(graphics, S(EdgeInset), (inputHeight - S(MagnifierSize)) / 2, S(MagnifierSize));

            using (var divider = new Pen(_theme.Divider))
                graphics.DrawLine(divider, 0, inputHeight, width, inputHeight);

            if (_matches.Count == 0)
            {
                // The empty state is the only text on screen, so it uses the stronger
                // title colour rather than the secondary preview grey.
                using (var brush = new SolidBrush(_theme.Title))
                {
                    graphics.DrawString("No matching inserts", _titleFont, brush,
                        S(EdgeInset), inputHeight + S(16));
                }
                return;
            }

            using (var titleBrush = new SolidBrush(_theme.Title))
            using (var previewBrush = new SolidBrush(_theme.Preview))
            using (var selectionBrush = new SolidBrush(_theme.Selection))
            {
                for (int index = 0; index < _matches.Count; index++)
                {
                    var entry = _matches[index];
                    int top = inputHeight + index * S(RowHeight);

                    if (index == _selectedIndex)
                        graphics.FillRectangle(selectionBrush, 0, top, width, S(RowHeight));

                    graphics.DrawString(
                        string.IsNullOrWhiteSpace(entry.Title) ? "Untitled" : entry.Title,
                        _titleFont, titleBrush, S(EdgeInset), top + S(8));
                    graphics.DrawString(
                        index < _previews.Count ? _previews[index] : BuildPreview(entry.Text, false),
                        _previewFont, previewBrush, S(EdgeInset), top + S(31));
                }
            }
        }

        private void DrawMagnifier(Graphics graphics, int x, int y, int size)
        {
            using (var pen = new Pen(_theme.Magnifier, Math.Max(1.5F, 1.6F * _scale)))
            {
                int diameter = (int)(size * 0.68F);
                int nudge = (int)(diameter * 0.12F);
                graphics.DrawEllipse(pen, x, y, diameter, diameter);
                graphics.DrawLine(pen,
                    x + diameter - nudge, y + diameter - nudge,
                    x + size, y + size);
            }
        }

        internal static string BuildPreview(string text, bool resolveTokens)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Empty insert";

            if (resolveTokens)
            {
                try { text = Tokens.TokenSubstitution.Resolve(text); }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // Resolving {clipboard} can fail while another process holds it.
                }
            }

            var preview = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (preview.Contains("  "))
                preview = preview.Replace("  ", " ");
            return preview.Length == 0 ? "Empty insert" : preview;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;

                _fadeTimer.Stop();
                _fadeTimer.Dispose();

                foreach (var font in _retiredFonts) font.Dispose();
                _retiredFonts.Clear();

                _queryBox.Font = null;
                if (_queryFont != null) { _queryFont.Dispose(); _queryFont = null; }
                if (_titleFont != null) { _titleFont.Dispose(); _titleFont = null; }
                if (_previewFont != null) { _previewFont.Dispose(); _previewFont = null; }
            }
            base.Dispose(disposing);
        }

        private static class NativeMethods
        {
            public const int WsExToolWindow = 0x00000080;

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);
        }
    }
}
