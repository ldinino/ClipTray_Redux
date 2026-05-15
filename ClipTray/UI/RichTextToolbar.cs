using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class RichTextToolbar : UserControl
    {
        private const string PlainTextLabel = "Plain Text";
        private const string PlainTextSeparator = "──────────────";

        private static readonly float[] SizePresets =
        {
            8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48
        };

        private readonly RichTextBox _target;
        private bool _syncing;

        private ToolStripComboBox _fontCombo;
        private ToolStripComboBox _sizeCombo;
        private ToolStripButton _boldBtn;
        private ToolStripButton _italicBtn;
        private ToolStripButton _underlineBtn;
        private ToolStripButton _strikeBtn;
        private ToolStripButton _colorBtn;
        private ToolStripButton _highlightBtn;
        private ToolStripButton _bulletBtn;
        private ToolStripDropDownButton _alignBtn;
        private ToolStripMenuItem _alignLeftItem;
        private ToolStripMenuItem _alignCenterItem;
        private ToolStripMenuItem _alignRightItem;
        private ToolStripButton _linkBtn;

        public RichTextToolbar(RichTextBox target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            InitializeComponents();
            _target.SelectionChanged += (s, e) => SyncFromSelection();
            SyncFromSelection();
        }

        private void InitializeComponents()
        {
            var strip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                RenderMode = ToolStripRenderMode.System,
                ImageScalingSize = new Size(16, 16)
            };

            _fontCombo = new ToolStripComboBox { Name = "font", Width = 110, AutoSize = false };
            _fontCombo.ComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _fontCombo.Items.Add(PlainTextLabel);
            _fontCombo.Items.Add(PlainTextSeparator);
            foreach (var family in FontFamily.Families)
            {
                try { _fontCombo.Items.Add(family.Name); }
                catch { /* skip families that throw */ }
            }
            _fontCombo.SelectedIndexChanged += FontCombo_SelectedIndexChanged;

            _sizeCombo = new ToolStripComboBox { Name = "size", Width = 45, AutoSize = false };
            foreach (var sz in SizePresets)
                _sizeCombo.Items.Add(sz.ToString());
            _sizeCombo.SelectedIndexChanged += SizeCombo_Changed;
            _sizeCombo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SizeCombo_Changed(s, e); e.Handled = true; e.SuppressKeyPress = true; } };

            _boldBtn = MakeToggleButton("B", "Bold (Ctrl+B)", new Font("Segoe UI", 9, FontStyle.Bold));
            _boldBtn.Click += (s, e) => ToggleStyle(FontStyle.Bold);

            _italicBtn = MakeToggleButton("I", "Italic (Ctrl+I)", new Font("Segoe UI", 9, FontStyle.Italic));
            _italicBtn.Click += (s, e) => ToggleStyle(FontStyle.Italic);

            _underlineBtn = MakeToggleButton("U", "Underline (Ctrl+U)", new Font("Segoe UI", 9, FontStyle.Underline));
            _underlineBtn.Click += (s, e) => ToggleStyle(FontStyle.Underline);

            _strikeBtn = MakeToggleButton("S", "Strikethrough", new Font("Segoe UI", 9, FontStyle.Strikeout));
            _strikeBtn.Click += (s, e) => ToggleStyle(FontStyle.Strikeout);

            _colorBtn = new ToolStripButton("A")
            {
                ToolTipText = "Text color",
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _colorBtn.Click += ColorBtn_Click;

            _highlightBtn = new ToolStripButton("ab")
            {
                ToolTipText = "Highlight color",
                BackColor = Color.Yellow,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _highlightBtn.Click += HighlightBtn_Click;

            _bulletBtn = new ToolStripButton("•")
            {
                ToolTipText = "Bulleted list",
                CheckOnClick = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _bulletBtn.Click += (s, e) =>
            {
                if (_syncing) return;
                _target.SelectionBullet = _bulletBtn.Checked;
                _target.Focus();
            };

            _alignBtn = new ToolStripDropDownButton("≡")
            {
                ToolTipText = "Alignment",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ShowDropDownArrow = false
            };
            _alignLeftItem = new ToolStripMenuItem("Left") { Tag = HorizontalAlignment.Left };
            _alignCenterItem = new ToolStripMenuItem("Center") { Tag = HorizontalAlignment.Center };
            _alignRightItem = new ToolStripMenuItem("Right") { Tag = HorizontalAlignment.Right };
            EventHandler alignHandler = (s, e) =>
            {
                var mi = (ToolStripMenuItem)s;
                _target.SelectionAlignment = (HorizontalAlignment)mi.Tag;
                _target.Focus();
                SyncFromSelection();
            };
            _alignLeftItem.Click += alignHandler;
            _alignCenterItem.Click += alignHandler;
            _alignRightItem.Click += alignHandler;
            _alignBtn.DropDownItems.Add(_alignLeftItem);
            _alignBtn.DropDownItems.Add(_alignCenterItem);
            _alignBtn.DropDownItems.Add(_alignRightItem);

            _linkBtn = new ToolStripButton("Link")
            {
                ToolTipText = "Insert hyperlink",
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _linkBtn.Click += LinkBtn_Click;

            strip.Items.AddRange(new ToolStripItem[]
            {
                _fontCombo,
                _sizeCombo,
                new ToolStripSeparator(),
                _boldBtn, _italicBtn, _underlineBtn, _strikeBtn,
                new ToolStripSeparator(),
                _colorBtn, _highlightBtn,
                new ToolStripSeparator(),
                _bulletBtn, _alignBtn,
                new ToolStripSeparator(),
                _linkBtn
            });

            Controls.Add(strip);
            Height = 28;
        }

        private static ToolStripButton MakeToggleButton(string text, string tooltip, Font font)
        {
            return new ToolStripButton(text)
            {
                ToolTipText = tooltip,
                CheckOnClick = true,
                Font = font,
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
        }

        private void SyncFromSelection()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                // Document-level plain check: when the entry has no formatting
                // anywhere, show "Plain Text" mode regardless of the selection's
                // typing attributes (which still carry the default font name).
                if (RichTextHelpers.DetectRichness(_target) == null)
                {
                    SetComboText(_fontCombo, PlainTextLabel);
                    SetComboText(_sizeCombo, "");
                    _boldBtn.Checked = false;
                    _italicBtn.Checked = false;
                    _underlineBtn.Checked = false;
                    _strikeBtn.Checked = false;
                    _bulletBtn.Checked = false;
                    _alignLeftItem.Checked = true;
                    _alignCenterItem.Checked = false;
                    _alignRightItem.Checked = false;
                    return;
                }

                var f = _target.SelectionFont;
                if (f != null)
                {
                    SetComboText(_fontCombo, f.FontFamily.Name);
                    SetComboText(_sizeCombo, f.Size.ToString());
                    _boldBtn.Checked = f.Bold;
                    _italicBtn.Checked = f.Italic;
                    _underlineBtn.Checked = f.Underline;
                    _strikeBtn.Checked = f.Strikeout;
                }
                else
                {
                    // Mixed selection — blank combos, leave toggles as-is
                    SetComboText(_fontCombo, "");
                    SetComboText(_sizeCombo, "");
                }

                _bulletBtn.Checked = _target.SelectionBullet;
                var align = _target.SelectionAlignment;
                _alignLeftItem.Checked = align == HorizontalAlignment.Left;
                _alignCenterItem.Checked = align == HorizontalAlignment.Center;
                _alignRightItem.Checked = align == HorizontalAlignment.Right;
            }
            finally
            {
                _syncing = false;
            }
        }

        private static void SetComboText(ToolStripComboBox combo, string text)
        {
            if (combo.Text != text)
                combo.Text = text;
        }

        private void FontCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            var name = _fontCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;

            if (name == PlainTextSeparator)
            {
                // Non-selectable visual separator — revert to whatever the
                // current document state is.
                SyncFromSelection();
                return;
            }

            if (name == PlainTextLabel)
            {
                RichTextHelpers.ConvertToPlain(_target);
                _target.Focus();
                SyncFromSelection();
                return;
            }

            ApplyFont(family: name, size: null, styleSetter: null);
            _target.Focus();
        }

        private void SizeCombo_Changed(object sender, EventArgs e)
        {
            if (_syncing) return;
            if (!float.TryParse(_sizeCombo.Text, out float size) || size <= 0) return;
            ApplyFont(family: null, size: size, styleSetter: null);
            _target.Focus();
        }

        private void ApplyFont(string family, float? size, Action<FontStyle> styleSetter)
        {
            var current = _target.SelectionFont;
            // Fallback when SelectionFont is null (mixed selection): use the
            // RichTextBox's own Font as a sensible base.
            var baseFont = current ?? _target.Font;
            var newFamily = family ?? baseFont.FontFamily.Name;
            var newSize = size ?? baseFont.Size;
            var newStyle = baseFont.Style;
            // styleSetter unused here; reserved for future expansion
            _target.SelectionFont = new Font(newFamily, newSize, newStyle);
        }

        private void ToggleStyle(FontStyle style)
        {
            if (_syncing) return;
            var baseFont = _target.SelectionFont ?? _target.Font;
            var newStyle = baseFont.Style ^ style;
            try
            {
                _target.SelectionFont = new Font(baseFont, newStyle);
            }
            catch (ArgumentException)
            {
                // Font family doesn't support the style — silently ignore
            }
            _target.Focus();
        }

        private void ColorBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new ColorDialog { Color = _target.SelectionColor })
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _target.SelectionColor = dlg.Color;
                    _colorBtn.ForeColor = dlg.Color;
                }
            }
            _target.Focus();
        }

        private void HighlightBtn_Click(object sender, EventArgs e)
        {
            using (var dlg = new ColorDialog { Color = _target.SelectionBackColor })
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _target.SelectionBackColor = dlg.Color;
                    _highlightBtn.BackColor = dlg.Color;
                }
            }
            _target.Focus();
        }

        private void LinkBtn_Click(object sender, EventArgs e)
        {
            string defaultDisplay = _target.SelectionLength > 0 ? _target.SelectedText : "";
            using (var dlg = new HyperlinkDialog(defaultDisplay))
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (string.IsNullOrEmpty(dlg.Url)) return;

                var display = string.IsNullOrEmpty(dlg.DisplayText) ? dlg.Url : dlg.DisplayText;

                // Capture pre-insert formatting so the user's next keystroke
                // doesn't inherit the link's blue/underline styling.
                var prevColor = _target.SelectionColor;
                var prevBack = _target.SelectionBackColor;
                var prevFont = _target.SelectionFont ?? _target.Font;

                // Insert as a real RTF HYPERLINK field — same structure browsers
                // and Word produce. Clickable in read-only previews and after
                // copying to other apps.
                _target.SelectedRtf = RichTextHelpers.BuildHyperlinkRtf(dlg.Url, display);

                // Cursor is now after the inserted field; reset formatting.
                _target.SelectionColor = prevColor;
                _target.SelectionBackColor = prevBack;
                _target.SelectionFont = prevFont;
            }
            _target.Focus();
        }
    }
}
