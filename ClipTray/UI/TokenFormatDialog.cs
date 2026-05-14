using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipTray.UI
{
    public class TokenFormatDialog : Form
    {
        private readonly string _tokenName;
        private readonly string _defaultFormat;
        private readonly string[] _presets;
        private ComboBox _formatCombo;
        private Label _previewLabel;

        public string Format { get; private set; }

        public TokenFormatDialog(string tokenName, string defaultFormat, string[] presets)
        {
            _tokenName = tokenName;
            _defaultFormat = defaultFormat;
            _presets = presets ?? new string[0];
            Format = defaultFormat;
            InitializeComponents();
            UpdatePreview();
        }

        private void InitializeComponents()
        {
            Text = "Insert {" + _tokenName + "}";
            Size = new Size(380, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var label = new Label
            {
                Text = "Format for {" + _tokenName + "}:",
                Location = new Point(12, 15),
                AutoSize = true
            };

            _formatCombo = new ComboBox
            {
                Location = new Point(12, 35),
                Size = new Size(340, 21),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _formatCombo.Items.AddRange(_presets);
            _formatCombo.Text = _defaultFormat;
            _formatCombo.TextChanged += (s, e) => UpdatePreview();

            var previewCaption = new Label
            {
                Text = "Preview:",
                Location = new Point(12, 70),
                AutoSize = true
            };

            _previewLabel = new Label
            {
                Location = new Point(75, 70),
                Size = new Size(277, 40),
                AutoEllipsis = true
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(196, 122),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK
            };
            okButton.Click += (s, e) => { Format = _formatCombo.Text; };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(277, 122),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = okButton;
            CancelButton = cancelButton;

            Controls.AddRange(new Control[]
            {
                label, _formatCombo, previewCaption, _previewLabel,
                okButton, cancelButton
            });
        }

        private void UpdatePreview()
        {
            var fmt = _formatCombo.Text;
            if (string.IsNullOrEmpty(fmt))
            {
                _previewLabel.Text = "(empty — default will be used)";
                return;
            }

            try
            {
                _previewLabel.Text = DateTime.Now.ToString(fmt);
            }
            catch (FormatException)
            {
                _previewLabel.Text = "(invalid format)";
            }
        }
    }
}
