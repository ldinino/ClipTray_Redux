using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ClipTray.ClipBar;
using ClipTray.Settings;

namespace ClipTray.UI
{
    /// <summary>
    /// Configures ClipBar. Sizing is deliberately absent: it is automatic, with
    /// file-only escape hatches for the rare display that needs overriding.
    /// </summary>
    public class ClipBarSettingsDialog : ClipTrayForm
    {
        private static readonly Color AvailableColor = Color.FromArgb(22, 120, 55);
        private static readonly Color ConflictColor = Color.FromArgb(170, 35, 35);

        /// <summary>
        /// Breathing room above and below every row. Applied symmetrically to both the
        /// caption and its control, which is what keeps the two vertically centred on
        /// each other whatever the control's height turns out to be.
        /// </summary>
        private const int RowGap = 4;

        private readonly AppSettings _settings;
        private readonly Func<HotKeyDefinition, bool> _availabilityProbe;

        private CheckBox _enabledBox;
        private CheckBox _ctrlBox;
        private CheckBox _altBox;
        private CheckBox _shiftBox;
        private CheckBox _winBox;
        private ComboBox _keyCombo;
        private Label _statusLabel;
        private ComboBox _backdropCombo;
        private TrackBar _transparencyBar;
        private Label _transparencyValue;
        private NumericUpDown _maxResultsInput;
        private ComboBox _themeCombo;
        private CheckBox _autoPasteBox;
        private CheckBox _rankRecentBox;
        private CheckBox _resolveTokensBox;
        private CheckBox _altEnterBox;
        private Button _okButton;
        private Button _applyButton;
        private ToolTip _toolTip;

        private bool _loading;
        private bool _dirty;

        /// <param name="availabilityProbe">
        /// Answers whether a combination can be claimed. Injected so the tray can
        /// report its own currently registered shortcut as available, and so tests
        /// do not touch real system hotkeys.
        /// </param>
        public ClipBarSettingsDialog(AppSettings settings, Func<HotKeyDefinition, bool> availabilityProbe = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _availabilityProbe = availabilityProbe ?? GlobalHotKey.IsAvailable;

            InitializeComponents();
            ConfigureDpiScaling();
            LoadFrom(_settings);
            UpdateHotKeyStatus();
        }

        /// <summary>
        /// Raised when Apply is pressed. The owner writes the values out and puts them
        /// into effect, then calls <see cref="NotifyApplied"/>.
        /// </summary>
        public event EventHandler ApplyRequested;

        /// <summary>The combination currently described by the controls, or null if incomplete.</summary>
        public HotKeyDefinition SelectedHotKey
        {
            get
            {
                var key = _keyCombo.SelectedItem as KeyChoice;
                if (key == null) return null;

                var candidate = new HotKeyDefinition(
                    _ctrlBox.Checked, _altBox.Checked, _shiftBox.Checked, _winBox.Checked, key.Key);
                return candidate.IsValid ? candidate : null;
            }
        }

        private void InitializeComponents()
        {
            Text = "ClipBar settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            // Sized from its contents rather than a hard-coded ClientSize, which left a
            // band of dead space under the last row - and a different amount of it at
            // every DPI, because the text does not scale linearly.
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _toolTip = new ToolTip();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(14, 12, 14, 6),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // The control column absorbs slack so labels can never push controls out
            // of view as text grows non-linearly with DPI.
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _enabledBox = new CheckBox
            {
                Name = "clipBarEnabled",
                Text = "Enable ClipBar",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            _enabledBox.CheckedChanged += (s, e) => UpdateEnabledState();
            AddSpanningRow(layout, _enabledBox);

            AddRow(layout, "Shortcut", BuildModifierRow());
            AddRow(layout, string.Empty, BuildKeyRow());
            AddRow(layout, string.Empty, BuildStatusRow());
            AddRow(layout, "Backdrop", BuildBackdropRow());
            AddRow(layout, "Transparency", BuildTransparencyRow());
            AddRow(layout, "Results shown", BuildResultsRow());
            AddRow(layout, "Theme", BuildThemeRow());
            AddSpanningRow(layout, BuildExtrasRow());

            Controls.Add(layout);
            Controls.Add(BuildButtonRow());
        }

        private Control BuildExtrasRow()
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0)
            };

            var heading = new Label
            {
                Text = "Extras",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 4)
            };
            panel.Controls.Add(heading);

            _autoPasteBox = MakeExtra("extraAutoPaste", "Paste automatically after copying",
                "Sends Ctrl+V to whatever window regains focus. It types into whichever "
                + "window is in front, so leave this off if that makes you nervous.");
            _rankRecentBox = MakeExtra("extraRankRecent", "List recently used inserts first",
                "Only separates matches that scored equally - it never promotes a weak match.");
            _resolveTokensBox = MakeExtra("extraResolveTokens", "Show what tokens will produce",
                "Previews show today's date rather than {date}.");
            _altEnterBox = MakeExtra("extraAltEnter", "Alt+Enter opens the insert in the editor",
                "Instead of copying it.");

            panel.Controls.Add(_autoPasteBox);
            panel.Controls.Add(_rankRecentBox);
            panel.Controls.Add(_resolveTokensBox);
            panel.Controls.Add(_altEnterBox);
            return panel;
        }

        private CheckBox MakeExtra(string name, string text, string tooltip)
        {
            var box = new CheckBox
            {
                Name = name,
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            };
            box.CheckedChanged += SettingChanged;
            _toolTip.SetToolTip(box, tooltip);
            return box;
        }

        private Control BuildModifierRow()
        {
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _ctrlBox = MakeModifier("modCtrl", "Ctrl");
            _altBox = MakeModifier("modAlt", "Alt");
            _shiftBox = MakeModifier("modShift", "Shift");
            _winBox = MakeModifier("modWin", "Win");

            flow.Controls.Add(_ctrlBox);
            flow.Controls.Add(_altBox);
            flow.Controls.Add(_shiftBox);
            flow.Controls.Add(_winBox);
            return flow;
        }

        private CheckBox MakeModifier(string name, string text)
        {
            var box = new CheckBox
            {
                Name = name,
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 3, 10, 3)
            };
            box.CheckedChanged += HotKeyChanged;
            return box;
        }

        private Control BuildKeyRow()
        {
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _keyCombo = new ComboBox
            {
                Name = "hotkeyKey",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 110,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 8, 0)
            };
            foreach (var choice in KeyChoice.All())
                _keyCombo.Items.Add(choice);
            _keyCombo.SelectedIndexChanged += HotKeyChanged;

            var testButton = new Button
            {
                Name = "hotkeyTest",
                Text = "Test",
                Width = 72,
                Height = 25,
                FlatStyle = FlatStyle.System,
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };
            testButton.Click += (s, e) => UpdateHotKeyStatus();
            _toolTip.SetToolTip(testButton, "Check whether another application already uses this shortcut");

            flow.Controls.Add(_keyCombo);
            flow.Controls.Add(testButton);
            return flow;
        }

        private Control BuildStatusRow()
        {
            _statusLabel = new Label
            {
                Name = "hotkeyStatus",
                AutoSize = true,
                Margin = Padding.Empty,
                MaximumSize = new Size(300, 0)
            };
            return _statusLabel;
        }

        private Control BuildBackdropRow()
        {
            _backdropCombo = new ComboBox
            {
                Name = "backdropCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Margin = Padding.Empty
            };
            _backdropCombo.Items.Add(new Choice<BackdropMode>(BackdropMode.None, "None (opaque)"));
            _backdropCombo.Items.Add(new Choice<BackdropMode>(BackdropMode.Translucent, "Translucent"));
            _backdropCombo.Items.Add(new Choice<BackdropMode>(BackdropMode.SystemAcrylic, "System acrylic (Windows 11)"));
            _backdropCombo.SelectedIndexChanged += SettingChanged;
            _toolTip.SetToolTip(_backdropCombo,
                "Translucent only shows through when Transparency is below 100%. "
                + "System acrylic blurs on its own and ignores Transparency; "
                + "before Windows 11 it falls back to Translucent.");
            return _backdropCombo;
        }

        private Control BuildTransparencyRow()
        {
            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            // Ticks would only add height, and the height is exactly what has to be
            // set explicitly: a TrackBar ignores Height while it is auto-sizing, which
            // is what left the slider floating above its caption.
            _transparencyBar = new TrackBar
            {
                Name = "transparencySlider",
                Minimum = AppSettings.MinTransparency,
                Maximum = AppSettings.MaxTransparency,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Width = 200,
                Height = 26,
                Margin = new Padding(0, 0, 10, 0)
            };
            _transparencyBar.ValueChanged += (s, e) => { UpdateTransparencyLabel(); SettingChanged(s, e); };

            _transparencyValue = new Label
            {
                Name = "transparencyValue",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };

            flow.Controls.Add(_transparencyBar);
            flow.Controls.Add(_transparencyValue);
            return flow;
        }

        private Control BuildResultsRow()
        {
            _maxResultsInput = new NumericUpDown
            {
                Name = "maxResults",
                Minimum = AppSettings.MinMaxResults,
                Maximum = AppSettings.MaxMaxResults,
                Width = 60,
                Margin = Padding.Empty
            };
            _maxResultsInput.ValueChanged += SettingChanged;
            return _maxResultsInput;
        }

        private Control BuildThemeRow()
        {
            _themeCombo = new ComboBox
            {
                Name = "themeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Margin = Padding.Empty
            };
            _themeCombo.Items.Add(new Choice<ThemeMode>(ThemeMode.System, "Follow system"));
            _themeCombo.Items.Add(new Choice<ThemeMode>(ThemeMode.Dark, "Dark"));
            _themeCombo.Items.Add(new Choice<ThemeMode>(ThemeMode.Light, "Light"));
            _themeCombo.SelectedIndexChanged += SettingChanged;
            return _themeCombo;
        }

        private Control BuildButtonRow()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 14, 10)
            };

            var cancelButton = new Button
            {
                Name = "cancelButton",
                Text = "Cancel",
                Width = 84,
                Height = 28,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 0, 0)
            };

            _okButton = new Button
            {
                Name = "okButton",
                Text = "OK",
                Width = 84,
                Height = 28,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0)
            };
            _okButton.Click += OkButton_Click;

            // Right-to-left flow, so adding Apply first puts it on the right: the
            // conventional OK / Cancel / Apply reading order.
            _applyButton = new Button
            {
                Name = "applyButton",
                Text = "Apply",
                Width = 84,
                Height = 28,
                Enabled = false,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(8, 0, 0, 0)
            };
            _applyButton.Click += ApplyButton_Click;

            panel.Controls.Add(_applyButton);
            panel.Controls.Add(cancelButton);
            panel.Controls.Add(_okButton);

            AcceptButton = _okButton;
            CancelButton = cancelButton;
            return panel;
        }

        private void AddRow(TableLayoutPanel layout, string caption, Control control)
        {
            // Equal top and bottom margins on both cells: the caption is centred in the
            // row and the control sits at the top of it, so they only line up when the
            // row is no taller than the control plus those margins.
            control.Margin = new Padding(
                control.Margin.Left, RowGap, control.Margin.Right, RowGap);

            var label = new Label
            {
                Text = caption,
                AutoSize = true,
                // Right-anchored so short captions sit beside their control instead of
                // stranding a gap, and anchoring on one axis only centres it vertically.
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, RowGap, 10, RowGap)
            };
            layout.Controls.Add(label, 0, layout.RowCount);
            layout.Controls.Add(control, 1, layout.RowCount);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }

        private void AddSpanningRow(TableLayoutPanel layout, Control control)
        {
            layout.Controls.Add(control, 0, layout.RowCount);
            layout.SetColumnSpan(control, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }

        private void LoadFrom(AppSettings settings)
        {
            _loading = true;
            try
            {
                _enabledBox.Checked = settings.ClipBarEnabled;

                var hotKey = settings.ClipBarHotKey ?? HotKeyDefinition.Default;
                _ctrlBox.Checked = hotKey.Control;
                _altBox.Checked = hotKey.Alt;
                _shiftBox.Checked = hotKey.Shift;
                _winBox.Checked = hotKey.Windows;
                SelectKey(hotKey.Key);

                SelectChoice(_backdropCombo, settings.Backdrop);
                SelectChoice(_themeCombo, settings.Theme);

                _transparencyBar.Value = AppSettings.Clamp(
                    settings.Transparency, AppSettings.MinTransparency, AppSettings.MaxTransparency);
                _maxResultsInput.Value = AppSettings.Clamp(
                    settings.MaxResults, AppSettings.MinMaxResults, AppSettings.MaxMaxResults);

                _autoPasteBox.Checked = settings.AutoPaste;
                _rankRecentBox.Checked = settings.RankRecentFirst;
                _resolveTokensBox.Checked = settings.ResolveTokensInPreview;
                _altEnterBox.Checked = settings.AltEnterOpensEditor;
            }
            finally
            {
                _loading = false;
            }

            UpdateTransparencyLabel();
            UpdateEnabledState();
            _dirty = false;
            UpdateApplyState();
        }

        private void SelectKey(Keys key)
        {
            for (int index = 0; index < _keyCombo.Items.Count; index++)
            {
                if (((KeyChoice)_keyCombo.Items[index]).Key == key)
                {
                    _keyCombo.SelectedIndex = index;
                    return;
                }
            }
            _keyCombo.SelectedIndex = 0;
        }

        private static void SelectChoice<T>(ComboBox combo, T value) where T : struct
        {
            for (int index = 0; index < combo.Items.Count; index++)
            {
                if (Equals(((Choice<T>)combo.Items[index]).Value, value))
                {
                    combo.SelectedIndex = index;
                    return;
                }
            }
            combo.SelectedIndex = 0;
        }

        /// <summary>Copies the chosen values onto the supplied settings object.</summary>
        public void ApplyTo(AppSettings settings)
        {
            if (settings == null) return;

            settings.ClipBarEnabled = _enabledBox.Checked;

            var hotKey = SelectedHotKey;
            if (hotKey != null) settings.ClipBarHotKey = hotKey;

            settings.Backdrop = ((Choice<BackdropMode>)_backdropCombo.SelectedItem).Value;
            settings.Theme = ((Choice<ThemeMode>)_themeCombo.SelectedItem).Value;
            settings.Transparency = _transparencyBar.Value;
            settings.MaxResults = (int)_maxResultsInput.Value;

            settings.AutoPaste = _autoPasteBox.Checked;
            settings.RankRecentFirst = _rankRecentBox.Checked;
            settings.ResolveTokensInPreview = _resolveTokensBox.Checked;
            settings.AltEnterOpensEditor = _altEnterBox.Checked;
        }

        private void HotKeyChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _dirty = true;
            UpdateHotKeyStatus();
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _dirty = true;
            UpdateApplyState();
        }

        /// <summary>Called by the owner once the values have been written and applied.</summary>
        public void NotifyApplied()
        {
            _dirty = false;
            UpdateApplyState();
        }

        private void UpdateApplyState()
        {
            if (_applyButton == null) return;

            _applyButton.Enabled = _dirty && IsSelectionUsable();
        }

        private bool IsSelectionUsable()
        {
            return !_enabledBox.Checked || SelectedHotKey != null;
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            var handler = ApplyRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void UpdateTransparencyLabel()
        {
            _transparencyValue.Text = _transparencyBar.Value + "%"
                + (_transparencyBar.Value == AppSettings.MaxTransparency ? "  (opaque)" : string.Empty);
        }

        private void UpdateEnabledState()
        {
            bool on = _enabledBox.Checked;
            foreach (Control control in new Control[]
                { _ctrlBox, _altBox, _shiftBox, _winBox, _keyCombo, _backdropCombo,
                  _transparencyBar, _maxResultsInput, _themeCombo,
                  _autoPasteBox, _rankRecentBox, _resolveTokensBox, _altEnterBox })
            {
                control.Enabled = on;
            }

            if (!_loading) _dirty = true;
            UpdateHotKeyStatus();
        }

        internal void UpdateHotKeyStatus()
        {
            if (!_enabledBox.Checked)
            {
                _statusLabel.Text = "ClipBar is turned off.";
                _statusLabel.ForeColor = SystemColors.GrayText;
                if (_okButton != null) _okButton.Enabled = true;
                UpdateApplyState();
                return;
            }

            var candidate = SelectedHotKey;
            if (candidate == null)
            {
                _statusLabel.Text = "Pick at least one modifier and a key.";
                _statusLabel.ForeColor = ConflictColor;
                if (_okButton != null) _okButton.Enabled = false;
                UpdateApplyState();
                return;
            }

            bool available = _availabilityProbe(candidate);
            _statusLabel.Text = available
                ? candidate + " is available."
                : candidate + " is already used by another application.";
            _statusLabel.ForeColor = available ? AvailableColor : ConflictColor;

            // A conflicting shortcut is allowed through - registration simply fails and
            // the tray reports it - but the warning stays visible.
            if (_okButton != null) _okButton.Enabled = true;
            UpdateApplyState();
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (_enabledBox.Checked && SelectedHotKey == null)
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(
                    this,
                    "Choose at least one modifier key and a key for the ClipBar shortcut.",
                    "ClipBar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null)
            {
                _toolTip.Dispose();
                _toolTip = null;
            }
            base.Dispose(disposing);
        }

        private sealed class Choice<T> where T : struct
        {
            public Choice(T value, string text)
            {
                Value = value;
                Text = text;
            }

            public T Value { get; }
            public string Text { get; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class KeyChoice
        {
            private KeyChoice(Keys key)
            {
                Key = key;
            }

            public Keys Key { get; }

            public override string ToString()
            {
                return HotKeyDefinition.Describe(Key);
            }

            public static IEnumerable<KeyChoice> All()
            {
                yield return new KeyChoice(Keys.Space);

                for (var key = Keys.A; key <= Keys.Z; key++)
                    yield return new KeyChoice(key);

                for (var key = Keys.D0; key <= Keys.D9; key++)
                    yield return new KeyChoice(key);

                for (var key = Keys.F1; key <= Keys.F12; key++)
                    yield return new KeyChoice(key);

                yield return new KeyChoice(Keys.Insert);
                yield return new KeyChoice(Keys.Delete);
                yield return new KeyChoice(Keys.Home);
                yield return new KeyChoice(Keys.End);
                yield return new KeyChoice(Keys.PageUp);
                yield return new KeyChoice(Keys.PageDown);
            }
        }
    }
}
