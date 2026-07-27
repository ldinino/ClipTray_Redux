using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClipTray.Data;
using ClipTray.Models;
using ClipTray.Settings;
using ClipTray.Tokens;

namespace ClipTray.UI
{
    public class EntriesDialog : ClipTrayForm
    {
        private const int InsertItemHeightAt96Dpi = 54;

        private static readonly Color SelectedItemColor = Color.FromArgb(255, 244, 194);
        private static readonly Color DividerColor = Color.FromArgb(220, 223, 228);
        private static readonly Color SaveColor = Color.FromArgb(42, 91, 173);

        private readonly List<ClipEntry> _entries;
        private readonly string _filePath;
        private readonly AppSettings _settings;

        private ListBox _listBox;
        private TextBox _titleBox;
        private ComposerRichTextBox _textBox;
        private NumericUpDown _menuSizeUpDown;
        private Label _statusLabel;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private Button _copyButton;
        private Button _previewButton;
        private Button _duplicateButton;
        private Button _deleteButton;
        private Button _discardButton;
        private Button _saveButton;
        private ToolTip _toolTip;

        private int _currentIndex = -1;
        private bool _isNewDraft;
        private bool _isDirty;
        private bool _loading;

        public int MenuSize
        {
            get { return (int)_menuSizeUpDown.Value; }
        }

        /// <summary>
        /// Raised when the user asks for ClipBar settings. The tray owns the hotkey and
        /// the ClipBar window, so it handles the dialog and applies the result.
        /// </summary>
        public event EventHandler ClipBarSettingsRequested;

        public EntriesDialog(List<ClipEntry> entries, string filePath, int menuSize, bool startNew = false, AppSettings settings = null, string selectTitle = null)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _settings = settings;

            InitializeComponents(menuSize);
            int designClientWidth = ClientSize.Width;
            ConfigureDpiScaling();
            float initialScale = ClientSize.Width / (float)designClientWidth;
            _listBox.ItemHeight = ScaleLogical(InsertItemHeightAt96Dpi, initialScale);
            DpiChanged += EntriesDialog_DpiChanged;
            RefreshListBox(-1);

            if (startNew || _entries.Count == 0)
                BeginNewDraft();
            else
                LoadEntry(IndexOfTitle(selectTitle));
        }

        /// <summary>Index of the named insert, or 0 when it is absent.</summary>
        private int IndexOfTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return 0;

            int index = _entries.FindIndex(
                entry => string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? index : 0;
        }

        private void InitializeComponents(int menuSize)
        {
            Text = "ClipTray Editor - " + Path.GetFileName(_filePath);
            ClientSize = new Size(1080, 650);
            MinimumSize = new Size(780, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            KeyPreview = true;

            _toolTip = new ToolTip();
            var workspace = new SplitContainer
            {
                Name = "workspaceSplit",
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.None,
                Size = ClientSize,
                SplitterDistance = 260,
                Panel1MinSize = 220,
                Panel2MinSize = 500,
                SplitterWidth = 5
            };

            BuildInsertPane(workspace.Panel1, menuSize, _toolTip);
            BuildDraftPane(workspace.Panel2, _toolTip);

            Controls.Add(workspace);
            AcceptButton = _saveButton;
            FormClosing += EntriesDialog_FormClosing;
        }

        private void BuildInsertPane(Control parent, int menuSize, ToolTip toolTip)
        {
            parent.BackColor = Color.FromArgb(246, 247, 249);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(12, 7, 8, 6),
                BackColor = Color.FromArgb(238, 240, 243)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));

            var insertsLabel = new Label
            {
                Text = "Inserts",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };
            var newButton = MakeButton("+", 30, NewButton_Click);
            newButton.Name = "newInsertButton";
            newButton.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            newButton.Margin = Padding.Empty;
            toolTip.SetToolTip(newButton, "New insert (Ctrl+N)");

            header.Controls.Add(insertsLabel, 0, 0);
            header.Controls.Add(newButton, 1, 0);

            _listBox = new ListBox
            {
                Name = "insertsList",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = InsertItemHeightAt96Dpi,
                IntegralHeight = false,
                BackColor = Color.White
            };
            _listBox.DrawItem += ListBox_DrawItem;
            _listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            _listBox.KeyDown += ListBox_KeyDown;

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new Padding(8, 8, 4, 6),
                BackColor = Color.FromArgb(238, 240, 243)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // The label column absorbs the slack and is the first thing to shrink.
            // Text does not scale exactly linearly with DPI, so leaving every column
            // AutoSize let accumulated growth push the spinner off the panel at 200%.
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _moveUpButton = MakeButton("↑", 32, (s, e) => MoveCurrent(-1));
            _moveUpButton.Name = "moveUpButton";
            _moveUpButton.Anchor = AnchorStyles.Left;
            _moveUpButton.Margin = new Padding(0, 0, 4, 0);
            toolTip.SetToolTip(_moveUpButton, "Move up (Alt+Up)");

            _moveDownButton = MakeButton("↓", 32, (s, e) => MoveCurrent(1));
            _moveDownButton.Name = "moveDownButton";
            _moveDownButton.Anchor = AnchorStyles.Left;
            _moveDownButton.Margin = new Padding(0, 0, 10, 0);
            toolTip.SetToolTip(_moveDownButton, "Move down (Alt+Down)");

            var menuSizeLabel = new Label
            {
                Text = "Menu size",
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 0, 5, 0)
            };
            _menuSizeUpDown = new NumericUpDown
            {
                Name = "menuSizeInput",
                Minimum = 1,
                Maximum = 100,
                Value = Math.Max(1, Math.Min(100, menuSize)),
                Location = Point.Empty,
                Width = 52,
                Margin = Padding.Empty
            };
            var menuSizeHost = new Panel
            {
                Name = "menuSizeHost",
                Size = new Size(52, 30),
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };
            menuSizeHost.Controls.Add(_menuSizeUpDown);
            Action centerMenuSize = () =>
            {
                _menuSizeUpDown.Width = menuSizeHost.ClientSize.Width;
                _menuSizeUpDown.Top = Math.Max(
                    0,
                    (menuSizeHost.ClientSize.Height - _menuSizeUpDown.Height) / 2);
            };
            menuSizeHost.Layout += (s, e) => centerMenuSize();
            menuSizeHost.SizeChanged += (s, e) => centerMenuSize();
            _menuSizeUpDown.SizeChanged += (s, e) => centerMenuSize();

            footer.Controls.Add(_moveUpButton, 0, 0);
            footer.Controls.Add(_moveDownButton, 1, 0);

            // Compact glyph rather than a caption: this footer is only ~260 logical
            // pixels wide and already carries four controls.
            if (_settings != null)
            {
                var clipBarButton = MakeButton("\u2699", 32, ClipBarButton_Click);
                clipBarButton.Name = "clipBarSettingsButton";
                clipBarButton.Anchor = AnchorStyles.Left;
                clipBarButton.Margin = new Padding(0, 0, 10, 0);
                toolTip.SetToolTip(clipBarButton, "ClipBar settings...");
                footer.Controls.Add(clipBarButton, 2, 0);
            }

            footer.Controls.Add(menuSizeLabel, 3, 0);
            footer.Controls.Add(menuSizeHost, 4, 0);

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_listBox, 0, 1);
            layout.Controls.Add(footer, 0, 2);
            parent.Controls.Add(layout);
        }

        private void BuildDraftPane(Control parent, ToolTip toolTip)
        {
            parent.BackColor = Color.White;

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                // Auto-sized rather than a fixed height: the action buttons need more
                // than twice 48px once fonts and paddings scale to 200%.
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(10, 7, 8, 6),
                BackColor = Color.FromArgb(250, 250, 251)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _titleBox = new TextBox
            {
                Name = "draftTitle",
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 10.5F),
                Margin = new Padding(0, 2, 8, 2)
            };
            _titleBox.TextChanged += DraftChanged;

            _copyButton = MakeButton("Copy", 62, CopyButton_Click);
            _previewButton = MakeButton("Preview", 72, PreviewButton_Click);
            _duplicateButton = MakeButton("Duplicate", 82, DuplicateButton_Click);
            _deleteButton = MakeButton("Delete", 66, DeleteButton_Click);
            _copyButton.Name = "copyButton";
            _previewButton.Name = "previewButton";
            _duplicateButton.Name = "duplicateButton";
            _deleteButton.Name = "deleteButton";
            _deleteButton.ForeColor = Color.FromArgb(170, 35, 35);
            toolTip.SetToolTip(_copyButton, "Copy this draft");
            toolTip.SetToolTip(_previewButton, "Preview with dynamic tokens resolved");
            toolTip.SetToolTip(_duplicateButton, "Duplicate insert (Ctrl+D)");
            toolTip.SetToolTip(_deleteButton, "Delete insert (Delete)");

            var headerActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };
            headerActions.Controls.Add(_copyButton);
            headerActions.Controls.Add(_previewButton);
            headerActions.Controls.Add(_duplicateButton);
            headerActions.Controls.Add(_deleteButton);

            header.Controls.Add(_titleBox, 0, 0);
            header.Controls.Add(headerActions, 1, 0);

            _textBox = new ComposerRichTextBox
            {
                Name = "draftEditor",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AcceptsTab = false,
                DetectUrls = true,
                Margin = Padding.Empty,
                Font = new Font("Segoe UI", 10F)
            };
            _textBox.TextChanged += DraftChanged;
            _textBox.LinkClicked += RichTextHelpers.LaunchClickedLink;

            var toolbar = new RichTextToolbar(_textBox)
            {
                Name = "formatToolbar",
                Dock = DockStyle.Top,
                Height = 30,
                Margin = Padding.Empty
            };

            var editorHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = Padding.Empty,
                BackColor = Color.White
            };
            editorHost.Controls.Add(_textBox);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(10, 8, 8, 7),
                BackColor = Color.FromArgb(246, 247, 249)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var insertActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };
            var pasteButton = MakeButton("Paste", 62, (s, e) => RichTextHelpers.PasteRichOrPlain(_textBox));
            pasteButton.Name = "pasteButton";
            pasteButton.Margin = new Padding(0, 0, 6, 0);
            var insertButton = MakeButton("Insert ▾", 72, null);
            insertButton.Name = "insertTokenButton";
            insertButton.Margin = Padding.Empty;
            TokenInsertMenu.AttachTo(insertButton, _textBox, this);
            insertActions.Controls.Add(pasteButton);
            insertActions.Controls.Add(insertButton);

            _statusLabel = new Label
            {
                Name = "draftStatus",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font, FontStyle.Italic),
                Padding = new Padding(8, 0, 0, 0)
            };

            var saveActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty
            };
            _saveButton = MakeButton("Save", 72, SaveButton_Click);
            _saveButton.Name = "saveButton";
            _saveButton.FlatStyle = FlatStyle.Flat;
            _saveButton.FlatAppearance.BorderColor = SaveColor;
            _saveButton.BackColor = SaveColor;
            _saveButton.ForeColor = Color.White;
            _saveButton.UseVisualStyleBackColor = false;
            _saveButton.Margin = Padding.Empty;
            _discardButton = MakeButton("Discard", 76, DiscardButton_Click);
            _discardButton.Name = "discardButton";
            _discardButton.Margin = new Padding(0, 0, 6, 0);
            saveActions.Controls.Add(_saveButton);
            saveActions.Controls.Add(_discardButton);

            footer.Controls.Add(insertActions, 0, 0);
            footer.Controls.Add(_statusLabel, 1, 0);
            footer.Controls.Add(saveActions, 2, 0);

            parent.Controls.Add(editorHost);
            parent.Controls.Add(footer);
            parent.Controls.Add(toolbar);
            parent.Controls.Add(header);
        }

        private Button MakeButton(string text, int width, EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                Margin = new Padding(3, 0, 3, 0),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            if (clickHandler != null)
                button.Click += clickHandler;
            return button;
        }

        private void RefreshListBox(int selectIndex)
        {
            _loading = true;
            _listBox.BeginUpdate();
            try
            {
                _listBox.Items.Clear();
                foreach (var entry in _entries)
                    _listBox.Items.Add(entry);

                _listBox.SelectedIndex = selectIndex >= 0 && selectIndex < _entries.Count
                    ? selectIndex
                    : -1;
            }
            finally
            {
                _listBox.EndUpdate();
                _loading = false;
            }
            UpdateButtonStates();
        }

        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _entries.Count) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var background = new SolidBrush(selected ? SelectedItemColor : Color.White))
                e.Graphics.FillRectangle(background, e.Bounds);

            var entry = _entries[e.Index];
            float scale = e.Bounds.Height / (float)InsertItemHeightAt96Dpi;
            int horizontalInset = ScaleLogical(12, scale);
            int rightInset = ScaleLogical(10, scale);
            var titleBounds = new Rectangle(
                e.Bounds.X + horizontalInset,
                e.Bounds.Y + ScaleLogical(7, scale),
                e.Bounds.Width - horizontalInset - rightInset,
                ScaleLogical(19, scale));
            var previewBounds = new Rectangle(
                e.Bounds.X + horizontalInset,
                e.Bounds.Y + ScaleLogical(28, scale),
                e.Bounds.Width - horizontalInset - rightInset,
                ScaleLogical(18, scale));
            using (var titleFont = new Font(e.Font, FontStyle.Bold))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    string.IsNullOrWhiteSpace(entry.Title) ? "Untitled" : entry.Title,
                    titleFont,
                    titleBounds,
                    Color.FromArgb(28, 31, 36),
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            TextRenderer.DrawText(
                e.Graphics,
                BuildPreview(entry.Text),
                e.Font,
                previewBounds,
                SystemColors.GrayText,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            using (var divider = new Pen(DividerColor))
                e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                e.DrawFocusRectangle();
        }

        private static string BuildPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "Empty draft";
            var preview = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (preview.Contains("  "))
                preview = preview.Replace("  ", " ");
            return preview;
        }

        private void EntriesDialog_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            _listBox.ItemHeight = ScaleLogical(
                InsertItemHeightAt96Dpi,
                e.DeviceDpiNew / 96F);
        }

        private static int ScaleLogical(int value, float scale)
        {
            return Math.Max(1, (int)Math.Round(value * scale));
        }

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;

            int nextIndex = _listBox.SelectedIndex;
            if (!_isNewDraft && nextIndex == _currentIndex) return;

            if (!TryResolveUnsavedChanges())
            {
                SetListSelection(_isNewDraft ? -1 : _currentIndex);
                return;
            }

            if (nextIndex >= 0)
                LoadEntry(nextIndex);
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;
            DeleteButton_Click(sender, EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void SetListSelection(int index)
        {
            _loading = true;
            try { _listBox.SelectedIndex = index; }
            finally { _loading = false; }
        }

        private void LoadEntry(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            _currentIndex = index;
            _isNewDraft = false;
            SetListSelection(index);

            var entry = _entries[index];
            _loading = true;
            try
            {
                _titleBox.Text = entry.Title ?? "";
                _textBox.Clear();
                if (!string.IsNullOrEmpty(entry.Rtf))
                {
                    try { _textBox.Rtf = entry.Rtf; }
                    catch (ArgumentException) { _textBox.Text = entry.Text ?? ""; }
                }
                else
                {
                    _textBox.Text = entry.Text ?? "";
                }
                _textBox.SelectionStart = 0;
                _textBox.SelectionLength = 0;
            }
            finally
            {
                _loading = false;
            }

            SetDirty(false);
        }

        private void BeginNewDraft()
        {
            _currentIndex = -1;
            _isNewDraft = true;
            SetListSelection(-1);

            _loading = true;
            try
            {
                _titleBox.Clear();
                _textBox.Clear();
            }
            finally
            {
                _loading = false;
            }

            SetDirty(false);
            ActiveControl = _titleBox;
        }

        private void DraftChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            SetDirty(true);
        }

        private void SetDirty(bool dirty)
        {
            _isDirty = dirty;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSavedEntry = !_isNewDraft && _currentIndex >= 0 && _currentIndex < _entries.Count;
            bool hasDraftText = _textBox != null && _textBox.TextLength > 0;
            bool hasTitle = _titleBox != null && !string.IsNullOrWhiteSpace(_titleBox.Text);

            _moveUpButton.Enabled = hasSavedEntry && _currentIndex > 0;
            _moveDownButton.Enabled = hasSavedEntry && _currentIndex < _entries.Count - 1;
            _copyButton.Enabled = hasDraftText;
            _previewButton.Enabled = hasDraftText;
            _duplicateButton.Enabled = hasSavedEntry;
            _deleteButton.Enabled = hasSavedEntry;
            _discardButton.Enabled = _isDirty;
            _saveButton.Enabled = _isDirty && hasTitle;
            _saveButton.BackColor = _saveButton.Enabled ? SaveColor : Color.FromArgb(218, 223, 232);
            _saveButton.ForeColor = _saveButton.Enabled ? Color.White : SystemColors.GrayText;

            if (_isDirty)
            {
                _statusLabel.Text = "Unsaved changes";
                _statusLabel.ForeColor = Color.FromArgb(150, 90, 20);
            }
            else
            {
                _statusLabel.Text = _isNewDraft ? "New insert" : "All changes saved";
                _statusLabel.ForeColor = SystemColors.GrayText;
            }
        }

        private bool TryResolveUnsavedChanges()
        {
            if (!_isDirty) return true;

            string title = string.IsNullOrWhiteSpace(_titleBox.Text) ? "this insert" : "\"" + _titleBox.Text.Trim() + "\"";
            var result = MessageBox.Show(
                this,
                "Save changes to " + title + "?",
                "Unsaved changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.No) return true;
            return SaveCurrent();
        }

        private void NewButton_Click(object sender, EventArgs e)
        {
            if (!TryResolveUnsavedChanges()) return;
            BeginNewDraft();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            SaveCurrent();
        }

        private bool SaveCurrent()
        {
            string title = _titleBox.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "Enter a title before saving.", "ClipTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _titleBox.Focus();
                return false;
            }

            string text = RichTextHelpers.GetVisibleText(_textBox);
            string rtf = RichTextHelpers.DetectRichness(_textBox);

            if (_isNewDraft)
            {
                var entry = new ClipEntry { Title = title, Text = text, Rtf = rtf };
                _entries.Add(entry);
                if (!SafeWrite())
                {
                    _entries.Remove(entry);
                    return false;
                }

                _currentIndex = _entries.Count - 1;
                _isNewDraft = false;
            }
            else
            {
                if (_currentIndex < 0 || _currentIndex >= _entries.Count) return false;

                var entry = _entries[_currentIndex];
                string oldTitle = entry.Title;
                string oldText = entry.Text;
                string oldRtf = entry.Rtf;
                entry.Title = title;
                entry.Text = text;
                entry.Rtf = rtf;

                if (!SafeWrite())
                {
                    entry.Title = oldTitle;
                    entry.Text = oldText;
                    entry.Rtf = oldRtf;
                    return false;
                }
            }

            RefreshListBox(_currentIndex);
            SetDirty(false);
            return true;
        }

        private void DiscardButton_Click(object sender, EventArgs e)
        {
            if (_isNewDraft)
                BeginNewDraft();
            else
                LoadEntry(_currentIndex);
        }

        private void ClipBarButton_Click(object sender, EventArgs e)
        {
            var handler = ClipBarSettingsRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void MoveCurrent(int offset)
        {
            if (!TryResolveUnsavedChanges()) return;
            if (_currentIndex < 0 || _currentIndex >= _entries.Count) return;

            int targetIndex = _currentIndex + offset;
            if (targetIndex < 0 || targetIndex >= _entries.Count) return;

            SwapEntries(_currentIndex, targetIndex);
            if (!SafeWrite())
            {
                SwapEntries(_currentIndex, targetIndex);
                return;
            }

            _currentIndex = targetIndex;
            RefreshListBox(_currentIndex);
            LoadEntry(_currentIndex);
        }

        private void SwapEntries(int first, int second)
        {
            var entry = _entries[first];
            _entries[first] = _entries[second];
            _entries[second] = entry;
        }

        private void DuplicateButton_Click(object sender, EventArgs e)
        {
            if (!TryResolveUnsavedChanges()) return;
            if (_currentIndex < 0 || _currentIndex >= _entries.Count) return;

            var source = _entries[_currentIndex];
            var duplicate = new ClipEntry
            {
                Title = BuildCopyTitle(source.Title),
                Text = RichTextHelpers.GetVisibleText(source.Rtf, source.Text),
                Rtf = source.Rtf
            };
            int duplicateIndex = _currentIndex + 1;
            _entries.Insert(duplicateIndex, duplicate);

            if (!SafeWrite())
            {
                _entries.RemoveAt(duplicateIndex);
                return;
            }

            RefreshListBox(duplicateIndex);
            LoadEntry(duplicateIndex);
        }

        private string BuildCopyTitle(string sourceTitle)
        {
            string root = (sourceTitle ?? "Untitled") + " copy";
            string candidate = root;
            int suffix = 2;
            while (_entries.Exists(entry => string.Equals(entry.Title, candidate, StringComparison.OrdinalIgnoreCase)))
                candidate = root + " " + suffix++;
            return candidate;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_currentIndex < 0 || _currentIndex >= _entries.Count) return;

            var entry = _entries[_currentIndex];
            string message = "Delete insert \"" + entry.Title + "\"?";
            if (_isDirty)
                message += "\n\nUnsaved changes to this insert will also be discarded.";

            var result = MessageBox.Show(
                this,
                message,
                "Delete insert",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            int removedIndex = _currentIndex;
            _entries.RemoveAt(removedIndex);
            if (!SafeWrite())
            {
                _entries.Insert(removedIndex, entry);
                RefreshListBox(removedIndex);
                return;
            }

            RefreshListBox(-1);
            if (_entries.Count == 0)
                BeginNewDraft();
            else
                LoadEntry(Math.Min(removedIndex, _entries.Count - 1));
        }

        private void CopyButton_Click(object sender, EventArgs e)
        {
            string text = RichTextHelpers.GetVisibleText(_textBox);
            if (string.IsNullOrEmpty(text)) return;

            string resolvedText = TokenSubstitution.Resolve(text);
            string rtf = RichTextHelpers.DetectRichness(_textBox);
            try
            {
                if (!string.IsNullOrEmpty(rtf))
                {
                    var data = RichTextHelpers.CreateClipboardData(
                        resolvedText,
                        TokenSubstitution.ResolveRtf(rtf));
                    Clipboard.SetDataObject(data, true);
                }
                else
                {
                    Clipboard.SetText(resolvedText);
                }
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }

        private void PreviewButton_Click(object sender, EventArgs e)
        {
            string text = RichTextHelpers.GetVisibleText(_textBox);
            if (string.IsNullOrEmpty(text)) return;

            string rtf = RichTextHelpers.DetectRichness(_textBox);
            using (var dialog = new PreviewDialog(
                string.IsNullOrWhiteSpace(_titleBox.Text) ? "Untitled" : _titleBox.Text.Trim(),
                TokenSubstitution.Resolve(text),
                string.IsNullOrEmpty(rtf) ? null : TokenSubstitution.ResolveRtf(rtf)))
            {
                dialog.ShowDialog(this);
            }
        }

        private bool SafeWrite()
        {
            try
            {
                FileWriter.Write(_filePath, _entries);
                return true;
            }
            catch (IOException ex)
            {
                MessageBox.Show(
                    this,
                    "Could not save file:\n" + ex.Message,
                    "ClipTray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private void EntriesDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!TryResolveUnsavedChanges())
                e.Cancel = true;
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveCurrent();
                return true;
            }
            if (keyData == (Keys.Control | Keys.N))
            {
                NewButton_Click(this, EventArgs.Empty);
                return true;
            }
            if (keyData == (Keys.Control | Keys.D))
            {
                DuplicateButton_Click(this, EventArgs.Empty);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Up))
            {
                MoveCurrent(-1);
                return true;
            }
            if (keyData == (Keys.Alt | Keys.Down))
            {
                MoveCurrent(1);
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null)
                _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}